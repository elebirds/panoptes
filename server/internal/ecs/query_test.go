package ecs

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestResolveCityAndServiceBindings(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			FacilityTakeoverTurns: 3,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "barracks", BuildingScope: "in_city", MaxHP: 80, TakeoverMode: "city_capture"},
			{ID: "farm", BuildingScope: "out_of_city", MaxHP: 60, TakeoverMode: "delayed"},
		},
		Units: []staticdata.UnitDefinition{
			{ID: "settler", Class: "civilian", MaxHP: 12, MoveRange: 2},
			{ID: "infantry", Class: "military", MaxHP: 20, Attack: 5, MoveRange: 1},
		},
	}))

	world := donburi.NewWorld()
	cityEntry := world.Entry(CreateNode(world, MapNode{ID: "C1", X: 0, Y: 0, Terrain: "plain"}))
	barracksEntry := world.Entry(CreateNode(world, MapNode{ID: "C2", X: 1, Y: 0, Terrain: "plain"}))
	farmEntry := world.Entry(CreateNode(world, MapNode{ID: "F1", X: 2, Y: 0, Terrain: "plain", IsResourcePoint: true, ResourceType: "food"}))
	settlerEntry := world.Entry(CreateUnit(world, "settler", "player-1", domain.Position{X: 0, Y: 0}))
	infantryEntry := world.Entry(CreateUnit(world, "infantry", "player-1", domain.Position{X: 1, Y: 0}))

	CreateBuilding(world, "city_core", "player-1", "C1", cityEntry)
	CreateBuilding(world, "barracks", "player-1", "C1", barracksEntry)
	CreateBuilding(world, "farm", "player-1", "C1", farmEntry)

	if got := ResolveCityID(cityEntry); got != "C1" {
		t.Fatalf("city core city id = %q, want C1", got)
	}
	if got := ResolveServiceCityID(cityEntry); got != "C1" {
		t.Fatalf("city core service city id = %q, want C1", got)
	}
	if got := ResolveCityID(barracksEntry); got != "C1" {
		t.Fatalf("barracks city id = %q, want C1", got)
	}
	if got := ResolveServiceCityID(barracksEntry); got != "C1" {
		t.Fatalf("barracks service city id = %q, want C1", got)
	}
	if got := ResolveCityID(farmEntry); got != "C1" {
		t.Fatalf("farm city id = %q, want C1", got)
	}
	if got := ResolveServiceCityID(farmEntry); got != "C1" {
		t.Fatalf("farm service city id = %q, want C1", got)
	}
	if !cityEntry.HasComponent(CityCoreC) {
		t.Fatalf("city core missing CityCoreC")
	}
	if !farmEntry.HasComponent(FacilityBindingC) {
		t.Fatalf("farm missing FacilityBindingC")
	}
	if !farmEntry.HasComponent(FacilityTakeoverC) {
		t.Fatalf("farm missing FacilityTakeoverC")
	}
	if !settlerEntry.HasComponent(UnitCategoryC) {
		t.Fatalf("settler missing UnitCategoryC")
	}
	if !infantryEntry.HasComponent(UnitCategoryC) {
		t.Fatalf("infantry missing UnitCategoryC")
	}
}

func TestBuildingRuntimeStateAndPlacementHelpers(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			FacilityTakeoverTurns:      4,
			InitialCityTerritoryRadius: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "farm", PlacementKind: "resource_node", BuildingScope: "out_of_city", RequiredResourceType: "food", MaxHP: 60, TakeoverMode: "delayed"},
			{ID: "barracks", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 80, TakeoverMode: "city_capture"},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "farm_food", BuildingID: "farm", WorkAmount: 3, BaseProgress: 1},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{ID: "default", Width: 3, Height: 3, NodeIndex: map[string]donburi.Entity{}}
	for y := 0; y < 3; y++ {
		for x := 0; x < 3; x++ {
			nodeID := string(rune('A'+y)) + string(rune('0'+x))
			entity := CreateNode(world, MapNode{ID: nodeID, X: x, Y: y, Terrain: "plain"})
			mapData.NodeIndex[nodeID] = entity
		}
	}
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, mapData)
	state.World = world

	cityEntry := world.Entry(mapData.NodeIndex["B1"])
	centerNode := NodeC.Get(cityEntry)
	centerNode.Owner = "player-1"
	centerNode.TerritoryOwner = "player-1"
	CreateBuilding(world, "barracks", "player-1", "B1", cityEntry)
	cityEntry.AddComponent(BuildingStateC)
	BuildingStateC.SetValue(cityEntry, BuildingStateComp{Disabled: true, DisabledReason: "outside_territory"})

	farmEntry := world.Entry(mapData.NodeIndex["B2"])
	farmNode := NodeC.Get(farmEntry)
	farmNode.Owner = "player-1"
	farmNode.TerritoryOwner = "player-1"
	farmNode.IsResource = true
	farmNode.ResourceType = "food"
	CreateBuilding(world, "farm", "player-1", "B1", farmEntry)
	farmEntry.AddComponent(BuildingOperationC)
	BuildingOperationC.SetValue(farmEntry, BuildingOperationComp{
		SelectedRecipeID: "farm_food",
		ProgressTurns:    1,
		RequiredTurns:    3,
		BlockedReason:    "insufficient_resources",
	})

	status, progress, required := BuildingRuntimeState(cityEntry, 1)
	if status != "disabled" || progress != 0 || required != 4 {
		t.Fatalf("city runtime = (%q,%d,%d), want (disabled,0,4)", status, progress, required)
	}
	status, progress, required = BuildingRuntimeState(farmEntry, 1)
	if status != "blocked" || progress != 0 || required != 4 {
		t.Fatalf("farm runtime = (%q,%d,%d), want (blocked,0,4)", status, progress, required)
	}

	cfg, _ := staticdata.Default().GetBuilding("farm")
	if got := CanPlaceBuildingAt(farmEntry, "player-1", cfg); got != "" {
		t.Fatalf("CanPlaceBuildingAt(food farm) = %q, want empty", got)
	}
	if got := CanPlaceBuildingAt(cityEntry, "player-1", cfg); got != "resource_only_required" {
		t.Fatalf("CanPlaceBuildingAt(non-resource farm) = %q, want resource_only_required", got)
	}
	if canFound, reason := CanFoundCityAt(state, cityEntry); canFound || reason != "territory_blocked" {
		t.Fatalf("CanFoundCityAt(occupied city) = (%v,%q), want (false,territory_blocked)", canFound, reason)
	}
}

func TestCanFoundCityAtRejectsCentersWithinMinimumCityDistance(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			FacilityTakeoverTurns:      2,
			InitialCityTerritoryRadius: 1,
			MinimumCityDistance:        4,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{ID: "default", Width: 9, Height: 9, NodeIndex: map[string]donburi.Entity{}}
	for y := 0; y < 9; y++ {
		for x := 0; x < 9; x++ {
			nodeID := string(rune('A'+x)) + string(rune('1'+y))
			entity := CreateNode(world, MapNode{ID: nodeID, X: x, Y: y, Terrain: "plain"})
			mapData.NodeIndex[nodeID] = entity
		}
	}

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, mapData)
	state.World = world

	existingCore := world.Entry(mapData.NodeIndex["D4"])
	existingNode := NodeC.Get(existingCore)
	existingNode.Owner = "player-1"
	existingNode.TerritoryOwner = "player-1"
	CreateBuilding(world, "city_core", "player-1", "D4", existingCore)
	state.EnsureCityState("player-1", "D4")

	target := world.Entry(mapData.NodeIndex["D7"])
	if canFound, reason := CanFoundCityAt(state, target); canFound || reason != "minimum_city_distance" {
		t.Fatalf("CanFoundCityAt(too close) = (%v,%q), want (false,minimum_city_distance)", canFound, reason)
	}
}
