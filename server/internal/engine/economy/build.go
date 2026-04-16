// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的建造结算逻辑。

package economy

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
	// 先在临时库存和临时点数预算上模拟扣款，确认本回合多条 build order 的竞争结果，
	// 再由事件 Apply 把最终结果落到权威状态。
	orders := append([]domain.BuildOrder{}, state.TurnRuntime.Planning.BuildOrders...)
	simulatedResources := make(map[string]domain.ResourceBag, len(state.Players))
	simulatedPoints := make(map[string]domain.PointBag, len(state.Players))
	reservedNodes := make(map[string]struct{}, len(orders))
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
		validation := ValidateBuildOrder(state, order.PlayerID, order.NodeID, order.BuildingType, order.CityID)
		if !validation.OK {
			events = append(events, event.BuildSkippedEvent{
				PlayerID:     order.PlayerID,
				NodeID:       order.NodeID,
				BuildingType: order.BuildingType,
				Reason:       validation.ErrorCode,
			})
			continue
		}
		if _, reserved := reservedNodes[order.NodeID]; reserved {
			// 同回合前序草案已经成功占住同一节点时，后序草案统一按 building_exists 跳过。
			events = append(events, event.BuildSkippedEvent{
				PlayerID:     order.PlayerID,
				NodeID:       order.NodeID,
				BuildingType: order.BuildingType,
				Reason:       "building_exists",
			})
			continue
		}
		resourceCost := state.ApplyResourceModifiers(order.PlayerID, string(staticdata.ModifierTriggerBuildingResourceCost), order.BuildingType, toResourceBag(validation.Building.ResourceCosts))
		pointCost := state.ApplyPointModifiers(order.PlayerID, string(staticdata.ModifierTriggerBuildingPointCost), order.BuildingType, toPointBag(validation.Building.PointCosts))
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
		reservedNodes[order.NodeID] = struct{}{}
		events = append(events, event.BuildingBuiltEvent{
			NodeID:       order.NodeID,
			BuildingType: order.BuildingType,
			Owner:        order.PlayerID,
			CityID:       order.CityID,
			Cost:         resourceCost,
			// 新建筑按统一生命周期规则延迟到下一回合上线。
			OnlineOnTurn: state.Turn + 1,
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
