// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证调试支持模块的集成联调流程。

package debug_test

import (
	"context"
	"os"
	"sync"
	"testing"
	"time"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/game"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"google.golang.org/protobuf/proto"
)

type captureTransport struct {
	mu   sync.RWMutex
	sent map[string][]proto.Message
}

func newCaptureTransport() *captureTransport {
	return &captureTransport{sent: make(map[string][]proto.Message)}
}

func (t *captureTransport) Send(_ context.Context, playerID string, msg proto.Message) error {
	t.mu.Lock()
	defer t.mu.Unlock()
	t.sent[playerID] = append(t.sent[playerID], msg)
	return nil
}

func (t *captureTransport) Broadcast(context.Context, string, proto.Message) error { return nil }

func (t *captureTransport) Stream(ctx context.Context, playerID string, msgs <-chan proto.Message) error {
	for msg := range msgs {
		if err := t.Send(ctx, playerID, msg); err != nil {
			return err
		}
	}
	return nil
}

func (t *captureTransport) snapshot(playerID string) []proto.Message {
	t.mu.RLock()
	defer t.mu.RUnlock()
	msgs := t.sent[playerID]
	out := make([]proto.Message, len(msgs))
	copy(out, msgs)
	return out
}

func TestRoundTrip(t *testing.T) {
	if os.Getenv("PANOPTES_RUN_DEBUG_INTEGRATION") != "1" {
		t.Skip("set PANOPTES_RUN_DEBUG_INTEGRATION=1 to run manual integration validation")
	}

	previousRegistry := game.Registry
	game.Registry = game.NewGameRoomRegistry()
	defer func() { game.Registry = previousRegistry }()

	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Manifest: staticdata.Manifest{
			SchemaVersion:  "2026-04-10",
			ContentVersion: "debug-test",
			BundleHash:     "debug-bundle",
			DefaultLocale:  "zh-CN",
			DefaultMapID:   "default",
		},
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning:     10,
			TokensPerTurn:             3,
			MaxTurns:                  2,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 10,
			SafeZoneRadius:            4,
		},
		Ministers: []staticdata.Minister{
			{ID: "m1", Name: "军事部长", Role: "military", Ability: 7, Personality: "bold", PersonalityDesc: "果断", Loyalty: 6, Ambition: 5},
			{ID: "m2", Name: "农业部长", Role: "agriculture", Ability: 6, Personality: "steady", PersonalityDesc: "稳健", Loyalty: 6, Ambition: 4},
			{ID: "m3", Name: "外交部长", Role: "diplomacy", Ability: 6, Personality: "calm", PersonalityDesc: "谨慎", Loyalty: 6, Ambition: 5},
		},
	}, &staticdata.MapRuntimeBundle{
		ID:     "default",
		Name:   "联调地图",
		Width:  20,
		Height: 20,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 2, Y: 10},
			{Slot: 1, X: 17, Y: 10},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "spawn_p1", X: 2, Y: 10, Terrain: "plain"},
			{ID: "spawn_p2", X: 17, Y: 10, Terrain: "plain"},
			{ID: "res_ore", X: 10, Y: 9, Terrain: "plain", IsResourcePoint: true, ResourceType: "ore", NodeName: "矿区"},
		},
		NamedNodes: map[string]string{"res_ore": "矿区"},
	}))

	tp := newCaptureTransport()
	room := game.NewRoom(
		"debug-roundtrip",
		[]game.Player{
			game.NewHumanPlayer("player-1", "alice", tp),
			game.NewBotPlayer("bot_abcdef01", "Bot", &game.RandomStrategy{}),
		},
		tp,
		&config.Config{MapID: "default", DevMode: true},
	)

	go room.Start()

	waitFor(t, 2*time.Second, func() bool {
		msg, ok := findMessage[*pb.MsgGameInit](tp.snapshot("player-1"))
		return ok && len(msg.GetNodes()) > 0
	})
	initMsg, _ := findMessage[*pb.MsgGameInit](tp.snapshot("player-1"))
	t.Log("✓ MsgGameInit 包含非空 Nodes 列表")

	waitFor(t, 2*time.Second, func() bool {
		msg, ok := findMessage[*pb.MsgPlanningStart](tp.snapshot("player-1"))
		return ok && msg.GetTurn() == initMsg.GetTurn() && msg.GetTokens() == 3
	})
	t.Log("✓ MsgPlanningStart 包含正确 Turn 和 Tokens")

	waitFor(t, 2*time.Second, func() bool {
		return countMessage[*pb.MsgMinisterReportChunk](tp.snapshot("player-1")) >= 2
	})
	t.Log("✓ MsgMinisterReportChunk 流式推送（多条）")

	room.Submit("player-1")
	waitFor(t, 3*time.Second, func() bool {
		msg, ok := findMessage[*pb.MsgTurnSettlement](tp.snapshot("player-1"))
		return ok && msg.GetPhase() == "resolving"
	})
	t.Log("✓ MsgTurnSettlement 在双方提交后推送")

	waitFor(t, 2*time.Second, func() bool {
		return countMessage[*pb.MsgPlanningStart](tp.snapshot("player-1")) >= 2
	})
	t.Log("✓ 结算后进入下一回合 planning")

	startTurn := initMsg.GetTurn()
	waitFor(t, 3*time.Second, func() bool {
		return room.State() != nil && int32(room.State().Turn) > startTurn
	})
	t.Log("✓ Turn 正确递增")
}

func waitFor(t *testing.T, timeout time.Duration, cond func() bool) {
	t.Helper()
	deadline := time.Now().Add(timeout)
	for time.Now().Before(deadline) {
		if cond() {
			return
		}
		time.Sleep(20 * time.Millisecond)
	}
	t.Fatalf("condition not met in %s", timeout)
}

func findMessage[T proto.Message](msgs []proto.Message) (T, bool) {
	var zero T
	for _, msg := range msgs {
		if typed, ok := msg.(T); ok {
			return typed, true
		}
	}
	return zero, false
}

func countMessage[T proto.Message](msgs []proto.Message) int {
	count := 0
	for _, msg := range msgs {
		if _, ok := msg.(T); ok {
			count++
		}
	}
	return count
}
