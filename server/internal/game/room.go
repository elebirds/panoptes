// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的房间模型与编排逻辑。

package game

import (
	"context"
	"errors"
	"log/slog"
	"strings"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/event"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/participant"
	"github.com/elebirds/panoptes/internal/game/planning"
	gameprojection "github.com/elebirds/panoptes/internal/game/projection"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	gameresolution "github.com/elebirds/panoptes/internal/game/resolution"
	gamesession "github.com/elebirds/panoptes/internal/game/session"
	gameturn "github.com/elebirds/panoptes/internal/game/turn"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/elebirds/panoptes/internal/transport"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type GameRoom struct {
	ID          string
	runtime     *gamesession.Runtime
	coordinator *gameturn.Coordinator
	prepared    *domain.GameState
}

type Room = GameRoom

var _ transport.GameRoom = (*GameRoom)(nil)
var _ planning.Session = (*GameRoom)(nil)

func NewRoom(id string, participants []ParticipantSpec, t transport.GameTransport, cfg *config.Config) *GameRoom {
	room := &GameRoom{
		ID: id,
	}
	room.runtime = gamesession.NewRuntime(id, buildParticipantBindings(participants), t, cfg)
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	return room
}

func NewPreparedRoom(id string, participants []ParticipantSpec, t transport.GameTransport, cfg *config.Config, state *domain.GameState) *GameRoom {
	room := NewRoom(id, participants, t, cfg)
	room.prepared = state
	return room
}

func (r *GameRoom) Start() {
	if r == nil || r.runtime == nil {
		return
	}
	Registry.InvalidateRoomsForParticipantsExcept(r.ID, r.ParticipantIDs())
	var err error
	if r.prepared != nil {
		err = r.runtime.InitializePrepared(r.prepared)
	} else {
		err = r.runtime.Initialize()
	}
	if err != nil {
		slog.Error("对局初始化失败", "room_id", r.ID, "error", err)
		return
	}
	Registry.Register(r)
	go r.coordinator.Start()
}

func (r *GameRoom) HandleGameCommand(ctx cmddispatch.InboundContext, cmd *pb.GameCommand) error {
	if r == nil || r.coordinator == nil {
		return ErrPhaseMismatch
	}
	if err := r.coordinator.HandleGameCommand(ctx, cmd); errors.Is(err, gameturn.ErrPhaseMismatch) {
		return ErrPhaseMismatch
	} else {
		return err
	}
}

func (r *GameRoom) State() *domain.GameState {
	if r == nil || r.runtime == nil {
		return nil
	}
	return r.runtime.State()
}

func (r *GameRoom) ParticipantIDs() []string {
	if r == nil || r.runtime == nil {
		return nil
	}
	return r.runtime.ParticipantIDs()
}

func (r *GameRoom) Participant(participantID string) (participant.Participant, bool) {
	if r == nil || r.runtime == nil {
		return participant.Participant{}, false
	}
	return r.runtime.Participant(participantID)
}

func (r *GameRoom) PlayerIDs() []string {
	return r.ParticipantIDs()
}

func (r *GameRoom) HumanParticipantIDs() []string {
	if r == nil || r.runtime == nil {
		return nil
	}
	humans := r.runtime.HumanParticipants()
	ids := make([]string, 0, len(humans))
	for _, currentParticipant := range humans {
		ids = append(ids, currentParticipant.ID)
	}
	return ids
}

func (r *GameRoom) IsHumanParticipant(participantID string) bool {
	if r == nil || r.runtime == nil {
		return false
	}
	for _, currentParticipant := range r.runtime.HumanParticipants() {
		if currentParticipant.ID == participantID {
			return true
		}
	}
	return false
}

func (r *GameRoom) Submit(playerID string) {
	if r != nil && r.coordinator != nil {
		r.coordinator.Submit(playerID)
	}
}

func (r *GameRoom) IsDevMode() bool {
	return r != nil && r.runtime != nil && r.runtime.IsDevMode()
}

func (r *GameRoom) IsMinisterStrongMode() bool {
	return r != nil && r.runtime != nil && r.runtime.IsMinisterStrongMode()
}

func (r *GameRoom) SendToPlayer(ctx context.Context, playerID string, msg proto.Message) error {
	if r == nil || r.runtime == nil {
		return nil
	}
	ctx = transport.ContextWithGameSessionID(ctx, r.ID)
	if hooks := currentDebugHooks(); hooks.RecordOutgoingMessage != nil {
		hooks.RecordOutgoingMessage(r.ID, playerID, msg, transport.EventMetaFromContext(ctx))
	}
	return r.runtime.SendToPlayer(ctx, playerID, msg)
}

func (r *GameRoom) QueueBuildOrder(order domain.BuildOrder) {
	if state := r.State(); state != nil {
		state.TurnRuntime.Planning.UpsertBuildOrder(order)
	}
}

func (r *GameRoom) QueueRecipeSelection(order domain.RecipeSelectionOrder) {
	if state := r.State(); state != nil {
		state.TurnRuntime.Planning.UpsertRecipeSelection(order)
	}
}

func (r *GameRoom) SetInstitutionLoadout(playerID string, institutionIDs []string) {
	if state := r.State(); state != nil {
		state.TurnRuntime.Planning.SetPendingInstitutionLoadout(playerID, append([]string(nil), institutionIDs...))
	}
}

func (r *GameRoom) SetMinisterDirective(playerID string, directive string) {
	state := r.State()
	if state == nil {
		return
	}
	if state.TurnRuntime.Planning.MinisterDirectives == nil {
		state.TurnRuntime.Planning.MinisterDirectives = make(map[string]string)
	}
	state.TurnRuntime.Planning.MinisterDirectives[playerID] = directive
}

func (r *GameRoom) SetWarDirectives(playerID string, directives []domain.WarZoneDirective) {
	state := r.State()
	if state == nil {
		return
	}
	for _, directive := range directives {
		state.TurnRuntime.Planning.UpsertWarDirective(playerID, directive)
	}
}

func (r *GameRoom) SetUnitOrder(order gameorders.UnitOrder) {
	gameorders.ApplyPlanningUnitOrder(r.State(), order, r.routePreviewCallbacks())
}

func (r *GameRoom) CancelUnitOrder(playerID string, unitID string) {
	gameorders.CancelPlanningUnitOrder(r.State(), playerID, unitID)
}

func (r *GameRoom) SendPlanningSnapshot(ctx context.Context, playerID string) error {
	return r.SendToPlayer(ctx, playerID, gamequery.BuildPlanningSnapshot(r.State(), playerID))
}

func (r *GameRoom) SetMinisterEngine(engine *ministerengine.MinisterEngine) {
	if r == nil || r.runtime == nil {
		return
	}
	r.runtime.SetMinisterEngine(engine)
}

func (r *GameRoom) RecordMinisterMemory(playerID string, role string, entry ministerengine.MemoryEntry) {
	if r == nil || r.runtime == nil {
		return
	}
	r.runtime.RecordMinisterMemory(playerID, role, entry)
}

// 亲政模式相关方法

func (r *GameRoom) SetPlayerMandateMode(playerID string, enabled bool) {
	if r == nil || r.runtime == nil {
		return
	}
	r.runtime.SetPlayerMandateMode(playerID, enabled)
}

func (r *GameRoom) IsPlayerInMandateMode(playerID string) bool {
	if r == nil || r.runtime == nil {
		return false
	}
	return r.runtime.IsPlayerInMandateMode(playerID)
}

func (r *GameRoom) HasParticipant(participantID string) bool {
	if strings.TrimSpace(participantID) == "" {
		return false
	}
	for _, id := range r.ParticipantIDs() {
		if id == participantID {
			return true
		}
	}
	return false
}

func (r *GameRoom) SetDebugFullMapVisibility(participantID string, enabled bool) {
	if r == nil || r.runtime == nil {
		return
	}
	r.runtime.SetDebugFullMapVisibility(participantID, enabled)
}

func (r *GameRoom) DebugFullMapVisibility(participantID string) bool {
	if r == nil || r.runtime == nil {
		return false
	}
	return r.runtime.DebugFullMapVisibility(participantID)
}

func (r *GameRoom) BuildObservationForParticipant(participantID string) *gamequery.ObservationSnapshot {
	if r == nil || r.runtime == nil {
		return nil
	}
	return r.runtime.BuildObservation(participantID)
}

func (r *GameRoom) RefreshDebugView(ctx context.Context, participantID string) (bool, error) {
	if r == nil || r.runtime == nil {
		return false, nil
	}
	return r.runtime.RefreshDebugView(ctx, participantID)
}

func (r *GameRoom) BuildNodeViewForPlayer(nodeID string, viewerID string) *pb.NodeView {
	if r == nil || r.runtime == nil {
		return nil
	}
	return r.runtime.RevealNodeView(viewerID, nodeID)
}

func (r *GameRoom) NodeByID(nodeID string) (*donburi.Entry, bool) {
	state := r.State()
	if state == nil {
		return nil, false
	}
	return state.GetNode(nodeID)
}

func (r *GameRoom) broadcastGameSync(collector *gameresolution.Collector) {
	state := r.State()
	if state == nil {
		return
	}

	nextPhase := domain.PhasePlanning.String()
	if state.IsOver || r.shouldStopAfterResolution() {
		nextPhase = ""
	}
	for _, participantID := range r.HumanParticipantIDs() {
		observation := r.runtime.BuildObservation(participantID)
		syncMsg := gameprojection.ProjectGameSyncFromObservation(
			state,
			observation,
			int32(state.Turn),
			domain.PhaseResolving.String(),
			nextPhase,
			collector,
		)
		_ = r.SendToPlayer(context.Background(), participantID, syncMsg)
		if hooks := currentDebugHooks(); hooks.RecordGameSync != nil {
			hooks.RecordGameSync(r.ID, participantID, syncMsg)
		}
	}
}

func (r *GameRoom) Broadcast(ctx context.Context, msg proto.Message) {
	if r != nil && r.runtime != nil {
		ctx = transport.ContextWithGameSessionID(ctx, r.ID)
		r.runtime.Broadcast(ctx, msg)
	}
}

func (r *GameRoom) NextChatSequence() int64 {
	if r == nil || r.runtime == nil {
		return 0
	}
	return r.runtime.NextChatSequence()
}

func (r *GameRoom) RunTurnResolution() {
	RunTurnResolution(r)
}

func (r *GameRoom) ShouldStopAfterResolution() bool {
	return r.shouldStopAfterResolution()
}

func (r *GameRoom) CheckGameOver() {
	r.checkGameOver()
}

func (r *GameRoom) HandleDraw() {
	r.handleDraw()
}

func (r *GameRoom) shouldStopAfterResolution() bool {
	state := r.State()
	if state == nil || state.IsOver {
		return true
	}
	rules := staticdata.Default().Rules()
	return rules.MaxTurns > 0 && state.Turn >= rules.MaxTurns
}

func (r *GameRoom) checkGameOver() {
	state := r.State()
	if state == nil || !state.IsOver {
		return
	}
	msg := &pb.MsgGameOver{WinnerId: state.WinnerID, Reason: state.OverReason, Narrative: state.Narrative}
	if hooks := currentDebugHooks(); hooks.RecordGameOver != nil {
		hooks.RecordGameOver(r.ID, msg)
	}
	r.Broadcast(context.Background(), msg)
	Registry.Unregister(r.ID)
	if r.runtime != nil {
		r.runtime.Cancel()
	}
}

func (r *GameRoom) handleDraw() {
	state := r.State()
	if state == nil {
		return
	}
	event.GameOverEvent{Reason: "timeout_draw"}.Apply(state.World, state)
	msg := &pb.MsgGameOver{WinnerId: "", Reason: "timeout_draw"}
	if hooks := currentDebugHooks(); hooks.RecordGameOver != nil {
		hooks.RecordGameOver(r.ID, msg)
	}
	r.Broadcast(context.Background(), msg)
	Registry.Unregister(r.ID)
	if r.runtime != nil {
		r.runtime.Cancel()
	}
}

func (r *GameRoom) forfeitDisconnectedPlayer(playerID string) bool {
	state := r.State()
	if state == nil || state.IsOver {
		return false
	}

	winnerID := r.firstPlayerExcept(playerID)
	if winnerID == "" {
		return false
	}

	event.GameOverEvent{WinnerID: winnerID, Reason: "player_disconnected"}.Apply(state.World, state)
	msg := &pb.MsgGameOver{WinnerId: winnerID, Reason: "player_disconnected"}
	if hooks := currentDebugHooks(); hooks.RecordGameOver != nil {
		hooks.RecordGameOver(r.ID, msg)
	}
	r.Broadcast(context.Background(), msg)
	if r.runtime != nil {
		r.runtime.Cancel()
	}
	return true
}

func (r *GameRoom) firstPlayerExcept(playerID string) string {
	for _, currentPlayerID := range r.ParticipantIDs() {
		if currentPlayerID == "" || currentPlayerID == playerID {
			continue
		}
		return currentPlayerID
	}
	return ""
}

func (r *GameRoom) nodeIDAt(pos domain.Position) string {
	state := r.State()
	if state == nil || state.World == nil {
		return ""
	}
	if entry, ok := domain.GetNodeAt(state.World, pos); ok {
		return ecs.NodeC.Get(entry).ID
	}
	return ""
}
