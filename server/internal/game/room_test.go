package game

import (
	"context"
	"errors"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	gamephase "github.com/elebirds/panoptes/internal/game/phase"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/elebirds/panoptes/internal/transport"
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
	if len(msgs) != 2 {
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
	if len(initMsg.GetNodes()) != 3 {
		t.Fatalf("nodes len = %d", len(initMsg.GetNodes()))
	}
	if initMsg.GetMapWidth() != 20 || initMsg.GetMapHeight() != 20 {
		t.Fatalf("map size = %dx%d", initMsg.GetMapWidth(), initMsg.GetMapHeight())
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
