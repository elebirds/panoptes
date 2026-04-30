// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-05-01 00:00:00 +0800
// Description: Provides backend-owned road connectivity queries.

package domain

import (
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type RoadNetworkStatus struct {
	PlayerID            string
	CapitalCityID       string
	ConnectedCityIDs    []string
	DisconnectedCityIDs []string
}

func RoadConnected(state *GameState, fromNodeID string, toNodeID string) bool {
	fromNodeID = strings.TrimSpace(fromNodeID)
	toNodeID = strings.TrimSpace(toNodeID)
	if state == nil || fromNodeID == "" || toNodeID == "" {
		return false
	}
	if fromNodeID == toNodeID {
		entry, ok := state.GetNode(fromNodeID)
		return ok && roadNetworkNodePassable(entry)
	}
	fromEntry, ok := state.GetNode(fromNodeID)
	if !ok || !roadNetworkNodePassable(fromEntry) {
		return false
	}
	toEntry, ok := state.GetNode(toNodeID)
	if !ok || !roadNetworkNodePassable(toEntry) {
		return false
	}

	visited := map[string]struct{}{fromNodeID: {}}
	queue := []string{fromNodeID}
	for len(queue) > 0 {
		currentID := queue[0]
		queue = queue[1:]
		currentEntry, ok := state.GetNode(currentID)
		if !ok {
			continue
		}
		currentPos := PositionC.Get(currentEntry)
		for _, nextPos := range (Position{Q: currentPos.Q, R: currentPos.R}).Neighbors() {
			nextEntry, ok := GetNodeAt(state.World, nextPos)
			if !ok || !roadNetworkNodePassable(nextEntry) {
				continue
			}
			nextID := NodeC.Get(nextEntry).ID
			if nextID == toNodeID {
				return true
			}
			if _, seen := visited[nextID]; seen {
				continue
			}
			visited[nextID] = struct{}{}
			queue = append(queue, nextID)
		}
	}
	return false
}

func PlayerRoadNetworkStatus(state *GameState, playerID string) RoadNetworkStatus {
	playerID = strings.TrimSpace(playerID)
	status := RoadNetworkStatus{PlayerID: playerID}
	if state == nil || playerID == "" {
		return status
	}
	playerState := state.Players[playerID]
	if playerState == nil {
		return status
	}
	capital := state.PrimaryCityState(playerID)
	if capital == nil {
		return status
	}
	status.CapitalCityID = capital.CityID
	capitalNodeID := strings.TrimSpace(capital.CoreNodeID)
	if capitalNodeID == "" {
		capitalNodeID = capital.CityID
	}

	cityIDs := make([]string, 0, len(playerState.Cities))
	for cityID := range playerState.Cities {
		if strings.TrimSpace(cityID) != "" {
			cityIDs = append(cityIDs, cityID)
		}
	}
	sort.Strings(cityIDs)
	for _, cityID := range cityIDs {
		city := playerState.Cities[cityID]
		if city == nil {
			continue
		}
		coreNodeID := strings.TrimSpace(city.CoreNodeID)
		if coreNodeID == "" {
			coreNodeID = cityID
		}
		if cityID == capital.CityID || RoadConnected(state, capitalNodeID, coreNodeID) {
			status.ConnectedCityIDs = append(status.ConnectedCityIDs, cityID)
			continue
		}
		status.DisconnectedCityIDs = append(status.DisconnectedCityIDs, cityID)
	}
	return status
}

func roadNetworkNodePassable(entry *donburi.Entry) bool {
	if entry == nil {
		return false
	}
	node := NodeC.Get(entry)
	if !node.HasRoad {
		return false
	}
	terrain, ok := staticdata.Default().GetTerrain(string(node.Terrain))
	if !ok {
		return true
	}
	return terrain.Passable || terrain.PassableWithRoad
}
