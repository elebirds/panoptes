package event

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestBuildingDeactivatedEventApplyMarksBuildingDisabled(t *testing.T) {
	t.Parallel()

	world := donburi.NewWorld()
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "farm", Category: "production", Combat: staticdata.BuildingCombat{MaxHP: 80}},
		},
	}))
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", X: 0, Y: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.CreateBuilding(world, "farm", "player-1", "", nodeEntry)

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"A1": nodeEntity},
	})
	state.World = world

	BuildingDeactivatedEvent{NodeID: "A1", Reason: "outside_territory"}.Apply(world, state)

	if !nodeEntry.HasComponent(ecs.BuildingStateC) {
		t.Fatalf("building state component missing")
	}
	buildingState := ecs.BuildingStateC.Get(nodeEntry)
	if !buildingState.Disabled {
		t.Fatalf("building should be disabled")
	}
	if buildingState.DisabledReason != "outside_territory" {
		t.Fatalf("disabled reason = %q, want outside_territory", buildingState.DisabledReason)
	}
}
