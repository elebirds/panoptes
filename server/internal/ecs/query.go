// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现ECS 适配层的查询辅助。

package ecs

import (
	"strings"

	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/staticdata"
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

func ResolveCityContext(state *domain.GameState, playerID string, cityID string) (*donburi.Entry, *domain.CityState, string) {
	return building.ResolveCityContext(state, playerID, cityID)
}

func CanPlaceBuildingAt(entry *donburi.Entry, playerID string, cfg staticdata.BuildingDefinition) string {
	if entry == nil {
		return "invalid_target"
	}
	node := NodeC.Get(entry)
	terrainID := normalizeRuntimeToken(string(node.Terrain))
	if terrainID != "" {
		if terrain, ok := staticdata.Default().GetTerrain(terrainID); ok && !terrain.Buildable {
			return "terrain_not_buildable"
		}
	}
	switch normalizeRuntimeToken(cfg.PlacementKind) {
	case "city_territory":
		player := normalizeRuntimeToken(playerID)
		if normalizeRuntimeToken(node.TerritoryOwner) != player && normalizeRuntimeToken(node.Owner) != player {
			return "outside_territory"
		}
	case "resource_node":
		if !node.IsResource {
			return "resource_only_required"
		}
		required := normalizeRuntimeToken(cfg.RequiredResourceType)
		if required != "" && normalizeRuntimeToken(node.ResourceType) != required {
			return "resource_type_mismatch"
		}
	}
	return ""
}

func TerritoryFootprint(state *domain.GameState, centerEntry *donburi.Entry) ([]*donburi.Entry, []string, string) {
	return building.TerritoryFootprint(state, centerEntry)
}

func CanFoundCityAt(state *domain.GameState, centerEntry *donburi.Entry) (bool, string) {
	return building.CanFoundCityAt(state, centerEntry)
}

func normalizeRuntimeToken(value string) string {
	return strings.ToLower(strings.TrimSpace(value))
}
