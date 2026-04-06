package maploader

import (
	"encoding/json"
	"fmt"
	"os"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

type MapNode = ecs.MapNode

type SpawnPoint struct {
	PlayerIndex int `json:"player_index"`
	X           int `json:"x"`
	Y           int `json:"y"`
}

type MapFile struct {
	ID            string            `json:"id"`
	Name          string            `json:"name"`
	Width         int               `json:"width"`
	Height        int               `json:"height"`
	SpawnPoints   []SpawnPoint      `json:"spawn_points"`
	Nodes         []MapNode         `json:"nodes"`
	CentralPoints []string          `json:"central_points"`
	NamedNodes    map[string]string `json:"named_nodes"`
}

func LoadMap(path string) (*MapFile, error) {
	raw, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("read map: %w", err)
	}

	var file MapFile
	if err := json.Unmarshal(raw, &file); err != nil {
		return nil, fmt.Errorf("unmarshal map: %w", err)
	}
	if file.NamedNodes == nil {
		file.NamedNodes = map[string]string{}
	}
	return &file, nil
}

func InitWorldFromMap(world donburi.World, mapFile *MapFile, playerIDs []string) *domain.MapData {
	mapData := &domain.MapData{
		ID:          mapFile.ID,
		Width:       mapFile.Width,
		Height:      mapFile.Height,
		SpawnPoints: make(map[int]domain.Position, len(mapFile.SpawnPoints)),
		NamedNodes:  make(map[string]string, len(mapFile.NamedNodes)),
		NodeIndex:   make(map[string]donburi.Entity, len(mapFile.Nodes)),
	}

	spawnOwners := make(map[domain.Position]string, len(mapFile.SpawnPoints))
	for _, spawn := range mapFile.SpawnPoints {
		pos := domain.Position{X: spawn.X, Y: spawn.Y}
		mapData.SpawnPoints[spawn.PlayerIndex] = pos
		if spawn.PlayerIndex < len(playerIDs) {
			spawnOwners[pos] = playerIDs[spawn.PlayerIndex]
		}
	}
	for nodeID, name := range mapFile.NamedNodes {
		mapData.NamedNodes[nodeID] = name
	}

	for _, node := range mapFile.Nodes {
		entity := ecs.CreateNode(world, node)
		entry := world.Entry(entity)
		if name, ok := mapData.NamedNodes[node.ID]; ok {
			ecs.NodeC.Get(entry).NodeName = name
		}
		pos := domain.Position{X: node.X, Y: node.Y}
		if owner, ok := spawnOwners[pos]; ok {
			ecs.NodeC.Get(entry).Owner = owner
		}
		mapData.NodeIndex[node.ID] = entity
	}

	return mapData
}
