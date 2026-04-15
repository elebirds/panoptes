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

func (s *GameState) RefreshTurnOutputBudget(playerID string, key ResourceKey, amount int, maxVal int) {
	if s == nil || amount == 0 {
		return
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return
	}
	if playerState.Resources == nil {
		playerState.Resources = NewResourceBag()
	}
	next := playerState.Resources.Get(key) + amount
	if next > maxVal {
		next = maxVal
	}
	playerState.Resources.Set(key, next)
}
