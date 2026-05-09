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

func TestEconomyRunnerClearsActiveRecipeSelection(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "barracks", BuildingScope: "in_city", MaxHP: 80, RecipeIDs: []string{"train_infantry"}},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "train_infantry", BuildingID: "barracks", WorkAmount: 2, BaseProgress: 1},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "B1", Q: 0, R: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.CreateBuilding(world, "barracks", "player-1", "C1", nodeEntry)
	nodeEntry.AddComponent(ecs.BuildingOperationC)
	ecs.BuildingOperationC.SetValue(nodeEntry, ecs.BuildingOperationComp{
		SelectedRecipeID:  "train_infantry",
		ProgressTurns:     1,
		RequiredTurns:     2,
		ConsumedResources: domain.NewResourceBag(),
		ConsumedPoints:    domain.NewPointBag(),
	})

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"B1": nodeEntity},
	})
	state.World = world
	state.TurnRuntime.Planning.RecipeSelections = []domain.RecipeSelectionOrder{
		{PlayerID: "player-1", NodeID: "B1", RecipeID: ""},
	}
	state.Players["player-1"].Research.UnlockRecipe("train_infantry")

	events := economy.NewRunner().Run(world, state)
	operation := ecs.BuildingOperationC.Get(nodeEntry)
	if operation.SelectedRecipeID != "" || operation.ProgressTurns != 0 || operation.RequiredTurns != 0 {
		t.Fatalf("operation after clear = %#v, want no selected recipe and reset progress", operation)
	}
	if !containsRecipeClearEvent(events, "B1") {
		t.Fatalf("events = %#v, want recipe clear event", events)
	}
}

func containsRecipeClearEvent(events []event.Event, nodeID string) bool {
	for _, evt := range events {
		changed, ok := evt.(event.RecipeSelectionChangedEvent)
		if ok && changed.NodeID == nodeID && changed.RecipeID == "" {
			return true
		}
	}
	return false
}
