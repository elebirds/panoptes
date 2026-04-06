package game

import (
	"context"
	"os"
	"path/filepath"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/transport"
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
	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{
		TurnTimeLimitDomestic: 12,
		TurnTimeLimitCombat:   18,
		TokensPerTurn:         3,
	})
	room.Turn = 7

	player := NewHumanPlayer("player-1", "alice", tp)
	player.NotifyTurn(context.Background(), room, "domestic")
	player.NotifyTurn(context.Background(), room, "combat")

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

	combat, ok := msgs[1].(*pb.MsgCombatPhaseStart)
	if !ok {
		t.Fatalf("combat type = %T", msgs[1])
	}
	if combat.GetTimeout() != 18 || combat.GetTokens() != 3 {
		t.Fatalf("combat payload = %#v", combat)
	}
}

func TestGameRoomStartSendsInitAndAdvancesTurns(t *testing.T) {
	previousRegistry := Registry
	Registry = NewGameRoomRegistry()
	defer func() { Registry = previousRegistry }()

	config.Data = config.GameData{
		Rules: config.RulesConfig{
			TokensPerTurn:      3,
			CastleBaseHP:       100,
			BuildPointsPerTurn: 10,
			SafeZoneRadius:     4,
		},
	}

	mapPath := writeTestMap(t)
	tp := newStubTransport()
	cfg := &config.Config{
		TurnTimeLimitDomestic: 2,
		TurnTimeLimitCombat:   2,
		TokensPerTurn:         3,
		MapPath:               mapPath,
	}
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
		return len(tp.sent["player-1"]) >= 2
	})

	if _, ok := Registry.GetRoomByPlayerID("player-1"); !ok {
		t.Fatalf("registry missing player room")
	}

	initMsg, ok := tp.sent["player-1"][0].(*pb.MsgGameInit)
	if !ok {
		t.Fatalf("init type = %T", tp.sent["player-1"][0])
	}
	if initMsg.GetYourPlayerId() != "player-1" {
		t.Fatalf("your player id = %q", initMsg.GetYourPlayerId())
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
		return room.Turn >= 2 && room.Phase == "domestic"
	})

	if room.cancelFn != nil {
		room.cancelFn()
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

func writeTestMap(t *testing.T) string {
	t.Helper()

	dir := t.TempDir()
	path := filepath.Join(dir, "default.json")
	if err := os.WriteFile(path, []byte(`{
  "id": "default",
  "width": 20,
  "height": 20,
  "spawn_points": [
    { "player_index": 0, "x": 2, "y": 10 },
    { "player_index": 1, "x": 17, "y": 10 }
  ],
  "nodes": [
    { "id": "spawn_p1", "x": 2, "y": 10, "terrain": "plain", "is_resource_point": false },
    { "id": "spawn_p2", "x": 17, "y": 10, "terrain": "plain", "is_resource_point": false },
    { "id": "K10", "x": 10, "y": 9, "terrain": "plain", "is_resource_point": true, "resource_type": "food" }
  ],
  "central_points": ["K10"],
  "named_nodes": { "K10": "龙脊" }
}`), 0o600); err != nil {
		t.Fatalf("WriteFile() error = %v", err)
	}
	return path
}
