package minister

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

type parserTestRoom struct {
	state *domain.GameState
}

func (r parserTestRoom) State() *domain.GameState {
	return r.state
}

func TestExecuteActionsIgnoresRepairRoadInCurrentMVP(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})

	events := ExecuteActions([]MinisterActionItem{
		{
			Type: "repair_road",
			Params: map[string]any{
				"from_node": "A1",
				"to_node":   "A2",
				"cost":      1,
			},
		},
	}, parserTestRoom{state: state}, "player-1")

	if len(events) != 0 {
		t.Fatalf("repair_road should be ignored in current MVP, got %d events", len(events))
	}
}
