// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局会话模块的启动同步流程。

package session

import (
	"context"
	"encoding/json"
	"log/slog"
	"strings"
	"time"

	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/game/participant"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/elebirds/panoptes/internal/transport"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
)

func (r *Runtime) sendStaticCatalogManifest(p participant.Participant) {
	manifest := staticdata.Default().Manifest()
	hashes := make([]*pb.CatalogSectionHash, 0, len(manifest.SectionHashes))
	for _, entry := range manifest.SectionHashes {
		hashes = append(hashes, &pb.CatalogSectionHash{
			SectionName: entry.SectionName,
			Hash:        entry.Hash,
		})
	}
	msg := &pb.MsgStaticCatalogManifest{
		Manifest: &pb.StaticCatalogManifest{
			SchemaVersion:    manifest.SchemaVersion,
			ContentVersion:   manifest.ContentVersion,
			BundleHash:       manifest.BundleHash,
			DefaultLocale:    manifest.DefaultLocale,
			DefaultMapId:     manifest.DefaultMapID,
			RequiredSections: append([]string(nil), manifest.RequiredSections...),
			SectionHashes:    hashes,
		},
	}
	_ = r.SendToParticipant(context.Background(), p.ID, msg)
}

func (r *Runtime) HandleStaticCatalogSyncRequest(ctx context.Context, playerID string, req *pb.MsgStaticCatalogSyncRequest) error {
	p, ok := r.findParticipant(playerID)
	if !ok {
		return transportproblem.InvalidRequest("participant not found for static catalog sync")
	}
	if !p.IsHuman() {
		return transportproblem.InvalidRequest("static catalog sync only supports human participants")
	}
	if r.isBootstrapReady(playerID) {
		return nil
	}

	catalog := staticdata.Default()
	if catalog == nil {
		return transportproblem.InternalError("static catalog is not initialized")
	}

	sections := resolveRequestedSections(catalog, req)
	for _, sectionName := range sections {
		if err := r.sendCatalogSection(ctx, playerID, catalog, sectionName); err != nil {
			return err
		}
	}

	_ = r.SendToParticipant(transport.ContextWithGameSessionID(ctx, r.gameSessionID()), p.ID, &pb.MsgStaticCatalogSyncComplete{
		AppliedBundleHash: catalog.BundleHash(),
		Success:           true,
	})

	r.sendBootstrapRemainder(p)
	r.markBootstrapReady(playerID)
	return nil
}

func (r *Runtime) sendConfigBatch(p participant.Participant) {
	mapBundle := r.resolveBootstrapMapBundle()
	if mapBundle == nil {
		return
	}

	raw, err := json.Marshal(mapBundle)
	if err != nil {
		slog.Warn("marshal bootstrap map config failed",
			"player_id", p.ID,
			"map_id", mapBundle.ID,
			"error", err,
		)
		return
	}

	msg := &pb.MsgConfigBatchJson{
		Configs: []*pb.ConfigJsonEntry{
			{
				Key:  "mapconfig",
				Json: string(raw),
			},
		},
	}
	_ = r.SendToParticipant(context.Background(), p.ID, msg)
}

func (r *Runtime) resolveBootstrapMapBundle() *staticdata.MapRuntimeBundle {
	catalog := staticdata.Default()
	if catalog == nil {
		return nil
	}

	if r != nil && r.state != nil && r.state.Map != nil {
		if mapID := strings.TrimSpace(r.state.Map.ID); mapID != "" {
			if bundle, ok := catalog.GetMap(mapID); ok && bundle != nil {
				return bundle
			}
		}
	}

	if defaultMapID := strings.TrimSpace(catalog.DefaultMapID()); defaultMapID != "" {
		if bundle, ok := catalog.GetMap(defaultMapID); ok && bundle != nil {
			return bundle
		}
	}

	return nil
}

func (r *Runtime) sendBootstrapMessages() error {
	r.bootstrapMu.Lock()
	r.bootstrapPlanningStartSent = false
	r.bootstrapReadyByPlayer = make(map[string]bool, len(r.participants))
	r.bootstrapMu.Unlock()
	for _, binding := range r.participants {
		if !binding.Participant.IsHuman() {
			r.markBootstrapReady(binding.Participant.ID)
			continue
		}
		r.sendStaticCatalogManifest(binding.Participant)
	}
	return nil
}

func (r *Runtime) sendBootstrapRemainder(p participant.Participant) {
	r.PreparePlanningStartStateIfNeeded()
	r.sendConfigBatch(p)
	r.sendGameInit(p)
	var planningStartEvents []event.Event
	if r.planningStartResult != nil {
		planningStartEvents = r.planningStartResult.Events
	}
	if msg := BuildPlanningStartMessageFromObservation(r.state, r.BuildObservation(p.ID), r.state.Phase, planningStartEvents); msg != nil {
		_ = r.SendToParticipant(context.Background(), p.ID, msg)
		r.bootstrapMu.Lock()
		r.bootstrapPlanningStartSent = true
		r.bootstrapMu.Unlock()
	}
}

func (r *Runtime) WaitBootstrapReady(ctx context.Context) bool {
	if r == nil {
		return false
	}
	if r.allHumanPlayersBootstrapReady() {
		return true
	}

	ticker := time.NewTicker(10 * time.Millisecond)
	defer ticker.Stop()

	for {
		select {
		case <-ctx.Done():
			return false
		case <-ticker.C:
			if r.allHumanPlayersBootstrapReady() {
				return true
			}
		}
	}
}

func (r *Runtime) allHumanPlayersBootstrapReady() bool {
	r.bootstrapMu.RLock()
	defer r.bootstrapMu.RUnlock()
	for _, binding := range r.participants {
		if !binding.Participant.IsHuman() {
			continue
		}
		if !r.bootstrapReadyByPlayer[binding.Participant.ID] {
			return false
		}
	}
	return true
}

func (r *Runtime) isBootstrapReady(playerID string) bool {
	r.bootstrapMu.RLock()
	defer r.bootstrapMu.RUnlock()
	return r.bootstrapReadyByPlayer[playerID]
}

func (r *Runtime) markBootstrapReady(playerID string) {
	r.bootstrapMu.Lock()
	defer r.bootstrapMu.Unlock()
	r.bootstrapReadyByPlayer[playerID] = true
}

func (r *Runtime) sendCatalogSection(ctx context.Context, playerID string, catalog *staticdata.Catalog, sectionName string) error {
	payload, ok := catalog.SectionPayload(sectionName)
	if !ok {
		return transportproblem.InvalidRequest("unknown static catalog section")
	}

	compressed, err := compressCatalogSection(payload)
	if err != nil {
		return transportproblem.InternalError("compress static catalog section failed")
	}
	hash := ""
	for _, entry := range catalog.Manifest().SectionHashes {
		if entry.SectionName == sectionName {
			hash = entry.Hash
			break
		}
	}
	chunks := splitCatalogSection(compressed, 32*1024)
	for i, chunk := range chunks {
		if err := r.SendToParticipant(transport.ContextWithGameSessionID(ctx, r.gameSessionID()), playerID, &pb.MsgStaticCatalogSectionChunk{
			SectionName: sectionName,
			SectionHash: hash,
			ChunkIndex:  uint32(i),
			ChunkCount:  uint32(len(chunks)),
			Compression: "gzip",
			Payload:     chunk,
		}); err != nil {
			return err
		}
	}
	return nil
}
