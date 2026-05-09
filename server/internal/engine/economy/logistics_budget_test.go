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

func TestRecipeCannotConsumeDisconnectedCityStorage(t *testing.T) {
	world, state, workshopEntry := newLogisticsRecipeState(false)
	state.AddResourceToCity("player-1", "C1", domain.ResourceFood, 4)

	events := economy.NewRunner().Run(world, state)

	if hasEventKind(events, "resource_flowed") {
		t.Fatalf("disconnected recipe should not emit resource flow: %#v", events)
	}
	if got := state.CityStorage("player-1", "C1").Get(domain.ResourceFood); got != 4 {
		t.Fatalf("capital food = %d, want unchanged 4", got)
	}
	operation := ecs.BuildingOperationC.Get(workshopEntry)
	if operation.ProgressTurns != 0 {
		t.Fatalf("progress = %d, want 0", operation.ProgressTurns)
	}
	if operation.BlockedReason != "insufficient_resources" {
		t.Fatalf("blocked reason = %q, want insufficient_resources", operation.BlockedReason)
	}
}

func TestRecipeImportsConnectedStorageUpToRoadCapacity(t *testing.T) {
	world, state, workshopEntry := newLogisticsRecipeState(true)
	state.AddResourceToCity("player-1", "C1", domain.ResourceFood, 4)

	events := economy.NewRunner().Run(world, state)

	if !hasEventKind(events, "resource_flowed") {
		t.Fatalf("connected recipe should emit resource flow: %#v", events)
	}
	if got := flowedFood(events); got != 2 {
		t.Fatalf("flowed food = %d, want road capacity 2", got)
	}
	operation := ecs.BuildingOperationC.Get(workshopEntry)
	if operation.ProgressTurns != 2 {
		t.Fatalf("progress = %d, want inefficient progress 2", operation.ProgressTurns)
	}
	if got := state.CityStorage("player-1", "C1").Get(domain.ResourceFood); got != 2 {
		t.Fatalf("capital food = %d, want 2 after import", got)
	}
	if got := state.PlayerResourceView("player-1").Get(domain.ResourceFood); got != 2 {
		t.Fatalf("player food view = %d, want aggregate 2", got)
	}
}

func newLogisticsRecipeState(connected bool) (donburi.World, *domain.GameState, *donburi.Entry) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 4,
			RoadBaseCapacity:          2,
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", BuildingScope: "city_core", PlacementKind: "city_foundation_center", MaxHP: 100},
			{ID: "workshop", BuildingScope: "in_city", PlacementKind: "city_territory", RecipeIDs: []string{"ration"}, DefaultRecipeID: "ration", MaxHP: 80},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "ration", BuildingID: "workshop", ResourceInputs: staticdata.ResourceAmounts{"food": 4}, WorkAmount: 4, BaseProgress: 4},
		},
	}))

	world := donburi.NewWorld()
	capitalEntity := ecs.CreateNode(world, ecs.MapNode{ID: "C1", Q: 0, R: 0, Terrain: "plain"})
	midEntity := ecs.CreateNode(world, ecs.MapNode{ID: "R1", Q: 1, R: 0, Terrain: "plain"})
	cityEntity := ecs.CreateNode(world, ecs.MapNode{ID: "D1", Q: 2, R: 0, Terrain: "plain"})
	workshopEntity := ecs.CreateNode(world, ecs.MapNode{ID: "D2", Q: 2, R: 1, Terrain: "plain"})
	index := map[string]donburi.Entity{
		"C1": capitalEntity,
		"R1": midEntity,
		"D1": cityEntity,
		"D2": workshopEntity,
	}
	for _, entity := range index {
		entry := world.Entry(entity)
		ecs.NodeC.Get(entry).Owner = "player-1"
		ecs.NodeC.Get(entry).TerritoryOwner = "player-1"
		ecs.NodeC.Get(entry).HasRoad = connected
	}
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", world.Entry(capitalEntity))
	ecs.CreateBuilding(world, "city_core", "player-1", "D1", world.Entry(cityEntity))
	workshopEntry := world.Entry(workshopEntity)
	ecs.CreateBuilding(world, "workshop", "player-1", "D1", workshopEntry)
	workshopEntry.AddComponent(ecs.BuildingOperationC)
	ecs.BuildingOperationC.SetValue(workshopEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "ration",
		RequiredTurns:    4,
	})

	state := domain.NewGameState("logistics-test", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default", NodeIndex: index})
	state.World = world
	state.EnsureCityState("player-1", "C1")
	state.EnsureCityState("player-1", "D1")
	state.Players["player-1"].CapitalCityID = "C1"
	state.Players["player-1"].Research.UnlockBuilding("workshop")
	state.Players["player-1"].Research.UnlockRecipe("ration")
	return world, state, workshopEntry
}

func flowedFood(events []event.Event) int {
	total := 0
	for _, evt := range events {
		flow, ok := evt.(event.ResourceFlowedEvent)
		if !ok {
			continue
		}
		total += flow.Resources.Get(domain.ResourceFood)
	}
	return total
}
