package event

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestTechnologyActivatedEventApplyRefreshesExistingBuildingMaxHP(t *testing.T) {
	world := donburi.NewWorld()
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "wall", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 120, TakeoverMode: "city_capture"},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID: "fortifications",
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "building.max_hp", TargetID: "city_core", ModifierType: "flat", Value: 30},
					{Trigger: "building.max_hp", TargetID: "wall", ModifierType: "flat", Value: 30},
				},
			},
		},
	}))

	nodeIndex := map[string]donburi.Entity{}
	createNode := func(id string, x int, y int) *donburi.Entry {
		entity := ecs.CreateNode(world, ecs.MapNode{ID: id, X: x, Y: y, Terrain: "plain"})
		nodeIndex[id] = entity
		entry := world.Entry(entity)
		node := ecs.NodeC.Get(entry)
		node.Owner = "player-1"
		node.TerritoryOwner = "player-1"
		return entry
	}

	cityEntry := createNode("C1", 0, 0)
	wallEntry := createNode("A1", 1, 0)
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", cityEntry)
	ecs.CreateBuilding(world, "wall", "player-1", "C1", wallEntry)

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.EnsureCityState("player-1", "C1")
	state.Players["player-1"].CapitalCityID = "C1"

	TechnologyActivatedEvent{
		PlayerID:     "player-1",
		TechnologyID: "fortifications",
	}.Apply(world, state)

	cityBuilding := ecs.BuildingC.Get(cityEntry)
	if cityBuilding.MaxHP != 130 || cityBuilding.HP != 130 {
		t.Fatalf("city core hp = %d/%d, want 130/130", cityBuilding.HP, cityBuilding.MaxHP)
	}
	wallBuilding := ecs.BuildingC.Get(wallEntry)
	if wallBuilding.MaxHP != 150 || wallBuilding.HP != 150 {
		t.Fatalf("wall hp = %d/%d, want 150/150", wallBuilding.HP, wallBuilding.MaxHP)
	}
}

func TestBuildingBuiltEventApplyUsesActiveBuildingMaxHPModifiers(t *testing.T) {
	world := donburi.NewWorld()
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "wall", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 120, TakeoverMode: "city_capture"},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID: "fortifications",
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "building.max_hp", TargetID: "wall", ModifierType: "flat", Value: 30},
				},
			},
		},
	}))

	cityEntity := ecs.CreateNode(world, ecs.MapNode{ID: "C1", X: 0, Y: 0, Terrain: "plain"})
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", X: 1, Y: 0, Terrain: "plain"})
	cityEntry := world.Entry(cityEntity)
	nodeEntry := world.Entry(nodeEntity)
	for _, entry := range []*donburi.Entry{cityEntry, nodeEntry} {
		node := ecs.NodeC.Get(entry)
		node.Owner = "player-1"
		node.TerritoryOwner = "player-1"
	}
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", cityEntry)

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"C1": cityEntity, "A1": nodeEntity},
	})
	state.World = world
	state.EnsureCityState("player-1", "C1")
	state.Players["player-1"].CapitalCityID = "C1"
	state.Players["player-1"].Research.MarkTechnologyActive("fortifications", 1)

	BuildingBuiltEvent{
		NodeID:       "A1",
		BuildingType: "wall",
		Owner:        "player-1",
		CityID:       "C1",
	}.Apply(world, state)

	building := ecs.BuildingC.Get(nodeEntry)
	if building.MaxHP != 150 || building.HP != 150 {
		t.Fatalf("new wall hp = %d/%d, want 150/150", building.HP, building.MaxHP)
	}
}
