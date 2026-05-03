package economy_test

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/economy"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestPolicyPriorityChangesSharedLogisticsShortageOutcome(t *testing.T) {
	warEvents := runPriorityLogisticsScenario(t, domain.PolicyWarPreparedness)
	if !eventCompletedNode(warEvents, "M1") {
		t.Fatalf("war policy should complete military recipe first: %#v", warEvents)
	}
	if eventCompletedNode(warEvents, "E1") {
		t.Fatalf("war policy should not also complete expansion recipe with one capacity: %#v", warEvents)
	}

	expansionEvents := runPriorityLogisticsScenario(t, domain.PolicyExpansion)
	if !eventCompletedNode(expansionEvents, "E1") {
		t.Fatalf("expansion policy should complete expansion recipe first: %#v", expansionEvents)
	}
	if eventCompletedNode(expansionEvents, "M1") {
		t.Fatalf("expansion policy should not also complete military recipe with one capacity: %#v", expansionEvents)
	}
}

func TestInstitutionModifierIncreasesSharedRoadCapacity(t *testing.T) {
	_, baseState := newPriorityLogisticsScenario()
	baseState.AddResourceToCity("player-1", "C1", domain.ResourceOre, 1)
	baseEvents := economy.NewRunner().Run(baseState.World, baseState)
	if countCompletedNodes(baseEvents) != 1 {
		t.Fatalf("base capacity should complete exactly one recipe: %#v", baseEvents)
	}

	_, institutionState := newPriorityLogisticsScenario()
	institutionState.AddResourceToCity("player-1", "C1", domain.ResourceOre, 1)
	institutionState.Players["player-1"].Institutions.ActivePolicyIDs = []string{"logistics_corps"}
	institutionEvents := economy.NewRunner().Run(institutionState.World, institutionState)
	if countCompletedNodes(institutionEvents) != 2 {
		t.Fatalf("logistics institution should complete two recipes with expanded capacity: %#v", institutionEvents)
	}
}

func TestIndustrialChainShortagePropagatesDownstreamUntilInputExists(t *testing.T) {
	world, state := newIndustrialChainScenario()

	firstEvents := economy.NewRunner().Run(world, state)
	if !eventCompletedNode(firstEvents, "U1") {
		t.Fatalf("upstream mine should complete first turn: %#v", firstEvents)
	}
	if eventCompletedNode(firstEvents, "D2") {
		t.Fatalf("downstream barracks should not complete before ore exists: %#v", firstEvents)
	}
	if got := ecs.BuildingOperationC.Get(mustNodeEntry(t, state, "D2")).BlockedReason; got != "insufficient_resources" {
		t.Fatalf("downstream blocked reason = %q, want insufficient_resources", got)
	}

	secondEvents := economy.NewRunner().Run(world, state)
	if !eventCompletedNode(secondEvents, "D2") {
		t.Fatalf("downstream barracks should complete after upstream ore enters storage: %#v", secondEvents)
	}
}

func runPriorityLogisticsScenario(t *testing.T, policy domain.Policy) []event.Event {
	t.Helper()
	world, state := newPriorityLogisticsScenario()
	state.Players["player-1"].Policy = policy
	return economy.NewRunner().Run(world, state)
}

func newPriorityLogisticsScenario() (donburi.World, *domain.GameState) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 4,
			RoadBaseCapacity:          1,
		},
		Terrains: []staticdata.TerrainDefinition{{ID: "plain", Passable: true, Buildable: true}},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", BuildingScope: "city_core", PlacementKind: "city_foundation_center", MaxHP: 100},
			{ID: "barracks", BuildingScope: "in_city", PlacementKind: "city_territory", RecipeIDs: []string{"train_infantry"}, DefaultRecipeID: "train_infantry", MaxHP: 80},
			{ID: "frontier_office", BuildingScope: "in_city", PlacementKind: "city_territory", RecipeIDs: []string{"organize_settler"}, DefaultRecipeID: "organize_settler", MaxHP: 80},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "train_infantry", BuildingID: "barracks", ResourceInputs: staticdata.ResourceAmounts{"ore": 1}, WorkAmount: 1, BaseProgress: 1, Tags: []string{"military"}},
			{ID: "organize_settler", BuildingID: "frontier_office", ResourceInputs: staticdata.ResourceAmounts{"ore": 1}, WorkAmount: 1, BaseProgress: 1, Tags: []string{"expansion"}},
		},
		Policies: []staticdata.PolicyDefinition{
			{ID: "war_preparedness", LogisticsPriority: []staticdata.LogisticsPriorityDefinition{{Tag: "military", Priority: 100}}},
			{ID: "expansion", LogisticsPriority: []staticdata.LogisticsPriorityDefinition{{Tag: "expansion", Priority: 100}}},
			{ID: "logistics_corps", Layer: "institutional", ModifierEffects: []staticdata.ModifierEffect{{Trigger: "logistics.road_capacity", ModifierType: "flat", Value: 1}}},
		},
	}))
	world, state := newTwoCityLogisticsWorld()
	ecs.CreateBuilding(world, "barracks", "player-1", "D1", mustNodeEntry(nil, state, "M1"))
	ecs.BuildingOperationC.SetValue(mustNodeEntry(nil, state, "M1"), ecs.BuildingOperationComp{SelectedRecipeID: "train_infantry", RequiredTurns: 1})
	ecs.CreateBuilding(world, "frontier_office", "player-1", "D1", mustNodeEntry(nil, state, "E1"))
	ecs.BuildingOperationC.SetValue(mustNodeEntry(nil, state, "E1"), ecs.BuildingOperationComp{SelectedRecipeID: "organize_settler", RequiredTurns: 1})
	state.AddResourceToCity("player-1", "C1", domain.ResourceOre, 1)
	for _, recipeID := range []string{"train_infantry", "organize_settler"} {
		state.Players["player-1"].Research.UnlockRecipe(recipeID)
	}
	return world, state
}

func newIndustrialChainScenario() (donburi.World, *domain.GameState) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 4,
			RoadBaseCapacity:          2,
		},
		Terrains: []staticdata.TerrainDefinition{{ID: "plain", Passable: true, Buildable: true}},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", BuildingScope: "city_core", PlacementKind: "city_foundation_center", MaxHP: 100},
			{ID: "mine", BuildingScope: "in_city", PlacementKind: "city_territory", RecipeIDs: []string{"mine_ore"}, DefaultRecipeID: "mine_ore", MaxHP: 80},
			{ID: "barracks", BuildingScope: "in_city", PlacementKind: "city_territory", RecipeIDs: []string{"train_infantry"}, DefaultRecipeID: "train_infantry", MaxHP: 80},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "mine_ore", BuildingID: "mine", WorkAmount: 1, BaseProgress: 1, Outputs: staticdata.RecipeOutputs{Resources: staticdata.ResourceAmounts{"ore": 1}}, Tags: []string{"ore"}},
			{ID: "train_infantry", BuildingID: "barracks", ResourceInputs: staticdata.ResourceAmounts{"ore": 1}, WorkAmount: 1, BaseProgress: 1, Tags: []string{"military"}},
		},
	}))
	world, state := newTwoCityLogisticsWorld()
	ecs.CreateBuilding(world, "mine", "player-1", "C1", mustNodeEntry(nil, state, "U1"))
	ecs.BuildingOperationC.SetValue(mustNodeEntry(nil, state, "U1"), ecs.BuildingOperationComp{SelectedRecipeID: "mine_ore", RequiredTurns: 1})
	ecs.CreateBuilding(world, "barracks", "player-1", "D1", mustNodeEntry(nil, state, "D2"))
	ecs.BuildingOperationC.SetValue(mustNodeEntry(nil, state, "D2"), ecs.BuildingOperationComp{SelectedRecipeID: "train_infantry", RequiredTurns: 1})
	for _, recipeID := range []string{"mine_ore", "train_infantry"} {
		state.Players["player-1"].Research.UnlockRecipe(recipeID)
	}
	return world, state
}

func newTwoCityLogisticsWorld() (donburi.World, *domain.GameState) {
	world := donburi.NewWorld()
	nodes := []ecs.MapNode{
		{ID: "C1", Q: 0, R: 0, Terrain: "plain"},
		{ID: "R1", Q: 1, R: 0, Terrain: "plain"},
		{ID: "D1", Q: 2, R: 0, Terrain: "plain"},
		{ID: "M1", Q: 2, R: 1, Terrain: "plain"},
		{ID: "E1", Q: 3, R: 0, Terrain: "plain"},
		{ID: "U1", Q: 0, R: 1, Terrain: "plain"},
		{ID: "D2", Q: 3, R: -1, Terrain: "plain"},
	}
	index := make(map[string]donburi.Entity, len(nodes))
	for _, node := range nodes {
		entity := ecs.CreateNode(world, node)
		entry := world.Entry(entity)
		ecs.NodeC.Get(entry).Owner = "player-1"
		ecs.NodeC.Get(entry).TerritoryOwner = "player-1"
		ecs.NodeC.Get(entry).HasRoad = true
		index[node.ID] = entity
	}
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", world.Entry(index["C1"]))
	ecs.CreateBuilding(world, "city_core", "player-1", "D1", world.Entry(index["D1"]))
	state := domain.NewGameState("priority-logistics-test", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default", NodeIndex: index})
	state.World = world
	state.EnsureCityState("player-1", "C1")
	state.EnsureCityState("player-1", "D1")
	state.Players["player-1"].CapitalCityID = "C1"
	return world, state
}

func eventCompletedNode(events []event.Event, nodeID string) bool {
	for _, evt := range events {
		completed, ok := evt.(event.RecipeCompletedEvent)
		if ok && completed.NodeID == nodeID {
			return true
		}
	}
	return false
}

func countCompletedNodes(events []event.Event) int {
	count := 0
	for _, evt := range events {
		if _, ok := evt.(event.RecipeCompletedEvent); ok {
			count++
		}
	}
	return count
}

func mustNodeEntry(t *testing.T, state *domain.GameState, nodeID string) *donburi.Entry {
	entry, ok := state.GetNode(nodeID)
	if !ok || entry == nil {
		if t != nil {
			t.Fatalf("missing node %s", nodeID)
		}
		panic("missing node " + nodeID)
	}
	return entry
}
