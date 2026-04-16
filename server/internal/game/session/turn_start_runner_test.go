package session

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func TestPlanningStartRunnerPromotesPendingTechAndInstitutionsWithoutRefreshingTokens(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			BaseResearchOutputPerTurn: 1,
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:           "agrarian_foundations",
				ResearchCost: 2,
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_building", TargetID: "farm"},
					{Type: "unlock_recipe", TargetID: "farm_food"},
					{Type: "unlock_policy", TargetID: "academy_charter"},
					{Type: "add_institution_slots", InstitutionSlots: 1},
				},
			},
		},
		Policies: []staticdata.PolicyDefinition{
			{ID: "academy_charter", Layer: "institutional", ActivationTiming: "next_turn"},
		},
	}))

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Turn = 3
	state.Players["player-1"].TokensLeft = 1
	state.Players["player-1"].Research.MarkTechnologyCompleted("agrarian_foundations", 2)
	state.Players["player-1"].Institutions.PendingPolicyIDs = []string{"academy_charter"}
	state.Players["player-1"].Institutions.PendingActivationTurn = 3
	state.Players["player-1"].Institutions.SlotCount = 1
	state.Players["player-1"].Institutions.UnlockCandidate("academy_charter")

	NewPlanningStartRunner().Run(state)

	if got := state.Players["player-1"].Research.ActiveTechnologyIDs(); len(got) != 1 || got[0] != "agrarian_foundations" {
		t.Fatalf("technology should activate on planning start")
	}
	if !state.IsBuildingUnlocked("player-1", "farm") {
		t.Fatalf("farm should unlock on planning start")
	}
	if !state.IsRecipeUnlocked("player-1", "farm_food") {
		t.Fatalf("farm_food should unlock on planning start")
	}
	if got := state.Players["player-1"].Institutions.ActivePolicyIDs; len(got) != 1 || got[0] != "academy_charter" {
		t.Fatalf("active institution policies = %#v, want academy_charter", got)
	}
	if got := state.Players["player-1"].TokensLeft; got != 1 {
		t.Fatalf("tokens left = %d, want planning start to preserve current tokens", got)
	}
}
