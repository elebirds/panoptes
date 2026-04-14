package game

import (
	"context"
	"fmt"
	"log/slog"
	"strings"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	"github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/event"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	gamereport "github.com/elebirds/panoptes/internal/game/resolution/report"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/elebirds/panoptes/internal/transport"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type GameRoom struct {
	ID        string
	Players   []Player
	Phase     string
	Turn      int
	cfg       *config.Config
	transport transport.GameTransport
	submitCh  chan string
	cancelFn  context.CancelFunc
	state     *domain.GameState

	pendingBuilds      []domain.BuildOrder
	ministerDirectives map[string]string
	warDirectives      map[string][]planning.WarZoneDirective
	plannedUnitOrders  map[string]gameorders.UnitOrder
	router             any
	ministerEngine     *minister.MinisterEngine
	planningService    *planning.Service
}

type Room = GameRoom

var _ transport.GameRoom = (*GameRoom)(nil)

func NewRoom(id string, players []Player, t transport.GameTransport, cfg *config.Config) *GameRoom {
	return &GameRoom{
		ID:                 id,
		Players:            players,
		cfg:                cfg,
		transport:          t,
		submitCh:           make(chan string, len(players)*4+16),
		pendingBuilds:      make([]domain.BuildOrder, 0),
		ministerDirectives: make(map[string]string),
		warDirectives:      make(map[string][]planning.WarZoneDirective),
		plannedUnitOrders:  make(map[string]gameorders.UnitOrder),
		ministerEngine:     minister.NewMinisterEngine(nil),
		planningService:    &planning.Service{},
	}
}

func (r *GameRoom) Start() {
	catalog := staticdata.Default()
	mapID := r.cfg.MapID
	if mapID == "" {
		mapID = catalog.DefaultMapID()
	}
	baseMap, err := maploader.LoadMap(catalog, mapID)
	if err != nil {
		slog.Error("地图加载失败", "room_id", r.ID, "error", err)
		return
	}

	world := donburi.NewWorld()
	playerIDs := r.humanPlayerIDs()
	usernames := r.humanUsernames()
	seed := time.Now().UnixNano()
	runtimeMap := maploader.GenerateProceduralMap(baseMap, len(playerIDs), seed)
	if runtimeMap == nil {
		slog.Error("procedural map generation failed", "room_id", r.ID, "map_id", mapID)
		return
	}
	mapData := maploader.InitWorldFromMap(world, runtimeMap, playerIDs)
	r.state = domain.NewGameState(r.ID, playerIDs, usernames, mapData)
	r.state.World = world
	r.spawnInitialBaseVehicles()
	r.grantDevStartingResources()
	r.initializeCastleStates()

	r.Turn = r.state.Turn
	r.Phase = r.state.Phase

	slog.Info("map initialized",
		"room_id", r.ID,
		"base_map_id", baseMap.ID,
		"runtime_map_id", runtimeMap.ID,
		"seed", seed,
		"nodes", len(runtimeMap.Nodes))

	for _, player := range r.Players {
		if player.IsBot() {
			continue
		}
		r.sendStaticCatalogManifest(player)
		r.sendGameInit(player)
	}

	Registry.Register(r)
	go r.runLoop()
}

func (r *GameRoom) spawnInitialBaseVehicles() {
	if r == nil || r.state == nil || r.state.World == nil || r.state.Map == nil {
		return
	}

	for playerID := range r.state.Players {
		if strings.TrimSpace(playerID) == "" {
			continue
		}

		if hasTerritoryExpansionUnit(r.state.World, playerID) {
			continue
		}

		spawnPos, ok := r.state.Map.PlayerSpawns[playerID]
		if !ok {
			continue
		}

		entity := ecs.CreateUnit(r.state.World, string(domain.UnitTypeSettler), playerID, spawnPos)
		if !r.state.World.Valid(entity) {
			slog.Warn("spawn base vehicle failed: invalid entity", "room_id", r.ID, "player_id", playerID)
		}
	}
}

func hasTerritoryExpansionUnit(world donburi.World, playerID string) bool {
	found := false
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found || entry == nil {
			return
		}

		stats := ecs.UnitStatsC.Get(entry)
		if stats.Faction != playerID {
			return
		}

		switch strings.ToLower(strings.TrimSpace(string(stats.Type))) {
		case "settler", "pioneer", "expander", "engineer":
			found = true
		}
	})
	return found
}

func (r *GameRoom) grantDevStartingResources() {
	if r == nil || r.state == nil || r.cfg == nil || !r.cfg.DevMode {
		return
	}

	for _, player := range r.state.Players {
		if player == nil {
			continue
		}
		player.Resources.Set(domain.ResourceOre, 200)
		player.Resources.Set(domain.ResourceWood, 200)
		player.Resources.Set(domain.ResourceFood, 200)
		player.Resources.Set(domain.ResourceRefinedOre, 100)
		player.Resources.Set(domain.ResourceEngineerMat, 100)
	}
}

func (r *GameRoom) initializeCastleStates() {
	if r == nil || r.state == nil || r.state.World == nil {
		return
	}

	primaryCastleByPlayer := make(map[string]string, len(r.state.Players))
	if r.state.Map != nil {
		for playerID, spawnPos := range r.state.Map.PlayerSpawns {
			entry, ok := domain.GetNodeAt(r.state.World, spawnPos)
			if !ok || entry == nil || !entry.HasComponent(ecs.BuildingC) {
				continue
			}

			building := ecs.BuildingC.Get(entry)
			if !strings.EqualFold(string(building.Type), "castle") {
				continue
			}

			primaryCastleByPlayer[playerID] = ecs.NodeC.Get(entry).ID
		}
	}

	ecs.NodesWithBuilding(r.state.World).Each(r.state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}

		building := ecs.BuildingC.Get(entry)
		if !strings.EqualFold(string(building.Type), "castle") {
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

		r.state.EnsureCastleState(playerID, node.ID)
	})

	for playerID, playerState := range r.state.Players {
		if playerState == nil || len(playerState.Castles) == 0 {
			continue
		}

		primaryCastleID := strings.TrimSpace(primaryCastleByPlayer[playerID])
		if primaryCastleID == "" {
			for castleID := range playerState.Castles {
				primaryCastleID = castleID
				break
			}
		}
		if primaryCastleID == "" {
			continue
		}

		castle := playerState.Castles[primaryCastleID]
		if castle == nil || !castle.Resources.IsZero() {
			continue
		}

		castle.Resources = playerState.Resources.Clone()
	}
}

func (r *GameRoom) runLoop() {
	ctx, cancel := context.WithCancel(context.Background())
	r.cancelFn = cancel
	defer Registry.Unregister(r.ID)
	rules := staticdata.Default().Rules()
	planningTimeoutSec := rules.TurnTimeLimitDomestic + rules.TurnTimeLimitCombat
	if planningTimeoutSec <= 0 {
		planningTimeoutSec = 35
	}

	for !r.state.IsOver {
		if ctx.Err() != nil {
			return
		}
		if r.ministerEngine != nil {
			r.ministerEngine.GenerateReports(ctx, r)
		}

		r.planningService.Enter(r)
		r.setPhase(domain.PhasePlanning)
		r.NotifyTurn(domain.PhasePlanning.String())
		r.waitAllSubmit(time.Duration(planningTimeoutSec) * time.Second)
		r.setPhase(domain.PhaseResolving)
		RunTurnResolution(r)
		if r.state.IsOver {
			break
		}

		if rules.MaxTurns > 0 && r.state.Turn >= rules.MaxTurns {
			r.handleDraw()
			break
		}
		r.state.Turn++
		r.Turn = r.state.Turn
	}
}

func (r *GameRoom) waitAllSubmit(timeout time.Duration) {
	submitted := make(map[string]bool, len(r.Players))
	timer := time.NewTimer(timeout)
	defer timer.Stop()

	for {
		select {
		case playerID := <-r.submitCh:
			if playerID == "timeout" {
				return
			}
			submitted[playerID] = true
			if len(submitted) >= len(r.Players) {
				return
			}
		case <-timer.C:
			return
		}
	}
}

func (r *GameRoom) submitTurn(playerID string) {
	r.submitCh <- playerID
}

func (r *GameRoom) OnHumanSubmitTurn(playerID string) {
	if !r.isPhase(domain.PhasePlanning) {
		slog.Warn("忽略非规划阶段提交", "room_id", r.ID, "player_id", playerID, "phase", r.Phase, "error", ErrPhaseMismatch)
		return
	}
	r.submitTurn(playerID)
}

func (r *GameRoom) OnHumanSubmitTurnChecked(playerID string) error {
	if !r.isPhase(domain.PhasePlanning) {
		return ErrPhaseMismatch
	}
	r.submitTurn(playerID)
	return nil
}

func (r *GameRoom) OnHumanMessage(playerID, msgType string, payload []byte) error {
	if !r.isMessageAllowed(msgType) {
		return ErrPhaseMismatch
	}
	return r.planningService.HandleMessage(r, playerID, msgType, payload)
}

func (r *GameRoom) State() *domain.GameState {
	return r.state
}

func (r *GameRoom) PlayerIDs() []string {
	ids := make([]string, 0, len(r.Players))
	for _, p := range r.Players {
		ids = append(ids, p.PlayerID())
	}
	return ids
}

func (r *GameRoom) NotifyTurn(phase string) {
	ctx := context.Background()
	for _, player := range r.Players {
		player.NotifyTurn(ctx, r, phase)
	}
}

func (r *GameRoom) Submit(playerID string) {
	r.submitCh <- playerID
}

func (r *GameRoom) IsDevMode() bool {
	return r != nil && r.cfg != nil && r.cfg.DevMode
}

func (r *GameRoom) SendToPlayer(playerID string, msg proto.Message) error {
	for _, player := range r.Players {
		if player.PlayerID() == playerID {
			return player.Send(msg)
		}
	}
	return fmt.Errorf("player %s not found", playerID)
}

func (r *GameRoom) QueueBuildOrder(order domain.BuildOrder) {
	r.pendingBuilds = append(r.pendingBuilds, order)
}

func (r *GameRoom) QueueResearchOrder(order domain.ResearchOrder) {
	if r == nil || r.state == nil {
		return
	}
	r.state.PendingResearchOrders = append(r.state.PendingResearchOrders, order)
}

func (r *GameRoom) QueueRecipeSelection(order domain.RecipeSelectionOrder) {
	if r == nil || r.state == nil {
		return
	}
	r.state.PendingRecipeSelections = append(r.state.PendingRecipeSelections, order)
}

func (r *GameRoom) SetMinisterDirective(playerID string, directive string) {
	r.ministerDirectives[playerID] = directive
}

func (r *GameRoom) SetWarDirectives(playerID string, directives []planning.WarZoneDirective) {
	r.warDirectives[playerID] = directives
}

func (r *GameRoom) SetUnitOrder(order gameorders.UnitOrder) {
	if order.UnitID == "" {
		return
	}
	if order.PlayerID == "" {
		order.PlayerID = r.playerIDForUnit(order.UnitID)
	}
	r.plannedUnitOrders[order.UnitID] = order
	if order.Action == gameorders.ActionMove {
		if combatOrder, ok := order.ToCombatOrder(); ok {
			r.syncActiveMarchWithOrder(combatOrder)
		}
	}
}

func (r *GameRoom) CancelUnitOrder(playerID string, unitID string) {
	order, ok := r.plannedUnitOrders[unitID]
	if !ok {
		return
	}
	if playerID != "" && order.PlayerID != "" && order.PlayerID != playerID {
		return
	}
	delete(r.plannedUnitOrders, unitID)
	delete(r.state.ActiveMarches, unitID)
}

func (r *GameRoom) BuildNodeViewForPlayer(nodeID string, viewerID string) *pb.NodeView {
	nodeEntry, ok := r.state.GetNode(nodeID)
	if !ok {
		return nil
	}
	return gamequery.BuildNodeView(r.state, nodeEntry, viewerID)
}

func (r *GameRoom) NodeByID(nodeID string) (*donburi.Entry, bool) {
	return r.state.GetNode(nodeID)
}

func (r *GameRoom) broadcastTurnSettlement(unitEvents []event.Event, mapEvents []*pb.TurnEvent, economyEvents []event.Event) {
	if r == nil || r.state == nil {
		return
	}

	nextPhase := domain.PhasePlanning.String()
	if r.state.IsOver || r.shouldStopAfterResolution() {
		nextPhase = ""
	}
	for _, player := range r.Players {
		if player.IsBot() {
			continue
		}
		msg := gamereport.BuildTurnSettlement(
			r.state,
			player.PlayerID(),
			int32(r.state.Turn),
			domain.PhaseResolving.String(),
			nextPhase,
			unitEvents,
			mapEvents,
			economyEvents,
		)
		_ = player.Send(msg)
	}
}

func (r *GameRoom) setPhase(phase domain.TurnPhase) {
	r.Phase = phase.String()
	if r.state != nil {
		r.state.Phase = phase.String()
	}
}

func (r *GameRoom) isPhase(phase domain.TurnPhase) bool {
	if r == nil || r.state == nil {
		return false
	}
	return r.state.Phase == phase.String()
}

func (r *GameRoom) isMessageAllowed(msgType string) bool {
	if r == nil || r.state == nil {
		return false
	}

	switch r.state.Phase {
	case domain.PhasePlanning.String():
		switch msgType {
		case "MsgSetPolicy",
			"MsgBuildStructure",
			"MsgRevealNode",
			"MsgSetMinisterDirective",
			"MsgSetResearchTarget",
			"MsgSetBuildingRecipe",
			"MsgSetWarZone",
			"MsgWarZoneDirective",
			"MsgIssueUnitOrder",
			"MsgCancelUnitOrder",
			"MsgPlanningPathPreviewRequest",
			"MsgSubmitTurn":
			return true
		}
	}

	return false
}

func (r *GameRoom) shouldStopAfterResolution() bool {
	if r == nil || r.state == nil || r.state.IsOver {
		return true
	}
	rules := staticdata.Default().Rules()
	return rules.MaxTurns > 0 && r.state.Turn >= rules.MaxTurns
}

func (r *GameRoom) checkGameOver() {
	if r.state == nil || !r.state.IsOver {
		return
	}
	msg := &pb.MsgGameOver{WinnerId: r.state.WinnerID, Reason: r.state.OverReason, Narrative: r.state.Narrative}
	r.Broadcast(msg)
	Registry.Unregister(r.ID)
	if r.cancelFn != nil {
		r.cancelFn()
	}
}

func (r *GameRoom) handleDraw() {
	r.state.IsOver = true
	r.state.WinnerID = ""
	r.state.OverReason = "timeout_draw"
	r.Broadcast(&pb.MsgGameOver{WinnerId: "", Reason: "timeout_draw"})
	Registry.Unregister(r.ID)
	if r.cancelFn != nil {
		r.cancelFn()
	}
}

func (r *GameRoom) prepareCombatOrders() {
	if r.state.PendingCombatOrders == nil {
		r.state.PendingCombatOrders = make(map[string]domain.CombatOrder)
	}
	clear(r.state.PendingCombatOrders)

	for unitID, march := range r.state.ActiveMarches {
		r.state.PendingCombatOrders[unitID] = domain.CombatOrder{
			PlayerID:     march.PlayerID,
			UnitID:       unitID,
			Action:       domain.CombatActionMove,
			TargetNodeID: march.DestinationNodeID,
			PathNodeIDs:  append([]string(nil), march.LastPreview.PathNodeIDs...),
		}
	}

	for unitID, order := range r.plannedUnitOrders {
		if combatOrder, ok := order.ToCombatOrder(); ok {
			if combatOrder.Action == domain.CombatActionMove {
				if march, ok := r.state.ActiveMarches[unitID]; ok && len(march.LastPreview.PathNodeIDs) > 0 {
					combatOrder.TargetNodeID = march.DestinationNodeID
					combatOrder.PathNodeIDs = append([]string(nil), march.LastPreview.PathNodeIDs...)
				} else if preview, ok := r.buildRoutePreview(unitID, combatOrder.TargetNodeID); ok {
					combatOrder.PathNodeIDs = append([]string(nil), preview.PathNodeIDs...)
				}
			}
			r.state.PendingCombatOrders[unitID] = combatOrder.Normalized()
			continue
		}

		if order.Action == gameorders.ActionSettleCity && strings.TrimSpace(order.TargetNodeID) != "" {
			r.state.PendingCombatOrders[unitID] = domain.CombatOrder{
				PlayerID:     order.PlayerID,
				UnitID:       order.UnitID,
				Action:       domain.CombatActionMove,
				TargetNodeID: order.TargetNodeID,
			}
		}
	}
}

func (r *GameRoom) setMoveIntent(unitID string, target domain.Position) {
	ecs.AllUnits(r.state.World).Each(r.state.World, func(entry *donburi.Entry) {
		stats := ecs.UnitStatsC.Get(entry)
		if stats.ID != unitID {
			return
		}
		if !entry.HasComponent(ecs.MoveIntentC) {
			entry.AddComponent(ecs.MoveIntentC)
		}
		ecs.MoveIntentC.SetValue(entry, ecs.MoveIntentComp{Target: target})
	})
}

func (r *GameRoom) nodeIDAt(pos domain.Position) string {
	if entry, ok := domain.GetNodeAt(r.state.World, pos); ok {
		return ecs.NodeC.Get(entry).ID
	}
	return ""
}

func (r *GameRoom) playerIDForUnit(unitID string) string {
	var playerID string
	ecs.AllUnits(r.state.World).Each(r.state.World, func(entry *donburi.Entry) {
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

func (r *GameRoom) Broadcast(msg proto.Message) {
	for _, player := range r.Players {
		if err := player.Send(msg); err != nil {
			slog.Warn("广播消息失败", "room_id", r.ID, "player_id", player.PlayerID(), "error", err)
		}
	}
}

func (r *GameRoom) sendGameInit(p Player) {
	if r.state == nil {
		return
	}
	msg := &pb.MsgGameInit{
		GameId:       r.state.GameID,
		YourPlayerId: p.PlayerID(),
		Turn:         int32(r.state.Turn),
		Phase:        r.state.Phase,
		MapWidth:     int32(r.state.Map.Width),
		MapHeight:    int32(r.state.Map.Height),
		MyPlayer:     gamequery.BuildPlayerView(r.state, p.PlayerID()),
		Ministers:    nil,
		Nodes:        gamequery.BuildNodeViews(r.state, p.PlayerID()),
		Units:        gamequery.BuildUnitViews(r.state),
	}
	if err := p.Send(msg); err != nil {
		slog.Warn("发送游戏初始化消息失败", "room_id", r.ID, "player_id", p.PlayerID(), "error", err)
	}
}

func (r *GameRoom) sendStaticCatalogManifest(p Player) {
	manifest := staticdata.Default().Manifest()
	msg := &pb.MsgStaticCatalogManifest{
		Manifest: &pb.StaticCatalogManifest{
			SchemaVersion:  manifest.SchemaVersion,
			ContentVersion: manifest.ContentVersion,
			BundleHash:     manifest.BundleHash,
			DefaultLocale:  manifest.DefaultLocale,
			DefaultMapId:   manifest.DefaultMapID,
		},
	}
	if err := p.Send(msg); err != nil {
		slog.Warn("发送静态目录清单失败", "room_id", r.ID, "player_id", p.PlayerID(), "error", err)
	}
}

func (r *GameRoom) humanPlayerIDs() []string {
	ids := make([]string, 0, len(r.Players))
	for _, player := range r.Players {
		if !player.IsBot() {
			ids = append(ids, player.PlayerID())
		}
	}
	return ids
}

func (r *GameRoom) humanUsernames() []string {
	usernames := make([]string, 0, len(r.Players))
	for _, player := range r.Players {
		if !player.IsBot() {
			usernames = append(usernames, player.Username())
		}
	}
	return usernames
}
