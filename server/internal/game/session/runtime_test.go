package session

import (
	"context"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"google.golang.org/protobuf/proto"
)

type capturePlayer struct {
	playerID string
	username string
	sent     []proto.Message
}

func (p *capturePlayer) PlayerID() string { return p.playerID }

func (p *capturePlayer) Username() string { return p.username }

func (p *capturePlayer) IsBot() bool { return false }

func (p *capturePlayer) Send(_ context.Context, msg proto.Message) error {
	p.sent = append(p.sent, msg)
	return nil
}

func TestRuntimeBootstrapDuringPlanningSendsPlanningStartWithSnapshotAndCurrentTokens(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 30, TokensPerTurn: 3},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := NewRuntime("game-1", []Player{player}, nil, nil)
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 4
	state.Phase = domain.PhasePlanning.String()
	state.Players["player-1"].TokensLeft = 1

	if err := runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}

	if len(player.sent) != 3 {
		t.Fatalf("send count = %d, want 3", len(player.sent))
	}
	start, ok := player.sent[2].(*pb.MsgPlanningStart)
	if !ok {
		t.Fatalf("message type = %T, want MsgPlanningStart", player.sent[2])
	}
	if got := start.GetTokens(); got != 1 {
		t.Fatalf("tokens = %d, want 1", got)
	}
	if got := start.GetTurn(); got != 4 {
		t.Fatalf("turn = %d, want 4", got)
	}
	if start.GetSnapshot() == nil {
		t.Fatalf("snapshot is nil")
	}
}

func TestRuntimeBootstrapOutsidePlanningDoesNotSendPlanningStart(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 30, TokensPerTurn: 3},
	}))

	player := &capturePlayer{playerID: "player-1", username: "alice"}
	runtime := NewRuntime("game-1", []Player{player}, nil, nil)
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 4
	state.Phase = domain.PhaseResolving.String()

	if err := runtime.InitializePrepared(state); err != nil {
		t.Fatalf("InitializePrepared() error = %v", err)
	}

	if len(player.sent) != 2 {
		t.Fatalf("send count = %d, want 2", len(player.sent))
	}
	if _, ok := player.sent[len(player.sent)-1].(*pb.MsgPlanningStart); ok {
		t.Fatalf("unexpected planning start in resolving bootstrap")
	}
}
