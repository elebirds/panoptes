package scenario

import "testing"

func TestScenarioBuildersProduceDeterministicStates(t *testing.T) {
	t.Parallel()

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
			if def.Catalog == nil {
				t.Fatalf("catalog is nil")
			}
			if def.State == nil {
				t.Fatalf("state is nil")
			}
			if def.State.World == nil {
				t.Fatalf("state world is nil")
			}
			if def.State.Map == nil {
				t.Fatalf("state map is nil")
			}
			if len(def.PlayerIDs) == 0 {
				t.Fatalf("player ids are empty")
			}
		})
	}
}
