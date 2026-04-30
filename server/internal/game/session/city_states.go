// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局会话城市状态初始化辅助函数。

package session

import (
	"strings"

	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

func (r *Runtime) initializeCityStates() {
	if r == nil || r.state == nil || r.state.World == nil {
		return
	}

	ecs.NodesWithBuilding(r.state.World).Each(r.state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}

		building := ecs.BuildingC.Get(entry)
		if !strings.EqualFold(string(building.Type), "city_core") {
			return
		}

		node := ecs.NodeC.Get(entry)
		playerID := strings.TrimSpace(building.Owner)
		if playerID == "" {
			playerID = strings.TrimSpace(node.Owner)
		}
		if playerID == "" {
			playerID = strings.TrimSpace(node.TerritoryOwner)
		}
		if playerID == "" {
			return
		}

		playerState := r.state.Players[playerID]
		if playerState == nil {
			return
		}
		cityState := r.state.EnsureCityState(playerID, node.ID)
		if cityState != nil {
			cityState.CoreNodeID = node.ID
			cityState.OwnerID = playerID
		}
		if r.state.Map != nil {
			if spawnPos, ok := r.state.Map.PlayerSpawns[playerID]; ok {
				pos := ecs.PositionC.Get(entry)
				if pos.Q == spawnPos.Q && pos.R == spawnPos.R {
					playerState.CapitalCityID = node.ID
					playerState.CapitalCityCoreHP = building.HP
				}
			}
		}
	})
}
