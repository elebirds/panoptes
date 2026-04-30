// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局会话模块的运行时状态与生命周期。

package session

import (
	"context"
	"fmt"
	"sync"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/game/participant"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/transport"
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

func (r *Runtime) State() *domain.GameState {
	return r.state
}

func (r *Runtime) SetState(state *domain.GameState) {
	r.state = state
	if r.state != nil {
		r.state.RefreshStructuredModel()
	}
	r.resetObservations()
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
