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
	pos := Position{X: PositionC.Get(nodeEntry).X, Y: PositionC.Get(nodeEntry).Y}
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
		if p.X == pos.X && p.Y == pos.Y {
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
	pos := Position{X: PositionC.Get(nodeEntry).X, Y: PositionC.Get(nodeEntry).Y}
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

func IsInSafeZone(world donburi.World, pos Position, ownerID string) bool {
	nodes := GetNodesByOwner(world, ownerID)
	if len(nodes) == 0 {
		return false
	}
	castlePos := Position{X: PositionC.Get(nodes[0]).X, Y: PositionC.Get(nodes[0]).Y}
	return pos.DistanceTo(castlePos) <= staticdata.Default().Rules().SafeZoneRadius
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
