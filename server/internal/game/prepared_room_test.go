package game

import (
	"context"
	"sync"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type asyncCaptureTransport struct {
	mu       sync.RWMutex
	sent     map[string][]proto.Message
	sentMeta map[string][]*pb.EventMeta
}

func newAsyncCaptureTransport() *asyncCaptureTransport {
	return &asyncCaptureTransport{
		sent:     make(map[string][]proto.Message),
		sentMeta: make(map[string][]*pb.EventMeta),
	}
}

func (t *asyncCaptureTransport) Send(ctx context.Context, playerID string, msg proto.Message) error {
	t.mu.Lock()
	defer t.mu.Unlock()
	t.sent[playerID] = append(t.sent[playerID], msg)
	t.sentMeta[playerID] = append(t.sentMeta[playerID], coretransport.EventMetaFromContext(ctx))
	return nil
}

func (t *asyncCaptureTransport) Broadcast(context.Context, string, proto.Message) error { return nil }

func (t *asyncCaptureTransport) Stream(ctx context.Context, playerID string, msgs <-chan proto.Message) error {
	for msg := range msgs {
		if err := t.Send(ctx, playerID, msg); err != nil {
			return err
		}
	}
	return nil
}

func (t *asyncCaptureTransport) snapshot(playerID string) []proto.Message {
	t.mu.RLock()
	defer t.mu.RUnlock()
	out := make([]proto.Message, len(t.sent[playerID]))
	copy(out, t.sent[playerID])
	return out
}

func (t *asyncCaptureTransport) snapshotMeta(playerID string) []*pb.EventMeta {
	t.mu.RLock()
	defer t.mu.RUnlock()
	out := make([]*pb.EventMeta, len(t.sentMeta[playerID]))
	copy(out, t.sentMeta[playerID])
	return out
}

func TestPreparedRoomStartUsesProvidedStateAndSendsGameInitBeforeOtherGameEvents(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{
			SchemaVersion:  "2026-04-15",
			ContentVersion: "prepared-room-test",
			BundleHash:     "prepared-room-test",
			DefaultLocale:  "zh-CN",
			DefaultMapID:   "prepared_map",
		},
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning:      1,
			TokensPerTurn:              3,
			BonusTokensPerTurn:         0,
			MaxTurns:                   10,
			CityCoreMaxHP:              100,
			SafeZoneRadius:             3,
			FacilityTakeoverTurns:      2,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			MinimumCityDistance:        2,
			InitialCityTerritoryRadius: 1,
		},
	}, &staticdata.MapRuntimeBundle{
		ID:     "prepared_map",
		Name:   "Prepared Map",
		Width:  4,
		Height: 4,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 1, Y: 1},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "B2", X: 1, Y: 1, Terrain: "plain"},
			{ID: "C2", X: 2, Y: 1, Terrain: "plain"},
		},
		NamedNodes: map[string]string{
			"B2": "起点",
		},
	}))

	state := newPreparedState(t)
	state.Turn = 4
	state.Phase = domain.PhasePlanning.String()

	tp := newAsyncCaptureTransport()
	room := NewPreparedRoom(
		state.GameID,
		[]Player{NewHumanPlayer("player-1", "alice", tp)},
		tp,
		&config.Config{DevMode: true},
		state,
	)

	go room.Start()

	waitForPrepared(t, 2*time.Second, func() bool {
		return len(tp.snapshot("player-1")) >= 3
	})

	msgs := tp.snapshot("player-1")
	metas := tp.snapshotMeta("player-1")
	initMsg, ok := msgs[0].(*pb.MsgGameInit)
	if !ok {
		t.Fatalf("msgs[0] type = %T, want *pb.MsgGameInit", msgs[0])
	}
	if initMsg.GetGameId() != state.GameID {
		t.Fatalf("game_id = %q, want %q", initMsg.GetGameId(), state.GameID)
	}
	if got := metaGameSessionID(metas[0]); got != state.GameID {
		t.Fatalf("msgs[0] meta.game_session_id = %q, want %q", got, state.GameID)
	}
	if initMsg.GetTurn() != 4 {
		t.Fatalf("init turn = %d, want 4", initMsg.GetTurn())
	}
	if got := len(initMsg.GetNodes()); got != 2 {
		t.Fatalf("init nodes len = %d, want 2", got)
	}

	if _, ok := msgs[1].(*pb.MsgStaticCatalogManifest); !ok {
		t.Fatalf("msgs[1] type = %T, want *pb.MsgStaticCatalogManifest", msgs[1])
	}
	if got := metaGameSessionID(metas[1]); got != state.GameID {
		t.Fatalf("msgs[1] meta.game_session_id = %q, want %q", got, state.GameID)
	}

	startMsg, ok := msgs[2].(*pb.MsgPlanningStart)
	if !ok {
		t.Fatalf("msgs[2] type = %T, want *pb.MsgPlanningStart", msgs[2])
	}
	if got := metaGameSessionID(metas[2]); got != state.GameID {
		t.Fatalf("msgs[2] meta.game_session_id = %q, want %q", got, state.GameID)
	}
	if startMsg.GetTurn() != 4 {
		t.Fatalf("planning start turn = %d, want 4", startMsg.GetTurn())
	}
	if startMsg.GetPhase() != domain.PhasePlanning.String() {
		t.Fatalf("planning start phase = %q, want %q", startMsg.GetPhase(), domain.PhasePlanning.String())
	}
	if startMsg.GetSnapshot() == nil {
		t.Fatalf("planning snapshot is nil")
	}
	if room.State() != state {
		t.Fatalf("room state pointer changed")
	}
}

func newPreparedState(t *testing.T) *domain.GameState {
	t.Helper()

	world := donburi.NewWorld()
	mapData := maploader.InitWorldFromMap(world, &staticdata.MapRuntimeBundle{
		ID:     "prepared_map",
		Name:   "Prepared Map",
		Width:  4,
		Height: 4,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 1, Y: 1},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "B2", X: 1, Y: 1, Terrain: "plain"},
			{ID: "C2", X: 2, Y: 1, Terrain: "plain"},
		},
		NamedNodes: map[string]string{
			"B2": "起点",
		},
	}, []string{"player-1"})
	state := domain.NewGameState("prepared-game", []string{"player-1"}, []string{"alice"}, mapData)
	state.World = world
	return state
}

func waitForPrepared(t *testing.T, timeout time.Duration, cond func() bool) {
	t.Helper()

	deadline := time.Now().Add(timeout)
	for time.Now().Before(deadline) {
		if cond() {
			return
		}
		time.Sleep(20 * time.Millisecond)
	}
	t.Fatalf("condition not met within %s", timeout)
}

func metaGameSessionID(meta *pb.EventMeta) string {
	if meta == nil {
		return ""
	}
	field := meta.ProtoReflect().Descriptor().Fields().ByName("game_session_id")
	if field == nil {
		return ""
	}
	return meta.ProtoReflect().Get(field).String()
}
