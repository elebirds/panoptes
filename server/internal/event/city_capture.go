// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载领域事件写入逻辑拆分后的事件族或事件辅助逻辑。

package event

import (
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/algo/geometry"
	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type CityCapturedEvent struct {
	NodeID       string
	CityID       string
	OldOwnerID   string
	NewOwnerID   string
	OnlineOnTurn int
}

func (e CityCapturedEvent) Apply(world donburi.World, state *domain.GameState) {
	if state == nil {
		return
	}
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !entry.HasComponent(ecs.BuildingC) {
		return
	}
	oldPlayer := state.Players[e.OldOwnerID]
	if oldPlayer != nil && oldPlayer.CapitalCityID == e.CityID {
		destroyCapitalCityCore(world, state, e.NodeID, e.NewOwnerID)
		return
	}
	newPlayer := state.Players[e.NewOwnerID]
	if newPlayer == nil {
		return
	}

	cityState := removeCityFromOldOwner(oldPlayer, e.CityID)
	if cityState == nil {
		cityState = newCapturedCityState(e.CityID)
	}
	addCapturedCityToNewOwner(newPlayer, cityState, e)
	applyCapturedCityCore(entry, e)
	transferCapturedCityFootprint(state, entry, e.NewOwnerID)
	transferCapturedCityBuildings(world, entry, e)
}

func (e CityCapturedEvent) Kind() string { return "city_captured" }

func (e CityCapturedEvent) String() string {
	return fmt.Sprintf("CityCapturedEvent city=%s old_owner=%s new_owner=%s", e.CityID, e.OldOwnerID, e.NewOwnerID)
}

func removeCityFromOldOwner(oldPlayer *domain.PlayerState, cityID string) *domain.CityState {
	if oldPlayer == nil {
		return nil
	}
	cityState := oldPlayer.Cities[cityID]
	delete(oldPlayer.Cities, cityID)
	return cityState
}

func newCapturedCityState(cityID string) *domain.CityState {
	return &domain.CityState{
		CityID:              cityID,
		CoreNodeID:          cityID,
		TerritoryBaseRadius: staticdata.Default().Rules().InitialCityTerritoryRadius,
	}
}

func addCapturedCityToNewOwner(newPlayer *domain.PlayerState, cityState *domain.CityState, e CityCapturedEvent) {
	cityState.OwnerID = e.NewOwnerID
	cityState.CoreNodeID = e.CityID
	cityState.OnlineOnTurn = e.OnlineOnTurn
	if newPlayer.Cities == nil {
		newPlayer.Cities = make(map[string]*domain.CityState)
	}
	newPlayer.Cities[e.CityID] = cityState
}

func applyCapturedCityCore(entry *donburi.Entry, e CityCapturedEvent) {
	coreBuilding := ecs.BuildingC.Get(entry)
	coreBuilding.Owner = e.NewOwnerID
	if coreBuilding.MaxHP > 0 {
		coreBuilding.HP = coreBuilding.MaxHP
	}
	coreNode := ecs.NodeC.Get(entry)
	coreNode.Owner = e.NewOwnerID
	coreNode.TerritoryOwner = e.NewOwnerID
	building.SetBinding(entry, domain.BuildingScopeCityCore, e.CityID, e.CityID)
	domain.SetBuildingLifecycleState(entry, domain.BuildingStatusDisabled, "pending_activation", e.OnlineOnTurn)
}

func transferCapturedCityFootprint(state *domain.GameState, cityCore *donburi.Entry, newOwnerID string) {
	footprintEntries, _, reason := building.TerritoryFootprint(state, cityCore)
	if reason != "" {
		return
	}
	for _, footprintEntry := range footprintEntries {
		if footprintEntry == nil {
			continue
		}
		node := ecs.NodeC.Get(footprintEntry)
		node.Owner = newOwnerID
		node.TerritoryOwner = newOwnerID
	}
}

func transferCapturedCityBuildings(world donburi.World, cityCore *donburi.Entry, e CityCapturedEvent) {
	ecs.NodesWithBuilding(world).Each(world, func(candidate *donburi.Entry) {
		if !shouldTransferCapturedCityBuilding(candidate, cityCore, e) {
			return
		}
		cfg, ok := staticdata.Default().GetBuilding(string(ecs.BuildingC.Get(candidate).Type))
		if !ok {
			return
		}
		if domain.NormalizeBuildingScope(cfg.BuildingScope) == domain.BuildingScopeOutOfCity {
			return
		}

		currentBuilding := ecs.BuildingC.Get(candidate)
		currentBuilding.Owner = e.NewOwnerID
		building.Rebind(candidate, e.CityID, e.CityID)
		candidateNode := ecs.NodeC.Get(candidate)
		candidateNode.Owner = e.NewOwnerID
		candidateNode.TerritoryOwner = e.NewOwnerID
		status, reason := building.CapturedLifecycleForBuilding(cfg)
		onlineOnTurn := 0
		if status == domain.BuildingStatusDisabled {
			onlineOnTurn = e.OnlineOnTurn
		}
		domain.SetBuildingLifecycleState(candidate, status, reason, onlineOnTurn)
	})
}

func shouldTransferCapturedCityBuilding(candidate, cityCore *donburi.Entry, e CityCapturedEvent) bool {
	if candidate == nil || candidate == cityCore || !candidate.HasComponent(ecs.BuildingC) {
		return false
	}
	currentBuilding := ecs.BuildingC.Get(candidate)
	if strings.TrimSpace(currentBuilding.Owner) != strings.TrimSpace(e.OldOwnerID) {
		return false
	}
	return building.ResolveCityID(candidate) == e.CityID
}

func destroyCapitalCityCore(world donburi.World, state *domain.GameState, nodeID string, conquerorFaction string) {
	nodeEntry, ok := findNodeByID(world, state, nodeID)
	if !ok || !nodeEntry.HasComponent(ecs.BuildingC) {
		return
	}
	currentBuilding := ecs.BuildingC.Get(nodeEntry)
	ownerState, ok := state.Players[currentBuilding.Owner]
	if !ok || ownerState == nil {
		return
	}
	if cityID := ecs.ResolveCityID(nodeEntry); cityID == "" || cityID != ownerState.CapitalCityID {
		return
	}
	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = conquerorFaction
	currentBuilding.Owner = conquerorFaction
	state.IsOver = true
	state.WinnerID = conquerorFaction
	state.OverReason = "city_core_destroyed"
	state.RefreshStructuredModel()
}

func NearestOwnedCityID(state *domain.GameState, playerID string, pos domain.Position, fallback string) string {
	if state == nil {
		return strings.TrimSpace(fallback)
	}
	playerState := state.Players[playerID]
	if playerState == nil || len(playerState.Cities) == 0 {
		return strings.TrimSpace(fallback)
	}
	bestID := strings.TrimSpace(fallback)
	bestDistance := -1
	for cityID, city := range playerState.Cities {
		if city == nil || strings.TrimSpace(city.CoreNodeID) == "" {
			continue
		}
		entry, ok := state.GetNode(city.CoreNodeID)
		if !ok {
			continue
		}
		cityPos := ecs.PositionC.Get(entry)
		distance := geometry.AxialDistance(pos, domain.Position{Q: cityPos.Q, R: cityPos.R})
		if bestDistance == -1 || distance < bestDistance {
			bestDistance = distance
			bestID = cityID
		}
	}
	return strings.TrimSpace(bestID)
}
