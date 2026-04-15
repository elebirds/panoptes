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
	"github.com/elebirds/panoptes/internal/event"
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
		{PlayerID: "player-1", NodeID: "A1", BuildingType: "farm", CityID: "C1"},
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
	ecs.CreateBuilding(world, "farm", "player-1", "C1", nodeEntry)
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

func TestEconomyPipelineRechargeAppliesResearchOutputModifierNextTurnPreview(t *testing.T) {
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
	if got := state.Players["player-1"].Research.CurrentProgress; got != 1 {
		t.Fatalf("research progress without active target = %d, want 1", got)
	}
	if got := state.EffectiveResearchOutput("player-1"); got != 3 {
		t.Fatalf("research output preview after unlock = %d, want 3", got)
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

func TestEconomyPipelineConsumesSameTurnIndustryBudgetInOrder(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "farm",
				PlacementKind: "city_territory",
				BuildingScope: "in_city",
				PointCosts:    staticdata.PointAmounts{"industry_output": 1},
				MaxHP:         80,
				TakeoverMode:  "city_capture",
			},
		},
	}))

	world := donburi.NewWorld()
	cityNode := ecs.CreateNode(world, ecs.MapNode{ID: "C1", X: 0, Y: 0, Terrain: "plain"})
	nodeA := ecs.CreateNode(world, ecs.MapNode{ID: "A1", X: 0, Y: 1, Terrain: "plain"})
	nodeB := ecs.CreateNode(world, ecs.MapNode{ID: "A2", X: 1, Y: 0, Terrain: "plain"})
	cityEntry := world.Entry(cityNode)
	entryA := world.Entry(nodeA)
	entryB := world.Entry(nodeB)
	for _, entry := range []*donburi.Entry{cityEntry, entryA, entryB} {
		node := ecs.NodeC.Get(entry)
		node.Owner = "player-1"
		node.TerritoryOwner = "player-1"
	}
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", cityEntry)

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID: "default",
		NodeIndex: map[string]donburi.Entity{
			"C1": cityNode,
			"A1": nodeA,
			"A2": nodeB,
		},
	})
	state.World = world
	state.EnsureCityState("player-1", "C1")
	state.Players["player-1"].CapitalCityID = "C1"
	state.Players["player-1"].Research.UnlockBuilding("farm")
	state.TurnRuntime.Planning.BuildOrders = []domain.BuildOrder{
		{PlayerID: "player-1", NodeID: "A1", BuildingType: "farm", CityID: "C1"},
		{PlayerID: "player-1", NodeID: "A2", BuildingType: "farm", CityID: "C1"},
	}

	events := engine.NewEconomyPipeline().Run(world, state)

	if !entryA.HasComponent(ecs.BuildingC) {
		t.Fatalf("first build should consume the only available industry budget: %#v", events)
	}
	if entryB.HasComponent(ecs.BuildingC) {
		t.Fatalf("second build should not resolve once industry budget is exhausted")
	}
	if got := state.TurnRuntime.Resolving.PointBudgets["player-1"].Get(domain.PointIndustryOutput); got != 0 {
		t.Fatalf("industry budget after resolving = %d, want 0", got)
	}
	if !hasEventKind(events, "point_budget_refreshed") {
		t.Fatalf("events should include point_budget_refreshed: %#v", events)
	}
	if !hasEventKind(events, "point_spent") {
		t.Fatalf("events should include point_spent: %#v", events)
	}
}

func TestEconomyPipelineBuildingModifiersAffectNextTurnPointPreview(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "workshop",
				PlacementKind: "city_territory",
				BuildingScope: "in_city",
				MaxHP:         80,
				TakeoverMode:  "city_capture",
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "point.output", PointKey: "industry_output", ModifierType: "flat", Value: 1},
				},
			},
		},
	}))

	world, state, nodeEntry := newOwnedNodeState()
	ecs.CreateBuilding(world, "workshop", "player-1", "", nodeEntry)

	if got := state.EffectiveIndustryOutput("player-1"); got != 3 {
		t.Fatalf("industry output with building modifier = %d, want 3", got)
	}
}

func TestEconomyPipelineBuildRevalidatesPlacementAtSettlement(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "farm",
				PlacementKind: "city_territory",
				BuildingScope: "in_city",
				PointCosts:    staticdata.PointAmounts{"industry_output": 1},
				MaxHP:         80,
				TakeoverMode:  "city_capture",
			},
		},
	}))

	world, state, nodeEntry := newOwnedNodeState()
	state.Players["player-1"].Research.UnlockBuilding("farm")
	state.TurnRuntime.Planning.BuildOrders = []domain.BuildOrder{
		{PlayerID: "player-1", NodeID: "A1", BuildingType: "farm", CityID: "C1"},
	}
	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = "player-2"
	node.TerritoryOwner = "player-2"

	events := engine.NewEconomyPipeline().Run(world, state)

	if nodeEntry.HasComponent(ecs.BuildingC) {
		t.Fatalf("build should be rejected once placement becomes invalid at settlement")
	}
	if hasEventKind(events, "point_spent") {
		t.Fatalf("point_spent should not be emitted for skipped builds: %#v", events)
	}
	if !hasEventWithReason(events, "building_skipped", "outside_territory") {
		t.Fatalf("events should include building_skipped outside_territory: %#v", events)
	}
}

func TestEconomyPipelineReportsSkippedRecipeWhenBuildingDisabled(t *testing.T) {
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
	ecs.CreateBuilding(world, "farm", "player-1", "C1", nodeEntry)
	ecs.BuildingOperationC.SetValue(nodeEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "farm_food",
		RequiredTurns:    1,
	})
	nodeEntry.AddComponent(ecs.BuildingStateC)
	ecs.BuildingStateC.SetValue(nodeEntry, ecs.BuildingStateComp{
		Disabled:       true,
		DisabledReason: "outside_territory",
	})
	state.Players["player-1"].Research.UnlockBuilding("farm")
	state.Players["player-1"].Research.UnlockRecipe("farm_food")

	events := engine.NewEconomyPipeline().Run(world, state)

	if !hasEventKind(events, "recipe_skipped") {
		t.Fatalf("events should include recipe_skipped: %#v", events)
	}
	if !hasEventWithReason(events, "recipe_skipped", "building_disabled") {
		t.Fatalf("events should include recipe_skipped building_disabled: %#v", events)
	}
}

func TestEconomyPipelineLowEfficiencyRecipeConsumesPartialInputAndProgress(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Buildings: []staticdata.BuildingDefinition{
			{ID: "barracks", PlacementKind: "city_territory", BuildingScope: "in_city", RecipeIDs: []string{"train_infantry"}, DefaultRecipeID: "train_infantry", MaxHP: 80, TakeoverMode: "city_capture"},
		},
		Recipes: []staticdata.RecipeDefinition{
			{
				ID:             "train_infantry",
				BuildingID:     "barracks",
				ResourceInputs: staticdata.ResourceAmounts{"food": 4},
				WorkAmount:     4,
				BaseProgress:   2,
			},
		},
	}))

	world, state, nodeEntry := newOwnedNodeState()
	ecs.CreateBuilding(world, "barracks", "player-1", "C1", nodeEntry)
	state.Players["player-1"].Resources.Set(domain.ResourceFood, 2)
	state.Players["player-1"].Research.UnlockBuilding("barracks")
	state.Players["player-1"].Research.UnlockRecipe("train_infantry")

	events := engine.NewEconomyPipeline().Run(world, state)

	operation := ecs.BuildingOperationC.Get(nodeEntry)
	if got := operation.ProgressTurns; got != 1 {
		t.Fatalf("progress turns = %d, want 1 after low-efficiency progress", got)
	}
	if got := state.Players["player-1"].Resources.Get(domain.ResourceFood); got != 1 {
		t.Fatalf("food after partial progress = %d, want 1", got)
	}
	if !hasEventKind(events, "recipe_progressed") {
		t.Fatalf("events should include recipe_progressed: %#v", events)
	}
	if hasEventKind(events, "recipe_completed") {
		t.Fatalf("events should not include recipe_completed: %#v", events)
	}
}

func TestEffectiveIndustryOutputIgnoresPendingActivationUntilOnline(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:            "workshop",
				PlacementKind: "city_territory",
				BuildingScope: "in_city",
				MaxHP:         80,
				TakeoverMode:  "city_capture",
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "point.output", PointKey: "industry_output", ModifierType: "flat", Value: 1},
				},
			},
		},
	}))

	world, state, nodeEntry := newOwnedNodeState()
	ecs.CreateBuilding(world, "workshop", "player-1", "C1", nodeEntry)
	domain.SetBuildingLifecycleState(nodeEntry, domain.BuildingStatusDisabled, "pending_activation", 2)

	state.Turn = 1
	if got := state.EffectiveIndustryOutput("player-1"); got != 2 {
		t.Fatalf("industry output before online turn = %d, want 2", got)
	}
	state.Turn = 2
	if got := state.EffectiveIndustryOutput("player-1"); got != 3 {
		t.Fatalf("industry output after online turn = %d, want 3", got)
	}
}

func TestEconomyPipelineCapturesNonCapitalCityWithoutGameOver(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: newPipelineRules(),
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "barracks", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 80, TakeoverMode: "city_capture", Tags: []string{"production"}},
			{ID: "wall", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 80, TakeoverMode: "city_capture", Tags: []string{"defense"}},
		},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 20, Attack: 6, AttackRange: 1, MoveRange: 2, VisionRange: 2, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
	}))

	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{}
	createNode := func(id string, x int, y int, owner string) *donburi.Entry {
		entity := ecs.CreateNode(world, ecs.MapNode{ID: id, X: x, Y: y, Terrain: "plain"})
		nodeIndex[id] = entity
		entry := world.Entry(entity)
		node := ecs.NodeC.Get(entry)
		node.Owner = owner
		node.TerritoryOwner = owner
		return entry
	}
	capitalEntry := createNode("C1", 0, 0, "player-1")
	cityEntry := createNode("C3", 2, 2, "player-1")
	barracksEntry := createNode("C4", 3, 2, "player-1")
	wallEntry := createNode("C2", 2, 1, "player-1")
	enemyCapitalEntry := createNode("E5", 4, 4, "player-2")
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", capitalEntry)
	ecs.CreateBuilding(world, "city_core", "player-1", "C3", cityEntry)
	ecs.CreateBuilding(world, "barracks", "player-1", "C3", barracksEntry)
	ecs.CreateBuilding(world, "wall", "player-1", "C3", wallEntry)
	ecs.CreateBuilding(world, "city_core", "player-2", "E5", enemyCapitalEntry)
	ecs.BuildingC.Get(cityEntry).HP = 0
	unitEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-2", domain.Position{X: 2, Y: 2}))
	ecs.UnitStatsC.Get(unitEntry).ID = "enemy-1"

	state := domain.NewGameState("city-capture", []string{"player-1", "player-2"}, []string{"alice", "bob"}, &domain.MapData{
		ID:           "city-capture",
		PlayerSpawns: map[string]domain.Position{"player-1": {X: 0, Y: 0}, "player-2": {X: 4, Y: 4}},
		NodeIndex:    nodeIndex,
	})
	state.World = world
	state.EnsureCityState("player-1", "C1")
	state.EnsureCityState("player-1", "C3")
	state.Players["player-1"].CapitalCityID = "C1"
	state.EnsureCityState("player-2", "E5")
	state.Players["player-2"].CapitalCityID = "E5"

	events := engine.NewEconomyPipeline().Run(world, state)

	if state.IsOver {
		t.Fatalf("state.IsOver = true, want false")
	}
	if _, ok := state.Players["player-1"].Cities["C3"]; ok {
		t.Fatalf("player-1 should no longer own city C3")
	}
	capturedCity := state.Players["player-2"].Cities["C3"]
	if capturedCity == nil || capturedCity.OwnerID != "player-2" || capturedCity.OnlineOnTurn != state.Turn+1 {
		t.Fatalf("captured city state = %#v, want transferred to player-2 and pending next turn", capturedCity)
	}
	if got := ecs.BuildingC.Get(cityEntry).Owner; got != "player-2" {
		t.Fatalf("captured city core owner = %q, want player-2", got)
	}
	if got := ecs.BuildingC.Get(barracksEntry).Owner; got != "player-2" {
		t.Fatalf("captured barracks owner = %q, want player-2", got)
	}
	if status, _ := domain.BuildingLifecycleStateAtTurn(wallEntry, state.Turn); status != domain.BuildingStatusRuined {
		t.Fatalf("wall status = %q, want ruined", status)
	}
	if !hasEventKind(events, "city_captured") {
		t.Fatalf("events should include city_captured: %#v", events)
	}
	if !hasEventKind(events, "building_ruined") {
		t.Fatalf("events should include building_ruined for ruined defensive buildings: %#v", events)
	}
}

func TestEconomyPipelineCompletesFacilityTakeoverAfterConsecutiveControl(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
			FacilityTakeoverTurns:     2,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "farm", PlacementKind: "resource_node", BuildingScope: "out_of_city", RequiredResourceType: "food", MaxHP: 80, TakeoverMode: "delayed"},
		},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 20, Attack: 6, AttackRange: 1, MoveRange: 2, VisionRange: 2, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
	}))

	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{}
	createNode := func(id string, x int, y int, owner string, resource bool) *donburi.Entry {
		entity := ecs.CreateNode(world, ecs.MapNode{ID: id, X: x, Y: y, Terrain: "plain", IsResourcePoint: resource, ResourceType: "food"})
		nodeIndex[id] = entity
		entry := world.Entry(entity)
		node := ecs.NodeC.Get(entry)
		node.Owner = owner
		node.TerritoryOwner = owner
		return entry
	}
	player1Capital := createNode("C1", 0, 0, "player-1", false)
	player2Capital := createNode("E5", 4, 4, "player-2", false)
	farmEntry := createNode("B2", 1, 1, "player-1", true)
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", player1Capital)
	ecs.CreateBuilding(world, "city_core", "player-2", "E5", player2Capital)
	ecs.CreateBuilding(world, "farm", "player-1", "C1", farmEntry)
	unitEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-2", domain.Position{X: 1, Y: 1}))
	ecs.UnitStatsC.Get(unitEntry).ID = "enemy-1"

	state := domain.NewGameState("facility-capture", []string{"player-1", "player-2"}, []string{"alice", "bob"}, &domain.MapData{
		ID:           "facility-capture",
		PlayerSpawns: map[string]domain.Position{"player-1": {X: 0, Y: 0}, "player-2": {X: 4, Y: 4}},
		NodeIndex:    nodeIndex,
	})
	state.World = world
	state.EnsureCityState("player-1", "C1")
	state.Players["player-1"].CapitalCityID = "C1"
	state.EnsureCityState("player-2", "E5")
	state.Players["player-2"].CapitalCityID = "E5"

	firstTurnEvents := engine.NewEconomyPipeline().Run(world, state)
	if !hasEventKind(firstTurnEvents, "facility_takeover_progressed") {
		t.Fatalf("first turn should include facility_takeover_progressed: %#v", firstTurnEvents)
	}
	if got := ecs.BuildingC.Get(farmEntry).Owner; got != "player-1" {
		t.Fatalf("farm owner after first turn = %q, want player-1", got)
	}
	state.Turn++

	secondTurnEvents := engine.NewEconomyPipeline().Run(world, state)
	if !hasEventKind(secondTurnEvents, "facility_takeover_completed") {
		t.Fatalf("second turn should include facility_takeover_completed: %#v", secondTurnEvents)
	}
	if got := ecs.BuildingC.Get(farmEntry).Owner; got != "player-2" {
		t.Fatalf("farm owner after capture = %q, want player-2", got)
	}
	if got := ecs.ResolveServiceCityID(farmEntry); got != "E5" {
		t.Fatalf("farm service city after capture = %q, want E5", got)
	}
	if status, _ := domain.BuildingLifecycleStateAtTurn(farmEntry, state.Turn); status != domain.BuildingStatusDisabled {
		t.Fatalf("farm status on takeover completion turn = %q, want disabled", status)
	}
}

func hasEventKind(events []event.Event, kind string) bool {
	for _, evt := range events {
		if evt != nil && evt.Kind() == kind {
			return true
		}
	}
	return false
}

func hasEventWithReason(events []event.Event, kind string, reason string) bool {
	for _, evt := range events {
		if evt == nil || evt.Kind() != kind {
			continue
		}
		switch current := evt.(type) {
		case event.BuildSkippedEvent:
			if current.Reason == reason {
				return true
			}
		case event.RecipeSkippedEvent:
			if current.Reason == reason {
				return true
			}
		}
	}
	return false
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
	cityEntity := ecs.CreateNode(world, ecs.MapNode{ID: "C1", X: 0, Y: 0, Terrain: "plain"})
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", X: 1, Y: 0, Terrain: "plain"})
	cityEntry := world.Entry(cityEntity)
	nodeEntry := world.Entry(nodeEntity)
	for _, entry := range []*donburi.Entry{cityEntry, nodeEntry} {
		ecs.NodeC.Get(entry).Owner = "player-1"
		ecs.NodeC.Get(entry).TerritoryOwner = "player-1"
	}
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", cityEntry)

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"C1": cityEntity, "A1": nodeEntity},
	})
	state.World = world
	state.EnsureCityState("player-1", "C1")
	state.Players["player-1"].CapitalCityID = "C1"
	return world, state, nodeEntry
}
