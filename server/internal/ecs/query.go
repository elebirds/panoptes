// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现ECS 适配层的查询辅助。

package ecs

import (
	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
	"github.com/yohamta/donburi/filter"
)

func AllNodes(world donburi.World) *donburi.Query {
	return newAllNodesQuery()
}

func AllUnits(world donburi.World) *donburi.Query {
	return newAllUnitsQuery()
}

func UnitsWithMoveIntent(world donburi.World) *donburi.Query {
	return donburi.NewQuery(filter.Contains(PositionC, UnitStatsC, MoveIntentC))
}

func SiegeUnits(world donburi.World) *donburi.Query {
	return donburi.NewQuery(filter.Contains(PositionC, UnitStatsC, SiegeAbilityC))
}

func DestroyUnits(world donburi.World) *donburi.Query {
	return donburi.NewQuery(filter.Contains(PositionC, UnitStatsC, DestroyAbilityC))
}

func RangedUnits(world donburi.World) *donburi.Query {
	return donburi.NewQuery(filter.Contains(PositionC, UnitStatsC, RangedAbilityC))
}

func NodesWithBuilding(world donburi.World) *donburi.Query {
	return donburi.NewQuery(filter.Contains(PositionC, NodeC, BuildingC))
}

func PoisonedUnits(world donburi.World) *donburi.Query {
	return donburi.NewQuery(filter.Contains(PositionC, UnitStatsC, PoisonEffectC))
}

func StarvingUnits(world donburi.World) *donburi.Query {
	return donburi.NewQuery(filter.Contains(PositionC, UnitStatsC, StarvingC))
}

// Donburi Query 会在对象内部缓存 archetype 匹配结果；服务端可能并发运行多个
// world，因此这里按调用创建查询，避免包级 Query 在并发读不同 world 时写同一份缓存。
func newAllNodesQuery() *donburi.Query {
	return donburi.NewQuery(filter.Contains(PositionC, NodeC))
}

func newAllUnitsQuery() *donburi.Query {
	return donburi.NewQuery(filter.Contains(PositionC, UnitStatsC))
}

func FindNodeByID(world donburi.World, nodeID string) (*donburi.Entry, bool) {
	var result *donburi.Entry
	newAllNodesQuery().Each(world, func(entry *donburi.Entry) {
		if result != nil {
			return
		}
		if NodeC.Get(entry).ID == nodeID {
			result = entry
		}
	})
	return result, result != nil
}

func ResolveCityID(entry *donburi.Entry) string {
	return building.ResolveCityID(entry)
}

func ResolveServiceCityID(entry *donburi.Entry) string {
	return building.ResolveServiceCityID(entry)
}

func BuildingRuntimeState(entry *donburi.Entry, currentTurn int) (string, int, int) {
	return building.RuntimeState(entry, currentTurn)
}

func ValidateBuildingPlacement(state *domain.GameState, nodeEntry *donburi.Entry, playerID string, cfg staticdata.BuildingDefinition, cityID string) string {
	return building.ValidatePlacement(state, nodeEntry, playerID, cfg, cityID)
}

func ValidateBuildingNodePlacement(state *domain.GameState, entry *donburi.Entry, playerID string, cfg staticdata.BuildingDefinition) string {
	return building.ValidateNodePlacement(state, entry, playerID, cfg)
}

func ResolveCityContext(state *domain.GameState, playerID string, cityID string) (*donburi.Entry, *domain.CityState, string) {
	return building.ResolveCityContext(state, playerID, cityID)
}

// Deprecated: use ValidateBuildingPlacement for command placement checks or
// ValidateBuildingNodePlacement for the narrow node-only preflight.
func CanPlaceBuildingAt(entry *donburi.Entry, playerID string, cfg staticdata.BuildingDefinition) string {
	return ValidateBuildingNodePlacement(nil, entry, playerID, cfg)
}

func TerritoryFootprint(state *domain.GameState, centerEntry *donburi.Entry) ([]*donburi.Entry, []string, string) {
	return building.TerritoryFootprint(state, centerEntry)
}

func CanFoundCityAt(state *domain.GameState, centerEntry *donburi.Entry) (bool, string) {
	return building.CanFoundCityAt(state, centerEntry)
}
