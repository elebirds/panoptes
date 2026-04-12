package maploader

import (
	"testing"

	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestLoadMapAndInitWorldFromMap(t *testing.T) {
	catalog := staticdata.NewCatalog(staticdata.CatalogBundle{}, &staticdata.MapRuntimeBundle{
		ID:     "default",
		Name:   "默认地图",
		Width:  20,
		Height: 20,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 2, Y: 10},
			{Slot: 1, X: 17, Y: 10},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "A1", X: 0, Y: 0, Terrain: "mountain"},
			{ID: "K10", X: 10, Y: 9, Terrain: "plain", IsResourcePoint: true, ResourceType: "food", NodeName: "龙脊"},
		},
		NamedNodes:    map[string]string{"K10": "龙脊"},
		CentralPoints: []string{"K10"},
	})

	mapFile, err := LoadMap(catalog, "default")
	if err != nil {
		t.Fatalf("LoadMap() error = %v", err)
	}
	if mapFile.ID != "default" || len(mapFile.Nodes) != 2 {
		t.Fatalf("mapFile = %#v", mapFile)
	}

	world := donburi.NewWorld()
	mapData := InitWorldFromMap(world, mapFile, []string{"player-1", "player-2"})
	if mapData.Width != 20 || mapData.Height != 20 {
		t.Fatalf("MapData size = %dx%d", mapData.Width, mapData.Height)
	}
	if mapData.SpawnPoints[0].X != 2 || mapData.SpawnPoints[1].X != 17 {
		t.Fatalf("SpawnPoints = %#v", mapData.SpawnPoints)
	}
	if mapData.NamedNodes["K10"] != "龙脊" {
		t.Fatalf("NamedNodes = %#v", mapData.NamedNodes)
	}
	if len(mapData.NodeIndex) != 2 {
		t.Fatalf("NodeIndex len = %d", len(mapData.NodeIndex))
	}

	entry := world.Entry(mapData.NodeIndex["K10"])
	node := ecs.NodeC.Get(entry)
	if node.NodeName != "龙脊" {
		t.Fatalf("NodeName = %q", node.NodeName)
	}
}

func TestInitWorldFromMapSetsSafeZoneReadyData(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{SafeZoneRadius: 4},
	}))

	world := donburi.NewWorld()
	mapFile := &staticdata.MapRuntimeBundle{
		ID:     "default",
		Width:  20,
		Height: 20,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 2, Y: 10},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "castle", X: 2, Y: 10, Terrain: "plain"},
		},
	}

	mapData := InitWorldFromMap(world, mapFile, []string{"player-1"})
	if mapData.SpawnPoints[0].X != 2 || mapData.SpawnPoints[0].Y != 10 {
		t.Fatalf("spawn = %#v", mapData.SpawnPoints[0])
	}
}

func TestInitWorldFromMapCreatesPrebuiltStructures(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "barracks", Category: "military_production", Combat: staticdata.BuildingCombat{MaxHP: 120}},
		},
	}))

	world := donburi.NewWorld()
	mapFile := &staticdata.MapRuntimeBundle{
		ID:     "legacy",
		Width:  4,
		Height: 4,
		Nodes: []staticdata.MapRuntimeNode{
			{
				ID:           "B2",
				X:            1,
				Y:            1,
				Terrain:      "plain",
				HasRoad:      true,
				Owner:        "green",
				BuildingType: "barracks",
				BuildingHP:   90,
			},
		},
		NamedNodes: map[string]string{},
	}

	mapData := InitWorldFromMap(world, mapFile, nil)
	entry := world.Entry(mapData.NodeIndex["B2"])

	if !entry.HasComponent(ecs.BuildingC) {
		t.Fatalf("node should have BuildingComp")
	}

	building := ecs.BuildingC.Get(entry)
	if building.Type != "barracks" || building.HP != 90 {
		t.Fatalf("building = %#v", building)
	}

	node := ecs.NodeC.Get(entry)
	if !node.HasRoad || node.Owner != "green" {
		t.Fatalf("node = %#v", node)
	}
}

func TestInitWorldFromMapResolvesOwnerSlotToPlayerID(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "farm", Category: "production", Combat: staticdata.BuildingCombat{MaxHP: 80}},
		},
	}))

	world := donburi.NewWorld()
	mapFile := &staticdata.MapRuntimeBundle{
		ID:     "legacy",
		Width:  4,
		Height: 4,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 0, Y: 0},
			{Slot: 1, X: 2, Y: 2},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{
				ID:           "C3",
				X:            2,
				Y:            2,
				Terrain:      "plain",
				Owner:        "yellow",
				OwnerSlot:    intPtr(1),
				BuildingType: "farm",
				BuildingHP:   100,
			},
		},
		NamedNodes: map[string]string{},
	}

	mapData := InitWorldFromMap(world, mapFile, []string{"player-1", "player-2"})
	entry := world.Entry(mapData.NodeIndex["C3"])

	node := ecs.NodeC.Get(entry)
	if node.Owner != "player-2" {
		t.Fatalf("resolved owner = %q", node.Owner)
	}
	if node.TerritoryOwner != "player-2" {
		t.Fatalf("resolved territory owner = %q", node.TerritoryOwner)
	}

	building := ecs.BuildingC.Get(entry)
	if building.Owner != "player-2" || building.HP != 80 || building.MaxHP != 80 {
		t.Fatalf("building = %#v", building)
	}
}

func TestInitWorldFromMapResolvesTerritoryOwnerSlotToPlayerID(t *testing.T) {
	world := donburi.NewWorld()
	mapFile := &staticdata.MapRuntimeBundle{
		ID:     "legacy",
		Width:  4,
		Height: 4,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 0, Y: 0},
			{Slot: 1, X: 2, Y: 2},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{
				ID:                 "D4",
				X:                  3,
				Y:                  3,
				Terrain:            "plain",
				Owner:              "yellow",
				OwnerSlot:          intPtr(1),
				TerritoryOwner:     "green",
				TerritoryOwnerSlot: intPtr(0),
			},
		},
		NamedNodes: map[string]string{},
	}

	mapData := InitWorldFromMap(world, mapFile, []string{"player-1", "player-2"})
	entry := world.Entry(mapData.NodeIndex["D4"])
	node := ecs.NodeC.Get(entry)

	if node.Owner != "player-2" {
		t.Fatalf("resolved owner = %q", node.Owner)
	}
	if node.TerritoryOwner != "player-1" {
		t.Fatalf("resolved territory owner = %q", node.TerritoryOwner)
	}
}

func intPtr(v int) *int {
	return &v
}
