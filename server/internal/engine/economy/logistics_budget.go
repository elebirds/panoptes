// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
package economy

import (
	"sort"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
)

func (b recipeProgressBudget) availableResources(state *domain.GameState, owner string, cityID string, need domain.ResourceBag) domain.ResourceBag {
	if !b.hasCityStorage[owner] || cityID == "" {
		return cloneResourceBag(b.resources[owner])
	}
	available := cloneResourceBag(b.cityResources[owner][cityID])
	if state == nil || state.EffectiveRoadBaseCapacity(owner) <= 0 {
		return available
	}
	for _, donorCityID := range b.reachableDonorCityIDs(state, owner, cityID) {
		remaining := b.routeCapacityRemaining(state, owner, donorCityID, cityID)
		donorStorage := b.cityResources[owner][donorCityID]
		for _, key := range need.Keys() {
			if remaining <= 0 {
				break
			}
			amount := minInt(donorStorage.Get(key), remaining)
			if amount <= 0 {
				continue
			}
			available.AddAmount(key, amount)
			remaining -= amount
		}
	}
	return available
}

func (b recipeProgressBudget) consumeResources(state *domain.GameState, owner string, cityID string, targetNodeID string, delta domain.ResourceBag) ([]event.Event, bool) {
	if delta == nil || delta.IsZero() {
		return nil, true
	}
	if !b.hasCityStorage[owner] || cityID == "" {
		if !b.resources[owner].CanAfford(delta) {
			return nil, false
		}
		b.resources[owner] = b.resources[owner].Sub(delta)
		return nil, true
	}
	if b.cityResources[owner] == nil {
		b.cityResources[owner] = make(map[string]domain.ResourceBag)
	}
	if b.cityResources[owner][cityID] == nil {
		b.cityResources[owner][cityID] = domain.NewResourceBag()
	}

	remaining := delta.Clone()
	local := b.cityResources[owner][cityID]
	for _, key := range remaining.Keys() {
		amount := minInt(local.Get(key), remaining.Get(key))
		if amount <= 0 {
			continue
		}
		local.AddAmount(key, -amount)
		remaining.AddAmount(key, -amount)
	}
	remaining = remaining.Normalize()
	if remaining.IsZero() {
		return nil, true
	}

	flows := make([]event.Event, 0)
	if state == nil || state.EffectiveRoadBaseCapacity(owner) <= 0 {
		return nil, false
	}
	for _, donorCityID := range b.reachableDonorCityIDs(state, owner, cityID) {
		donorStorage := b.cityResources[owner][donorCityID]
		if donorStorage == nil {
			continue
		}
		moved := domain.NewResourceBag()
		remainingCapacity := b.routeCapacityRemaining(state, owner, donorCityID, cityID)
		for _, key := range remaining.Keys() {
			if remainingCapacity <= 0 {
				break
			}
			amount := minInt(donorStorage.Get(key), remaining.Get(key))
			amount = minInt(amount, remainingCapacity)
			if amount <= 0 {
				continue
			}
			donorStorage.AddAmount(key, -amount)
			remaining.AddAmount(key, -amount)
			moved.AddAmount(key, amount)
			remainingCapacity -= amount
			b.consumeRouteCapacity(owner, donorCityID, cityID, amount)
		}
		if !moved.IsZero() {
			flows = append(flows, event.ResourceFlowedEvent{
				Owner:      owner,
				FromCityID: donorCityID,
				ToCityID:   cityID,
				FromNodeID: state.CityCoreNodeID(owner, donorCityID),
				ToNodeID:   targetNodeID,
				Resources:  moved,
			})
		}
		if remaining.Normalize().IsZero() {
			return flows, true
		}
	}
	return nil, false
}

func (b recipeProgressBudget) routeCapacityRemaining(state *domain.GameState, owner string, fromCityID string, toCityID string) int {
	capacity := 0
	if state != nil {
		capacity = state.EffectiveRoadBaseCapacity(owner)
	}
	if capacity <= 0 {
		return 0
	}
	used := 0
	if b.capacityConsumed[owner] != nil {
		used = b.capacityConsumed[owner][routeCapacityKey(fromCityID, toCityID)]
	}
	if used >= capacity {
		return 0
	}
	return capacity - used
}

func (b recipeProgressBudget) consumeRouteCapacity(owner string, fromCityID string, toCityID string, amount int) {
	if amount <= 0 {
		return
	}
	if b.capacityConsumed[owner] == nil {
		b.capacityConsumed[owner] = make(map[string]int)
	}
	b.capacityConsumed[owner][routeCapacityKey(fromCityID, toCityID)] += amount
}

func routeCapacityKey(fromCityID string, toCityID string) string {
	return fromCityID + "->" + toCityID
}

func (b recipeProgressBudget) reachableDonorCityIDs(state *domain.GameState, owner string, cityID string) []string {
	if state == nil || owner == "" || cityID == "" {
		return nil
	}
	targetCoreNodeID := state.CityCoreNodeID(owner, cityID)
	if targetCoreNodeID == "" {
		return nil
	}
	ids := make([]string, 0, len(b.cityResources[owner]))
	for donorCityID := range b.cityResources[owner] {
		if donorCityID == "" || donorCityID == cityID {
			continue
		}
		donorCoreNodeID := state.CityCoreNodeID(owner, donorCityID)
		if donorCoreNodeID == "" || !domain.RoadConnected(state, donorCoreNodeID, targetCoreNodeID) {
			continue
		}
		ids = append(ids, donorCityID)
	}
	sort.Strings(ids)
	return ids
}

func minInt(a int, b int) int {
	if a < b {
		return a
	}
	return b
}
