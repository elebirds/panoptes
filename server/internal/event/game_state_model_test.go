package event

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

func TestGameOverEventRefreshesStructuredOutcome(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, nil)

	GameOverEvent{WinnerID: "player-1", Reason: "timeout_draw"}.Apply(state.World, state)

	if !state.Outcome.IsOver {
		t.Fatalf("structured outcome is not over")
	}
	if state.Outcome.WinnerID != "player-1" || state.Outcome.Reason != "timeout_draw" {
		t.Fatalf("structured outcome = %+v", state.Outcome)
	}
}
