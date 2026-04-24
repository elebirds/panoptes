// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义领域模型的地图节点模型。

package domain

import (
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
	"github.com/yohamta/donburi/filter"
)

var (
	nodeQuery = donburi.NewQuery(filter.Contains(PositionC, NodeC))
	unitQuery = donburi.NewQuery(filter.Contains(PositionC, UnitStatsC))
)

func IsContested(world donburi.World, nodeEntry *donburi.Entry) bool {
	if nodeEntry == nil {
		return false
	}
	nodePos := PositionC.Get(nodeEntry)
	pos := Position{Q: nodePos.Q, R: nodePos.R}
	factions := UnitsByFactionAtNode(world, pos)
	return len(factions) > 1
}

func GetOwner(nodeEntry *donburi.Entry) string {
	if nodeEntry == nil {
		return ""
	}
	return NodeC.Get(nodeEntry).Owner
}

func HasBuilding(nodeEntry *donburi.Entry) bool {
	return nodeEntry != nil && nodeEntry.HasComponent(BuildingC)
}

func HasRoad(nodeEntry *donburi.Entry) bool {
	if nodeEntry == nil {
		return false
	}
	return NodeC.Get(nodeEntry).HasRoad
}

func GetNodeAt(world donburi.World, pos Position) (*donburi.Entry, bool) {
	var result *donburi.Entry
	nodeQuery.Each(world, func(entry *donburi.Entry) {
		if result != nil {
			return
		}
		p := PositionC.Get(entry)
		if p.Q == pos.Q && p.R == pos.R {
			result = entry
		}
	})
	return result, result != nil
}

func GetUnitsAtNode(world donburi.World, nodeID string) []*donburi.Entry {
	nodeEntry, ok := findNodeByID(world, nodeID)
	if !ok {
		return nil
	}
	nodePos := PositionC.Get(nodeEntry)
	pos := Position{Q: nodePos.Q, R: nodePos.R}
	return GetUnitsByNode(world, pos)
}

func GetNodesByOwner(world donburi.World, ownerID string) []*donburi.Entry {
	nodes := make([]*donburi.Entry, 0)
	nodeQuery.Each(world, func(entry *donburi.Entry) {
		if NodeC.Get(entry).Owner == ownerID {
			nodes = append(nodes, entry)
		}
	})
	return nodes
}

func IsInSafeZone(state *GameState, pos Position, ownerID string) bool {
	if state == nil || state.Map == nil {
		return false
	}
	spawnPos, ok := state.Map.PlayerSpawns[ownerID]
	if !ok {
		return false
	}
	return pos.DistanceTo(spawnPos) <= staticdata.Default().Rules().SafeZoneRadius
}

func findNodeByID(world donburi.World, nodeID string) (*donburi.Entry, bool) {
	var result *donburi.Entry
	nodeQuery.Each(world, func(entry *donburi.Entry) {
		if result != nil {
			return
		}
		if NodeC.Get(entry).ID == nodeID {
			result = entry
		}
	})
	return result, result != nil
}
