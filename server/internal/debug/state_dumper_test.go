package debug

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestBuildStateSummaryUsesResolvedLifecycleForPendingActivation(t *testing.T) {
	state, nodeEntry := newStateSummaryFixture()
	state.Turn = 1
	domain.SetBuildingLifecycleState(nodeEntry, domain.BuildingStatusDisabled, "pending_activation", 2)

	before := BuildStateSummary(state)
	if got := before.Buildings["F1"]; !got.Disabled || got.DisabledReason != "pending_activation" {
		t.Fatalf("turn 1 building summary = %#v, want disabled pending_activation", got)
	}

	state.Turn = 2
	after := BuildStateSummary(state)
	if got := after.Buildings["F1"]; got.Disabled || got.DisabledReason != "" {
		t.Fatalf("turn 2 building summary = %#v, want enabled empty reason", got)
	}
}

func TestBuildStateSummaryUsesResolvedLifecycleForTakeoverCompletion(t *testing.T) {
	state, nodeEntry := newStateSummaryFixture()
	state.Turn = 5
	domain.SetBuildingLifecycleState(nodeEntry, domain.BuildingStatusDisabled, "pending_activation", 6)
	nodeEntry.AddComponent(ecs.FacilityTakeoverC)
	ecs.FacilityTakeoverC.SetValue(nodeEntry, ecs.FacilityTakeoverComp{
		Mode:      "delayed",
		Progress:  3,
		Required:  3,
		Completed: true,
	})

	before := BuildStateSummary(state)
	if got := before.Buildings["F1"]; !got.Disabled || got.DisabledReason != "pending_activation" {
		t.Fatalf("turn 5 building summary = %#v, want disabled pending_activation", got)
	}

	state.Turn = 6
	after := BuildStateSummary(state)
	if got := after.Buildings["F1"]; got.Disabled || got.DisabledReason != "" {
		t.Fatalf("turn 6 building summary = %#v, want enabled empty reason", got)
	}
}

func newStateSummaryFixture() (*domain.GameState, *donburi.Entry) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
			FacilityTakeoverTurns:     3,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "farm", BuildingScope: "out_of_city", PlacementKind: "resource_node", MaxHP: 60, TakeoverMode: "delayed"},
		},
	}))

	world := donburi.NewWorld()
	entity := ecs.CreateNode(world, ecs.MapNode{
		ID:              "F1",
		Q:               0,
		R:               0,
		Terrain:         "plain",
		IsResourcePoint: true,
		ResourceType:    "food",
	})
	nodeEntry := world.Entry(entity)
	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = "player-1"
	node.TerritoryOwner = "player-1"
	ecs.CreateBuilding(world, "farm", "player-1", "C1", nodeEntry)

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"F1": entity},
	})
	state.World = world
	return state, nodeEntry
}
