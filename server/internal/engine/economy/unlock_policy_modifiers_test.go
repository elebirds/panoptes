package economy_test

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/economy"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestValidateBuildAndRecipeSelectionTreatUnreferencedContentAsBaselineUnlocked(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "farm",
				PlacementKind: "city_territory",
				BuildingScope: "in_city",
				RecipeIDs:     []string{"farm_food"},
				MaxHP:         80,
				TakeoverMode:  "city_capture",
			},
			{
				ID:            "barracks",
				PlacementKind: "city_territory",
				BuildingScope: "in_city",
				MaxHP:         90,
				TakeoverMode:  "city_capture",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID:           "farm_food",
				BuildingID:   "farm",
				WorkAmount:   1,
				BaseProgress: 1,
				Outputs:      staticdata.RecipeOutputs{Resources: staticdata.ResourceAmounts{"food": 2}},
			},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:           "militia_mobilization",
				Branch:       "military",
				Tier:         2,
				ResearchCost: 2,
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_building", TargetID: "barracks"},
				},
			},
		},
	}))

	world, state, nodeEntry := newOwnedNodeState()

	buildValidation := economy.ValidateBuildOrder(state, "player-1", "A1", "farm", "C1")
	if !buildValidation.OK {
		t.Fatalf("baseline farm build should be available, got %#v", buildValidation)
	}

	ecs.CreateBuilding(world, "farm", "player-1", "C1", nodeEntry)
	recipeValidation := economy.ValidateRecipeSelection(state, "player-1", "A1", "farm_food")
	if !recipeValidation.OK {
		t.Fatalf("baseline farm recipe should be available, got %#v", recipeValidation)
	}
}

func TestRecoveryPolicyBuffsRecipeOutputSameTurn(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:              "farm",
				PlacementKind:   "city_territory",
				BuildingScope:   "in_city",
				ResourceCosts:   staticdata.ResourceAmounts{},
				PointCosts:      staticdata.PointAmounts{"industry_output": 1},
				RecipeIDs:       []string{"farm_food"},
				DefaultRecipeID: "farm_food",
				MaxHP:           80,
				TakeoverMode:    "city_capture",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID:           "farm_food",
				BuildingID:   "farm",
				WorkAmount:   1,
				BaseProgress: 1,
				Outputs:      staticdata.RecipeOutputs{Resources: staticdata.ResourceAmounts{"food": 2}},
			},
		},
		Policies: []staticdata.PolicyDefinition{
			{
				ID:               "recovery",
				Layer:            "national",
				ActivationTiming: "same_turn",
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "recipe.resource_output", TargetID: "farm_food", ResourceKey: "food", ModifierType: "flat", Value: 1},
				},
			},
		},
	}))

	world, state, nodeEntry := newOwnedNodeState()
	ecs.CreateBuilding(world, "farm", "player-1", "C1", nodeEntry)
	nodeEntry.AddComponent(ecs.BuildingOperationC)
	ecs.BuildingOperationC.SetValue(nodeEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "farm_food",
		RequiredTurns:    1,
	})
	state.Players["player-1"].Policy = domain.PolicyRecovery
	state.Players["player-1"].Research.UnlockRecipe("farm_food")

	economy.NewRunner().Run(world, state)

	if got := state.Players["player-1"].Resources.Get(domain.ResourceFood); got != 3 {
		t.Fatalf("food after recovery policy recipe = %d, want 3", got)
	}
}

func TestWarPreparednessCompletesArcherRecipeInOneTurn(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:              "archery",
				PlacementKind:   "city_territory",
				BuildingScope:   "in_city",
				ResourceCosts:   staticdata.ResourceAmounts{},
				PointCosts:      staticdata.PointAmounts{"industry_output": 1},
				RecipeIDs:       []string{"archery_archer"},
				DefaultRecipeID: "archery_archer",
				MaxHP:           90,
				TakeoverMode:    "city_capture",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID:             "archery_archer",
				BuildingID:     "archery",
				ResourceInputs: staticdata.ResourceAmounts{"food": 1, "wood": 1},
				PointInputs:    staticdata.PointAmounts{"industry_output": 1},
				WorkAmount:     2,
				BaseProgress:   1,
				Outputs:        staticdata.RecipeOutputs{Units: []string{"archer"}},
			},
		},
		Units: []staticdata.UnitDefinition{
			{
				ID:          "archer",
				Class:       "ranged",
				MaxHP:       20,
				Attack:      8,
				AttackRange: 2,
				MoveRange:   2,
				VisionRange: 4,
				TrainCost:   staticdata.ResourceAmounts{},
				Upkeep:      staticdata.ResourceAmounts{"food": 1},
				Flags: staticdata.UnitFlags{
					CanCapture:          true,
					CanAttackStructures: false,
				},
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
		Policies: []staticdata.PolicyDefinition{
			{
				ID:               "war_preparedness",
				Layer:            "national",
				ActivationTiming: "same_turn",
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "recipe.work_amount", TargetID: "archery_archer", ModifierType: "flat", Value: -1},
				},
			},
		},
	}))

	world, state, nodeEntry := newOwnedNodeState()
	ecs.CreateBuilding(world, "archery", "player-1", "C1", nodeEntry)
	nodeEntry.AddComponent(ecs.BuildingOperationC)
	ecs.BuildingOperationC.SetValue(nodeEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "archery_archer",
		RequiredTurns:    2,
	})
	state.Players["player-1"].Policy = domain.PolicyWarPreparedness
	state.Players["player-1"].Resources.Set(domain.ResourceFood, 1)
	state.Players["player-1"].Resources.Set(domain.ResourceWood, 1)
	state.Players["player-1"].Research.UnlockRecipe("archery_archer")

	economy.NewRunner().Run(world, state)

	unitID := findUnitIDByType(world, "player-1", "archer")
	if unitID == "" {
		t.Fatalf("archer should be produced in one turn under war_preparedness")
	}
}

func TestReorganizationPolicyRaisesIndustryOutputSameTurn(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Policies: []staticdata.PolicyDefinition{
			{
				ID:               "reorganization",
				Layer:            "national",
				ActivationTiming: "same_turn",
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "point.output", PointKey: "industry_output", ModifierType: "flat", Value: 1},
				},
			},
		},
	}))

	_, state, _ := newOwnedNodeState()
	state.Players["player-1"].Policy = domain.PolicyReorganization

	if got := state.EffectiveIndustryOutput("player-1"); got != 3 {
		t.Fatalf("industry output with reorganization = %d, want 3", got)
	}
}

func TestExpansionPolicyCompletesSettlerRecipeInOneTurn(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:              "city_core",
				PlacementKind:   "city_foundation_center",
				BuildingScope:   "city_core",
				ResourceCosts:   staticdata.ResourceAmounts{},
				PointCosts:      staticdata.PointAmounts{"industry_output": 1},
				RecipeIDs:       []string{"city_core_settler"},
				DefaultRecipeID: "city_core_settler",
				MaxHP:           100,
				TakeoverMode:    "disabled",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID:             "city_core_settler",
				BuildingID:     "city_core",
				ResourceInputs: staticdata.ResourceAmounts{"food": 2, "wood": 1},
				PointInputs:    staticdata.PointAmounts{"industry_output": 1},
				WorkAmount:     2,
				BaseProgress:   1,
				Outputs:        staticdata.RecipeOutputs{Units: []string{"settler"}},
			},
		},
		Units: []staticdata.UnitDefinition{
			{
				ID:          "settler",
				Class:       "civilian",
				MaxHP:       12,
				Attack:      0,
				AttackRange: 0,
				MoveRange:   2,
				VisionRange: 2,
				TrainCost:   staticdata.ResourceAmounts{},
				Upkeep:      staticdata.ResourceAmounts{"food": 1},
				Flags:       staticdata.UnitFlags{CanCapture: true},
			},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
		Policies: []staticdata.PolicyDefinition{
			{
				ID:               "expansion",
				Layer:            "national",
				ActivationTiming: "same_turn",
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "recipe.work_amount", TargetID: "city_core_settler", ModifierType: "flat", Value: -1},
				},
			},
		},
	}))

	world, state, _ := newOwnedNodeState()
	cityEntry, ok := state.GetNode("C1")
	if !ok {
		t.Fatalf("missing city core node C1")
	}
	cityEntry.AddComponent(ecs.BuildingOperationC)
	ecs.BuildingOperationC.SetValue(cityEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "city_core_settler",
		RequiredTurns:    2,
	})
	state.Players["player-1"].Policy = domain.PolicyExpansion
	state.Players["player-1"].Resources.Set(domain.ResourceFood, 2)
	state.Players["player-1"].Resources.Set(domain.ResourceWood, 1)
	state.Players["player-1"].Research.UnlockRecipe("city_core_settler")

	economy.NewRunner().Run(world, state)

	unitID := findUnitIDByType(world, "player-1", "settler")
	if unitID == "" {
		t.Fatalf("settler should be produced in one turn under expansion policy")
	}
}

func findUnitIDByType(world donburi.World, owner string, unitType string) string {
	unitID := ""
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if unitID != "" || entry == nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.Faction == owner && string(stats.Type) == unitType {
			unitID = stats.ID
		}
	})
	return unitID
}
