package event

import (
	"fmt"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestCityFoundedEventApplyClaimsTerritoryAndCreatesPendingCityCore(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			CityCoreMaxHP:              30,
			InitialCityTerritoryRadius: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 30, TakeoverMode: "disabled"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	world := donburi.NewWorld()
	nodeIndex := make(map[string]donburi.Entity)
	for y := 0; y < 3; y++ {
		for x := 0; x < 3; x++ {
			nodeID := fmt.Sprintf("%c%d", 'A'+x, y+1)
			entity := ecs.CreateNode(world, ecs.MapNode{ID: nodeID, Q: x, R: y, Terrain: "plain"})
			nodeIndex[nodeID] = entity
		}
	}
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "city-founded",
		Width:     3,
		Height:    3,
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	state.Turn = 3

	centerEntry := world.Entry(nodeIndex["B1"])
	settlerEntry := world.Entry(ecs.CreateUnit(world, "settler", "player-1", domain.Position{Q: 1, R: 1}))
	ecs.UnitStatsC.Get(settlerEntry).ID = "settler-1"

	evt := CityFoundedEvent{
		PlayerID:     "player-1",
		UnitID:       "settler-1",
		CityID:       "B1",
		CenterNodeID: "B1",
		TerritoryIDs: []string{"A1", "B1", "C1", "A2", "B2", "C2", "A3", "B3", "C3"},
		OnlineOnTurn: 4,
	}
	evt.Apply(world, state)

	for _, nodeID := range evt.TerritoryIDs {
		entry := world.Entry(nodeIndex[nodeID])
		node := ecs.NodeC.Get(entry)
		if node.Owner != "player-1" || node.TerritoryOwner != "player-1" {
			t.Fatalf("%s owner state = %#v, want player-1 control", nodeID, node)
		}
	}
	if !centerEntry.HasComponent(ecs.BuildingC) {
		t.Fatalf("center node missing building after city founded")
	}
	building := ecs.BuildingC.Get(centerEntry)
	if got := string(building.Type); got != "city_core" {
		t.Fatalf("building type = %q, want city_core", got)
	}
	status, reason := domain.BuildingLifecycleStateAtTurn(centerEntry, state.Turn)
	if status != domain.BuildingStatusDisabled || reason != "pending_activation" {
		t.Fatalf("city core lifecycle = (%q,%q), want pending activation disabled", status, reason)
	}
	city := state.EnsureCityState("player-1", "B1")
	if city == nil || city.OnlineOnTurn != 4 {
		t.Fatalf("city state = %#v, want online_on_turn=4", city)
	}
	if _, ok := testFindUnitEntryByID(world, "settler-1"); ok {
		t.Fatalf("settler-1 should be removed after founding city")
	}
}

func testFindUnitEntryByID(world donburi.World, unitID string) (*donburi.Entry, bool) {
	var found *donburi.Entry
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found != nil || entry == nil {
			return
		}
		if ecs.UnitStatsC.Get(entry).ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}
