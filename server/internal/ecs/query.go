// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现ECS 适配层的查询辅助。

package ecs

import (
	"strings"

	"github.com/elebirds/panoptes/internal/algo/geometry"
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
	if entry == nil || !entry.HasComponent(BuildingC) {
		return ""
	}
	if entry.HasComponent(CityCoreC) {
		if cityID := strings.TrimSpace(CityCoreC.Get(entry).CityID); cityID != "" {
			return cityID
		}
	}
	building := BuildingC.Get(entry)
	if cityID := strings.TrimSpace(building.CityID); cityID != "" {
		return cityID
	}
	if entry.HasComponent(ServiceCityC) {
		return strings.TrimSpace(ServiceCityC.Get(entry).CityID)
	}
	if entry.HasComponent(FacilityBindingC) {
		return strings.TrimSpace(FacilityBindingC.Get(entry).CityID)
	}
	return ""
}

func ResolveServiceCityID(entry *donburi.Entry) string {
	if entry == nil || !entry.HasComponent(BuildingC) {
		return ""
	}
	if entry.HasComponent(CityCoreC) {
		return strings.TrimSpace(CityCoreC.Get(entry).CityID)
	}
	if entry.HasComponent(ServiceCityC) {
		return strings.TrimSpace(ServiceCityC.Get(entry).CityID)
	}
	if entry.HasComponent(FacilityBindingC) {
		return strings.TrimSpace(FacilityBindingC.Get(entry).CityID)
	}
	return ""
}

func BuildingRuntimeState(entry *donburi.Entry, currentTurn int) (string, int, int) {
	if entry == nil || !entry.HasComponent(BuildingC) {
		return "empty", 0, 0
	}

	progress := 0
	required := 0
	if entry.HasComponent(FacilityTakeoverC) {
		takeover := FacilityTakeoverC.Get(entry)
		progress = takeover.Progress
		required = takeover.Required
	}

	status, _ := domain.BuildingLifecycleStateAtTurn(entry, currentTurn)
	switch status {
	case domain.BuildingStatusDisabled,
		domain.BuildingStatusContested,
		domain.BuildingStatusTakeover,
		domain.BuildingStatusRuined:
		return status, progress, required
	case domain.BuildingStatusBlocked:
		return domain.BuildingStatusBlocked, progress, required
	case domain.BuildingStatusActive:
		return domain.BuildingStatusActive, progress, required
	}

	if entry.HasComponent(BuildingOperationC) {
		operation := BuildingOperationC.Get(entry)
		if strings.TrimSpace(operation.BlockedReason) != "" {
			return "blocked", progress, required
		}
		if strings.TrimSpace(operation.SelectedRecipeID) != "" {
			return "active", progress, required
		}
	}
	return "idle", progress, required
}

func ValidateBuildingPlacement(state *domain.GameState, nodeEntry *donburi.Entry, playerID string, cfg staticdata.BuildingDefinition, cityID string) string {
	if state == nil || nodeEntry == nil {
		return "invalid_target"
	}
	scope := normalizeRuntimeToken(cfg.BuildingScope)
	if scope == "city_core" {
		return "invalid_directive"
	}
	cityID = strings.TrimSpace(cityID)
	if cityID == "" {
		return "invalid_request"
	}
	cityEntry, cityState, errCode := ResolveCityContext(state, playerID, cityID)
	if errCode != "" {
		return errCode
	}
	if !domain.IsCityOnline(state, cityState) {
		return "invalid_directive"
	}
	if errCode := CanPlaceBuildingAt(nodeEntry, playerID, cfg); errCode != "" {
		return errCode
	}
	if scope != "in_city" {
		return ""
	}
	radius := staticdata.Default().Rules().InitialCityTerritoryRadius
	if radius <= 0 {
		radius = 1
	}
	targetPos := PositionC.Get(nodeEntry)
	cityPos := PositionC.Get(cityEntry)
	dx := targetPos.X - cityPos.X
	if dx < 0 {
		dx = -dx
	}
	dy := targetPos.Y - cityPos.Y
	if dy < 0 {
		dy = -dy
	}
	if dx <= radius && dy <= radius {
		return ""
	}
	return "outside_territory"
}

func ResolveCityContext(state *domain.GameState, playerID string, cityID string) (*donburi.Entry, *domain.CityState, string) {
	if state == nil {
		return nil, nil, "invalid_target"
	}
	cityID = strings.TrimSpace(cityID)
	if cityID == "" {
		return nil, nil, "invalid_request"
	}
	cityEntry, ok := state.GetNode(cityID)
	if !ok || !cityEntry.HasComponent(BuildingC) {
		return nil, nil, "invalid_target"
	}
	building := BuildingC.Get(cityEntry)
	if normalizeRuntimeToken(string(building.Type)) != "city_core" {
		return nil, nil, "invalid_target"
	}
	node := NodeC.Get(cityEntry)
	player := normalizeRuntimeToken(playerID)
	if normalizeRuntimeToken(building.Owner) != player && normalizeRuntimeToken(node.Owner) != player && normalizeRuntimeToken(node.TerritoryOwner) != player {
		return nil, nil, "unauthorized"
	}
	ownerState, cityState := state.FindCityState(cityID)
	if ownerState == nil || cityState == nil {
		return cityEntry, &domain.CityState{
			CityID:              cityID,
			CoreNodeID:          cityID,
			OwnerID:             playerID,
			TerritoryBaseRadius: staticdata.Default().Rules().InitialCityTerritoryRadius,
		}, ""
	}
	if normalizeRuntimeToken(ownerState.PlayerID) != player {
		return nil, nil, "unauthorized"
	}
	return cityEntry, cityState, ""
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
	if state == nil || state.World == nil || centerEntry == nil {
		return nil, nil, "invalid_target"
	}
	centerPos := PositionC.Get(centerEntry)
	entries := make([]*donburi.Entry, 0, 9)
	ids := make([]string, 0, 9)
	for dy := -1; dy <= 1; dy++ {
		for dx := -1; dx <= 1; dx++ {
			pos := domain.Position{X: centerPos.X + dx, Y: centerPos.Y + dy}
			entry, ok := domain.GetNodeAt(state.World, pos)
			if !ok {
				return nil, nil, "territory_out_of_bounds"
			}
			entries = append(entries, entry)
			ids = append(ids, NodeC.Get(entry).ID)
		}
	}
	return entries, ids, ""
}

func CanFoundCityAt(state *domain.GameState, centerEntry *donburi.Entry) (bool, string) {
	footprintEntries, _, reason := TerritoryFootprint(state, centerEntry)
	if reason != "" {
		return false, reason
	}
	if centerEntry == nil {
		return false, "invalid_target"
	}
	centerNodeID := strings.TrimSpace(NodeC.Get(centerEntry).ID)
	for _, entry := range footprintEntries {
		if entry == nil {
			continue
		}
		node := NodeC.Get(entry)
		if node.IsResource {
			return false, "territory_blocked"
		}
		if !entry.HasComponent(BuildingC) {
			continue
		}
		building := BuildingC.Get(entry)
		if strings.TrimSpace(node.ID) == centerNodeID && normalizeRuntimeToken(string(building.Type)) == "city_core" {
			continue
		}
		return false, "territory_blocked"
	}
	centerPos := PositionC.Get(centerEntry)
	minimumDistance := staticdata.Default().Rules().MinimumCityDistance
	if minimumDistance > 0 {
		NodesWithBuilding(state.World).Each(state.World, func(entry *donburi.Entry) {
			if entry == nil || !entry.HasComponent(BuildingC) {
				return
			}
			building := BuildingC.Get(entry)
			if normalizeRuntimeToken(string(building.Type)) != "city_core" {
				return
			}
			node := NodeC.Get(entry)
			if strings.TrimSpace(node.ID) == centerNodeID {
				return
			}
			pos := PositionC.Get(entry)
			if geometry.Manhattan(
				domain.Position{X: centerPos.X, Y: centerPos.Y},
				domain.Position{X: pos.X, Y: pos.Y},
			) < minimumDistance {
				reason = "minimum_city_distance"
			}
		})
		if reason != "" {
			return false, reason
		}
	}
	return true, ""
}

func normalizeRuntimeToken(value string) string {
	return strings.ToLower(strings.TrimSpace(value))
}
