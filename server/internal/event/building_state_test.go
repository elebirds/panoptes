package event

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestBuildingStatusChangedEventApplyMarksBuildingDisabled(t *testing.T) {
	t.Parallel()

	world := donburi.NewWorld()
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "farm", MaxHP: 80},
		},
	}))
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", Q: 0, R: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.CreateBuilding(world, "farm", "player-1", "", nodeEntry)

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"A1": nodeEntity},
	})
	state.World = world

	BuildingStatusChangedEvent{
		NodeID: "A1",
		Status: domain.BuildingStatusDisabled,
		Reason: "outside_territory",
	}.Apply(world, state)

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

func TestFacilityTakeoverCompletedEventApplyTransfersOwnershipAndBinding(t *testing.T) {
	world := donburi.NewWorld()
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			CityCoreMaxHP:              100,
			InitialCityTerritoryRadius: 1,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			FacilityTakeoverTurns:      2,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "farm", PlacementKind: "resource_node", BuildingScope: "out_of_city", RequiredResourceType: "food", MaxHP: 80, TakeoverMode: "delayed"},
		},
	}))

	nodeIndex := map[string]donburi.Entity{}
	createNode := func(id string, x int, y int, owner string, resource bool) *donburi.Entry {
		entity := ecs.CreateNode(world, ecs.MapNode{ID: id, Q: x, R: y, Terrain: "plain", IsResourcePoint: resource, ResourceType: "food"})
		nodeIndex[id] = entity
		entry := world.Entry(entity)
		node := ecs.NodeC.Get(entry)
		node.Owner = owner
		node.TerritoryOwner = owner
		return entry
	}

	player1Capital := createNode("A2", 0, 1, "player-1", false)
	player2Capital := createNode("E2", 4, 1, "player-2", false)
	farmEntry := createNode("C2", 2, 1, "player-1", true)
	ecs.CreateBuilding(world, "city_core", "player-1", "A2", player1Capital)
	ecs.CreateBuilding(world, "city_core", "player-2", "E2", player2Capital)
	ecs.CreateBuilding(world, "farm", "player-1", "A2", farmEntry)
	ecs.FacilityTakeoverC.SetValue(farmEntry, ecs.FacilityTakeoverComp{
		Mode:               "delayed",
		Progress:           1,
		Required:           2,
		ControllerPlayerID: "player-2",
		Completed:          false,
	})

	state := domain.NewGameState("game-1", []string{"player-1", "player-2"}, []string{"alice", "bob"}, &domain.MapData{
		ID:           "default",
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 1}, "player-2": {Q: 4, R: 1}},
		NodeIndex:    nodeIndex,
	})
	state.World = world
	state.EnsureCityState("player-1", "A2")
	state.Players["player-1"].CapitalCityID = "A2"
	state.EnsureCityState("player-2", "E2")
	state.Players["player-2"].CapitalCityID = "E2"

	FacilityTakeoverCompletedEvent{
		NodeID:        "C2",
		NewOwnerID:    "player-2",
		ServiceCityID: "E2",
		OnlineOnTurn:  5,
	}.Apply(world, state)

	building := ecs.BuildingC.Get(farmEntry)
	if building.Owner != "player-2" || ecs.ResolveCityID(farmEntry) != "E2" {
		t.Fatalf("building after takeover = %#v, want owner player-2 city E2", building)
	}
	if got := ecs.ResolveServiceCityID(farmEntry); got != "E2" {
		t.Fatalf("service city after takeover = %q, want E2", got)
	}
	node := ecs.NodeC.Get(farmEntry)
	if node.Owner != "player-2" || node.TerritoryOwner != "player-2" {
		t.Fatalf("node owner after takeover = %#v, want player-2", node)
	}
	takeover := ecs.FacilityTakeoverC.Get(farmEntry)
	if !takeover.Completed || takeover.ControllerPlayerID != "player-2" || takeover.Progress != 0 {
		t.Fatalf("takeover runtime after completion = %#v", takeover)
	}
	status, reason := domain.BuildingLifecycleStateAtTurn(farmEntry, 4)
	if status != domain.BuildingStatusDisabled || reason != "pending_activation" {
		t.Fatalf("building lifecycle before online turn = (%q, %q), want disabled pending_activation", status, reason)
	}
	status, reason = domain.BuildingLifecycleStateAtTurn(farmEntry, 5)
	if status != domain.BuildingStatusIdle || reason != "" {
		t.Fatalf("building lifecycle on online turn = (%q, %q), want idle", status, reason)
	}
}
