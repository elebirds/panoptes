// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-15 16:20:00 +0800
// Description: 验证经济结算引擎在新版静态数据语义下的核心回归。

package production_test

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestEconomyPipelineResearchUnlockDoesNotEnableSameTurnBuild(t *testing.T) {
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
				ID:           "agrarian_foundations",
				Branch:       "agriculture",
				Tier:         1,
				ResearchCost: 1,
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_building", TargetID: "farm"},
					{Type: "unlock_recipe", TargetID: "farm_food"},
				},
			},
		},
	}))

	world, state, nodeEntry := newOwnedNodeState()
	state.Players["player-1"].Research.CurrentProgress = 1
	state.Players["player-1"].Research.CurrentTargetTechnologyID = "agrarian_foundations"
	state.TurnRuntime.Planning.BuildOrders = []domain.BuildOrder{
		{PlayerID: "player-1", NodeID: "A1", BuildingType: "farm"},
	}

	engine.NewEconomyPipeline().Run(world, state)

	if nodeEntry.HasComponent(ecs.BuildingC) {
		t.Fatalf("building should remain unavailable until next turn")
	}
	if !state.Players["player-1"].Research.HasTechnology("agrarian_foundations") {
		t.Fatalf("technology not unlocked")
	}
}

func TestEconomyPipelineRecipeProducesResources(t *testing.T) {
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
	}))

	world, state, nodeEntry := newOwnedNodeState()
	ecs.CreateBuilding(world, "farm", "player-1", "", nodeEntry)
	ecs.BuildingOperationC.SetValue(nodeEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "farm_food",
		RequiredTurns:    1,
	})
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

func TestEconomyPipelineResearchGrantAppliesResourcesAndUnits(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:           "militia_mobilization",
				Branch:       "military",
				Tier:         1,
				ResearchCost: 1,
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "grant", GrantResources: staticdata.ResourceAmounts{"food": 3}, GrantUnits: []string{"infantry"}},
				},
			},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "C1", X: 1, Y: 1, Terrain: "plain"})
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:           "default",
		PlayerSpawns: map[string]domain.Position{"player-1": {X: 1, Y: 1}},
		NodeIndex:    map[string]donburi.Entity{"C1": nodeEntity},
	})
	state.World = world
	state.Players["player-1"].Research.CurrentProgress = 1
	state.Players["player-1"].Research.CurrentTargetTechnologyID = "militia_mobilization"

	engine.NewEconomyPipeline().Run(world, state)

	if got := state.Players["player-1"].Resources.Get(domain.ResourceFood); got != 3 {
		t.Fatalf("food after grant = %d, want 3", got)
	}
	if got := domain.GetUnitsByNode(world, domain.Position{X: 1, Y: 1}); len(got) != 1 {
		t.Fatalf("granted units at spawn = %d, want 1", len(got))
	}
}

func TestEconomyPipelineRechargeAppliesResearchOutputModifierNextTurn(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:           "research_boost",
				Branch:       "governance",
				Tier:         1,
				ResearchCost: 1,
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "point.output", PointKey: "research_output", ModifierType: "flat", Value: 2},
				},
			},
		},
	}))

	world := donburi.NewWorld()
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.World = world
	state.Players["player-1"].Research.CurrentProgress = 1
	state.Players["player-1"].Research.CurrentTargetTechnologyID = "research_boost"

	engine.NewEconomyPipeline().Run(world, state)
	if got := state.Players["player-1"].Research.CurrentProgress; got != 1 {
		t.Fatalf("research progress after unlock turn = %d, want 1", got)
	}

	engine.NewEconomyPipeline().Run(world, state)
	if got := state.Players["player-1"].Research.CurrentProgress; got != 4 {
		t.Fatalf("research progress after next-turn modifier = %d, want 4", got)
	}
}

func TestEconomyPipelineResearchUnlockClearsCurrentTarget(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID:           "agrarian_foundations",
				Branch:       "agriculture",
				Tier:         1,
				ResearchCost: 1,
			},
		},
	}))

	world := donburi.NewWorld()
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.World = world
	state.Players["player-1"].Research.CurrentTargetTechnologyID = "agrarian_foundations"
	state.Players["player-1"].Research.CurrentProgress = 1

	engine.NewEconomyPipeline().Run(world, state)

	if got := state.Players["player-1"].Research.CurrentTargetTechnologyID; got != "" {
		t.Fatalf("current target after unlock = %q, want empty", got)
	}
}

func newPipelineRules() staticdata.Rules {
	return staticdata.Rules{
		TokensPerTurn:             3,
		CityCoreMaxHP:             100,
		BaseResearchOutputPerTurn: 1,
		BaseIndustryOutputPerTurn: 2,
	}
}

func newOwnedNodeState() (donburi.World, *domain.GameState, *donburi.Entry) {
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
	return world, state, nodeEntry
}
