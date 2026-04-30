// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: Builds map-action settlement events from state-only planning orders.

package orders

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

// BuildMapActionEvents converts pending planning map actions into domain events
// without requiring a GameRoom.
func BuildMapActionEvents(state *domain.GameState) []event.Event {
	if state == nil {
		return nil
	}
	events := make([]event.Event, 0)
	for _, directive := range state.TurnRuntime.Planning.UnitOrders {
		switch UnitAction(directive.Action) {
		case ActionSettleCity:
			if evt, ok := cityFoundingEvent(state, directive); ok {
				events = append(events, evt)
			}
		}
	}
	return events
}

func cityFoundingEvent(state *domain.GameState, order domain.UnitDirective) (event.Event, bool) {
	if state == nil {
		return nil, false
	}
	unitEntry, ok := findUnitEntryByID(state.World, order.UnitID)
	if !ok {
		return event.CityFoundingFailedEvent{PlayerID: order.PlayerID, UnitID: order.UnitID, Reason: "unit_not_found"}, true
	}

	stats := ecs.UnitStatsC.Get(unitEntry)
	if stats.Faction != order.PlayerID || !isTerritoryExpansionUnit(stats.Type) {
		return event.CityFoundingFailedEvent{PlayerID: order.PlayerID, UnitID: order.UnitID, Reason: "invalid_unit_type"}, true
	}

	unitPos := ecs.PositionC.Get(unitEntry)
	centerEntry, ok := domain.GetNodeAt(state.World, domain.Position{Q: unitPos.Q, R: unitPos.R})
	if !ok {
		return event.CityFoundingFailedEvent{PlayerID: order.PlayerID, UnitID: order.UnitID, Reason: "invalid_target"}, true
	}
	centerNodeID := ecs.NodeC.Get(centerEntry).ID
	if target := strings.TrimSpace(order.TargetNodeID); target != "" && target != centerNodeID {
		targetEntry, found := state.GetNode(target)
		if !found {
			return event.CityFoundingFailedEvent{PlayerID: order.PlayerID, UnitID: order.UnitID, Reason: "invalid_target"}, true
		}
		centerEntry = targetEntry
		centerNodeID = target
	}

	if ok, reason := ecs.CanFoundCityAt(state, centerEntry); !ok {
		return event.CityFoundingFailedEvent{PlayerID: order.PlayerID, UnitID: order.UnitID, Reason: reason}, true
	}
	_, footprintIDs, reason := ecs.TerritoryFootprint(state, centerEntry)
	if reason != "" {
		return event.CityFoundingFailedEvent{PlayerID: order.PlayerID, UnitID: order.UnitID, Reason: reason}, true
	}

	return event.CityFoundedEvent{
		PlayerID:     order.PlayerID,
		UnitID:       order.UnitID,
		CityID:       centerNodeID,
		CenterNodeID: centerNodeID,
		TerritoryIDs: append([]string(nil), footprintIDs...),
		OnlineOnTurn: state.Turn + 1,
	}, true
}

func findUnitEntryByID(world donburi.World, unitID string) (*donburi.Entry, bool) {
	if world == nil || strings.TrimSpace(unitID) == "" {
		return nil, false
	}
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
