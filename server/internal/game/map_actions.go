// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的地图动作结算逻辑。

package game

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/yohamta/donburi"
)

func (r *GameRoom) applyPlannedMapActions() []*pb.TurnEvent {
	state := r.State()
	if r == nil || state == nil {
		return nil
	}
	events := make([]*pb.TurnEvent, 0)
	for _, directive := range state.TurnRuntime.Planning.UnitOrders {
		switch gameorders.UnitAction(directive.Action) {
		case gameorders.ActionSettleCity:
			if evt, ok := r.applySettleCityOrder(directive); ok {
				events = append(events, evt)
			}
		}
	}
	return events
}

func (r *GameRoom) applySettleCityOrder(order domain.UnitDirective) (*pb.TurnEvent, bool) {
	state := r.State()
	if r == nil || state == nil {
		return nil, false
	}
	unitEntry, ok := findUnitEntryByID(state.World, order.UnitID)
	if !ok {
		return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": "unit_not_found"}}, true
	}

	stats := ecs.UnitStatsC.Get(unitEntry)
	if stats.Faction != order.PlayerID || !isTerritoryExpansionUnit(stats.Type) {
		return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": "invalid_unit_type"}}, true
	}

	unitPos := ecs.PositionC.Get(unitEntry)
	centerEntry, ok := domain.GetNodeAt(state.World, domain.Position{X: unitPos.X, Y: unitPos.Y})
	if !ok {
		return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": "invalid_target"}}, true
	}
	centerNodeID := ecs.NodeC.Get(centerEntry).ID
	if target := strings.TrimSpace(order.TargetNodeID); target != "" && target != centerNodeID {
		targetEntry, found := r.NodeByID(target)
		if !found {
			return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": "invalid_target"}}, true
		}
		centerEntry = targetEntry
		centerNodeID = target
	}

	if ok, reason := ecs.CanFoundCityAt(state, centerEntry); !ok {
		return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": reason}}, true
	}
	footprintEntries, footprintIDs, reason := ecs.TerritoryFootprint(state, centerEntry)
	if reason != "" {
		return &pb.TurnEvent{Type: "settle_city_failed", Data: map[string]string{"unit_id": order.UnitID, "reason": reason}}, true
	}

	for _, entry := range footprintEntries {
		node := ecs.NodeC.Get(entry)
		node.TerritoryOwner = order.PlayerID
		node.Owner = order.PlayerID
	}
	setBuildingOnNode(state, centerEntry, "city_core", order.PlayerID, centerNodeID, state.Turn+1)
	cityState := state.EnsureCityState(order.PlayerID, centerNodeID)
	if cityState != nil {
		cityState.OnlineOnTurn = state.Turn + 1
	}
	state.World.Remove(unitEntry.Entity())

	return &pb.TurnEvent{
		Type: "city_founded",
		Data: map[string]string{
			"player_id":        order.PlayerID,
			"unit_id":          order.UnitID,
			"city_id":          centerNodeID,
			"center_node_id":   centerNodeID,
			"updated_nodes":    strings.Join(footprintIDs, ","),
		},
	}, true
}

func setBuildingOnNode(state *domain.GameState, nodeEntry *donburi.Entry, buildingType, owner string, cityID string, onlineOnTurn int) {
	if state == nil || state.World == nil || nodeEntry == nil {
		return
	}
	ecs.CreateBuilding(state.World, buildingType, owner, cityID, nodeEntry)
	domain.SetBuildingLifecycleState(nodeEntry, domain.BuildingStatusDisabled, "pending_activation", onlineOnTurn)
	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = owner
}

func findUnitEntryByID(world donburi.World, unitID string) (*donburi.Entry, bool) {
	var found *donburi.Entry
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found != nil || entry == nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}

func isTerritoryExpansionUnit(unitType domain.UnitType) bool {
	switch normalizeMapActionToken(string(unitType)) {
	case "settler", "pioneer", "expander", "engineer":
		return true
	default:
		return false
	}
}

func normalizeMapActionToken(value string) string {
	return strings.ToLower(strings.TrimSpace(value))
}
