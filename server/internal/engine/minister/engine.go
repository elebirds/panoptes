// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现部长引擎的引擎协调逻辑。

package minister

import (
	"context"
	"fmt"
	"log/slog"
	"strings"
	"sync"
	"time"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/llm"
	"github.com/elebirds/panoptes/internal/staticdata"
	"google.golang.org/protobuf/proto"
)

type RuntimeRoom interface {
	State() *domain.GameState
	PlayerIDs() []string
	SendToPlayer(playerID string, msg proto.Message) error
}

type MinisterEngine struct {
	llmClient llm.LLMClient
	memories  map[string]*MinisterMemory
	mu        sync.Mutex
}

func NewMinisterEngine(client llm.LLMClient) *MinisterEngine {
	return &MinisterEngine{llmClient: client, memories: make(map[string]*MinisterMemory)}
}

func (e *MinisterEngine) GenerateReports(ctx context.Context, room RuntimeRoom) {
	if room == nil || room.State() == nil {
		return
	}
	syncMode := e.llmClient == nil
	for _, playerID := range room.PlayerIDs() {
		for _, profile := range pickProfiles() {
			if syncMode {
				e.generateOneReport(ctx, playerID, profile, room)
				continue
			}
			go e.generateOneReport(ctx, playerID, profile, room)
		}
	}
}

func (e *MinisterEngine) generateOneReport(ctx context.Context, playerID string, profile MinisterProfile, room RuntimeRoom) {
	state := room.State()
	memory := e.getOrCreateMemory(playerID, profile.Role)
	prompt := BuildMinisterPrompt(profile.Role, profile, state, playerID, memory)
	prompt.SessionID = fmt.Sprintf("%s:%s:%d", playerID, profile.Role, state.Turn)

	raw, ok := e.streamOrFallback(ctx, room, playerID, profile.Role, prompt)
	if !ok {
		return
	}
	output, err := ParseMinisterResponse(raw)
	if err != nil {
		slog.Warn("parse minister response failed", "player_id", playerID, "role", profile.Role, "err", err)
		return
	}
	if err := room.SendToPlayer(playerID, &pb.MsgMinisterMetrics{MinisterRole: profile.Role, Metrics: output.Metrics}); err != nil {
		slog.Warn("send minister metrics failed", "player_id", playerID, "role", profile.Role, "err", err)
	}

	actionEvents := ExecuteActions(output.Actions, actionRoom{state: state}, playerID)
	for _, ev := range actionEvents {
		ev.Apply(state.World, state)
	}
	event.MinisterActedEvent{
		MinisterRole: profile.Role,
		PlayerID:     playerID,
		ActionID:     output.ActionID,
		Report:       output.Report,
	}.Apply(state.World, state)

	memory.Add(MemoryEntry{Turn: state.Turn, Type: "action", Content: output.Report, Outcome: "executed", PlayerResp: "ignored"})
}

func (e *MinisterEngine) streamOrFallback(ctx context.Context, room RuntimeRoom, playerID, role string, req llm.CompletionRequest) (string, bool) {
	if e.llmClient == nil {
		fallback := "目前局势稳定，建议优先巩固补给线并保持战区侦察。"
		_ = room.SendToPlayer(playerID, &pb.MsgMinisterReportChunk{MinisterRole: role, Chunk: fallback, IsFinal: true})
		return fallbackJSON(fallback), true
	}

	ctx, cancel := context.WithTimeout(ctx, 5*time.Second)
	defer cancel()
	stream, err := e.llmClient.Stream(ctx, req)
	if err != nil {
		slog.Warn("minister llm stream failed", "player_id", playerID, "role", role, "err", err)
		fallback := "当前汇报链路拥堵，建议按既定国策稳步推进。"
		_ = room.SendToPlayer(playerID, &pb.MsgMinisterReportChunk{MinisterRole: role, Chunk: fallback, IsFinal: true})
		return fallbackJSON(fallback), true
	}

	var b strings.Builder
	for chunk := range stream {
		if chunk == "" {
			continue
		}
		b.WriteString(chunk)
		_ = room.SendToPlayer(playerID, &pb.MsgMinisterReportChunk{MinisterRole: role, Chunk: chunk, IsFinal: false})
	}
	_ = room.SendToPlayer(playerID, &pb.MsgMinisterReportChunk{MinisterRole: role, Chunk: "", IsFinal: true})
	return b.String(), true
}

func (e *MinisterEngine) getOrCreateMemory(playerID, role string) *MinisterMemory {
	key := playerID + ":" + role
	e.mu.Lock()
	defer e.mu.Unlock()
	if m, ok := e.memories[key]; ok {
		return m
	}
	m := &MinisterMemory{PlayerID: playerID, Role: role}
	e.memories[key] = m
	return m
}

func pickProfiles() []MinisterProfile {
	pool := staticdata.Default().Ministers()
	if len(pool) == 0 {
		return []MinisterProfile{{ID: "finance", Name: "财政大臣", Role: "finance", Ability: 5, Personality: "steady", PersonalityDesc: "稳健", Loyalty: 6, Ambition: 5}}
	}
	out := make([]MinisterProfile, 0, len(pool))
	for _, p := range pool {
		out = append(out, MinisterProfile{
			ID:              p.ID,
			Name:            p.Name,
			Role:            p.Role,
			Ability:         p.Ability,
			Personality:     p.Personality,
			PersonalityDesc: p.PersonalityDesc,
			Loyalty:         p.Loyalty,
			Ambition:        p.Ambition,
		})
	}
	return out
}

func fallbackJSON(report string) string {
	return `{"report":"` + report + `","metrics":[],"actions":[],"action_id":"fallback"}`
}

type actionRoom struct {
	state *domain.GameState
}

func (r actionRoom) State() *domain.GameState {
	return r.state
}
