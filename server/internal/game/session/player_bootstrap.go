// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局会话玩家出生点启动辅助函数。

package session

import (
	"fmt"
	"log/slog"
	"strings"

	buildingcore "github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func (r *Runtime) bootstrapStartingPlayers() error {
	if r == nil || r.state == nil || r.state.World == nil || r.state.Map == nil {
		return nil
	}

	for _, rawPlayerID := range r.participantIDs() {
		playerID := strings.TrimSpace(rawPlayerID)
		if playerID == "" {
			continue
		}

		spawnPos, ok := r.state.Map.PlayerSpawns[playerID]
		if !ok {
			continue
		}

		spawnEntry, ok := domain.GetNodeAt(r.state.World, spawnPos)
		if !ok || spawnEntry == nil {
			continue
		}

		r.ensureCapitalAtSpawn(playerID, spawnEntry)

		playerState := r.state.Players[playerID]
		capitalNodeID := ""
		if playerState != nil {
			capitalNodeID = playerState.CapitalCityID
		}
		slog.Info("runtime bootstrap player",
			"game_id", r.ID,
			"player_id", playerID,
			"capital_node", capitalNodeID,
		)
	}

	if err := r.validateBootstrapState(); err != nil {
		return fmt.Errorf("runtime bootstrap invariant failed: %w", err)
	}
	return nil
}

func (r *Runtime) ensureCapitalAtSpawn(playerID string, spawnEntry *donburi.Entry) {
	if r == nil || r.state == nil || spawnEntry == nil {
		return
	}

	if !spawnEntry.HasComponent(ecs.BuildingC) {
		ecs.CreateBuilding(r.state.World, "city_core", playerID, ecs.NodeC.Get(spawnEntry).ID, spawnEntry)
		r.state.RefreshBuildingMaxHPAtEntry(spawnEntry)
	}
	if !spawnEntry.HasComponent(ecs.BuildingC) {
		return
	}

	building := ecs.BuildingC.Get(spawnEntry)
	if !strings.EqualFold(string(building.Type), "city_core") {
		return
	}

	cityID := ecs.NodeC.Get(spawnEntry).ID
	footprintEntries, _, reason := ecs.TerritoryFootprint(r.state, spawnEntry)
	if reason == "" {
		for _, entry := range footprintEntries {
			if entry == nil {
				continue
			}
			node := ecs.NodeC.Get(entry)
			node.Owner = playerID
			node.TerritoryOwner = playerID
		}
	} else {
		node := ecs.NodeC.Get(spawnEntry)
		node.Owner = playerID
		node.TerritoryOwner = playerID
	}

	building.Owner = playerID
	buildingcore.SetBinding(spawnEntry, domain.BuildingScopeCityCore, cityID, cityID)
	playerState := r.state.Players[playerID]
	if playerState == nil {
		return
	}
	playerState.CapitalCityID = cityID
	playerState.CapitalCityCoreHP = building.HP
	cityState := r.state.EnsureCityState(playerID, cityID)
	if cityState != nil {
		cityState.CoreNodeID = cityID
		cityState.OwnerID = playerID
		cityState.OnlineOnTurn = 0
	}

	r.ensureStartingInfantryAtSpawn(playerID, spawnEntry)
}

func (r *Runtime) ensureStartingInfantryAtSpawn(playerID string, spawnEntry *donburi.Entry) {
	if r == nil || r.state == nil || r.state.World == nil || spawnEntry == nil {
		return
	}
	if _, ok := staticdata.Default().GetUnit(string(domain.UnitTypeInfantry)); !ok {
		return
	}

	pos := ecs.PositionC.Get(spawnEntry)
	spawnPos := domain.Position{Q: pos.Q, R: pos.R}
	infantryPos, ok := domain.ResolveUnitSpawnPosition(r.state, spawnPos)
	if !ok {
		return
	}
	// 起始赠送兵同样遵守全局不堆叠规则：
	// 只要目标格上已经有任何单位，这次补给就直接跳过，避免 bootstrap 偷偷制造例外。
	if domain.HasUnitAtNode(r.state.World, infantryPos) {
		return
	}
	ecs.CreateUnit(r.state.World, string(domain.UnitTypeInfantry), playerID, infantryPos)
}

func (r *Runtime) validateBootstrapState() error {
	if r == nil || r.state == nil {
		return fmt.Errorf("state missing")
	}

	playerIDs := r.participantIDs()
	if len(playerIDs) == 0 {
		return fmt.Errorf("no participants available for bootstrap")
	}

	for _, rawPlayerID := range playerIDs {
		playerID := strings.TrimSpace(rawPlayerID)
		if playerID == "" {
			continue
		}

		playerState := r.state.Players[playerID]
		if playerState == nil {
			return fmt.Errorf("player %s state missing", playerID)
		}
		if strings.TrimSpace(playerState.CapitalCityID) == "" {
			return fmt.Errorf("player %s capital city missing", playerID)
		}

		capitalEntry, ok := r.state.GetNode(playerState.CapitalCityID)
		if !ok || capitalEntry == nil {
			return fmt.Errorf("player %s capital node %s missing", playerID, playerState.CapitalCityID)
		}
		if !capitalEntry.HasComponent(ecs.BuildingC) {
			return fmt.Errorf("player %s capital node %s missing city_core building", playerID, playerState.CapitalCityID)
		}
		building := ecs.BuildingC.Get(capitalEntry)
		if !strings.EqualFold(string(building.Type), "city_core") {
			return fmt.Errorf("player %s capital node %s has building %s", playerID, playerState.CapitalCityID, building.Type)
		}
		if strings.TrimSpace(building.Owner) != playerID {
			return fmt.Errorf("player %s capital node %s owned by %s", playerID, playerState.CapitalCityID, building.Owner)
		}
	}
	return nil
}
