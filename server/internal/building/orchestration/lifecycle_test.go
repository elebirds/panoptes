package orchestration

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestLifecycleSystemCapturesNonCapitalCityWithoutGameOver(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:               3,
			CityCoreMaxHP:               100,
			BaseResearchOutputPerTurn:   1,
			BaseIndustryOutputPerTurn:   2,
			InitialCityTerritoryRadius:  1,
		},
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

	events := (&LifecycleSystem{}).Run(world, state)
	if !hasEventKind(events, "city_captured") {
		t.Fatalf("events should include city_captured: %#v", events)
	}
	applyEvents(world, state, events)

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
}

func TestLifecycleSystemCompletesFacilityTakeoverAfterConsecutiveControl(t *testing.T) {
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

	firstTurnEvents := (&LifecycleSystem{}).Run(world, state)
	if !hasEventKind(firstTurnEvents, "facility_takeover_progressed") {
		t.Fatalf("first turn should include facility_takeover_progressed: %#v", firstTurnEvents)
	}
	applyEvents(world, state, firstTurnEvents)
	if got := ecs.BuildingC.Get(farmEntry).Owner; got != "player-1" {
		t.Fatalf("farm owner after first turn = %q, want player-1", got)
	}
	state.Turn++

	secondTurnEvents := (&LifecycleSystem{}).Run(world, state)
	if !hasEventKind(secondTurnEvents, "facility_takeover_completed") {
		t.Fatalf("second turn should include facility_takeover_completed: %#v", secondTurnEvents)
	}
	applyEvents(world, state, secondTurnEvents)
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

func applyEvents(world donburi.World, state *domain.GameState, events []event.Event) {
	for _, evt := range events {
		if evt == nil {
			continue
		}
		evt.Apply(world, state)
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
