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

type NodeNetworkStatus struct {
	PlayerID   string
	CityID     string
	RoadStatus string
	Status     string
	Connected  bool
}

const (
	NetworkStatusConnected     = "connected"
	NetworkStatusDisconnected  = "disconnected"
	NetworkStatusNotApplicable = "not_applicable"
	NetworkStatusUnknown       = "unknown"
)

func RoadStatusForNode(state *GameState, nodeID string) RoadStatus {
	nodeID = strings.TrimSpace(nodeID)
	if state == nil || nodeID == "" {
		return RoadStatusDestroyed
	}
	entry, ok := state.GetNode(nodeID)
	if !ok || entry == nil {
		return RoadStatusDestroyed
	}
	if NodeC.Get(entry).HasRoad {
		return RoadStatusIntact
	}
	return RoadStatusDestroyed
}

func NodeNetworkStatusForPlayer(state *GameState, playerID string, nodeID string) NodeNetworkStatus {
	nodeID = strings.TrimSpace(nodeID)
	status := NodeNetworkStatus{
		PlayerID:   strings.TrimSpace(playerID),
		RoadStatus: string(RoadStatusForNode(state, nodeID)),
		Status:     NetworkStatusUnknown,
	}
	if state == nil || nodeID == "" {
		return status
	}
	entry, ok := state.GetNode(nodeID)
	if !ok || entry == nil {
		return status
	}
	status.PlayerID = networkStatusPlayerID(entry, status.PlayerID)
	if status.PlayerID == "" {
		status.Status = NetworkStatusNotApplicable
		return status
	}
	cityID, cityCoreNodeID := networkStatusCity(state, status.PlayerID, entry)
	status.CityID = cityID
	if cityCoreNodeID == "" {
		status.Status = NetworkStatusNotApplicable
		return status
	}
	if playerState := state.Players[status.PlayerID]; playerState != nil {
		if city := playerState.Cities[nodeID]; city != nil {
			capital := state.PrimaryCityState(status.PlayerID)
			capitalCoreNodeID := ""
			if capital != nil {
				capitalCoreNodeID = cityCoreNodeIDForCity(capital)
			}
			if capitalCoreNodeID == "" || city.CityID == capital.CityID || RoadConnected(state, capitalCoreNodeID, cityCoreNodeID) {
				status.Status = NetworkStatusConnected
				status.Connected = true
				return status
			}
			status.Status = NetworkStatusDisconnected
			return status
		}
	}
	if nodeID == cityCoreNodeID || RoadConnected(state, cityCoreNodeID, nodeID) {
		status.Status = NetworkStatusConnected
		status.Connected = true
		return status
	}
	status.Status = NetworkStatusDisconnected
	return status
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

func networkStatusPlayerID(entry *donburi.Entry, fallback string) string {
	if fallback != "" {
		return fallback
	}
	if entry == nil {
		return ""
	}
	if entry.HasComponent(BuildingC) {
		if owner := strings.TrimSpace(BuildingC.Get(entry).Owner); owner != "" {
			return owner
		}
	}
	node := NodeC.Get(entry)
	if owner := strings.TrimSpace(node.TerritoryOwner); owner != "" {
		return owner
	}
	return strings.TrimSpace(node.Owner)
}

func networkStatusCity(state *GameState, playerID string, entry *donburi.Entry) (string, string) {
	if state == nil || entry == nil || playerID == "" {
		return "", ""
	}
	nodeID := strings.TrimSpace(NodeC.Get(entry).ID)
	if entry.HasComponent(BuildingBindingC) {
		binding := BuildingBindingC.Get(entry)
		cityID := strings.TrimSpace(binding.ServiceCityID)
		if cityID == "" {
			cityID = strings.TrimSpace(binding.CityID)
		}
		return cityID, cityCoreNodeID(state, playerID, cityID)
	}
	if entry.HasComponent(BuildingC) && strings.EqualFold(string(BuildingC.Get(entry).Type), BuildingScopeCityCore) {
		return nodeID, nodeID
	}
	playerState := state.Players[playerID]
	if playerState == nil {
		return "", ""
	}
	if city := playerState.Cities[nodeID]; city != nil {
		return city.CityID, cityCoreNodeID(state, playerID, city.CityID)
	}
	cityIDs := make([]string, 0, len(playerState.Cities))
	for cityID := range playerState.Cities {
		cityIDs = append(cityIDs, cityID)
	}
	sort.Strings(cityIDs)
	for _, cityID := range cityIDs {
		coreNodeID := cityCoreNodeID(state, playerID, cityID)
		if coreNodeID != "" && RoadConnected(state, coreNodeID, nodeID) {
			return cityID, coreNodeID
		}
	}
	if capital := state.PrimaryCityState(playerID); capital != nil {
		return capital.CityID, cityCoreNodeID(state, playerID, capital.CityID)
	}
	return "", ""
}

func cityCoreNodeID(state *GameState, playerID string, cityID string) string {
	if state == nil || playerID == "" || cityID == "" {
		return ""
	}
	playerState := state.Players[playerID]
	if playerState == nil {
		return ""
	}
	city := playerState.Cities[cityID]
	if city == nil {
		return ""
	}
	return cityCoreNodeIDForCity(city)
}

func cityCoreNodeIDForCity(city *CityState) string {
	if city == nil {
		return ""
	}
	if coreNodeID := strings.TrimSpace(city.CoreNodeID); coreNodeID != "" {
		return coreNodeID
	}
	return strings.TrimSpace(city.CityID)
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
