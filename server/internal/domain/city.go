// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-15 22:30:00 +0800
// Description: 定义城市领域模型与目录辅助。

package domain

import "sort"

type CityState struct {
	CityID  string
	NodeID  string
	OwnerID string
}

func (s *GameState) EnsureCityState(playerID string, cityID string) *CityState {
	if s == nil || cityID == "" {
		return nil
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return nil
	}
	if playerState.Cities == nil {
		playerState.Cities = make(map[string]*CityState)
	}
	if city, ok := playerState.Cities[cityID]; ok && city != nil {
		if city.NodeID == "" {
			city.NodeID = cityID
		}
		if city.OwnerID == "" {
			city.OwnerID = playerID
		}
		return city
	}

	city := &CityState{
		CityID:  cityID,
		NodeID:  cityID,
		OwnerID: playerID,
	}
	playerState.Cities[cityID] = city
	return city
}

// PrimaryCityState returns a stable fallback city for player-scoped logic.
func (s *GameState) PrimaryCityState(playerID string) *CityState {
	if s == nil {
		return nil
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil || len(playerState.Cities) == 0 {
		return nil
	}
	ids := make([]string, 0, len(playerState.Cities))
	for cityID := range playerState.Cities {
		if cityID != "" {
			ids = append(ids, cityID)
		}
	}
	if len(ids) == 0 {
		return nil
	}
	sort.Strings(ids)
	return playerState.Cities[ids[0]]
}
