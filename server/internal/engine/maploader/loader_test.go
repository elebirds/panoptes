package maploader

import (
	"os"
	"path/filepath"
	"testing"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

func TestLoadMapAndInitWorldFromMap(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "default.json")
	if err := os.WriteFile(path, []byte(`{
  "id": "default",
  "width": 20,
  "height": 20,
  "spawn_points": [
    { "player_index": 0, "x": 2, "y": 10 },
    { "player_index": 1, "x": 17, "y": 10 }
  ],
  "nodes": [
    { "id": "A1", "x": 0, "y": 0, "terrain": "mountain", "is_resource_point": false },
    { "id": "K10", "x": 10, "y": 9, "terrain": "plain", "is_resource_point": true, "resource_type": "food" }
  ],
  "central_points": ["K10"],
  "named_nodes": { "K10": "龙脊" }
}`), 0o600); err != nil {
		t.Fatalf("WriteFile() error = %v", err)
	}

	mapFile, err := LoadMap(path)
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
	config.Data = config.GameData{
		Rules: config.RulesConfig{SafeZoneRadius: 4},
	}

	world := donburi.NewWorld()
	mapFile := &MapFile{
		ID:     "default",
		Width:  20,
		Height: 20,
		SpawnPoints: []SpawnPoint{
			{PlayerIndex: 0, X: 2, Y: 10},
		},
		Nodes: []MapNode{
			{ID: "castle", X: 2, Y: 10, Terrain: "plain"},
		},
	}

	mapData := InitWorldFromMap(world, mapFile, []string{"player-1"})
	if mapData.SpawnPoints[0].X != 2 || mapData.SpawnPoints[0].Y != 10 {
		t.Fatalf("spawn = %#v", mapData.SpawnPoints[0])
	}
}
