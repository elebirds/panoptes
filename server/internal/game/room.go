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

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	gamereport "github.com/elebirds/panoptes/internal/game/resolution/report"
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
	Players     []Player
	runtime     *gamesession.Runtime
	coordinator *gameturn.Coordinator
}

type Room = GameRoom

var _ transport.GameRoom = (*GameRoom)(nil)
var _ planning.Session = (*GameRoom)(nil)

func NewRoom(id string, players []Player, t transport.GameTransport, cfg *config.Config) *GameRoom {
	sessionPlayers := make([]gamesession.Player, 0, len(players))
	for _, player := range players {
		sessionPlayers = append(sessionPlayers, player)
	}

	room := &GameRoom{
		ID:      id,
		Players: append([]Player(nil), players...),
	}
	room.runtime = gamesession.NewRuntime(id, sessionPlayers, t, cfg)
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	return room
}

func (r *GameRoom) Start() {
	if r == nil || r.runtime == nil {
		return
	}
	if err := r.runtime.Initialize(); err != nil {
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

func (r *GameRoom) PlayerIDs() []string {
	if r == nil || r.runtime == nil {
		return nil
	}
	return r.runtime.PlayerIDs()
}

func (r *GameRoom) NotifyTurn(phase string) {
	ctx := context.Background()
	for _, player := range r.Players {
		player.NotifyTurn(ctx, r, phase)
	}
}

func (r *GameRoom) Submit(playerID string) {
	if r != nil && r.coordinator != nil {
		r.coordinator.Submit(playerID)
	}
}

func (r *GameRoom) IsDevMode() bool {
	return r != nil && r.runtime != nil && r.runtime.IsDevMode()
}

func (r *GameRoom) SendToPlayer(ctx context.Context, playerID string, msg proto.Message) error {
	if r == nil || r.runtime == nil {
		return nil
	}
	return r.runtime.SendToPlayer(ctx, playerID, msg)
}

func (r *GameRoom) QueueBuildOrder(order domain.BuildOrder) {
	if state := r.State(); state != nil {
		state.TurnRuntime.Planning.BuildOrders = append(state.TurnRuntime.Planning.BuildOrders, order)
	}
}

func (r *GameRoom) QueueResearchOrder(order domain.ResearchOrder) {
	if state := r.State(); state != nil {
		state.TurnRuntime.Planning.ResearchOrders = append(state.TurnRuntime.Planning.ResearchOrders, order)
	}
}

func (r *GameRoom) QueueRecipeSelection(order domain.RecipeSelectionOrder) {
	if state := r.State(); state != nil {
		state.TurnRuntime.Planning.RecipeSelections = append(state.TurnRuntime.Planning.RecipeSelections, order)
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
	if state.TurnRuntime.Planning.WarDirectives == nil {
		state.TurnRuntime.Planning.WarDirectives = make(map[string][]domain.WarZoneDirective)
	}
	state.TurnRuntime.Planning.WarDirectives[playerID] = append([]domain.WarZoneDirective(nil), directives...)
}

func (r *GameRoom) SetUnitOrder(order gameorders.UnitOrder) {
	state := r.State()
	if state == nil || order.UnitID == "" {
		return
	}
	if state.TurnRuntime.Planning.UnitOrders == nil {
		state.TurnRuntime.Planning.UnitOrders = make(map[string]domain.UnitDirective)
	}
	if order.PlayerID == "" {
		order.PlayerID = r.playerIDForUnit(order.UnitID)
	}
	state.TurnRuntime.Planning.UnitOrders[order.UnitID] = order.ToDirective()
	if resolutionOrder, ok := order.ToResolutionOrder(); ok && resolutionOrder.Action == domain.UnitResolutionActionMove {
		r.syncActiveMarchWithOrder(resolutionOrder)
		return
	}
	delete(state.TurnRuntime.Resolving.ActiveMarches, order.UnitID)
}

func (r *GameRoom) CancelUnitOrder(playerID string, unitID string) {
	state := r.State()
	if state == nil {
		return
	}
	directive, ok := state.TurnRuntime.Planning.UnitOrders[unitID]
	if !ok {
		return
	}
	if playerID != "" && directive.PlayerID != "" && directive.PlayerID != playerID {
		return
	}
	delete(state.TurnRuntime.Planning.UnitOrders, unitID)
	delete(state.TurnRuntime.Resolving.ActiveMarches, unitID)
}

func (r *GameRoom) SendPlanningSnapshot(ctx context.Context, playerID string) error {
	return r.SendToPlayer(ctx, playerID, gamequery.BuildPlanningSnapshot(r.State(), playerID))
}

func (r *GameRoom) BuildNodeViewForPlayer(nodeID string, viewerID string) *pb.NodeView {
	state := r.State()
	if state == nil {
		return nil
	}
	nodeEntry, ok := state.GetNode(nodeID)
	if !ok {
		return nil
	}
	return gamequery.BuildNodeView(state, nodeEntry, viewerID)
}

func (r *GameRoom) NodeByID(nodeID string) (*donburi.Entry, bool) {
	state := r.State()
	if state == nil {
		return nil, false
	}
	return state.GetNode(nodeID)
}

func (r *GameRoom) broadcastTurnSettlement(unitEvents []event.Event, mapEvents []*pb.TurnEvent, economyEvents []event.Event) {
	state := r.State()
	if state == nil {
		return
	}

	nextPhase := domain.PhasePlanning.String()
	if state.IsOver || r.shouldStopAfterResolution() {
		nextPhase = ""
	}
	for _, player := range r.Players {
		if player.IsBot() {
			continue
		}
		msg := gamereport.BuildTurnSettlement(
			state,
			player.PlayerID(),
			int32(state.Turn),
			domain.PhaseResolving.String(),
			nextPhase,
			unitEvents,
			mapEvents,
			economyEvents,
		)
		_ = player.Send(context.Background(), msg)
	}
}

func (r *GameRoom) Broadcast(ctx context.Context, msg proto.Message) {
	if r != nil && r.runtime != nil {
		r.runtime.Broadcast(ctx, msg)
	}
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
	state.IsOver = true
	state.WinnerID = ""
	state.OverReason = "timeout_draw"
	r.Broadcast(context.Background(), &pb.MsgGameOver{WinnerId: "", Reason: "timeout_draw"})
	Registry.Unregister(r.ID)
	if r.runtime != nil {
		r.runtime.Cancel()
	}
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

func (r *GameRoom) playerIDForUnit(unitID string) string {
	state := r.State()
	if state == nil || state.World == nil {
		return ""
	}
	var playerID string
	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		if playerID != "" {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.ID == unitID {
			playerID = stats.Faction
		}
	})
	return playerID
}
