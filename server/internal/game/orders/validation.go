// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规划单位指令、地图动作与 resolving 单位订单的状态转换逻辑。

package orders

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

// ValidatePlanningUnitOrder returns the public error code for an invalid
// planning unit order, or an empty string when the order is accepted.
func ValidatePlanningUnitOrder(state *domain.GameState, playerID string, order UnitOrder) string {
	if state == nil || order.UnitID == "" || order.Action == "" {
		return "invalid_request"
	}
	unitEntry, ok := findOwnedUnit(state, order.UnitID, playerID)
	if !ok {
		return "unit_not_found"
	}
	stats := ecs.UnitStatsC.Get(unitEntry)

	switch order.Action {
	case ActionHold:
		return ""
	case ActionMove:
		if order.TargetNodeID == "" {
			return "invalid_request"
		}
		if _, ok := state.GetNode(order.TargetNodeID); !ok {
			return "invalid_target"
		}
		return ""
	case ActionAttack:
		hasUnitTarget := order.TargetUnitID != ""
		hasNodeTarget := order.TargetNodeID != ""
		if hasUnitTarget == hasNodeTarget || !unitCanAttack(unitEntry, stats.Type) {
			return "invalid_directive"
		}
		if hasUnitTarget {
			targetEntry, ok := findUnitByID(state, order.TargetUnitID)
			if !ok {
				return "invalid_target"
			}
			targetStats := ecs.UnitStatsC.Get(targetEntry)
			if targetStats.Faction == playerID {
				return "invalid_target"
			}
			return ""
		}
		nodeEntry, ok := state.GetNode(order.TargetNodeID)
		if !ok || nodeEntry == nil || !nodeEntry.HasComponent(ecs.BuildingC) {
			return "invalid_target"
		}
		building := ecs.BuildingC.Get(nodeEntry)
		if building.Owner == "" || building.Owner == playerID {
			return "invalid_target"
		}
		if !unitCanAttackStructures(unitEntry, stats.Type) {
			return "invalid_directive"
		}
		if !isStructureTargetInRange(unitEntry, nodeEntry, stats.AttackRange) {
			if activeMarchTargetInRange(state, order.UnitID, nodeEntry, stats.AttackRange) {
				return ""
			}
			if order.SecondaryNodeID != "" {
				if secondaryNodeEntry, ok := state.GetNode(order.SecondaryNodeID); ok && isStructureTargetInRangeFromNode(secondaryNodeEntry, nodeEntry, stats.AttackRange) {
					return ""
				}
			}
			return "invalid_target"
		}
		return ""
	case ActionCharge:
		if !unitCanCharge(unitEntry) {
			return "invalid_directive"
		}
		return ""
	case ActionSettleCity:
		return ""
	case ActionBuildRoad, ActionRepairRoad:
		return validateRoadAction(state, order, unitEntry)
	default:
		return "invalid_directive"
	}
}

func validateRoadAction(state *domain.GameState, order UnitOrder, unitEntry *donburi.Entry) string {
	if !unitCanBuildRoad(unitEntry) {
		return "invalid_directive"
	}
	fromNodeID, toNodeID, ok := roadActionEndpoints(state, order, unitEntry)
	if !ok {
		return "invalid_request"
	}
	fromEntry, ok := state.GetNode(fromNodeID)
	if !ok || !roadActionTerrainAllowsRoad(fromEntry) {
		return "invalid_target"
	}
	toEntry, ok := state.GetNode(toNodeID)
	if !ok || !roadActionTerrainAllowsRoad(toEntry) {
		return "invalid_target"
	}
	if !roadActionEndpointsAdjacent(fromEntry, toEntry) {
		return "invalid_target"
	}
	if !unitCanWorkRoadEndpoint(unitEntry, fromEntry, toEntry) {
		return "invalid_target"
	}
	if ecs.NodeC.Get(fromEntry).HasRoad && ecs.NodeC.Get(toEntry).HasRoad {
		return "invalid_target"
	}
	return ""
}

func unitCanBuildRoad(entry *donburi.Entry) bool {
	if entry == nil {
		return false
	}
	if entry.HasComponent(ecs.UnitCapabilitiesC) && ecs.UnitCapabilitiesC.Get(entry).Civilian {
		return true
	}
	stats := ecs.UnitStatsC.Get(entry)
	if strings.Contains(normalizeMapActionToken(string(stats.Type)), "engineer") {
		return true
	}
	if cfg, ok := staticdata.Default().GetUnit(string(stats.Type)); ok {
		return cfg.Class == "civilian" || strings.Contains(normalizeMapActionToken(cfg.ID), "engineer")
	}
	return false
}

func roadActionEndpoints(state *domain.GameState, order UnitOrder, unitEntry *donburi.Entry) (string, string, bool) {
	targetNodeID := strings.TrimSpace(order.TargetNodeID)
	secondaryNodeID := strings.TrimSpace(order.SecondaryNodeID)
	if targetNodeID == "" {
		return "", "", false
	}
	if secondaryNodeID != "" {
		return targetNodeID, secondaryNodeID, true
	}
	unitNodeID := nodeIDForEntry(state, unitEntry)
	if unitNodeID == "" {
		return "", "", false
	}
	return unitNodeID, targetNodeID, true
}

func roadActionTerrainAllowsRoad(entry *donburi.Entry) bool {
	if entry == nil {
		return false
	}
	node := ecs.NodeC.Get(entry)
	terrain, ok := staticdata.Default().GetTerrain(string(node.Terrain))
	if !ok {
		return true
	}
	return terrain.Passable || terrain.PassableWithRoad || terrain.Buildable
}

func roadActionEndpointsAdjacent(fromEntry *donburi.Entry, toEntry *donburi.Entry) bool {
	fromPos := ecs.PositionC.Get(fromEntry)
	toPos := ecs.PositionC.Get(toEntry)
	return (domain.Position{Q: fromPos.Q, R: fromPos.R}).DistanceTo(domain.Position{Q: toPos.Q, R: toPos.R}) == 1
}

func unitCanWorkRoadEndpoint(unitEntry *donburi.Entry, fromEntry *donburi.Entry, toEntry *donburi.Entry) bool {
	unitPos := ecs.PositionC.Get(unitEntry)
	fromPos := ecs.PositionC.Get(fromEntry)
	toPos := ecs.PositionC.Get(toEntry)
	unit := domain.Position{Q: unitPos.Q, R: unitPos.R}
	return unit.DistanceTo(domain.Position{Q: fromPos.Q, R: fromPos.R}) <= 1 ||
		unit.DistanceTo(domain.Position{Q: toPos.Q, R: toPos.R}) <= 1
}

func nodeIDForEntry(state *domain.GameState, entry *donburi.Entry) string {
	if state == nil || entry == nil {
		return ""
	}
	pos := ecs.PositionC.Get(entry)
	return nodeIDAt(state, domain.Position{Q: pos.Q, R: pos.R})
}

func activeMarchTargetInRange(state *domain.GameState, unitID string, nodeEntry *donburi.Entry, attackRange int) bool {
	if state == nil || state.TurnRuntime.Resolving.ActiveMarches == nil {
		return false
	}
	march, ok := state.TurnRuntime.Resolving.ActiveMarches[unitID]
	if !ok || march.DestinationNodeID == "" {
		return false
	}
	marchNodeEntry, ok := state.GetNode(march.DestinationNodeID)
	return ok && isStructureTargetInRangeFromNode(marchNodeEntry, nodeEntry, attackRange)
}

func unitCanAttack(entry *donburi.Entry, unitType domain.UnitType) bool {
	if entry != nil && entry.HasComponent(ecs.UnitCapabilitiesC) {
		return ecs.UnitCapabilitiesC.Get(entry).CanAttack()
	}
	if cfg, ok := staticdata.Default().GetUnit(string(unitType)); ok {
		return cfg.Attack > 0 && cfg.AttackRange > 0 && cfg.Class != "civilian"
	}
	return false
}

func unitCanAttackStructures(entry *donburi.Entry, unitType domain.UnitType) bool {
	if entry != nil && entry.HasComponent(ecs.UnitCapabilitiesC) {
		return ecs.UnitCapabilitiesC.Get(entry).CanAttackStructures
	}
	if cfg, ok := staticdata.Default().GetUnit(string(unitType)); ok {
		return cfg.Flags.CanAttackStructures
	}
	return false
}

func unitCanCharge(entry *donburi.Entry) bool {
	return entry != nil && entry.HasComponent(ecs.UnitCapabilitiesC) && ecs.UnitCapabilitiesC.Get(entry).Charge
}

func isStructureTargetInRange(unitEntry *donburi.Entry, nodeEntry *donburi.Entry, attackRange int) bool {
	if unitEntry == nil || nodeEntry == nil || attackRange <= 0 {
		return false
	}
	unitPos := ecs.PositionC.Get(unitEntry)
	nodePos := ecs.PositionC.Get(nodeEntry)
	return domain.Position{Q: unitPos.Q, R: unitPos.R}.DistanceTo(domain.Position{Q: nodePos.Q, R: nodePos.R}) <= attackRange
}

func isStructureTargetInRangeFromNode(fromNodeEntry *donburi.Entry, targetNodeEntry *donburi.Entry, attackRange int) bool {
	if fromNodeEntry == nil || targetNodeEntry == nil || attackRange <= 0 {
		return false
	}
	fromPos := ecs.PositionC.Get(fromNodeEntry)
	targetPos := ecs.PositionC.Get(targetNodeEntry)
	return domain.Position{Q: fromPos.Q, R: fromPos.R}.DistanceTo(domain.Position{Q: targetPos.Q, R: targetPos.R}) <= attackRange
}

func findOwnedUnit(state *domain.GameState, unitID string, ownerID string) (*donburi.Entry, bool) {
	entry, ok := findUnitByID(state, unitID)
	if !ok {
		return nil, false
	}
	return entry, ecs.UnitStatsC.Get(entry).Faction == ownerID
}

func findUnitByID(state *domain.GameState, unitID string) (*donburi.Entry, bool) {
	if state == nil || state.World == nil {
		return nil, false
	}
	return findUnitEntryByID(state.World, unitID)
}
