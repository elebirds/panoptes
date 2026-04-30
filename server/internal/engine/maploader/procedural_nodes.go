// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Procedural runtime node materialization helpers.

package maploader

import "github.com/elebirds/panoptes/internal/staticdata"

func buildRuntimeNodes(grid [][]string, width, height int, baseNodesByPos map[mapPoint]staticdata.MapRuntimeNode, resourceByPos map[mapPoint]string) []staticdata.MapRuntimeNode {
	nodes := make([]staticdata.MapRuntimeNode, 0, width*height)
	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			p := mapPoint{X: x, Y: y}
			sourceNode, hasSourceNode := baseNodesByPos[p]
			resourceType := ""
			isResource := false
			if rt, ok := resourceByPos[p]; ok {
				isResource = true
				resourceType = rt
			}

			nodeID := makeNodeID(x, y)
			nodeName := ""
			if hasSourceNode {
				if sourceNode.ID != "" {
					nodeID = sourceNode.ID
				}
				nodeName = sourceNode.NodeName
			}

			nodes = append(nodes, staticdata.MapRuntimeNode{
				ID:              nodeID,
				X:               x,
				Y:               y,
				Terrain:         grid[y][x],
				HasRoad:         false,
				IsResourcePoint: isResource,
				ResourceType:    resourceType,
				NodeName:        nodeName,
				Owner:           "",
				TerritoryOwner:  "",
				BuildingType:    "",
				BuildingHP:      0,
			})
		}
	}
	return nodes
}

func indexBaseNodesByPos(nodes []staticdata.MapRuntimeNode) map[mapPoint]staticdata.MapRuntimeNode {
	index := make(map[mapPoint]staticdata.MapRuntimeNode, len(nodes))
	for _, node := range nodes {
		index[mapPoint{X: node.X, Y: node.Y}] = node
	}
	return index
}

func cloneNamedNodes(src map[string]string) map[string]string {
	if len(src) == 0 {
		return map[string]string{}
	}
	dst := make(map[string]string, len(src))
	for key, value := range src {
		dst[key] = value
	}
	return dst
}

func makeNodeID(x, y int) string {
	return "N_" + itoa(x) + "_" + itoa(y)
}
