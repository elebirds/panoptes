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
	"github.com/elebirds/panoptes/internal/staticdata"
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
		case ActionBuildRoad:
			if evt, ok := roadBuiltEvent(state, directive); ok {
				events = append(events, evt)
			}
		case ActionRepairRoad:
			if evt, ok := roadRepairedEvent(state, directive); ok {
				events = append(events, evt)
			}
		case ActionDestroyRoad:
			if evt, ok := roadDestroyedEvent(state, directive); ok {
				events = append(events, evt)
			}
		case ActionBuildImprovement:
			if evt, ok := improvementBuiltEvent(state, directive); ok {
				events = append(events, evt)
			}
		case ActionRepairImprovement:
			if evt, ok := improvementRepairedEvent(state, directive); ok {
				events = append(events, evt)
			}
		case ActionRaidStorage:
			if evt, ok := storageRaidedEvent(state, directive); ok {
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

func roadBuiltEvent(state *domain.GameState, order domain.UnitDirective) (event.Event, bool) {
	fromNodeID, toNodeID, ok := roadEventEndpoints(state, order)
	if !ok {
		return nil, false
	}
	return event.RoadBuiltEvent{
		FromNode: fromNodeID,
		ToNode:   toNodeID,
		Owner:    order.PlayerID,
	}, true
}

func roadRepairedEvent(state *domain.GameState, order domain.UnitDirective) (event.Event, bool) {
	fromNodeID, toNodeID, ok := roadEventEndpoints(state, order)
	if !ok {
		return nil, false
	}
	return event.RoadRepairedEvent{
		FromNode: fromNodeID,
		ToNode:   toNodeID,
		Owner:    order.PlayerID,
	}, true
}

func roadDestroyedEvent(state *domain.GameState, order domain.UnitDirective) (event.Event, bool) {
	fromNodeID, toNodeID, ok := roadEventEndpoints(state, order)
	if !ok {
		return nil, false
	}
	return event.RoadDestroyedEvent{
		FromNode:    fromNodeID,
		ToNode:      toNodeID,
		DestroyerID: order.UnitID,
	}, true
}

func improvementBuiltEvent(state *domain.GameState, order domain.UnitDirective) (event.Event, bool) {
	targetEntry, ok := state.GetNode(strings.TrimSpace(order.TargetNodeID))
	if !ok {
		return nil, false
	}
	buildingTypeID, ok := improvementBuildingType(FromDirective(order), targetEntry)
	if !ok {
		return nil, false
	}
	cfg, ok := staticdata.Default().GetBuilding(buildingTypeID)
	if !ok {
		return nil, false
	}
	cost, _ := domain.ResourceBagFromAmounts(cfg.ResourceCosts)
	return event.BuildingBuiltEvent{
		NodeID:       order.TargetNodeID,
		BuildingType: buildingTypeID,
		Owner:        order.PlayerID,
		CityID:       improvementCityID(state, order.PlayerID, FromDirective(order)),
		Cost:         cost,
		OnlineOnTurn: state.Turn + 1,
	}, true
}

func improvementRepairedEvent(state *domain.GameState, order domain.UnitDirective) (event.Event, bool) {
	targetEntry, ok := state.GetNode(strings.TrimSpace(order.TargetNodeID))
	if !ok || !targetEntry.HasComponent(ecs.BuildingC) {
		return nil, false
	}
	if owner := strings.TrimSpace(ecs.BuildingC.Get(targetEntry).Owner); owner != "" && owner != strings.TrimSpace(order.PlayerID) {
		return nil, false
	}
	return event.BuildingRepairedEvent{
		NodeID: order.TargetNodeID,
		Owner:  order.PlayerID,
	}, true
}

func storageRaidedEvent(state *domain.GameState, order domain.UnitDirective) (event.Event, bool) {
	cityID := strings.TrimSpace(order.TargetNodeID)
	targetPlayerID := storageRaidTargetPlayer(state, cityID)
	if targetPlayerID == "" {
		return nil, false
	}
	resources := storageRaidResources(state.CityStorage(targetPlayerID, cityID), staticdata.Default().Rules().StorageRaidAmount)
	if resources.IsZero() {
		return nil, false
	}
	return event.StorageRaidedEvent{
		TargetPlayerID: targetPlayerID,
		CityID:         cityID,
		RaiderID:       order.UnitID,
		Resources:      resources,
	}, true
}

func storageRaidTargetPlayer(state *domain.GameState, cityID string) string {
	if state == nil || cityID == "" {
		return ""
	}
	for playerID, playerState := range state.Players {
		if playerState == nil {
			continue
		}
		if city := playerState.Cities[cityID]; city != nil {
			return playerID
		}
	}
	return ""
}

func storageRaidResources(storage domain.ResourceBag, raidAmount int) domain.ResourceBag {
	out := domain.NewResourceBag()
	if storage == nil || raidAmount <= 0 {
		return out
	}
	for _, key := range storage.Keys() {
		amount := storage.Get(key)
		if amount > raidAmount {
			amount = raidAmount
		}
		out.Set(key, amount)
		break
	}
	return out
}

func roadEventEndpoints(state *domain.GameState, order domain.UnitDirective) (string, string, bool) {
	if state == nil {
		return "", "", false
	}
	unitEntry, ok := findUnitEntryByID(state.World, order.UnitID)
	if !ok {
		return "", "", false
	}
	fromNodeID, toNodeID, ok := roadActionEndpoints(state, FromDirective(order), unitEntry)
	if !ok {
		return "", "", false
	}
	if _, ok := state.GetNode(fromNodeID); !ok {
		return "", "", false
	}
	if _, ok := state.GetNode(toNodeID); !ok {
		return "", "", false
	}
	return fromNodeID, toNodeID, true
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
