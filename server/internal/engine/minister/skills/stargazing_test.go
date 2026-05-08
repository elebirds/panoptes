package skills

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func TestActivateStargazingQueuesNextTurnFullMapVision(t *testing.T) {
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{
		MinisterSkillCards: []staticdata.MinisterSkillCard{
			{
				ID:            "stargazing",
				EffectKey:     StargazingEffectKey,
				DelayTurns:    1,
				DurationTurns: 1,
				RoleTags:      []string{"domestic"},
			},
		},
	})
	state := domain.NewGameState("stargazing", []string{"player-1"}, []string{"alice"}, &domain.MapData{})
	state.Players["player-1"].MinisterSkillLoadouts = domain.DefaultMinisterSkillLoadouts(catalog.MinisterSkillCards())
	state.Turn = 2

	result, ok := NewRegistry(Stargazing{}).Activate(state, "player-1", "domestic", "stargazing", catalog)
	if !ok {
		t.Fatalf("Activate() = false")
	}
	if result.ActiveTurn != 3 || result.ExpiresAfterTurn != 3 {
		t.Fatalf("result = %#v, want active turn 3 only", result)
	}
	if domain.PlayerHasFullMapVision(state, "player-1") {
		t.Fatalf("full map vision active before next turn")
	}

	state.Turn = 3
	if !domain.PlayerHasFullMapVision(state, "player-1") {
		t.Fatalf("full map vision inactive on next turn")
	}
}
