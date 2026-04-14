package game

import (
	"context"
	"testing"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
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
		Rules: staticdata.Rules{TurnTimeLimitDomestic: 12, TurnTimeLimitCombat: 18, TokensPerTurn: 3},
	}))

	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{})
	room.Turn = 7
	room.Phase = domain.PhasePlanning.String()
	room.state = domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{})
	room.state.Phase = domain.PhasePlanning.String()

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

func TestGameRoomRejectsActionsOutsidePlanning(t *testing.T) {
	t.Parallel()

	room := NewRoom("game-1", nil, newStubTransport(), &config.Config{})
	room.state = domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{})
	room.setPhase(domain.PhaseResolving)

	if err := room.OnHumanSubmitTurnChecked("player-1"); err != ErrPhaseMismatch {
		t.Fatalf("submit error = %v, want %v", err, ErrPhaseMismatch)
	}
	if err := room.OnHumanMessage("player-1", "MsgSetPolicy", nil); err != ErrPhaseMismatch {
		t.Fatalf("message error = %v, want %v", err, ErrPhaseMismatch)
	}
}
