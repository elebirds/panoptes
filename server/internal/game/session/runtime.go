// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局会话模块的运行时状态与生命周期。

package session

import (
	"bytes"
	"compress/gzip"
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"strings"
	"sync"
	"time"

	buildingcore "github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/game/participant"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/elebirds/panoptes/internal/transport"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type ParticipantBinding struct {
	Participant participant.Participant
	Controller  Controller
}

type Runtime struct {
	ID           string
	participants []ParticipantBinding
	cfg          *config.Config
	transport    transport.GameTransport
	observations *gamequery.ObservationStore
	submitCh     chan string
	cancelFn     context.CancelFunc
	state        *domain.GameState

	bootstrapMu                sync.RWMutex
	bootstrapPlanningStartSent bool
	bootstrapReadyByPlayer     map[string]bool
	chatMu                     sync.Mutex
	chatHistory                []*pb.ChatEntry
	nextChatSequence           int64
	// 同一回合内，bootstrap 消息与正式 planning 广播都必须看到同一份 planning-start 结果，
	// 不能因为重复 Prepare 而重复激活 technology / institution。
	planningStartPreparedTurn int
	planningStartResult       *PlanningStartResult
	preparedMinisterDrafts    map[int]map[string][]domain.MinisterDraft
	preparedMinisterDraftsMu  sync.RWMutex
	ministerEngine            *ministerengine.MinisterEngine
}

func NewRuntime(id string, participants []ParticipantBinding, t transport.GameTransport, cfg *config.Config) *Runtime {
	return &Runtime{
		ID:                     id,
		participants:           append([]ParticipantBinding(nil), participants...),
		cfg:                    cfg,
		transport:              t,
		observations:           gamequery.NewObservationStore(),
		submitCh:               make(chan string, len(participants)*4+16),
		bootstrapReadyByPlayer: make(map[string]bool, len(participants)),
		preparedMinisterDrafts: make(map[int]map[string][]domain.MinisterDraft),
	}
}

func (r *Runtime) Initialize() error {
	catalog := staticdata.Default()
	mapID := ""
	if r.cfg != nil {
		mapID = r.cfg.MapID
	}
	if mapID == "" {
		mapID = catalog.DefaultMapID()
	}
	baseMap, err := maploader.LoadMap(catalog, mapID)
	if err != nil {
		return fmt.Errorf("load map %s: %w", mapID, err)
	}

	world := donburi.NewWorld()
	playerIDs := r.participantIDs()
	usernames := r.participantUsernames()
	seed := time.Now().UnixNano()
	runtimeMap := maploader.GenerateProceduralMap(baseMap, len(playerIDs), seed)
	if runtimeMap == nil {
		return fmt.Errorf("generate procedural map for %s", mapID)
	}
	mapData := maploader.InitWorldFromMap(world, runtimeMap, playerIDs)
	r.state = domain.NewGameState(r.ID, playerIDs, usernames, mapData)
	r.state.World = world
	if r.observations == nil {
		r.observations = gamequery.NewObservationStore()
	} else {
		r.observations.Reset()
	}
	if err := r.bootstrapStartingPlayers(); err != nil {
		return err
	}
	r.grantDevStartingResources()
	r.grantDevStartingUnlocks(catalog)
	r.initializeCityStates()
	return r.sendBootstrapMessages()
}

func (r *Runtime) InitializePrepared(state *domain.GameState) error {
	if state == nil {
		return fmt.Errorf("prepared state is nil")
	}
	if state.World == nil {
		return fmt.Errorf("prepared state world is nil")
	}
	if state.Map == nil {
		return fmt.Errorf("prepared state map is nil")
	}

	r.state = state
	if r.observations == nil {
		r.observations = gamequery.NewObservationStore()
	} else {
		r.observations.Reset()
	}
	if r.state.GameID == "" {
		r.state.GameID = r.ID
	}
	if r.state.NodeIndex == nil {
		r.state.NodeIndex = make(map[string]donburi.Entity)
	}
	if r.state.Map != nil && r.state.Map.NodeIndex != nil && len(r.state.NodeIndex) == 0 {
		for nodeID, entity := range r.state.Map.NodeIndex {
			r.state.NodeIndex[nodeID] = entity
		}
	}
	r.grantDevStartingResources()
	r.grantDevStartingUnlocks(staticdata.Default())
	r.initializeCityStates()
	return r.sendBootstrapMessages()
}

func (r *Runtime) State() *domain.GameState {
	return r.state
}

func (r *Runtime) SetState(state *domain.GameState) {
	r.state = state
	if r.observations == nil {
		r.observations = gamequery.NewObservationStore()
	} else {
		r.observations.Reset()
	}
	r.preparedMinisterDraftsMu.Lock()
	r.planningStartPreparedTurn = 0
	r.planningStartResult = nil
	r.preparedMinisterDrafts = make(map[int]map[string][]domain.MinisterDraft)
	r.preparedMinisterDraftsMu.Unlock()
	r.bootstrapMu.Lock()
	r.bootstrapReadyByPlayer = make(map[string]bool, len(r.participants))
	r.bootstrapPlanningStartSent = false
	r.bootstrapMu.Unlock()
}

func (r *Runtime) SubmitChannel() chan string {
	return r.submitCh
}

func (r *Runtime) SetCancelFunc(cancel context.CancelFunc) {
	r.cancelFn = cancel
}

func (r *Runtime) Cancel() {
	if r.cancelFn != nil {
		r.cancelFn()
	}
}

func (r *Runtime) IsDevMode() bool {
	return r != nil && r.cfg != nil && r.cfg.DevMode
}

func (r *Runtime) ParticipantIDs() []string {
	return r.participantIDs()
}

func (r *Runtime) PlayerIDs() []string {
	return r.ParticipantIDs()
}

func (r *Runtime) Participants() []participant.Participant {
	if r == nil {
		return nil
	}
	out := make([]participant.Participant, 0, len(r.participants))
	for _, binding := range r.participants {
		out = append(out, binding.Participant)
	}
	return out
}

func (r *Runtime) Participant(participantID string) (participant.Participant, bool) {
	return r.findParticipant(participantID)
}

func (r *Runtime) Controller(participantID string) (Controller, bool) {
	binding, ok := r.findParticipantBinding(participantID)
	if !ok || binding.Controller == nil {
		return nil, false
	}
	return binding.Controller, true
}

func (r *Runtime) HumanParticipants() []participant.Participant {
	if r == nil {
		return nil
	}
	out := make([]participant.Participant, 0, len(r.participants))
	for _, binding := range r.participants {
		if !binding.Participant.IsHuman() {
			continue
		}
		out = append(out, binding.Participant)
	}
	return out
}

func (r *Runtime) ParticipantCount() int {
	return len(r.participants)
}

func (r *Runtime) PlayerCount() int {
	return r.ParticipantCount()
}

func (r *Runtime) ConsumeBootstrapPlanningStart() bool {
	if r == nil {
		return false
	}
	r.bootstrapMu.Lock()
	defer r.bootstrapMu.Unlock()
	if !r.bootstrapPlanningStartSent {
		return false
	}
	r.bootstrapPlanningStartSent = false
	return true
}

func (r *Runtime) SendToParticipant(ctx context.Context, participantID string, msg proto.Message) error {
	ctx = transport.ContextWithGameSessionID(ctx, r.gameSessionID())
	binding, ok := r.findParticipantBinding(participantID)
	if !ok {
		return fmt.Errorf("participant %s not found", participantID)
	}
	if !binding.Participant.IsHuman() || r.transport == nil {
		return nil
	}
	return r.transport.Send(ctx, participantID, msg)
}

func (r *Runtime) SendToPlayer(ctx context.Context, playerID string, msg proto.Message) error {
	return r.SendToParticipant(ctx, playerID, msg)
}

func (r *Runtime) HumanPlayerIDs() []string {
	humans := r.HumanParticipants()
	ids := make([]string, 0, len(humans))
	for _, currentParticipant := range humans {
		ids = append(ids, currentParticipant.ID)
	}
	return ids
}

func (r *Runtime) SetMinisterEngine(engine *ministerengine.MinisterEngine) {
	if r == nil {
		return
	}
	r.ministerEngine = engine
}

func (r *Runtime) RecordMinisterMemory(playerID string, role string, entry ministerengine.MemoryEntry) {
	if r == nil || r.ministerEngine == nil {
		return
	}
	r.ministerEngine.RecordMemory(playerID, role, entry)
}

func (r *Runtime) PreparePlanningStartStateIfNeeded() {
	if r == nil || r.state == nil || r.state.Phase != domain.PhasePlanning.String() {
		return
	}
	r.preparedMinisterDraftsMu.RLock()
	if r.planningStartPreparedTurn == r.state.Turn {
		r.preparedMinisterDraftsMu.RUnlock()
		return
	}
	r.preparedMinisterDraftsMu.RUnlock()
	// 这里是 planning-start 状态推进的唯一受控入口。
	// 其它调用方只读取缓存结果，不再各自直接推进状态。
	result := PreparePlanningStartState(r.state)
	r.PrepareMinisterDraftCacheForTurn(r.state.Turn)
	r.ApplyPreparedMinisterDrafts(r.state.Turn)
	r.preparedMinisterDraftsMu.Lock()
	r.planningStartResult = result
	r.planningStartPreparedTurn = r.state.Turn
	r.preparedMinisterDraftsMu.Unlock()
}

func (r *Runtime) PlanningStartResult() *PlanningStartResult {
	if r == nil {
		return nil
	}
	r.preparedMinisterDraftsMu.RLock()
	defer r.preparedMinisterDraftsMu.RUnlock()
	return r.planningStartResult
}

func (r *Runtime) SendPlanningStart(ctx context.Context, participantID string) error {
	if r == nil || r.state == nil {
		return nil
	}
	p, ok := r.findParticipant(participantID)
	if !ok || !p.IsHuman() {
		return nil
	}
	var planningStartEvents []event.Event
	r.preparedMinisterDraftsMu.RLock()
	if r.planningStartResult != nil {
		planningStartEvents = r.planningStartResult.Events
	}
	r.preparedMinisterDraftsMu.RUnlock()
	msg := BuildPlanningStartMessageFromObservation(r.state, r.BuildObservation(participantID), r.state.Phase, planningStartEvents)
	if msg == nil {
		return nil
	}
	return r.SendToParticipant(ctx, participantID, msg)
}

func (r *Runtime) GenerateMinisterReports(ctx context.Context) {
	if r == nil || r.ministerEngine == nil {
		return
	}
	r.ministerEngine.GenerateReports(ctx, r)
}

func (r *Runtime) Broadcast(ctx context.Context, msg proto.Message) {
	ctx = transport.ContextWithGameSessionID(ctx, r.gameSessionID())
	if r.transport == nil {
		return
	}
	for _, binding := range r.participants {
		if !binding.Participant.IsHuman() {
			continue
		}
		_ = r.transport.Send(ctx, binding.Participant.ID, msg)
	}
}

func (r *Runtime) NextChatSequence() int64 {
	if r == nil {
		return 0
	}
	r.chatMu.Lock()
	defer r.chatMu.Unlock()
	r.nextChatSequence++
	return r.nextChatSequence
}

func (r *Runtime) ChatHistorySnapshot() []*pb.ChatEntry {
	if r == nil {
		return nil
	}
	r.chatMu.Lock()
	defer r.chatMu.Unlock()
	out := make([]*pb.ChatEntry, 0, len(r.chatHistory))
	for _, entry := range r.chatHistory {
		if entry == nil {
			continue
		}
		out = append(out, proto.Clone(entry).(*pb.ChatEntry))
	}
	return out
}

func (r *Runtime) gameSessionID() string {
	if r == nil {
		return ""
	}
	if r.state != nil && r.state.GameID != "" {
		return r.state.GameID
	}
	return r.ID
}

func (r *Runtime) participantIDs() []string {
	ids := make([]string, 0, len(r.participants))
	for _, binding := range r.participants {
		if strings.TrimSpace(binding.Participant.ID) == "" {
			continue
		}
		ids = append(ids, binding.Participant.ID)
	}
	return ids
}

func (r *Runtime) participantUsernames() []string {
	usernames := make([]string, 0, len(r.participants))
	for _, binding := range r.participants {
		usernames = append(usernames, binding.Participant.Username)
	}
	return usernames
}

func (r *Runtime) grantDevStartingResources() {
	if r == nil || r.state == nil || !r.IsDevMode() {
		return
	}

	for _, player := range r.state.Players {
		if player == nil {
			continue
		}
		player.Resources.Set(domain.ResourceOre, 200)
		player.Resources.Set(domain.ResourceWood, 200)
		player.Resources.Set(domain.ResourceFood, 200)
	}
}

func (r *Runtime) grantDevStartingUnlocks(catalog *staticdata.Catalog) {
	if r == nil || r.state == nil || !r.IsDevMode() || catalog == nil {
		return
	}

	buildingIDs := catalog.BuildingIDs()
	if len(buildingIDs) == 0 {
		return
	}

	for _, player := range r.state.Players {
		if player == nil {
			continue
		}

		for _, buildingID := range buildingIDs {
			if strings.TrimSpace(buildingID) == "" {
				continue
			}
			player.Research.UnlockBuilding(buildingID)
		}
	}
}

func (r *Runtime) bootstrapStartingPlayers() error {
	if r == nil || r.state == nil || r.state.World == nil || r.state.Map == nil {
		return nil
	}

	for _, rawPlayerID := range r.participantIDs() {
		playerID := strings.TrimSpace(rawPlayerID)
		if playerID == "" {
			continue
		}

		spawnPos, ok := r.state.Map.PlayerSpawns[playerID]
		if !ok {
			continue
		}

		spawnEntry, ok := domain.GetNodeAt(r.state.World, spawnPos)
		if !ok || spawnEntry == nil {
			continue
		}

		r.ensureCapitalAtSpawn(playerID, spawnEntry)

		playerState := r.state.Players[playerID]
		capitalNodeID := ""
		if playerState != nil {
			capitalNodeID = playerState.CapitalCityID
		}
		slog.Info("runtime bootstrap player",
			"game_id", r.ID,
			"player_id", playerID,
			"capital_node", capitalNodeID,
		)
	}

	if err := r.validateBootstrapState(); err != nil {
		return fmt.Errorf("runtime bootstrap invariant failed: %w", err)
	}
	return nil
}

func (r *Runtime) ensureCapitalAtSpawn(playerID string, spawnEntry *donburi.Entry) {
	if r == nil || r.state == nil || spawnEntry == nil {
		return
	}

	if !spawnEntry.HasComponent(ecs.BuildingC) {
		ecs.CreateBuilding(r.state.World, "city_core", playerID, ecs.NodeC.Get(spawnEntry).ID, spawnEntry)
		r.state.RefreshBuildingMaxHPAtEntry(spawnEntry)
	}
	if !spawnEntry.HasComponent(ecs.BuildingC) {
		return
	}

	building := ecs.BuildingC.Get(spawnEntry)
	if !strings.EqualFold(string(building.Type), "city_core") {
		return
	}

	cityID := ecs.NodeC.Get(spawnEntry).ID
	footprintEntries, _, reason := ecs.TerritoryFootprint(r.state, spawnEntry)
	if reason == "" {
		for _, entry := range footprintEntries {
			if entry == nil {
				continue
			}
			node := ecs.NodeC.Get(entry)
			node.Owner = playerID
			node.TerritoryOwner = playerID
		}
	} else {
		node := ecs.NodeC.Get(spawnEntry)
		node.Owner = playerID
		node.TerritoryOwner = playerID
	}

	building.Owner = playerID
	buildingcore.SetBinding(spawnEntry, domain.BuildingScopeCityCore, cityID, cityID)
	playerState := r.state.Players[playerID]
	if playerState == nil {
		return
	}
	playerState.CapitalCityID = cityID
	playerState.CapitalCityCoreHP = building.HP
	cityState := r.state.EnsureCityState(playerID, cityID)
	if cityState != nil {
		cityState.CoreNodeID = cityID
		cityState.OwnerID = playerID
		cityState.OnlineOnTurn = 0
	}
}

func (r *Runtime) validateBootstrapState() error {
	if r == nil || r.state == nil {
		return fmt.Errorf("state missing")
	}

	playerIDs := r.participantIDs()
	if len(playerIDs) == 0 {
		return fmt.Errorf("no participants available for bootstrap")
	}

	for _, rawPlayerID := range playerIDs {
		playerID := strings.TrimSpace(rawPlayerID)
		if playerID == "" {
			continue
		}

		playerState := r.state.Players[playerID]
		if playerState == nil {
			return fmt.Errorf("player %s state missing", playerID)
		}
		if strings.TrimSpace(playerState.CapitalCityID) == "" {
			return fmt.Errorf("player %s capital city missing", playerID)
		}

		capitalEntry, ok := r.state.GetNode(playerState.CapitalCityID)
		if !ok || capitalEntry == nil {
			return fmt.Errorf("player %s capital node %s missing", playerID, playerState.CapitalCityID)
		}
		if !capitalEntry.HasComponent(ecs.BuildingC) {
			return fmt.Errorf("player %s capital node %s missing city_core building", playerID, playerState.CapitalCityID)
		}
		building := ecs.BuildingC.Get(capitalEntry)
		if !strings.EqualFold(string(building.Type), "city_core") {
			return fmt.Errorf("player %s capital node %s has building %s", playerID, playerState.CapitalCityID, building.Type)
		}
		if strings.TrimSpace(building.Owner) != playerID {
			return fmt.Errorf("player %s capital node %s owned by %s", playerID, playerState.CapitalCityID, building.Owner)
		}
	}
	return nil
}

func (r *Runtime) initializeCityStates() {
	if r == nil || r.state == nil || r.state.World == nil {
		return
	}

	ecs.NodesWithBuilding(r.state.World).Each(r.state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}

		building := ecs.BuildingC.Get(entry)
		if !strings.EqualFold(string(building.Type), "city_core") {
			return
		}

		node := ecs.NodeC.Get(entry)
		playerID := strings.TrimSpace(building.Owner)
		if playerID == "" {
			playerID = strings.TrimSpace(node.Owner)
		}
		if playerID == "" {
			playerID = strings.TrimSpace(node.TerritoryOwner)
		}
		if playerID == "" {
			return
		}

		playerState := r.state.Players[playerID]
		if playerState == nil {
			return
		}
		cityState := r.state.EnsureCityState(playerID, node.ID)
		if cityState != nil {
			cityState.CoreNodeID = node.ID
			cityState.OwnerID = playerID
		}
		if r.state.Map != nil {
			if spawnPos, ok := r.state.Map.PlayerSpawns[playerID]; ok {
				pos := ecs.PositionC.Get(entry)
				if pos.X == spawnPos.X && pos.Y == spawnPos.Y {
					playerState.CapitalCityID = node.ID
					playerState.CapitalCityCoreHP = building.HP
				}
			}
		}
	})
}

func (r *Runtime) sendGameInit(p participant.Participant) {
	if r.state == nil {
		return
	}
	observation := r.BuildObservation(p.ID)
	msg := &pb.MsgGameInit{
		GameId:       r.state.GameID,
		YourPlayerId: p.ID,
		Turn:         int32(r.state.Turn),
		Phase:        r.state.Phase,
		MapWidth:     int32(r.state.Map.Width),
		MapHeight:    int32(r.state.Map.Height),
		MyPlayer:     observation.MyPlayer,
		Ministers:    gamequery.BuildMinisterRosterViews(),
		Nodes:        observation.Nodes,
		Units:        observation.Units,
	}
	_ = r.SendToParticipant(context.Background(), p.ID, msg)
}

func (r *Runtime) BuildObservation(participantID string) *gamequery.ObservationSnapshot {
	if r == nil {
		return nil
	}
	if r.observations == nil {
		r.observations = gamequery.NewObservationStore()
	}
	return r.observations.BuildObservation(r.state, participantID)
}

func (r *Runtime) SetDebugFullMapVisibility(participantID string, enabled bool) {
	if r == nil {
		return
	}
	if r.observations == nil {
		r.observations = gamequery.NewObservationStore()
	}
	r.observations.SetOmniscient(participantID, enabled)
}

func (r *Runtime) DebugFullMapVisibility(participantID string) bool {
	if r == nil || r.observations == nil {
		return false
	}
	return r.observations.IsOmniscient(participantID)
}

func (r *Runtime) RefreshDebugView(ctx context.Context, participantID string) (bool, error) {
	if r == nil || r.state == nil || r.state.Phase != domain.PhasePlanning.String() {
		return false, nil
	}
	p, ok := r.findParticipant(participantID)
	if !ok || !p.IsHuman() {
		return false, nil
	}
	return true, r.SendPlanningStart(ctx, participantID)
}

func (r *Runtime) RevealNodeView(participantID string, nodeID string) *pb.NodeView {
	if r == nil {
		return nil
	}
	if r.observations == nil {
		r.observations = gamequery.NewObservationStore()
	}
	return r.observations.RevealNodeView(r.state, participantID, nodeID)
}

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

func (r *Runtime) findParticipant(playerID string) (participant.Participant, bool) {
	for _, binding := range r.participants {
		if binding.Participant.ID == playerID {
			return binding.Participant, true
		}
	}
	return participant.Participant{}, false
}

func (r *Runtime) findParticipantBinding(participantID string) (ParticipantBinding, bool) {
	for _, binding := range r.participants {
		if binding.Participant.ID == participantID {
			return binding, true
		}
	}
	return ParticipantBinding{}, false
}

func resolveRequestedSections(catalog *staticdata.Catalog, req *pb.MsgStaticCatalogSyncRequest) []string {
	if catalog == nil {
		return nil
	}
	manifest := catalog.Manifest()
	if req == nil {
		return append([]string(nil), manifest.RequiredSections...)
	}
	if req.GetForceFullSync() {
		return append([]string(nil), manifest.RequiredSections...)
	}
	if len(req.GetSectionNames()) == 0 {
		if req.GetBundleHash() != "" && req.GetBundleHash() == catalog.BundleHash() {
			return nil
		}
		return append([]string(nil), manifest.RequiredSections...)
	}

	allowed := make(map[string]struct{}, len(manifest.RequiredSections))
	for _, section := range manifest.RequiredSections {
		allowed[section] = struct{}{}
	}

	seen := make(map[string]struct{}, len(req.GetSectionNames()))
	sections := make([]string, 0, len(req.GetSectionNames()))
	for _, section := range req.GetSectionNames() {
		section = strings.TrimSpace(section)
		if section == "" {
			continue
		}
		if _, ok := allowed[section]; !ok {
			continue
		}
		if _, ok := seen[section]; ok {
			continue
		}
		seen[section] = struct{}{}
		sections = append(sections, section)
	}
	return sections
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

func compressCatalogSection(raw []byte) ([]byte, error) {
	var buffer bytes.Buffer
	writer := gzip.NewWriter(&buffer)
	if _, err := writer.Write(raw); err != nil {
		_ = writer.Close()
		return nil, err
	}
	if err := writer.Close(); err != nil {
		return nil, err
	}
	return buffer.Bytes(), nil
}

func splitCatalogSection(raw []byte, chunkSize int) [][]byte {
	if chunkSize <= 0 || len(raw) <= chunkSize {
		return [][]byte{raw}
	}
	chunks := make([][]byte, 0, (len(raw)+chunkSize-1)/chunkSize)
	for start := 0; start < len(raw); start += chunkSize {
		end := start + chunkSize
		if end > len(raw) {
			end = len(raw)
		}
		chunk := make([]byte, end-start)
		copy(chunk, raw[start:end])
		chunks = append(chunks, chunk)
	}
	return chunks
}
