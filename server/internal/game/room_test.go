package game

import (
	"context"
	"errors"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	gamephase "github.com/elebirds/panoptes/internal/game/phase"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/elebirds/panoptes/internal/transport"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/proto"
)

type stubTransport struct {
	sent map[string][]proto.Message
}

func newStubTransport() *stubTransport {
	return &stubTransport{sent: make(map[string][]proto.Message)}
}

func (t *stubTransport) Send(playerID string, msg proto.Message) error {
	t.sent[playerID] = append(t.sent[playerID], msg)
	return nil
}

func (t *stubTransport) Broadcast(string, proto.Message) error { return nil }
func (t *stubTransport) Stream(playerID string, msgs <-chan proto.Message) error {
	for msg := range msgs {
		if err := t.Send(playerID, msg); err != nil {
			return err
		}
	}
	return nil
}

var _ transport.GameTransport = (*stubTransport)(nil)

func TestHumanPlayerNotifyTurnSendsPhaseMessages(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TurnTimeLimitDomestic: 12, TurnTimeLimitCombat: 18, TokensPerTurn: 3},
	}))
	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{})
	room.Turn = 7

	player := NewHumanPlayer("player-1", "alice", tp)
	player.NotifyTurn(context.Background(), room, "domestic_planning")
	player.NotifyTurn(context.Background(), room, "combat_planning")

	msgs := tp.sent["player-1"]
	if len(msgs) != 3 {
		t.Fatalf("send count = %d", len(msgs))
	}

	domestic, ok := msgs[0].(*pb.MsgDomesticPhaseStart)
	if !ok {
		t.Fatalf("domestic type = %T", msgs[0])
	}
	if domestic.GetTimeout() != 12 || domestic.GetTurn() != 7 || domestic.GetTokens() != 3 {
		t.Fatalf("domestic payload = %#v", domestic)
	}
	if domestic.GetPhase() != "domestic_planning" {
		t.Fatalf("domestic phase = %q", domestic.GetPhase())
	}

	combat, ok := msgs[1].(*pb.MsgCombatPhaseStart)
	if !ok {
		t.Fatalf("combat type = %T", msgs[1])
	}
	if combat.GetTimeout() != 18 || combat.GetTurn() != 7 || combat.GetTokens() != 3 {
		t.Fatalf("combat payload = %#v", combat)
	}
	if combat.GetPhase() != "combat_planning" {
		t.Fatalf("combat phase = %q", combat.GetPhase())
	}

	snapshot, ok := msgs[2].(*pb.MsgCombatOrdersSnapshot)
	if !ok {
		t.Fatalf("combat snapshot type = %T", msgs[2])
	}
	if snapshot.GetTurn() != 7 || snapshot.GetPhase() != "combat_planning" {
		t.Fatalf("combat snapshot payload = %#v", snapshot)
	}
}

func TestGameRoomStartSendsInitAndAdvancesTurns(t *testing.T) {
	previousRegistry := Registry
	Registry = NewGameRoomRegistry()
	defer func() { Registry = previousRegistry }()

	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{
			SchemaVersion:  "2026-04-06",
			ContentVersion: "test",
			BundleHash:     "bundle-hash",
			DefaultLocale:  "zh-CN",
			DefaultMapID:   "default",
		},
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			CastleBaseHP:       100,
			BuildPointsPerTurn: 10,
			SafeZoneRadius:     4,
		},
	}, &staticdata.MapRuntimeBundle{
		ID:     "default",
		Name:   "测试地图",
		Width:  20,
		Height: 20,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 2, Y: 10},
			{Slot: 1, X: 17, Y: 10},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "spawn_p1", X: 2, Y: 10, Terrain: "plain"},
			{ID: "spawn_p2", X: 17, Y: 10, Terrain: "plain"},
			{ID: "K10", X: 10, Y: 9, Terrain: "plain", IsResourcePoint: true, ResourceType: "food", NodeName: "龙脊"},
		},
		NamedNodes:    map[string]string{"K10": "龙脊"},
		CentralPoints: []string{"K10"},
	}))

	tp := newStubTransport()
	cfg := &config.Config{MapID: "default"}
	room := NewRoom(
		"game-1",
		[]Player{
			NewHumanPlayer("player-1", "alice", tp),
			NewBotPlayer("bot_abcdwxyz", "Bot", &RandomStrategy{}),
		},
		tp,
		cfg,
	)

	go room.Start()

	waitFor(t, time.Second, func() bool {
		return len(tp.sent["player-1"]) >= 3
	})

	if _, ok := Registry.GetRoomByPlayerID("player-1"); !ok {
		t.Fatalf("registry missing player room")
	}

	if _, ok := tp.sent["player-1"][0].(*pb.MsgStaticCatalogManifest); !ok {
		t.Fatalf("manifest type = %T", tp.sent["player-1"][0])
	}

	initMsg, ok := tp.sent["player-1"][1].(*pb.MsgGameInit)
	if !ok {
		t.Fatalf("init type = %T", tp.sent["player-1"][1])
	}
	if initMsg.GetYourPlayerId() != "player-1" {
		t.Fatalf("your player id = %q", initMsg.GetYourPlayerId())
	}
	if initMsg.GetPhase() != "domestic_planning" {
		t.Fatalf("init phase = %q", initMsg.GetPhase())
	}
	if initMsg.GetMapWidth() != 20 || initMsg.GetMapHeight() != 20 {
		t.Fatalf("map size = %dx%d", initMsg.GetMapWidth(), initMsg.GetMapHeight())
	}
	expectedNodeCount := int(initMsg.GetMapWidth() * initMsg.GetMapHeight())
	if len(initMsg.GetNodes()) != expectedNodeCount {
		t.Fatalf("nodes len = %d, expected = %d", len(initMsg.GetNodes()), expectedNodeCount)
	}
	if initMsg.GetMyPlayer().GetTokensLeft() != 3 {
		t.Fatalf("tokens left = %d", initMsg.GetMyPlayer().GetTokensLeft())
	}
	if initMsg.GetMyPlayer().GetMainCastleHp() != 100 {
		t.Fatalf("main castle hp = %d", initMsg.GetMyPlayer().GetMainCastleHp())
	}
	if room.state == nil || room.state.Map == nil {
		t.Fatalf("state not initialized")
	}

	room.OnHumanSubmitDomestic("player-1")
	waitFor(t, 1500*time.Millisecond, func() bool {
		return hasMessage[*pb.MsgCombatPhaseStart](tp.sent["player-1"])
	})

	room.OnHumanSubmitCombat("player-1")
	waitFor(t, 2*time.Second, func() bool {
		return room.Turn >= 2 && room.Phase == "domestic_planning"
	})

	if room.cancelFn != nil {
		room.cancelFn()
	}
}

func TestGameRoomRejectsOutOfPhaseMessages(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			CastleBaseHP:       100,
			BuildPointsPerTurn: 10,
		},
	}))

	room := NewRoom("game-1", nil, newStubTransport(), &config.Config{})
	room.state = domain.NewGameState(
		"game-1",
		[]string{"player-1"},
		[]string{"alice"},
		&domain.MapData{},
	)
	room.currentPhase = &gamephase.DomesticPhase{}
	room.setPhase(domain.PhaseDomesticPlanning)
	room.currentPhase.Enter(room)

	payload, err := protojson.Marshal(&pb.MsgCombatOrder{
		UnitId:       "unit-1",
		Action:       "move",
		TargetNodeId: "A1",
	})
	if err != nil {
		t.Fatalf("Marshal() error = %v", err)
	}

	err = room.OnHumanMessage("player-1", "MsgCombatOrder", payload)
	if err == nil {
		t.Fatalf("expected out-of-phase error")
	}
	if !errors.Is(err, ErrPhaseMismatch) {
		t.Fatalf("error = %v", err)
	}
}

func TestGameRoom_SetCombatOrderUpdatesActiveMarches(t *testing.T) {
	room, unitID := newCombatRoomForTest(t)

	room.SetCombatOrder(domain.CombatOrder{
		PlayerID:     "player-1",
		UnitID:       unitID,
		Action:       domain.CombatActionMove,
		TargetNodeID: "N2_0",
	})

	march, ok := room.state.ActiveMarches[unitID]
	if !ok {
		t.Fatalf("ActiveMarches missing move order")
	}
	if march.DestinationNodeID != "N2_0" {
		t.Fatalf("DestinationNodeID = %q, want %q", march.DestinationNodeID, "N2_0")
	}

	room.SetCombatOrder(domain.CombatOrder{
		PlayerID: "player-1",
		UnitID:   unitID,
		Action:   domain.CombatActionHold,
	})

	if _, ok := room.state.ActiveMarches[unitID]; ok {
		t.Fatalf("ActiveMarch should be cleared by hold order")
	}
}

func TestGameRoom_PrepareCombatOrdersUsesActiveMarches(t *testing.T) {
	room, unitID := newCombatRoomForTest(t)
	room.state.ActiveMarches[unitID] = domain.ActiveMarch{
		PlayerID:          "player-1",
		UnitID:            unitID,
		Action:            domain.CombatActionMove,
		DestinationNodeID: "N2_0",
	}

	room.prepareCombatOrders()

	order, ok := room.state.PendingCombatOrders[unitID]
	if !ok {
		t.Fatalf("PendingCombatOrders missing active march order")
	}
	if order.Action != domain.CombatActionMove {
		t.Fatalf("Action = %q, want %q", order.Action, domain.CombatActionMove)
	}
	if order.TargetNodeID != "N2_0" {
		t.Fatalf("TargetNodeID = %q, want %q", order.TargetNodeID, "N2_0")
	}
}

func TestRunCombatSettlement_ActiveMarchContinuesUntilDestination(t *testing.T) {
	room, unitID := newCombatRoomForTestSize(t, 5)
	room.SetCombatOrder(domain.CombatOrder{
		PlayerID:     "player-1",
		UnitID:       unitID,
		Action:       domain.CombatActionMove,
		TargetNodeID: "N4_0",
	})

	RunCombatSettlement(room)
	if got := roomUnitPosition(t, room, unitID); got != (domain.Position{X: 2, Y: 0}) {
		t.Fatalf("after first settlement position = %#v, want %#v", got, domain.Position{X: 2, Y: 0})
	}
	if _, ok := room.state.ActiveMarches[unitID]; !ok {
		t.Fatalf("ActiveMarch should remain after first settlement")
	}

	RunCombatSettlement(room)
	if got := roomUnitPosition(t, room, unitID); got != (domain.Position{X: 4, Y: 0}) {
		t.Fatalf("after second settlement position = %#v, want %#v", got, domain.Position{X: 4, Y: 0})
	}
	if _, ok := room.state.ActiveMarches[unitID]; ok {
		t.Fatalf("ActiveMarch should clear after reaching destination")
	}
}

func TestRunCombatSettlement_ActiveMarchKeepsOriginalPathAcrossTurns(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			CastleBaseHP:       100,
			BuildPointsPerTurn: 10,
		},
		Units: []staticdata.UnitDefinition{
			{ID: "warrior", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true}, Tags: []string{"melee"}},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", MoveCostNoRoad: 2, Passable: true},
			{ID: "forest", MoveCostNoRoad: 4, Passable: true},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "combat-room-path-persist-test",
		Width:        5,
		Height:       2,
		SpawnPoints:  map[int]domain.Position{0: {X: 0, Y: 0}},
		PlayerSpawns: map[string]domain.Position{"player-1": {X: 0, Y: 0}},
		NamedNodes:   map[string]string{},
		NodeIndex:    map[string]donburi.Entity{},
	}
	for y := 0; y < 2; y++ {
		for x := 0; x < 5; x++ {
			nodeID := roomTestNodeIDXY(x, y)
			entity := ecs.CreateNode(world, ecs.MapNode{ID: nodeID, X: x, Y: y, Terrain: "plain"})
			mapData.NodeIndex[nodeID] = entity
		}
	}

	room := NewRoom("room-combat-persist", nil, newStubTransport(), &config.Config{})
	room.state = domain.NewGameState("room-combat-persist", []string{"player-1"}, []string{"alice"}, mapData)
	room.state.World = world

	entry := world.Entry(ecs.CreateUnit(world, "warrior", "player-1", domain.Position{X: 0, Y: 0}))
	unitID := ecs.UnitStatsC.Get(entry).ID

	room.SetCombatOrder(domain.CombatOrder{
		PlayerID:     "player-1",
		UnitID:       unitID,
		Action:       domain.CombatActionMove,
		TargetNodeID: roomTestNodeIDXY(4, 0),
	})

	RunCombatSettlement(room)
	if got := roomUnitPosition(t, room, unitID); got != (domain.Position{X: 2, Y: 0}) {
		t.Fatalf("after first settlement position = %#v, want %#v", got, domain.Position{X: 2, Y: 0})
	}

	setNodeTerrainAndRoad(t, room, roomTestNodeIDXY(3, 0), "forest", false)
	setNodeTerrainAndRoad(t, room, roomTestNodeIDXY(2, 1), "plain", true)
	setNodeTerrainAndRoad(t, room, roomTestNodeIDXY(3, 1), "plain", true)
	setNodeTerrainAndRoad(t, room, roomTestNodeIDXY(4, 1), "plain", true)

	RunCombatSettlement(room)
	if got := roomUnitPosition(t, room, unitID); got != (domain.Position{X: 4, Y: 0}) {
		t.Fatalf("after second settlement position = %#v, want %#v", got, domain.Position{X: 4, Y: 0})
	}
}

func TestHumanPlayerNotifyTurnCombatSendsOrdersSnapshot(t *testing.T) {
	tp := newStubTransport()
	room, unitID := newCombatRoomForTest(t)
	room.transport = tp
	room.Turn = 4
	room.state.ActiveMarches[unitID] = domain.ActiveMarch{
		PlayerID:          "player-1",
		UnitID:            unitID,
		Action:            domain.CombatActionMove,
		DestinationNodeID: "N2_0",
		LastPreview: domain.RoutePreview{
			PathNodeIDs:     []string{"N0_0", "N1_0", "N2_0"},
			FirstTurnNodeID: "N2_0",
			TotalTurns:      1,
			TurnStops:       []domain.MarchTurnStop{{TurnIndex: 1, NodeID: "N2_0"}},
		},
	}

	player := NewHumanPlayer("player-1", "alice", tp)
	player.NotifyTurn(context.Background(), room, "combat_planning")

	msgs := tp.sent["player-1"]
	if len(msgs) != 2 {
		t.Fatalf("send count = %d, want 2", len(msgs))
	}

	if _, ok := msgs[0].(*pb.MsgCombatPhaseStart); !ok {
		t.Fatalf("first message type = %T, want MsgCombatPhaseStart", msgs[0])
	}

	snapshot, ok := msgs[1].(*pb.MsgCombatOrdersSnapshot)
	if !ok {
		t.Fatalf("second message type = %T, want MsgCombatOrdersSnapshot", msgs[1])
	}
	if len(snapshot.GetOrders()) != 1 {
		t.Fatalf("orders len = %d, want 1", len(snapshot.GetOrders()))
	}
	order := snapshot.GetOrders()[0]
	if order.GetUnitId() != unitID {
		t.Fatalf("unit id = %q, want %q", order.GetUnitId(), unitID)
	}
	if order.GetFirstTurnNodeId() != "N2_0" {
		t.Fatalf("first_turn_node_id = %q, want %q", order.GetFirstTurnNodeId(), "N2_0")
	}
}

func TestRegistryRegisterAndUnregister(t *testing.T) {
	registry := NewGameRoomRegistry()
	room := NewRoom("game-1", []Player{
		NewBotPlayer("bot_abcdwxyz", "Bot", &RandomStrategy{}),
	}, newStubTransport(), &config.Config{})

	registry.Register(room)

	if _, ok := registry.GetRoomByPlayerID("bot_abcdwxyz"); !ok {
		t.Fatalf("registry should contain bot mapping")
	}

	registry.Unregister("game-1")
	if _, ok := registry.GetRoomByPlayerID("bot_abcdwxyz"); ok {
		t.Fatalf("registry should remove bot mapping")
	}
}

func newCombatRoomForTest(t *testing.T) (*GameRoom, string) {
	return newCombatRoomForTestSize(t, 3)
}

func newCombatRoomForTestSize(t *testing.T, width int) (*GameRoom, string) {
	t.Helper()

	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			CastleBaseHP:       100,
			BuildPointsPerTurn: 10,
		},
		Units: []staticdata.UnitDefinition{
			{ID: "warrior", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true}, Tags: []string{"melee"}},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", MoveCostNoRoad: 2, Passable: true},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "combat-room-test",
		Width:        width,
		Height:       1,
		SpawnPoints:  map[int]domain.Position{0: {X: 0, Y: 0}},
		PlayerSpawns: map[string]domain.Position{"player-1": {X: 0, Y: 0}},
		NamedNodes:   map[string]string{},
		NodeIndex:    map[string]donburi.Entity{},
	}
	for x := 0; x < width; x++ {
		entity := ecs.CreateNode(world, ecs.MapNode{ID: roomTestNodeID(x), X: x, Y: 0, Terrain: "plain"})
		mapData.NodeIndex[roomTestNodeID(x)] = entity
	}

	room := NewRoom("room-combat", nil, newStubTransport(), &config.Config{})
	room.state = domain.NewGameState("room-combat", []string{"player-1"}, []string{"alice"}, mapData)
	room.state.World = world

	entry := world.Entry(ecs.CreateUnit(world, "warrior", "player-1", domain.Position{X: 0, Y: 0}))
	unitID := ecs.UnitStatsC.Get(entry).ID
	return room, unitID
}

func roomUnitPosition(t *testing.T, room *GameRoom, unitID string) domain.Position {
	t.Helper()
	var found domain.Position
	ok := false
	ecs.AllUnits(room.state.World).Each(room.state.World, func(entry *donburi.Entry) {
		if ok || ecs.UnitStatsC.Get(entry).ID != unitID {
			return
		}
		pos := ecs.PositionC.Get(entry)
		found = domain.Position{X: pos.X, Y: pos.Y}
		ok = true
	})
	if !ok {
		t.Fatalf("unit %s not found", unitID)
	}
	return found
}

func roomTestNodeID(x int) string {
	return "N" + string(rune('0'+x)) + "_0"
}

func roomTestNodeIDXY(x int, y int) string {
	return "N" + string(rune('0'+x)) + "_" + string(rune('0'+y))
}

func setNodeTerrainAndRoad(t *testing.T, room *GameRoom, nodeID string, terrain string, hasRoad bool) {
	t.Helper()

	entry, ok := room.state.GetNode(nodeID)
	if !ok {
		t.Fatalf("node %s not found", nodeID)
	}
	node := ecs.NodeC.Get(entry)
	node.Terrain = domain.Terrain(terrain)
	node.HasRoad = hasRoad
	ecs.NodeC.SetValue(entry, *node)
}

func TestToProtoResourcesMapsKnownKeysAndIgnoresUnknown(t *testing.T) {
	got := toProtoResourceBag(domain.ResourceBag{
		domain.ResourceOre:            3,
		domain.ResourceWood:           4,
		domain.ResourceFood:           5,
		domain.ResourceRefinedOre:     6,
		domain.ResourceEngineerMat:    7,
		domain.ResourceBuildPoints:    8,
		domain.ResourceKey("crystal"): 99,
	})

	items := make(map[string]int32, len(got.GetItems()))
	for _, item := range got.GetItems() {
		items[item.GetKey()] = item.GetAmount()
	}
	if items["ore"] != 3 || items["wood"] != 4 || items["food"] != 5 {
		t.Fatalf("basic proto resources = %#v", items)
	}
	if items["refined_ore"] != 6 || items["engineer_material"] != 7 || items["build_points"] != 8 {
		t.Fatalf("advanced proto resources = %#v", items)
	}
	if items["crystal"] != 99 {
		t.Fatalf("custom resource missing = %#v", items)
	}
}

func TestToDomesticChangeIncludesCastleIDForBuiltBuilding(t *testing.T) {
	change := toDomesticChange(event.BuildingBuiltEvent{
		NodeID:       "node-7",
		BuildingType: "farm",
		Owner:        "player-1",
		CastleID:     "castle-a",
	})

	if change == nil {
		t.Fatalf("change is nil")
	}
	if got := change.GetData()["castle_id"]; got != "castle-a" {
		t.Fatalf("castle_id = %q", got)
	}
}

func TestInitializeCastleStatesSeedsPrimaryCastleResources(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			CastleBaseHP:       100,
			BuildPointsPerTurn: 10,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "castle", Category: "city", Combat: staticdata.BuildingCombat{MaxHP: 100}},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "castle-a", X: 2, Y: 3, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.NodeC.Get(nodeEntry).Owner = "player-1"
	ecs.NodeC.Get(nodeEntry).TerritoryOwner = "player-1"
	ecs.CreateBuilding(world, "castle", "player-1", "castle-a", nodeEntry)

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID: "default",
		PlayerSpawns: map[string]domain.Position{
			"player-1": {X: 2, Y: 3},
		},
		NodeIndex: map[string]donburi.Entity{
			"castle-a": nodeEntity,
		},
	})
	state.World = world

	room := NewRoom("game-1", nil, newStubTransport(), &config.Config{})
	room.state = state

	room.initializeCastleStates()

	player := room.state.Players["player-1"]
	if player == nil {
		t.Fatalf("player missing")
	}
	castle := player.Castles["castle-a"]
	if castle == nil {
		t.Fatalf("primary castle state missing")
	}
	if castle.OwnerID != "player-1" {
		t.Fatalf("castle owner = %q", castle.OwnerID)
	}
	if castle.Resources.Get(domain.ResourceBuildPoints) != player.Resources.Get(domain.ResourceBuildPoints) {
		t.Fatalf("castle build points = %d, player build points = %d", castle.Resources.Get(domain.ResourceBuildPoints), player.Resources.Get(domain.ResourceBuildPoints))
	}
}

func TestAppendCastleResourceSnapshotsIncludesOwnedCastleResources(t *testing.T) {
	player := &domain.PlayerState{
		PlayerID:  "player-1",
		Resources: domain.NewResourceBag(),
		Castles: map[string]*domain.CastleState{
			"castle-a": {
				CastleID: "castle-a",
				OwnerID:  "player-1",
				Resources: domain.ResourceBag{
					domain.ResourceFood:        7,
					domain.ResourceWood:        4,
					domain.ResourceBuildPoints: 3,
				},
			},
		},
	}

	changes := appendCastleResourceSnapshots([]*pb.DomesticChange{
		{Type: "building_built", Data: map[string]string{"node_id": "node-1"}},
	}, player)

	if len(changes) != 2 {
		t.Fatalf("changes len = %d", len(changes))
	}
	snapshot := changes[1]
	if snapshot.GetType() != "castle_resource_snapshot" {
		t.Fatalf("snapshot type = %q", snapshot.GetType())
	}
	if snapshot.GetData()["castle_id"] != "castle-a" {
		t.Fatalf("castle_id = %q", snapshot.GetData()["castle_id"])
	}
	if snapshot.GetData()["food"] != "7" || snapshot.GetData()["wood"] != "4" || snapshot.GetData()["build_points"] != "3" {
		t.Fatalf("snapshot data = %#v", snapshot.GetData())
	}
}

func TestBuildPlayerViewIncludesResearchState(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			CastleBaseHP:       100,
			StartingTechPoints: 1,
			TechPointsPerTurn:  2,
			TechPointsMax:      5,
		},
	}))

	room := NewRoom("game-1", nil, newStubTransport(), &config.Config{})
	room.state = domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{})
	room.state.Players["player-1"].Research.TechPoints = 4
	room.state.Players["player-1"].Research.UnlockTechnology("agri_unlock_farm")

	view := room.buildPlayerView("player-1")
	if view.GetResearch() == nil {
		t.Fatalf("research view is nil")
	}
	if view.GetResearch().GetTechPoints() != 4 {
		t.Fatalf("tech_points = %d", view.GetResearch().GetTechPoints())
	}
	if view.GetResearch().GetTechPointsIncome() != 2 || view.GetResearch().GetTechPointsCap() != 5 {
		t.Fatalf("research income/cap = %#v", view.GetResearch())
	}
	if len(view.GetResearch().GetUnlockedTechnologyIds()) != 1 || view.GetResearch().GetUnlockedTechnologyIds()[0] != "agri_unlock_farm" {
		t.Fatalf("unlocked technologies = %#v", view.GetResearch().GetUnlockedTechnologyIds())
	}
}

func waitFor(t *testing.T, timeout time.Duration, fn func() bool) {
	t.Helper()
	deadline := time.Now().Add(timeout)
	for time.Now().Before(deadline) {
		if fn() {
			return
		}
		time.Sleep(10 * time.Millisecond)
	}
	t.Fatalf("condition not met within %s", timeout)
}

func hasMessage[T proto.Message](msgs []proto.Message) bool {
	for _, msg := range msgs {
		if _, ok := msg.(T); ok {
			return true
		}
	}
	return false
}
