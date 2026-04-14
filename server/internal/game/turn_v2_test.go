// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14
// Description: 验证 Turn V2 人类玩家通知与统一规划阶段约束。

package game

import (
	"context"
	"testing"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	gamesession "github.com/elebirds/panoptes/internal/game/session"
	gameturn "github.com/elebirds/panoptes/internal/game/turn"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
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

func TestHumanPlayerNotifyTurnSendsPlanningStartWithSnapshot(t *testing.T) {
	t.Parallel()

	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 30, TokensPerTurn: 3},
	}))

	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{})
	room.runtime = nil
	room.runtime = newTestRuntime("game-1", tp)
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.runtime.State().Turn = 7
	room.runtime.State().Phase = domain.PhasePlanning.String()

	player := NewHumanPlayer("player-1", "alice", tp)
	player.NotifyTurn(context.Background(), room, domain.PhasePlanning.String())

	msgs := tp.sent["player-1"]
	if len(msgs) != 1 {
		t.Fatalf("send count = %d, want 1", len(msgs))
	}

	start, ok := msgs[0].(*pb.MsgPlanningStart)
	if !ok {
		t.Fatalf("message type = %T, want MsgPlanningStart", msgs[0])
	}
	if start.GetTimeout() != 30 || start.GetTurn() != 7 || start.GetTokens() != 3 {
		t.Fatalf("planning payload = %#v", start)
	}
	if start.GetPhase() != domain.PhasePlanning.String() {
		t.Fatalf("phase = %q, want %q", start.GetPhase(), domain.PhasePlanning.String())
	}
	if start.GetSnapshot() == nil {
		t.Fatalf("snapshot is nil")
	}
	if start.GetSnapshot().GetTurn() != 7 || start.GetSnapshot().GetPhase() != domain.PhasePlanning.String() {
		t.Fatalf("snapshot payload = %#v", start.GetSnapshot())
	}
}

func TestHumanPlayerNotifyTurnPrefersUnifiedPlanningTimeout(t *testing.T) {
	t.Parallel()

	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning: 21,
			TokensPerTurn:         3,
		},
	}))

	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{})
	room.runtime = newTestRuntime("game-1", tp)
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.runtime.State().Turn = 3
	room.runtime.State().Phase = domain.PhasePlanning.String()

	player := NewHumanPlayer("player-1", "alice", tp)
	player.NotifyTurn(context.Background(), room, domain.PhasePlanning.String())

	msgs := tp.sent["player-1"]
	if len(msgs) != 1 {
		t.Fatalf("send count = %d, want 1", len(msgs))
	}

	start, ok := msgs[0].(*pb.MsgPlanningStart)
	if !ok {
		t.Fatalf("message type = %T, want MsgPlanningStart", msgs[0])
	}
	if start.GetTimeout() != 21 {
		t.Fatalf("timeout = %d, want 21", start.GetTimeout())
	}
}

func TestGameRoomRejectsActionsOutsidePlanning(t *testing.T) {
	t.Parallel()

	room := NewRoom("game-1", nil, newStubTransport(), &config.Config{})
	room.runtime = newTestRuntime("game-1", newStubTransport())
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.State().Phase = domain.PhaseResolving.String()

	if err := room.OnHumanSubmitTurnChecked("player-1"); err != ErrPhaseMismatch {
		t.Fatalf("submit error = %v, want %v", err, ErrPhaseMismatch)
	}
	if err := room.OnHumanMessage("player-1", "MsgSetPolicy", nil); err != ErrPhaseMismatch {
		t.Fatalf("message error = %v, want %v", err, ErrPhaseMismatch)
	}
}

func newTestRuntime(gameID string, tp *stubTransport) *gamesession.Runtime {
	runtime := gamesession.NewRuntime(gameID, nil, tp, &config.Config{})
	state := domain.NewGameState(gameID, []string{"player-1"}, []string{"alice"}, &domain.MapData{})
	runtime.SetState(state)
	return runtime
}
