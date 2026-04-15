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
	orders := append([]domain.BuildOrder{}, state.TurnRuntime.Planning.BuildOrders...)
	orders = append(orders, state.TurnRuntime.Planning.MinisterBuilds...)

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
		cost := state.ApplyResourceModifiers(order.PlayerID, string(staticdata.ModifierTriggerBuildingResourceCost), order.BuildingType, toResourceBag(cfg.ResourceCosts))
		if !state.CanAffordFromCity(order.PlayerID, order.CityID, cost) {
			continue
		}
		events = append(events, event.BuildingBuiltEvent{
			NodeID:       order.NodeID,
			BuildingType: order.BuildingType,
			Owner:        order.PlayerID,
			CityID:       order.CityID,
			Cost:         cost,
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
