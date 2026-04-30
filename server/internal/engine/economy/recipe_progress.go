// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Recipe progress settlement loop.

package economy

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type recipeProgressBudget struct {
	resources      map[string]domain.ResourceBag
	cityResources  map[string]map[string]domain.ResourceBag
	hasCityStorage map[string]bool
	points         map[string]domain.PointBag
}

func runRecipeProgress(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	if state == nil {
		return events
	}
	// 这里使用 per-player 的模拟库存，而不是一边遍历一边直接扣权威状态。
	// 这样同回合多个建筑会按遍历顺序共享同一份预算，但直到事件 Apply 前都不会把 ECS 写脏。
	budget := newRecipeProgressBudget(state)

	ecs.NodesWithBuilding(world).Each(world, func(entry *donburi.Entry) {
		if !entry.HasComponent(ecs.BuildingOperationC) {
			return
		}
		appendRecipeProgressForEntry(&events, entry, state, budget)
	})

	return events
}

func newRecipeProgressBudget(state *domain.GameState) recipeProgressBudget {
	budget := recipeProgressBudget{
		resources:      make(map[string]domain.ResourceBag, len(state.Players)),
		cityResources:  make(map[string]map[string]domain.ResourceBag, len(state.Players)),
		hasCityStorage: make(map[string]bool, len(state.Players)),
		points:         make(map[string]domain.PointBag, len(state.Players)),
	}
	for playerID, playerState := range state.Players {
		if playerState == nil {
			continue
		}
		budget.resources[playerID] = playerState.Resources.Clone()
		budget.cityResources[playerID] = make(map[string]domain.ResourceBag, len(playerState.Cities))
		for cityID, city := range playerState.Cities {
			if city == nil {
				continue
			}
			budget.cityResources[playerID][cityID] = cloneResourceBag(city.Storage)
			if city.Storage != nil && !city.Storage.IsZero() {
				budget.hasCityStorage[playerID] = true
			}
		}
		budget.points[playerID] = state.EnsurePointBudget(playerID).Clone()
	}
	return budget
}
