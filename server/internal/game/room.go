package game

import (
	"context"
	"fmt"
	"log/slog"
	"reflect"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	"github.com/elebirds/panoptes/internal/engine/minister"
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

	currentPhase       gamephase.Phase
	pendingBuilds      []domain.BuildOrder
	ministerDirectives map[string]string
	combatDirectives   map[string][]gamephase.WarZoneDirective
	combatOrders       map[string]domain.CombatOrder
	vetoUnits          map[string]map[string]bool
	microOrders        map[string]map[string]string
	router             any
	ministerEngine     *minister.MinisterEngine
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
		combatDirectives:   make(map[string][]gamephase.WarZoneDirective),
		combatOrders:       make(map[string]domain.CombatOrder),
		vetoUnits:          make(map[string]map[string]bool),
		microOrders:        make(map[string]map[string]string),
		ministerEngine:     minister.NewMinisterEngine(nil),
	}
}

func (r *GameRoom) Start() {
	catalog := staticdata.Default()
	mapID := r.cfg.MapID
	if mapID == "" {
		mapID = catalog.DefaultMapID()
	}
	mapFile, err := maploader.LoadMap(catalog, mapID)
	if err != nil {
		slog.Error("地图加载失败", "room_id", r.ID, "error", err)
		return
	}

	world := donburi.NewWorld()
	playerIDs := r.humanPlayerIDs()
	usernames := r.humanUsernames()
	mapData := maploader.InitWorldFromMap(world, mapFile, playerIDs)
	r.state = domain.NewGameState(r.ID, playerIDs, usernames, mapData)
	r.state.World = world

	r.Turn = r.state.Turn
	r.Phase = r.state.Phase

	slog.Info("地图加载成功", "room_id", r.ID, "map_id", mapFile.ID, "nodes", len(mapFile.Nodes))

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
		r.Phase = "domestic"
		r.state.Phase = "domestic"
		r.currentPhase.Enter(r)
		r.waitAllSubmit(time.Duration(domesticTimeoutSec) * time.Second)
		RunDomesticSettlement(r)
		if r.state.IsOver {
			break
		}

		r.currentPhase = &gamephase.CombatPhase{}
		r.Phase = "combat"
		r.state.Phase = "combat"
		r.currentPhase.Enter(r)
		r.waitAllSubmit(time.Duration(combatTimeoutSec) * time.Second)
		RunCombatSettlement(r)

		r.state.Turn++
		r.Turn = r.state.Turn
		if rules.MaxTurns > 0 && r.state.Turn > rules.MaxTurns {
			r.handleDraw()
			break
		}
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
	r.submitDomestic(playerID)
}

func (r *GameRoom) OnHumanSubmitCombat(playerID string) {
	r.submitCombat(playerID)
}

func (r *GameRoom) OnHumanMessage(playerID, msgType string, payload []byte) error {
	if r.currentPhase == nil {
		return fmt.Errorf("phase not initialized")
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
	if phase == "combat" {
		combatEvents := make([]*pb.CombatEvent, 0, len(events))
		for _, e := range events {
			if payload := e.ClientPayload(); payload != nil {
				combatEvents = append(combatEvents, payload)
			}
		}
		r.Broadcast(&pb.MsgCombatSettlement{Events: combatEvents})
		return
	}

	changes := make([]*pb.DomesticChange, 0, len(events))
	for _, e := range events {
		eventType := reflect.TypeOf(e)
		typeName := "unknown"
		if eventType != nil {
			typeName = eventType.Name()
		}
		changes = append(changes, &pb.DomesticChange{Type: typeName, Data: map[string]string{"detail": e.String()}})
	}

	for _, player := range r.Players {
		if player.IsBot() {
			continue
		}
		playerState := r.state.Players[player.PlayerID()]
		msg := &pb.MsgDomesticSettlement{Changes: changes}
		if playerState != nil {
			msg.MyResourcesAfter = toProtoResourceBag(playerState.Resources)
		}
		_ = player.Send(msg)
	}
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
