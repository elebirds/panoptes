package scenario

import (
	"regexp"
	"testing"
)

func TestScenarioBuildersProduceDeterministicStates(t *testing.T) {
	t.Parallel()

	slugPattern := regexp.MustCompile(`^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$`)
	tests := []struct {
		name  string
		build func() (*Definition, error)
	}{
		{name: "research_unlock_build", build: ResearchUnlockBuild},
		{name: "industry_budget_exhaustion", build: IndustryBudgetExhaustion},
		{name: "building_modifier_point_preview", build: BuildingModifierPointPreview},
		{name: "settler_found_city", build: SettlerFoundCity},
		{name: "recipe_blocked_by_input", build: RecipeBlockedByInput},
		{name: "disabled_recipe_skipped", build: DisabledRecipeSkipped},
		{name: "outer_facility_capture", build: OuterFacilityCapture},
		{name: "capital_destroy_gameover", build: CapitalDestroyGameOver},
	}

	for _, tc := range tests {
		tc := tc
		t.Run(tc.name, func(t *testing.T) {
			def, err := tc.build()
			if err != nil {
				t.Fatalf("build() error = %v", err)
			}
			if def == nil {
				t.Fatalf("definition is nil")
			}
			if def.Name != tc.name {
				t.Fatalf("definition name = %q, want %q", def.Name, tc.name)
			}
			if !slugPattern.MatchString(def.Name) {
				t.Fatalf("definition name = %q, want lowercase snake_case slug", def.Name)
			}
			if def.Catalog == nil {
				t.Fatalf("catalog is nil")
			}
			if got := def.Catalog.DefaultMapID(); got != def.Name {
				t.Fatalf("catalog default map id = %q, want scenario name %q", got, def.Name)
			}
			if got := def.Catalog.BundleHash(); got != def.Name {
				t.Fatalf("catalog bundle hash = %q, want scenario name %q", got, def.Name)
			}
			if def.State == nil {
				t.Fatalf("state is nil")
			}
			if def.State.GameID != def.Name {
				t.Fatalf("state game id = %q, want scenario name %q", def.State.GameID, def.Name)
			}
			if def.State.World == nil {
				t.Fatalf("state world is nil")
			}
			if def.State.Map == nil {
				t.Fatalf("state map is nil")
			}
			if def.State.Map.ID != def.Name {
				t.Fatalf("state map id = %q, want scenario name %q", def.State.Map.ID, def.Name)
			}
			if len(def.PlayerIDs) == 0 {
				t.Fatalf("player ids are empty")
			}
			seenPlayers := map[string]struct{}{}
			for _, playerID := range def.PlayerIDs {
				if playerID == "" {
					t.Fatalf("player ids contain empty id")
				}
				if _, ok := seenPlayers[playerID]; ok {
					t.Fatalf("player id %q appears more than once", playerID)
				}
				seenPlayers[playerID] = struct{}{}
				if def.State.Players[playerID] == nil {
					t.Fatalf("state missing player %q from scenario player ids", playerID)
				}
			}
		})
	}
}
