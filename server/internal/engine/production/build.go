// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的建造结算逻辑。

package production

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type BuildSystem struct{}

func (s *BuildSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	if state == nil {
		return events
	}
	orders := append([]domain.BuildOrder{}, state.TurnRuntime.Planning.BuildOrders...)
	orders = append(orders, state.TurnRuntime.Planning.MinisterBuilds...)
	simulatedResources := make(map[string]domain.ResourceBag, len(state.Players))
	simulatedPoints := make(map[string]domain.PointBag, len(state.Players))
	for playerID, playerState := range state.Players {
		if playerState == nil {
			continue
		}
		simulatedResources[playerID] = playerState.Resources.Clone()
		simulatedPoints[playerID] = state.EnsurePointBudget(playerID).Clone()
	}

	for _, order := range orders {
		if _, ok := state.Players[order.PlayerID]; !ok {
			continue
		}
		if !state.IsBuildingUnlocked(order.PlayerID, order.BuildingType) {
			continue
		}
		cfg, ok := staticdata.Default().GetBuilding(order.BuildingType)
		if !ok {
			continue
		}
		resourceCost := state.ApplyResourceModifiers(order.PlayerID, string(staticdata.ModifierTriggerBuildingResourceCost), order.BuildingType, toResourceBag(cfg.ResourceCosts))
		pointCost := state.ApplyPointModifiers(order.PlayerID, string(staticdata.ModifierTriggerBuildingPointCost), order.BuildingType, toPointBag(cfg.PointCosts))
		availableResources := simulatedResources[order.PlayerID]
		availablePoints := simulatedPoints[order.PlayerID]
		if !availableResources.CanAfford(resourceCost) {
			events = append(events, event.BuildSkippedEvent{
				PlayerID:     order.PlayerID,
				NodeID:       order.NodeID,
				BuildingType: order.BuildingType,
				Reason:       "insufficient_resources",
			})
			continue
		}
		if !availablePoints.CanAfford(pointCost) {
			events = append(events, event.BuildSkippedEvent{
				PlayerID:     order.PlayerID,
				NodeID:       order.NodeID,
				BuildingType: order.BuildingType,
				Reason:       "insufficient_points",
			})
			continue
		}
		simulatedResources[order.PlayerID] = availableResources.Sub(resourceCost)
		for _, key := range pointCost.Keys() {
			availablePoints.AddAmount(key, -pointCost.Get(key))
			events = append(events, event.PointSpentEvent{
				PlayerID: order.PlayerID,
				Key:      key,
				Amount:   pointCost.Get(key),
				Reason:   "build_structure",
			})
		}
		events = append(events, event.BuildingBuiltEvent{
			NodeID:       order.NodeID,
			BuildingType: order.BuildingType,
			Owner:        order.PlayerID,
			CityID:       order.CityID,
			Cost:         resourceCost,
		})
	}
	return events
}

func toResourceBag(amounts map[string]int) domain.ResourceBag {
	bag := domain.NewResourceBag()
	for key, value := range amounts {
		bag.Set(domain.ResourceKey(key), value)
	}
	return bag
}

func toPointBag(amounts map[string]int) domain.PointBag {
	bag := domain.NewPointBag()
	for key, value := range amounts {
		bag.Set(domain.PointKey(key), value)
	}
	return bag
}
