// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-15 22:30:00 +0800
// Description: 定义玩家级权威库存与回合预算辅助。

package domain

// CanAffordResources checks affordability against the player-global inventory.
func (s *GameState) CanAffordResources(playerID string, cost ResourceBag) bool {
	if s == nil || cost == nil || cost.IsZero() {
		return true
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return false
	}
	return playerState.Resources.CanAfford(cost)
}

// ConsumeResources subtracts from the player-global inventory.
func (s *GameState) ConsumeResources(playerID string, _ string, cost ResourceBag) bool {
	if s == nil || cost == nil || cost.IsZero() {
		return true
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil || !playerState.Resources.CanAfford(cost) {
		return false
	}
	playerState.Resources = playerState.Resources.Sub(cost)
	return true
}

func (s *GameState) AddResource(playerID string, key ResourceKey, delta int) {
	if s == nil || delta == 0 {
		return
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return
	}
	if playerState.Resources == nil {
		playerState.Resources = NewResourceBag()
	}
	playerState.Resources.AddAmount(key, delta)
}

func (s *GameState) AddResources(playerID string, delta ResourceBag) {
	if s == nil || delta == nil || delta.IsZero() {
		return
	}
	for _, key := range delta.Keys() {
		s.AddResource(playerID, key, delta.Get(key))
	}
}

func (s *GameState) EnsurePointBudget(playerID string) PointBag {
	if s == nil {
		return nil
	}
	if s.TurnRuntime.Resolving.PointBudgets == nil {
		s.TurnRuntime.Resolving.PointBudgets = make(map[string]PointBag)
	}
	if budget, ok := s.TurnRuntime.Resolving.PointBudgets[playerID]; ok && budget != nil {
		return budget
	}
	budget := NewPointBag()
	s.TurnRuntime.Resolving.PointBudgets[playerID] = budget
	return budget
}

func (s *GameState) CanAffordPoints(playerID string, cost PointBag) bool {
	if s == nil || cost == nil || cost.IsZero() {
		return true
	}
	return s.EnsurePointBudget(playerID).CanAfford(cost)
}

func (s *GameState) ConsumePoints(playerID string, cost PointBag) bool {
	if s == nil || cost == nil || cost.IsZero() {
		return true
	}
	budget := s.EnsurePointBudget(playerID)
	if budget == nil || !budget.CanAfford(cost) {
		return false
	}
	for _, key := range cost.Keys() {
		budget.AddAmount(key, -cost.Get(key))
	}
	return true
}

func (s *GameState) RefreshPointBudget(playerID string, key PointKey, amount int) {
	if s == nil || amount == 0 {
		return
	}
	budget := s.EnsurePointBudget(playerID)
	if budget == nil {
		return
	}
	budget.Set(key, amount)
}

func (s *GameState) ClearPointBudgets() {
	if s == nil || s.TurnRuntime.Resolving.PointBudgets == nil {
		return
	}
	for playerID := range s.TurnRuntime.Resolving.PointBudgets {
		s.TurnRuntime.Resolving.PointBudgets[playerID] = NewPointBag()
	}
}
