// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证回合协调模块的Turn V2 回合约束。

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
	coretransport "github.com/elebirds/panoptes/internal/transport"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	"google.golang.org/protobuf/proto"
)

type stubTransport struct {
	sent     map[string][]proto.Message
	sentMeta map[string][]*pb.EventMeta
}

func newStubTransport() *stubTransport {
	return &stubTransport{
		sent:     make(map[string][]proto.Message),
		sentMeta: make(map[string][]*pb.EventMeta),
	}
}

func (t *stubTransport) Send(ctx context.Context, playerID string, msg proto.Message) error {
	t.sent[playerID] = append(t.sent[playerID], msg)
	t.sentMeta[playerID] = append(t.sentMeta[playerID], coretransport.EventMetaFromContext(ctx))
	return nil
}

func (t *stubTransport) Broadcast(context.Context, string, proto.Message) error { return nil }

func (t *stubTransport) Stream(ctx context.Context, playerID string, msgs <-chan proto.Message) error {
	for msg := range msgs {
		if err := t.Send(ctx, playerID, msg); err != nil {
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

	if err := room.HandleGameCommand(cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.GameCommand{
		Body: &pb.GameCommand_Planning{
			Planning: &pb.PlanningCommand{
				Body: &pb.PlanningCommand_SubmitTurn{
					SubmitTurn: &pb.MsgSubmitTurn{},
				},
			},
		},
	}); err != ErrPhaseMismatch {
		t.Fatalf("submit error = %v, want %v", err, ErrPhaseMismatch)
	}
	if err := room.HandleGameCommand(cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.GameCommand{
		Body: &pb.GameCommand_Planning{
			Planning: &pb.PlanningCommand{
				Body: &pb.PlanningCommand_SetPolicy{
					SetPolicy: &pb.MsgSetPolicy{},
				},
			},
		},
	}); err != ErrPhaseMismatch {
		t.Fatalf("message error = %v, want %v", err, ErrPhaseMismatch)
	}
}

func TestHandleGameCommandPropagatesRequestMetaToOutboundResponses(t *testing.T) {
	t.Parallel()

	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{})
	room.runtime = newTestRuntime("game-1", tp)
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.State().Phase = domain.PhasePlanning.String()
	room.State().Players["player-1"].TokensLeft = 3

	err := room.HandleGameCommand(cmddispatch.InboundContext{
		PlayerID:  "player-1",
		RequestID: "req-123",
		TraceID:   "trace-456",
	}, &pb.GameCommand{
		Body: &pb.GameCommand_Planning{
			Planning: &pb.PlanningCommand{
				Body: &pb.PlanningCommand_SetPolicy{
					SetPolicy: &pb.MsgSetPolicy{NationalPolicyId: "expansion"},
				},
			},
		},
	})
	if err != nil {
		t.Fatalf("HandleGameCommand() error = %v", err)
	}

	msgs := tp.sent["player-1"]
	if len(msgs) != 1 {
		t.Fatalf("send count = %d, want 1", len(msgs))
	}
	if _, ok := msgs[0].(*pb.MsgTokenResult); !ok {
		t.Fatalf("message type = %T, want MsgTokenResult", msgs[0])
	}

	metas := tp.sentMeta["player-1"]
	if len(metas) != 1 {
		t.Fatalf("meta count = %d, want 1", len(metas))
	}
	if metas[0] == nil {
		t.Fatalf("event meta = nil, want request correlation")
	}
	if metas[0].GetRequestId() != "req-123" {
		t.Fatalf("request_id = %q, want req-123", metas[0].GetRequestId())
	}
	if metas[0].GetTraceId() != "trace-456" {
		t.Fatalf("trace_id = %q, want trace-456", metas[0].GetTraceId())
	}
}

func newTestRuntime(gameID string, tp *stubTransport) *gamesession.Runtime {
	players := []gamesession.Player{
		NewHumanPlayer("player-1", "alice", tp),
	}
	runtime := gamesession.NewRuntime(gameID, players, tp, &config.Config{})
	state := domain.NewGameState(gameID, []string{"player-1"}, []string{"alice"}, &domain.MapData{})
	runtime.SetState(state)
	return runtime
}
