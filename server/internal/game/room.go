package game

import (
	"context"
	"fmt"
	"log/slog"
	"reflect"
	"sort"
	"strconv"
	"strings"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	"github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/engine/production"
	"github.com/elebirds/panoptes/internal/event"
	gamephase "github.com/elebirds/panoptes/internal/game/phase"
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

	currentPhase            gamephase.Phase
	pendingBuilds           []domain.BuildOrder
	ministerDirectives      map[string]string
	combatDirectives        map[string][]gamephase.WarZoneDirective
	combatOrders            map[string]domain.CombatOrder
	combatDeployOrders      map[string]pendingTerritoryDeploy
	pendingTerritoryDeploys map[string]pendingTerritoryDeploy
	vetoUnits               map[string]map[string]bool
	microOrders             map[string]map[string]string
	router                  any
	ministerEngine          *minister.MinisterEngine
}

type pendingTerritoryDeploy struct {
	PlayerID     string
	UnitID       string
	CenterNodeID string
}

type Room = GameRoom

var _ transport.GameRoom = (*GameRoom)(nil)

func NewRoom(id string, players []Player, t transport.GameTransport, cfg *config.Config) *GameRoom {
	return &GameRoom{
		ID:                      id,
		Players:                 players,
		cfg:                     cfg,
		transport:               t,
		submitCh:                make(chan string, len(players)*4+16),
		pendingBuilds:           make([]domain.BuildOrder, 0),
		ministerDirectives:      make(map[string]string),
		combatDirectives:        make(map[string][]gamephase.WarZoneDirective),
		combatOrders:            make(map[string]domain.CombatOrder),
		combatDeployOrders:      make(map[string]pendingTerritoryDeploy),
		pendingTerritoryDeploys: make(map[string]pendingTerritoryDeploy),
		vetoUnits:               make(map[string]map[string]bool),
		microOrders:             make(map[string]map[string]string),
		ministerEngine:          minister.NewMinisterEngine(nil),
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
	domesticTimeoutSec := rules.TurnTimeLimitDomestic
	if domesticTimeoutSec <= 0 {
		domesticTimeoutSec = 15
	}
	combatTimeoutSec := rules.TurnTimeLimitCombat
	if combatTimeoutSec <= 0 {
		combatTimeoutSec = 20
	}

	for !r.state.IsOver {
		if ctx.Err() != nil {
			return
		}
		if r.ministerEngine != nil {
			r.ministerEngine.GenerateReports(ctx, r)
		}

		r.currentPhase = &gamephase.DomesticPhase{}
		r.setPhase(domain.PhaseDomesticPlanning)
		r.applyPendingTerritoryDeploys()
		r.applyQueuedBuildOrdersAtDomesticStart()
		r.currentPhase.Enter(r)
		r.waitAllSubmit(time.Duration(domesticTimeoutSec) * time.Second)
		r.setPhase(domain.PhaseDomesticResolving)
		RunDomesticSettlement(r)
		if r.state.IsOver {
			break
		}

		r.currentPhase = &gamephase.CombatPhase{}
		r.setPhase(domain.PhaseCombatPlanning)
		r.currentPhase.Enter(r)
		r.waitAllSubmit(time.Duration(combatTimeoutSec) * time.Second)
		r.setPhase(domain.PhaseCombatResolving)
		RunCombatSettlement(r)
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
			if r.currentPhase != nil {
				r.currentPhase.Timeout(r)
			}
			return
		}
	}
}

func (r *GameRoom) submitDomestic(playerID string) {
	r.submitCh <- playerID
}

func (r *GameRoom) submitCombat(playerID string) {
	r.submitCh <- playerID
}

func (r *GameRoom) OnHumanSubmitDomestic(playerID string) {
	if err := r.OnHumanSubmitDomesticChecked(playerID); err != nil {
		slog.Warn("忽略非内政阶段提交", "room_id", r.ID, "player_id", playerID, "phase", r.Phase, "error", err)
	}
}

func (r *GameRoom) OnHumanSubmitCombat(playerID string) {
	if err := r.OnHumanSubmitCombatChecked(playerID); err != nil {
		slog.Warn("忽略非战斗阶段提交", "room_id", r.ID, "player_id", playerID, "phase", r.Phase, "error", err)
	}
}

func (r *GameRoom) OnHumanSubmitDomesticChecked(playerID string) error {
	if !r.isPhase(domain.PhaseDomesticPlanning) {
		return ErrPhaseMismatch
	}
	r.submitDomestic(playerID)
	return nil
}

func (r *GameRoom) OnHumanSubmitCombatChecked(playerID string) error {
	if !r.isPhase(domain.PhaseCombatPlanning) {
		return ErrPhaseMismatch
	}
	r.submitCombat(playerID)
	return nil
}

func (r *GameRoom) OnHumanMessage(playerID, msgType string, payload []byte) error {
	if r.currentPhase == nil || !r.isMessageAllowed(msgType) {
		return ErrPhaseMismatch
	}
	return r.currentPhase.HandleMessage(r, playerID, msgType, payload)
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

func (r *GameRoom) SetMinisterDirective(playerID string, directive string) {
	r.ministerDirectives[playerID] = directive
}

func (r *GameRoom) SetWarDirectives(playerID string, directives []gamephase.WarZoneDirective) {
	r.combatDirectives[playerID] = directives
}

func (r *GameRoom) SetVetoUnit(playerID string, unitID string) {
	if _, ok := r.vetoUnits[playerID]; !ok {
		r.vetoUnits[playerID] = make(map[string]bool)
	}
	r.vetoUnits[playerID][unitID] = true
}

func (r *GameRoom) SetMicroOrder(playerID string, unitID string, targetNode string) {
	if _, ok := r.microOrders[playerID]; !ok {
		r.microOrders[playerID] = make(map[string]string)
	}
	r.microOrders[playerID][unitID] = targetNode
	r.SetCombatOrder(domain.CombatOrder{
		PlayerID:     playerID,
		UnitID:       unitID,
		Action:       domain.CombatActionMove,
		TargetNodeID: targetNode,
	})
}

func (r *GameRoom) SetCombatOrder(order domain.CombatOrder) {
	order = order.Normalized()
	if order.UnitID == "" {
		return
	}
	if order.PlayerID == "" {
		order.PlayerID = r.playerIDForUnit(order.UnitID)
	}

	if strings.EqualFold(string(order.Action), string(domain.CombatActionDeploy)) {
		centerNodeID := strings.TrimSpace(order.TargetNodeID)
		if moveOrder, ok := r.combatOrders[order.UnitID]; ok &&
			strings.EqualFold(string(moveOrder.Action), string(domain.CombatActionMove)) {
			centerNodeID = strings.TrimSpace(moveOrder.TargetNodeID)
		}
		r.combatDeployOrders[order.UnitID] = pendingTerritoryDeploy{
			PlayerID:     strings.TrimSpace(order.PlayerID),
			UnitID:       strings.TrimSpace(order.UnitID),
			CenterNodeID: centerNodeID,
		}
		return
	}

	if strings.EqualFold(string(order.Action), string(domain.CombatActionMove)) {
		// New move intent overrides and cancels a previously queued deploy intent
		// for the same unit within the same combat planning window.
		delete(r.combatDeployOrders, order.UnitID)
	}

	r.combatOrders[order.UnitID] = order
}

func (r *GameRoom) BuildNodeViewForPlayer(nodeID string, viewerID string) *pb.NodeView {
	nodeEntry, ok := r.state.GetNode(nodeID)
	if !ok {
		return nil
	}
	return r.buildNodeView(nodeEntry, viewerID)
}

func (r *GameRoom) NodeByID(nodeID string) (*donburi.Entry, bool) {
	return r.state.GetNode(nodeID)
}

func (r *GameRoom) broadcastSettlement(phase string, events []event.Event) {
	if phase == domain.PhaseCombatResolving.String() {
		combatEvents := make([]*pb.CombatEvent, 0, len(events))
		for _, e := range events {
			if payload := e.ClientPayload(); payload != nil {
				combatEvents = append(combatEvents, payload)
			}
		}
		nextPhase := domain.PhaseDomesticPlanning.String()
		if r.shouldStopAfterCombatSettlement() {
			nextPhase = ""
		}
		r.Broadcast(&pb.MsgCombatSettlement{
			Events:    combatEvents,
			Turn:      int32(r.state.Turn),
			Phase:     phase,
			NextPhase: nextPhase,
		})
		return
	}

	changes := make([]*pb.DomesticChange, 0, len(events))
	for _, e := range events {
		changes = append(changes, toDomesticChange(e))
	}

	for _, player := range r.Players {
		if player.IsBot() {
			continue
		}
		playerState := r.state.Players[player.PlayerID()]
		nextPhase := domain.PhaseCombatPlanning.String()
		if r.state.IsOver {
			nextPhase = ""
		}
		msg := &pb.MsgDomesticSettlement{
			// 在常规 domestic changes 之外，为当前玩家额外附加自己的城堡资源快照。
			// 这里复用现有 DomesticChange 通道下发 castle_resource_snapshot，
			// 避免仅为了资源看板新增一轮 proto 结构改造。
			Changes:   appendCastleResourceSnapshots(changes, playerState),
			Turn:      int32(r.state.Turn),
			Phase:     phase,
			NextPhase: nextPhase,
		}
		if playerState != nil {
			msg.MyResourcesAfter = toProtoResourceBag(playerState.Resources)
		}
		_ = player.Send(msg)
	}
}

// appendCastleResourceSnapshots appends one snapshot change per castle.
//
// 这些快照面向客户端资源看板消费，描述的是“本次结算完成后，每座城堡当前的
// 资源状态”。由于消息是按玩家分别发送的，所以这里只附加当前玩家自己名下的
// 城堡资源，而不会广播其他玩家的细节。
func appendCastleResourceSnapshots(base []*pb.DomesticChange, playerState *domain.PlayerState) []*pb.DomesticChange {
	if playerState == nil || len(playerState.Castles) == 0 {
		return base
	}

	changes := make([]*pb.DomesticChange, 0, len(base)+len(playerState.Castles))
	changes = append(changes, base...)

	castleIDs := make([]string, 0, len(playerState.Castles))
	for castleID := range playerState.Castles {
		if strings.TrimSpace(castleID) != "" {
			castleIDs = append(castleIDs, strings.TrimSpace(castleID))
		}
	}
	sort.Strings(castleIDs)

	for _, castleID := range castleIDs {
		castle := playerState.Castles[castleID]
		if castle == nil {
			continue
		}
		resources := castle.Resources
		if resources == nil {
			resources = domain.NewResourceBag()
		}
		data := map[string]string{
			"castle_id":         castleID,
			"ore":               strconv.Itoa(resources.Get(domain.ResourceOre)),
			"wood":              strconv.Itoa(resources.Get(domain.ResourceWood)),
			"food":              strconv.Itoa(resources.Get(domain.ResourceFood)),
			"refined_ore":       strconv.Itoa(resources.Get(domain.ResourceRefinedOre)),
			"engineer_material": strconv.Itoa(resources.Get(domain.ResourceEngineerMat)),
			"build_points":      strconv.Itoa(resources.Get(domain.ResourceBuildPoints)),
		}
		changes = append(changes, &pb.DomesticChange{
			Type: "castle_resource_snapshot",
			Data: data,
		})
	}

	return changes
}

func toDomesticChange(e event.Event) *pb.DomesticChange {
	if e == nil {
		return &pb.DomesticChange{
			Type: "unknown",
			Data: map[string]string{},
		}
	}

	switch evt := e.(type) {
	case event.BuildingBuiltEvent:
		hp := resolveBuiltBuildingHP(evt.BuildingType)
		return &pb.DomesticChange{
			Type: "building_built",
			Data: map[string]string{
				"node_id":       strings.TrimSpace(evt.NodeID),
				"building_type": strings.TrimSpace(evt.BuildingType),
				"owner":         strings.TrimSpace(evt.Owner),
				"castle_id":     strings.TrimSpace(evt.CastleID),
				"building_hp":   strconv.Itoa(hp),
			},
		}
	}

	eventType := reflect.TypeOf(e)
	typeName := "unknown"
	if eventType != nil {
		typeName = eventType.Name()
	}
	return &pb.DomesticChange{
		Type: typeName,
		Data: map[string]string{
			"detail": e.String(),
		},
	}
}

func resolveBuiltBuildingHP(buildingType string) int {
	const fallback = 100

	catalog := staticdata.Default()
	if catalog == nil {
		return fallback
	}

	if cfg, ok := catalog.GetBuilding(strings.TrimSpace(buildingType)); ok && cfg.Combat.MaxHP > 0 {
		return cfg.Combat.MaxHP
	}

	if strings.EqualFold(strings.TrimSpace(buildingType), "castle") {
		if hp := catalog.Rules().CastleBaseHP; hp > 0 {
			return hp
		}
	}

	return fallback
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
	case domain.PhaseDomesticPlanning.String():
		switch msgType {
		case "MsgSetPolicy", "MsgTokenBuild", "MsgTokenReveal", "MsgMinisterDirective", "MsgSubmitDomestic":
			return true
		}
	case domain.PhaseCombatPlanning.String():
		switch msgType {
		case "MsgSetWarZone", "MsgWarZoneDirective", "MsgTokenVetoCombat", "MsgTokenMicro", "MsgCombatOrder", "MsgSubmitCombat":
			return true
		}
	}

	return false
}

func (r *GameRoom) shouldStopAfterCombatSettlement() bool {
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

	for _, order := range r.state.MinisterMoveOrders {
		if r.isVetoed(order.PlayerID, order.UnitID) {
			continue
		}
		targetNodeID := r.nodeIDAt(order.Target)
		r.state.PendingCombatOrders[order.UnitID] = domain.CombatOrder{
			PlayerID:     order.PlayerID,
			UnitID:       order.UnitID,
			Action:       domain.CombatActionMove,
			TargetNodeID: targetNodeID,
		}
	}

	for unitID, order := range r.combatOrders {
		if r.isVetoed(order.PlayerID, unitID) {
			continue
		}
		r.state.PendingCombatOrders[unitID] = order.Normalized()
	}
}

func (r *GameRoom) collectPendingTerritoryDeploysFromCombatOrders() {
	for unitID, deployOrder := range r.combatDeployOrders {
		if r.isVetoed(deployOrder.PlayerID, unitID) {
			continue
		}

		playerID := strings.TrimSpace(deployOrder.PlayerID)
		if playerID == "" {
			playerID = strings.TrimSpace(r.playerIDForUnit(unitID))
		}
		if playerID == "" {
			continue
		}

		centerNodeID := strings.TrimSpace(deployOrder.CenterNodeID)
		if moveOrder, ok := r.state.PendingCombatOrders[unitID]; ok &&
			strings.EqualFold(string(moveOrder.Action), string(domain.CombatActionMove)) {
			centerNodeID = strings.TrimSpace(moveOrder.TargetNodeID)
		}

		r.pendingTerritoryDeploys[unitID] = pendingTerritoryDeploy{
			PlayerID:     playerID,
			UnitID:       strings.TrimSpace(unitID),
			CenterNodeID: centerNodeID,
		}
	}
}

func (r *GameRoom) applyPendingTerritoryDeploys() {
	if len(r.pendingTerritoryDeploys) == 0 {
		return
	}

	for unitID, order := range r.pendingTerritoryDeploys {
		if order.PlayerID == "" || order.UnitID == "" {
			delete(r.pendingTerritoryDeploys, unitID)
			continue
		}

		if err := gamephase.ApplyTerritoryExpansion(r, order.PlayerID, order.UnitID, order.CenterNodeID); err != nil {
			slog.Warn("deploy territory expansion failed", "room_id", r.ID, "player_id", order.PlayerID, "unit_id", order.UnitID, "error", err)
		}
		delete(r.pendingTerritoryDeploys, unitID)
	}
}

func (r *GameRoom) applyQueuedBuildOrdersAtDomesticStart() {
	if r == nil || r.state == nil || r.state.World == nil {
		return
	}
	if len(r.pendingBuilds) == 0 {
		return
	}

	if r.state.PendingBuilds == nil {
		r.state.PendingBuilds = make([]domain.BuildOrder, 0, len(r.pendingBuilds))
	} else {
		r.state.PendingBuilds = r.state.PendingBuilds[:0]
	}
	r.state.PendingBuilds = append(r.state.PendingBuilds, r.pendingBuilds...)
	r.pendingBuilds = r.pendingBuilds[:0]

	buildSystem := production.BuildSystem{}
	events := buildSystem.Run(r.state.World, r.state)
	for _, evt := range events {
		if evt == nil {
			continue
		}
		evt.Apply(r.state.World, r.state)
	}

	if len(events) > 0 {
		r.broadcastSettlement(domain.PhaseDomesticPlanning.String(), events)
	}

	r.state.PendingBuilds = r.state.PendingBuilds[:0]
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

func (r *GameRoom) isVetoed(playerID string, unitID string) bool {
	if vetoes, ok := r.vetoUnits[playerID]; ok {
		return vetoes[unitID]
	}
	return false
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
		MyPlayer:     r.buildPlayerView(p.PlayerID()),
		Ministers:    nil,
		Nodes:        r.buildNodeViews(p.PlayerID()),
		Units:        r.buildUnitViews(),
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

func (r *GameRoom) buildPlayerView(playerID string) *pb.PlayerView {
	playerState := r.state.Players[playerID]
	if playerState == nil {
		return &pb.PlayerView{Id: playerID}
	}

	warZones := make([]*pb.WarZone, 0, len(playerState.WarZones))
	for _, zone := range playerState.WarZones {
		warZones = append(warZones, &pb.WarZone{
			Id:         zone.ID,
			Name:       zone.Name,
			NodeIds:    zone.NodeIDs,
			Directive:  zone.Directive,
			TargetNode: zone.Target,
		})
	}

	return &pb.PlayerView{
		Id:       playerState.PlayerID,
		Username: playerState.Username,
		Resources: func() *pb.ResourceBag {
			return toProtoResourceBag(playerState.Resources)
		}(),
		TokensLeft:    int32(playerState.TokensLeft),
		CurrentPolicy: string(playerState.Policy),
		MainCastleHp:  int32(playerState.MainCastleHP),
		MaxCastleHp:   int32(staticdata.Default().Rules().CastleBaseHP),
		WarZones:      warZones,
	}
}

func (r *GameRoom) buildNodeViews(playerID string) []*pb.NodeView {
	nodes := make([]*pb.NodeView, 0, ecs.AllNodes(r.state.World).Count(r.state.World))
	ecs.AllNodes(r.state.World).Each(r.state.World, func(entry *donburi.Entry) {
		nodes = append(nodes, r.buildNodeView(entry, playerID))
	})
	return nodes
}

func (r *GameRoom) buildNodeView(entry *donburi.Entry, playerID string) *pb.NodeView {
	node := ecs.NodeC.Get(entry)
	pos := ecs.PositionC.Get(entry)
	unitsByFaction := domain.UnitsByFactionAtNode(r.state.World, domain.Position{X: pos.X, Y: pos.Y})

	myCount := len(unitsByFaction[playerID])
	enemyCount := 0
	for faction, units := range unitsByFaction {
		if faction == playerID {
			continue
		}
		enemyCount += len(units)
	}

	view := &pb.NodeView{
		Id:              node.ID,
		Pos:             &pb.Position{X: int32(pos.X), Y: int32(pos.Y)},
		Terrain:         string(node.Terrain),
		Owner:           node.Owner,
		TerritoryOwner:  node.TerritoryOwner,
		MyUnitCount:     int32(myCount),
		EnemyUnitCount:  int32(enemyCount),
		HasRoad:         node.HasRoad,
		IsResourcePoint: node.IsResource,
		ResourceType:    node.ResourceType,
		IsSafeZone:      domain.IsInSafeZone(r.state, domain.Position{X: pos.X, Y: pos.Y}, playerID),
	}
	if entry.HasComponent(ecs.BuildingC) {
		building := ecs.BuildingC.Get(entry)
		view.BuildingType = string(building.Type)
		view.BuildingHp = int32(building.HP)
		view.WallLevel = int32(building.WallLevel)
	}
	return view
}

func (r *GameRoom) buildUnitViews() []*pb.UnitView {
	units := make([]*pb.UnitView, 0, ecs.AllUnits(r.state.World).Count(r.state.World))
	ecs.AllUnits(r.state.World).Each(r.state.World, func(entry *donburi.Entry) {
		stats := ecs.UnitStatsC.Get(entry)
		pos := ecs.PositionC.Get(entry)
		units = append(units, &pb.UnitView{
			Id:       stats.ID,
			Faction:  stats.Faction,
			UnitType: string(stats.Type),
			Hp:       int32(stats.HP),
			MaxHp:    int32(stats.MaxHP),
			Pos:      &pb.Position{X: int32(pos.X), Y: int32(pos.Y)},
		})
	})
	return units
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

func toProtoResourceBag(resources domain.ResourceBag) *pb.ResourceBag {
	items := make([]*pb.ResourceValue, 0, len(resources))
	for _, key := range resources.Keys() {
		items = append(items, &pb.ResourceValue{
			Key:    string(key),
			Amount: int32(resources.Get(key)),
		})
	}
	return &pb.ResourceBag{Items: items}
}
