// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载后端测试/调试场景的构造与布置辅助逻辑。

package scenario

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/staticdata"
)

func cityBuildMap(id string) *staticdata.MapRuntimeBundle {
	zero := 0
	return &staticdata.MapRuntimeBundle{
		ID:     id,
		Name:   id,
		Width:  3,
		Height: 3,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 0, Y: 0},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "A1", X: 0, Y: 0, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero, BuildingType: "city_core"},
			{ID: "A2", X: 0, Y: 1, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero},
			{ID: "B1", X: 1, Y: 0, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero},
			{ID: "B2", X: 1, Y: 1, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero},
		},
		NamedNodes: map[string]string{
			"A1": "主城",
			"A2": "空地",
		},
	}
}

func expansionMap(id string) *staticdata.MapRuntimeBundle {
	return &staticdata.MapRuntimeBundle{
		ID:     id,
		Name:   id,
		Width:  5,
		Height: 5,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 2, Y: 2},
		},
		Nodes: allPlainNodes(5, 5),
		NamedNodes: map[string]string{
			"C3": "建城点",
		},
	}
}

func contestedFacilityMap(id string) *staticdata.MapRuntimeBundle {
	zero := 0
	one := 1
	return &staticdata.MapRuntimeBundle{
		ID:     id,
		Name:   id,
		Width:  3,
		Height: 3,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 0, Y: 0},
			{Slot: 1, X: 2, Y: 2},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "A1", X: 0, Y: 0, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero, BuildingType: "city_core"},
			{ID: "B2", X: 1, Y: 1, Terrain: "plain", IsResourcePoint: true, ResourceType: "food", TerritoryOwnerSlot: &one},
			{ID: "C3", X: 2, Y: 2, Terrain: "plain", OwnerSlot: &one, TerritoryOwnerSlot: &one, BuildingType: "city_core"},
		},
		NamedNodes: map[string]string{
			"B2": "争议农田",
		},
	}
}

func capitalSiegeMap(id string) *staticdata.MapRuntimeBundle {
	zero := 0
	one := 1
	return &staticdata.MapRuntimeBundle{
		ID:     id,
		Name:   id,
		Width:  2,
		Height: 2,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 0, Y: 0},
			{Slot: 1, X: 1, Y: 0},
		},
		Nodes: []staticdata.MapRuntimeNode{
			{ID: "A1", X: 0, Y: 0, Terrain: "plain", OwnerSlot: &zero, TerritoryOwnerSlot: &zero, BuildingType: "city_core"},
			{ID: "B1", X: 1, Y: 0, Terrain: "plain", OwnerSlot: &one, TerritoryOwnerSlot: &one},
		},
		NamedNodes: map[string]string{
			"A1": "主城",
			"B1": "前线",
		},
	}
}

func allPlainNodes(width int, height int) []staticdata.MapRuntimeNode {
	nodes := make([]staticdata.MapRuntimeNode, 0, width*height)
	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			nodes = append(nodes, staticdata.MapRuntimeNode{
				ID:      nodeID(x, y),
				X:       x,
				Y:       y,
				Terrain: "plain",
			})
		}
	}
	return nodes
}

func nodeID(x int, y int) string {
	return string(rune('A'+x)) + fmt.Sprintf("%d", y+1)
}
