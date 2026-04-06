package ecs

import (
	"github.com/yohamta/donburi"
	"github.com/yohamta/donburi/filter"
)

var (
	allNodesQuery          = donburi.NewQuery(filter.Contains(PositionC, NodeC))
	allUnitsQuery          = donburi.NewQuery(filter.Contains(PositionC, UnitStatsC))
	unitsWithMoveQuery     = donburi.NewQuery(filter.Contains(PositionC, UnitStatsC, MoveIntentC))
	siegeUnitsQuery        = donburi.NewQuery(filter.Contains(PositionC, UnitStatsC, SiegeAbilityC))
	destroyUnitsQuery      = donburi.NewQuery(filter.Contains(PositionC, UnitStatsC, DestroyAbilityC))
	rangedUnitsQuery       = donburi.NewQuery(filter.Contains(PositionC, UnitStatsC, RangedAbilityC))
	nodesWithBuildingQuery = donburi.NewQuery(filter.Contains(PositionC, NodeC, BuildingC))
	poisonedUnitsQuery     = donburi.NewQuery(filter.Contains(PositionC, UnitStatsC, PoisonEffectC))
	starvingUnitsQuery     = donburi.NewQuery(filter.Contains(PositionC, UnitStatsC, StarvingC))
)

func AllNodes(world donburi.World) *donburi.Query {
	return allNodesQuery
}

func AllUnits(world donburi.World) *donburi.Query {
	return allUnitsQuery
}

func UnitsWithMoveIntent(world donburi.World) *donburi.Query {
	return unitsWithMoveQuery
}

func SiegeUnits(world donburi.World) *donburi.Query {
	return siegeUnitsQuery
}

func DestroyUnits(world donburi.World) *donburi.Query {
	return destroyUnitsQuery
}

func RangedUnits(world donburi.World) *donburi.Query {
	return rangedUnitsQuery
}

func NodesWithBuilding(world donburi.World) *donburi.Query {
	return nodesWithBuildingQuery
}

func PoisonedUnits(world donburi.World) *donburi.Query {
	return poisonedUnitsQuery
}

func StarvingUnits(world donburi.World) *donburi.Query {
	return starvingUnitsQuery
}

func FindNodeByID(world donburi.World, nodeID string) (*donburi.Entry, bool) {
	var result *donburi.Entry
	allNodesQuery.Each(world, func(entry *donburi.Entry) {
		if result != nil {
			return
		}
		if NodeC.Get(entry).ID == nodeID {
			result = entry
		}
	})
	return result, result != nil
}
