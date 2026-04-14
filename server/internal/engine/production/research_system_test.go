package production_test

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestDomesticPipelineResearchDoesNotAllowSameTurnBuild(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			StartingTechPoints: 1,
			TechPointsPerTurn:  1,
			TechPointsMax:      5,
			BuildPointsPerTurn: 10,
			BuildPointsMax:     30,
			CastleBaseHP:       100,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID: "farm", Category: "production", PlacementRule: "city_only", RequiredResourceType: "",
				BuildCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{},
				RecipeIDs: []string{"farm_food"}, DefaultRecipeID: "farm_food",
				Combat: staticdata.BuildingCombat{MaxHP: 80},
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID: "farm_food", BuildingID: "farm", Cost: staticdata.ResourceAmounts{},
				DurationTurns: 1, DelayPenalty: staticdata.RecipeDelayPenalty{Mode: "add_turns", Value: 1},
				Outputs: staticdata.RecipeOutputs{Resources: staticdata.ResourceAmounts{"food": 2}},
			},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID: "agri_unlock_farm", Branch: "agriculture", Tier: 1, TechPointCost: 1,
				Effects: []staticdata.TechnologyEffect{
					{Type: "unlock_building", TargetID: "farm"},
					{Type: "unlock_recipe", TargetID: "farm_food"},
				},
			},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", X: 0, Y: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.NodeC.Get(nodeEntry).Owner = "player-1"
	ecs.NodeC.Get(nodeEntry).TerritoryOwner = "player-1"

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID: "default",
		NodeIndex: map[string]donburi.Entity{
			"A1": nodeEntity,
		},
	})
	state.World = world
	state.TurnRuntime.Planning.ResearchOrders = []domain.ResearchOrder{
		{PlayerID: "player-1", TechnologyID: "agri_unlock_farm"},
	}
	state.TurnRuntime.Planning.BuildOrders = []domain.BuildOrder{
		{PlayerID: "player-1", NodeID: "A1", BuildingType: "farm"},
	}

	events := engine.NewEconomyPipeline().Run(world, state)
	if len(events) == 0 {
		t.Fatalf("expected domestic events")
	}
	if nodeEntry.HasComponent(ecs.BuildingC) {
		t.Fatalf("building should remain unavailable until next turn")
	}
	if !state.Players["player-1"].Research.HasTechnology("agri_unlock_farm") {
		t.Fatalf("technology not unlocked")
	}
	if state.Players["player-1"].Research.TechPoints != 1 {
		t.Fatalf("tech_points = %d, want 1 after recharge", state.Players["player-1"].Research.TechPoints)
	}
}

func TestDomesticPipelineRecipeProducesResourcesWhenSelected(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			StartingTechPoints: 0,
			TechPointsPerTurn:  1,
			TechPointsMax:      5,
			BuildPointsPerTurn: 10,
			BuildPointsMax:     30,
			CastleBaseHP:       100,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID: "farm", Category: "production", PlacementRule: "city_only",
				BuildCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{},
				RecipeIDs: []string{"farm_food"}, DefaultRecipeID: "farm_food",
				Combat: staticdata.BuildingCombat{MaxHP: 80},
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID: "farm_food", BuildingID: "farm", Cost: staticdata.ResourceAmounts{},
				DurationTurns: 1, DelayPenalty: staticdata.RecipeDelayPenalty{Mode: "add_turns", Value: 1},
				Outputs: staticdata.RecipeOutputs{Resources: staticdata.ResourceAmounts{"food": 2}},
			},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", X: 0, Y: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.NodeC.Get(nodeEntry).Owner = "player-1"
	ecs.NodeC.Get(nodeEntry).TerritoryOwner = "player-1"
	ecs.CreateBuilding(world, "farm", "player-1", "", nodeEntry)
	ecs.BuildingOperationC.SetValue(nodeEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "farm_food",
		RequiredTurns:    1,
	})

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"A1": nodeEntity},
	})
	state.World = world
	state.Players["player-1"].Research.UnlockBuilding("farm")
	state.Players["player-1"].Research.UnlockRecipe("farm_food")

	engine.NewEconomyPipeline().Run(world, state)

	if got := state.Players["player-1"].Resources.Get(domain.ResourceFood); got != 2 {
		t.Fatalf("food after recipe = %d, want 2", got)
	}
	operation := ecs.BuildingOperationC.Get(nodeEntry)
	if operation.ProgressTurns != 0 {
		t.Fatalf("progress turns = %d, want reset to 0", operation.ProgressTurns)
	}
}

func TestDomesticPipelineRecipeAddsDelayWhenResourcesMissing(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			StartingTechPoints: 0,
			TechPointsPerTurn:  1,
			TechPointsMax:      5,
			BuildPointsPerTurn: 10,
			BuildPointsMax:     30,
			CastleBaseHP:       100,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID: "smelter", Category: "production", PlacementRule: "city_only",
				BuildCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{},
				RecipeIDs: []string{"smelter_refined_ore"}, DefaultRecipeID: "smelter_refined_ore",
				Combat: staticdata.BuildingCombat{MaxHP: 80},
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID: "smelter_refined_ore", BuildingID: "smelter", Cost: staticdata.ResourceAmounts{"ore": 2},
				DurationTurns: 1, DelayPenalty: staticdata.RecipeDelayPenalty{Mode: "add_turns", Value: 2},
				Outputs: staticdata.RecipeOutputs{Resources: staticdata.ResourceAmounts{"refined_ore": 1}},
			},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", X: 0, Y: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.NodeC.Get(nodeEntry).Owner = "player-1"
	ecs.NodeC.Get(nodeEntry).TerritoryOwner = "player-1"
	ecs.CreateBuilding(world, "smelter", "player-1", "", nodeEntry)
	ecs.BuildingOperationC.SetValue(nodeEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "smelter_refined_ore",
		RequiredTurns:    1,
	})

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"A1": nodeEntity},
	})
	state.World = world
	state.Players["player-1"].Research.UnlockBuilding("smelter")
	state.Players["player-1"].Research.UnlockRecipe("smelter_refined_ore")

	engine.NewEconomyPipeline().Run(world, state)

	operation := ecs.BuildingOperationC.Get(nodeEntry)
	if operation.DelayTurns != 2 {
		t.Fatalf("delay turns = %d, want 2", operation.DelayTurns)
	}
	if got := state.Players["player-1"].Resources.Get(domain.ResourceRefinedOre); got != 0 {
		t.Fatalf("refined_ore after blocked recipe = %d, want 0", got)
	}
}

func TestDomesticPipelineBuildAppliesBuildingCostModifier(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			StartingTechPoints: 0,
			TechPointsPerTurn:  1,
			TechPointsMax:      5,
			BuildPointsPerTurn: 1,
			BuildPointsMax:     10,
			CastleBaseHP:       100,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID: "farm", Category: "production", PlacementRule: "city_only",
				BuildCost: staticdata.ResourceAmounts{"build_points": 2},
				RecipeIDs: []string{"farm_food"}, DefaultRecipeID: "farm_food",
				Combat: staticdata.BuildingCombat{MaxHP: 80},
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID: "farm_food", BuildingID: "farm", Cost: staticdata.ResourceAmounts{},
				DurationTurns: 1, DelayPenalty: staticdata.RecipeDelayPenalty{Mode: "add_turns", Value: 1},
				Outputs: staticdata.RecipeOutputs{Resources: staticdata.ResourceAmounts{"food": 2}},
			},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID: "construction_discount", Branch: "industry", Tier: 1, TechPointCost: 1,
				Effects: []staticdata.TechnologyEffect{
					{Type: "unlock_building", TargetID: "farm"},
					{Type: "unlock_recipe", TargetID: "farm_food"},
					{Type: "modifier", Trigger: "building.build_cost", TargetID: "farm", ResourceKey: "build_points", ModifierType: "flat", Value: -1},
				},
			},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", X: 0, Y: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.NodeC.Get(nodeEntry).Owner = "player-1"
	ecs.NodeC.Get(nodeEntry).TerritoryOwner = "player-1"

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"A1": nodeEntity},
	})
	state.World = world
	state.Players["player-1"].Research.UnlockTechnology("construction_discount")
	state.Players["player-1"].Research.UnlockBuilding("farm")
	state.Players["player-1"].Research.UnlockRecipe("farm_food")
	state.TurnRuntime.Planning.BuildOrders = []domain.BuildOrder{{PlayerID: "player-1", NodeID: "A1", BuildingType: "farm"}}

	engine.NewEconomyPipeline().Run(world, state)

	if !nodeEntry.HasComponent(ecs.BuildingC) {
		t.Fatalf("building should be created after discounted cost")
	}
}

func TestDomesticPipelineRecipeAppliesOutputModifier(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			StartingTechPoints: 0,
			TechPointsPerTurn:  1,
			TechPointsMax:      5,
			BuildPointsPerTurn: 10,
			BuildPointsMax:     30,
			CastleBaseHP:       100,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID: "farm", Category: "production", PlacementRule: "city_only",
				BuildCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{},
				RecipeIDs: []string{"farm_food"}, DefaultRecipeID: "farm_food",
				Combat: staticdata.BuildingCombat{MaxHP: 80},
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID: "farm_food", BuildingID: "farm", Cost: staticdata.ResourceAmounts{},
				DurationTurns: 1, DelayPenalty: staticdata.RecipeDelayPenalty{Mode: "add_turns", Value: 1},
				Outputs: staticdata.RecipeOutputs{Resources: staticdata.ResourceAmounts{"food": 2}},
			},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID: "agri_bonus", Branch: "agriculture", Tier: 1, TechPointCost: 1,
				Effects: []staticdata.TechnologyEffect{
					{Type: "unlock_building", TargetID: "farm"},
					{Type: "unlock_recipe", TargetID: "farm_food"},
					{Type: "modifier", Trigger: "recipe.output", TargetID: "farm_food", ResourceKey: "food", ModifierType: "flat", Value: 1},
				},
			},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", X: 0, Y: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.NodeC.Get(nodeEntry).Owner = "player-1"
	ecs.NodeC.Get(nodeEntry).TerritoryOwner = "player-1"
	ecs.CreateBuilding(world, "farm", "player-1", "", nodeEntry)
	ecs.BuildingOperationC.SetValue(nodeEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "farm_food",
		RequiredTurns:    1,
	})

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"A1": nodeEntity},
	})
	state.World = world
	state.Players["player-1"].Research.UnlockTechnology("agri_bonus")
	state.Players["player-1"].Research.UnlockBuilding("farm")
	state.Players["player-1"].Research.UnlockRecipe("farm_food")

	engine.NewEconomyPipeline().Run(world, state)

	if got := state.Players["player-1"].Resources.Get(domain.ResourceFood); got != 3 {
		t.Fatalf("food after modified recipe = %d, want 3", got)
	}
}

func TestDomesticPipelineRecipeConsumesCostAndProducesUnit(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			StartingTechPoints: 0,
			TechPointsPerTurn:  1,
			TechPointsMax:      5,
			BuildPointsPerTurn: 10,
			BuildPointsMax:     30,
			CastleBaseHP:       100,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID: "barracks", Category: "military", PlacementRule: "city_only",
				BuildCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{},
				RecipeIDs: []string{"train_warrior"}, DefaultRecipeID: "train_warrior",
				Combat: staticdata.BuildingCombat{MaxHP: 80},
			},
		},
		Units: []staticdata.UnitDefinition{
			{ID: "warrior", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID: "train_warrior", BuildingID: "barracks", Cost: staticdata.ResourceAmounts{"food": 2},
				DurationTurns: 1, DelayPenalty: staticdata.RecipeDelayPenalty{Mode: "add_turns", Value: 1},
				Outputs: staticdata.RecipeOutputs{Units: []string{"warrior"}},
			},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", X: 0, Y: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.NodeC.Get(nodeEntry).Owner = "player-1"
	ecs.NodeC.Get(nodeEntry).TerritoryOwner = "player-1"
	ecs.CreateBuilding(world, "barracks", "player-1", "", nodeEntry)
	ecs.BuildingOperationC.SetValue(nodeEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "train_warrior",
		RequiredTurns:    1,
	})

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"A1": nodeEntity},
	})
	state.World = world
	state.Players["player-1"].Resources.Set(domain.ResourceFood, 2)
	state.Players["player-1"].Research.UnlockBuilding("barracks")
	state.Players["player-1"].Research.UnlockRecipe("train_warrior")

	engine.NewEconomyPipeline().Run(world, state)

	if got := state.Players["player-1"].Resources.Get(domain.ResourceFood); got != 0 {
		t.Fatalf("food after training = %d, want 0", got)
	}
	if got := domain.GetUnitsByNode(world, domain.Position{X: 0, Y: 0}); len(got) != 1 {
		t.Fatalf("units at barracks = %d, want 1", len(got))
	}
}

func TestDomesticPipelineResearchGrantAppliesResourcesAndUnits(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			StartingTechPoints: 1,
			TechPointsPerTurn:  1,
			TechPointsMax:      5,
			BuildPointsPerTurn: 10,
			BuildPointsMax:     30,
			CastleBaseHP:       100,
		},
		Units: []staticdata.UnitDefinition{
			{ID: "warrior", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID: "mil_bonus", Branch: "military", Tier: 1, TechPointCost: 1,
				Effects: []staticdata.TechnologyEffect{
					{Type: "grant", GrantResources: staticdata.ResourceAmounts{"food": 3}, GrantUnits: []string{"warrior"}},
				},
			},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "C1", X: 1, Y: 1, Terrain: "plain"})
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID: "default",
		PlayerSpawns: map[string]domain.Position{
			"player-1": domain.Position{X: 1, Y: 1},
		},
		NodeIndex: map[string]donburi.Entity{"C1": nodeEntity},
	})
	state.World = world
	state.TurnRuntime.Planning.ResearchOrders = []domain.ResearchOrder{{PlayerID: "player-1", TechnologyID: "mil_bonus"}}

	engine.NewEconomyPipeline().Run(world, state)

	if got := state.Players["player-1"].Resources.Get(domain.ResourceFood); got != 3 {
		t.Fatalf("food after grant = %d, want 3", got)
	}
	if got := domain.GetUnitsByNode(world, domain.Position{X: 1, Y: 1}); len(got) != 1 {
		t.Fatalf("granted units at spawn = %d, want 1", len(got))
	}
}

func TestDomesticPipelineRechargeAppliesTechIncomeModifierNextTurn(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:      3,
			StartingTechPoints: 1,
			TechPointsPerTurn:  1,
			TechPointsMax:      5,
			BuildPointsPerTurn: 10,
			BuildPointsMax:     30,
			CastleBaseHP:       100,
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID: "research_boost", Branch: "industry", Tier: 1, TechPointCost: 1,
				Effects: []staticdata.TechnologyEffect{
					{Type: "modifier", Trigger: "player.tech_point_income", ModifierType: "flat", Value: 2},
				},
			},
		},
	}))

	world := donburi.NewWorld()
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.World = world
	state.TurnRuntime.Planning.ResearchOrders = []domain.ResearchOrder{{PlayerID: "player-1", TechnologyID: "research_boost"}}

	engine.NewEconomyPipeline().Run(world, state)

	if got := state.Players["player-1"].Research.TechPoints; got != 1 {
		t.Fatalf("tech_points after unlock turn = %d, want 1", got)
	}

	engine.NewEconomyPipeline().Run(world, state)

	if got := state.Players["player-1"].Research.TechPoints; got != 4 {
		t.Fatalf("tech_points after next-turn modified recharge = %d, want 4", got)
	}
}
