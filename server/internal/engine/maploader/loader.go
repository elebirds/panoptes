package maploader

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func LoadMap(catalog *staticdata.Catalog, mapID string) (*staticdata.MapRuntimeBundle, error) {
	if catalog == nil {
		return nil, fmt.Errorf("static catalog is nil")
	}
	mapFile, ok := catalog.GetMap(mapID)
	if !ok {
		return nil, fmt.Errorf("map %q not found", mapID)
	}
	return mapFile, nil
}

func InitWorldFromMap(world donburi.World, mapFile *staticdata.MapRuntimeBundle, playerIDs []string) *domain.MapData {
	mapData := &domain.MapData{
		ID:           mapFile.ID,
		Width:        mapFile.Width,
		Height:       mapFile.Height,
		SpawnPoints:  make(map[int]domain.Position, len(mapFile.SpawnPoints)),
		PlayerSpawns: make(map[string]domain.Position, len(mapFile.SpawnPoints)),
		NamedNodes:   make(map[string]string, len(mapFile.NamedNodes)),
		NodeIndex:    make(map[string]donburi.Entity, len(mapFile.Nodes)),
	}

	spawnOwners := make(map[domain.Position]string, len(mapFile.SpawnPoints))
	for _, spawn := range mapFile.SpawnPoints {
		pos := domain.Position{X: spawn.X, Y: spawn.Y}
		mapData.SpawnPoints[spawn.Slot] = pos
		if spawn.Slot < len(playerIDs) {
			spawnOwners[pos] = playerIDs[spawn.Slot]
			mapData.PlayerSpawns[playerIDs[spawn.Slot]] = pos
		}
	}
	for nodeID, name := range mapFile.NamedNodes {
		mapData.NamedNodes[nodeID] = name
	}

	for _, node := range mapFile.Nodes {
		entity := ecs.CreateNode(world, ecs.MapNode{
			ID:              node.ID,
			X:               node.X,
			Y:               node.Y,
			Terrain:         node.Terrain,
			IsResourcePoint: node.IsResourcePoint,
			ResourceType:    node.ResourceType,
		})
		entry := world.Entry(entity)
		ecs.NodeC.Get(entry).NodeName = node.NodeName
		pos := domain.Position{X: node.X, Y: node.Y}
		owner := resolveNodeOwner(node, pos, playerIDs, spawnOwners)
		ecs.NodeC.Get(entry).Owner = owner
		ecs.NodeC.Get(entry).HasRoad = node.HasRoad
		if node.BuildingType != "" {
			ecs.CreateBuilding(world, node.BuildingType, owner, entry)
			if node.BuildingHP > 0 {
				building := ecs.BuildingC.Get(entry)
				if node.BuildingHP < building.MaxHP {
					building.HP = node.BuildingHP
				}
			}
		}
		mapData.NodeIndex[node.ID] = entity
	}

	return mapData
}

func resolveNodeOwner(node staticdata.MapRuntimeNode, pos domain.Position, playerIDs []string, spawnOwners map[domain.Position]string) string {
	if node.OwnerSlot != nil {
		slot := *node.OwnerSlot
		if slot >= 0 && slot < len(playerIDs) {
			return playerIDs[slot]
		}
	}
	if node.Owner != "" {
		return node.Owner
	}
	return spawnOwners[pos]
}
