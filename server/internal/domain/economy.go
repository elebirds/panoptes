// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-15 22:30:00 +0800
// Description: 定义玩家级权威库存与回合预算辅助。

package domain

// CanAffordResources checks affordability against the player compatibility view.
func (s *GameState) CanAffordResources(playerID string, cost ResourceBag) bool {
	if s == nil || cost == nil || cost.IsZero() {
		return true
	}
	return s.PlayerResourceView(playerID).CanAfford(cost)
}

// ConsumeResources subtracts from city storage when a city is provided.
// If a state has not yet adopted city-local storage, it falls back to the
// legacy player resource bag for compatibility with older tests and saves.
func (s *GameState) ConsumeResources(playerID string, cityID string, cost ResourceBag) bool {
	if s == nil || cost == nil || cost.IsZero() {
		return true
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return false
	}
	if cityID != "" && s.playerHasCityStorage(playerID) {
		city := s.EnsureCityState(playerID, cityID)
		if city == nil || !city.Storage.CanAfford(cost) {
			return false
		}
		city.Storage = city.Storage.Sub(cost)
		s.refreshPlayerResourceProjection(playerID)
		return true
	}
	if cityID == "" && s.playerHasCityStorage(playerID) {
		return s.consumeResourcesAcrossCities(playerID, cost)
	}
	if !playerState.Resources.CanAfford(cost) {
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
	if primary := s.PrimaryCityState(playerID); primary != nil {
		s.migrateLegacyResourcesToCity(playerID, primary.CityID)
		if primary.Storage == nil {
			primary.Storage = NewResourceBag()
		}
		primary.Storage.AddAmount(key, delta)
		s.refreshPlayerResourceProjection(playerID)
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

func (s *GameState) AddResourceToCity(playerID string, cityID string, key ResourceKey, delta int) {
	if s == nil || playerID == "" || delta == 0 {
		return
	}
	if cityID == "" {
		if primary := s.PrimaryCityState(playerID); primary != nil {
			cityID = primary.CityID
		}
	}
	city := s.EnsureCityState(playerID, cityID)
	if city == nil {
		s.AddResource(playerID, key, delta)
		return
	}
	s.migrateLegacyResourcesToCity(playerID, cityID)
	if city.Storage == nil {
		city.Storage = NewResourceBag()
	}
	city.Storage.AddAmount(key, delta)
	s.refreshPlayerResourceProjection(playerID)
}

func (s *GameState) AddResourcesToCity(playerID string, cityID string, delta ResourceBag) {
	if s == nil || delta == nil || delta.IsZero() {
		return
	}
	for _, key := range delta.Keys() {
		s.AddResourceToCity(playerID, cityID, key, delta.Get(key))
	}
}

func (s *GameState) TransferResourcesBetweenCities(playerID string, fromCityID string, toCityID string, resources ResourceBag) bool {
	if s == nil || resources == nil || resources.IsZero() {
		return true
	}
	fromCity := s.EnsureCityState(playerID, fromCityID)
	toCity := s.EnsureCityState(playerID, toCityID)
	if fromCity == nil || toCity == nil || !fromCity.Storage.CanAfford(resources) {
		return false
	}
	fromCity.Storage = fromCity.Storage.Sub(resources)
	if toCity.Storage == nil {
		toCity.Storage = NewResourceBag()
	}
	toCity.Storage = toCity.Storage.Add(resources)
	s.refreshPlayerResourceProjection(playerID)
	return true
}

func (s *GameState) CityStorage(playerID string, cityID string) ResourceBag {
	city := s.CityState(playerID, cityID)
	if city == nil || city.Storage == nil {
		return NewResourceBag()
	}
	return city.Storage.Clone()
}

func (s *GameState) PlayerResourceView(playerID string) ResourceBag {
	if s == nil {
		return NewResourceBag()
	}
	playerState := s.Players[playerID]
	if playerState == nil {
		return NewResourceBag()
	}
	if !s.playerHasCityStorage(playerID) {
		return playerState.Resources.Clone()
	}
	return s.aggregateCityStorage(playerID)
}

func (s *GameState) playerHasCityStorage(playerID string) bool {
	playerState := s.Players[playerID]
	if playerState == nil {
		return false
	}
	for _, city := range playerState.Cities {
		if city != nil && city.Storage != nil && !city.Storage.IsZero() {
			return true
		}
	}
	return false
}

func (s *GameState) aggregateCityStorage(playerID string) ResourceBag {
	total := NewResourceBag()
	playerState := s.Players[playerID]
	if playerState == nil {
		return total
	}
	for _, city := range playerState.Cities {
		if city == nil || city.Storage == nil {
			continue
		}
		total = total.Add(city.Storage)
	}
	return total
}

func (s *GameState) refreshPlayerResourceProjection(playerID string) {
	playerState := s.Players[playerID]
	if playerState == nil || !s.playerHasCityStorage(playerID) {
		return
	}
	playerState.Resources = s.aggregateCityStorage(playerID)
}

func (s *GameState) consumeResourcesAcrossCities(playerID string, cost ResourceBag) bool {
	if !s.PlayerResourceView(playerID).CanAfford(cost) {
		return false
	}
	remaining := cost.Clone()
	if primary := s.PrimaryCityState(playerID); primary != nil {
		consumeFromCityStorage(primary, remaining)
	}
	playerState := s.Players[playerID]
	for _, city := range playerState.Cities {
		if remaining.IsZero() {
			break
		}
		consumeFromCityStorage(city, remaining)
	}
	s.refreshPlayerResourceProjection(playerID)
	return true
}

func consumeFromCityStorage(city *CityState, remaining ResourceBag) {
	if city == nil || city.Storage == nil || remaining == nil || remaining.IsZero() {
		return
	}
	for _, key := range remaining.Keys() {
		amount := city.Storage.Get(key)
		if amount <= 0 {
			continue
		}
		if amount > remaining.Get(key) {
			amount = remaining.Get(key)
		}
		city.Storage.AddAmount(key, -amount)
		remaining.AddAmount(key, -amount)
	}
}

func (s *GameState) migrateLegacyResourcesToCity(playerID string, cityID string) {
	playerState := s.Players[playerID]
	if playerState == nil || cityID == "" || playerState.Resources == nil || playerState.Resources.IsZero() || s.playerHasCityStorage(playerID) {
		return
	}
	city := s.EnsureCityState(playerID, cityID)
	if city == nil {
		return
	}
	if city.Storage == nil {
		city.Storage = NewResourceBag()
	}
	city.Storage = city.Storage.Add(playerState.Resources)
	s.refreshPlayerResourceProjection(playerID)
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
