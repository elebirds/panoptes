// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载后端测试/调试场景的构造与布置辅助逻辑。

package scenario

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func newState(gameID string, catalog *staticdata.Catalog, playerIDs []string, usernames []string, mapBundle *staticdata.MapRuntimeBundle) (*domain.GameState, error) {
	if catalog == nil {
		return nil, fmt.Errorf("catalog is nil")
	}
	if mapBundle == nil {
		return nil, fmt.Errorf("map bundle is nil")
	}

	staticdata.SetDefault(catalog)
	world := donburi.NewWorld()
	mapData := maploader.InitWorldFromMap(world, mapBundle, playerIDs)
	state := domain.NewGameState(gameID, playerIDs, usernames, mapData)
	state.World = world
	state.GameID = gameID
	initializeScenarioCities(state)
	return state, nil
}

func initializeScenarioCities(state *domain.GameState) {
	if state == nil || state.World == nil {
		return
	}
	ecs.NodesWithBuilding(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil || !entry.HasComponent(ecs.BuildingC) {
			return
		}
		building := ecs.BuildingC.Get(entry)
		if string(building.Type) != "city_core" || building.Owner == "" {
			return
		}
		node := ecs.NodeC.Get(entry)
		cityState := state.EnsureCityState(building.Owner, node.ID)
		if cityState != nil {
			cityState.OnlineOnTurn = 0
		}
		if state.Map == nil {
			return
		}
		corePos := ecs.PositionC.Get(entry)
		if spawnPos, ok := state.Map.PlayerSpawns[building.Owner]; ok && spawnPos.Q == corePos.Q && spawnPos.R == corePos.R {
			playerState := state.Players[building.Owner]
			if playerState != nil {
				playerState.CapitalCityID = node.ID
				playerState.CapitalCityCoreHP = building.HP
			}
		}
	})
}
