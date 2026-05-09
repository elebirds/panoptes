// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局会话运行时的地图与状态初始化。

package session

import (
	"fmt"
	"time"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func (r *Runtime) Initialize() error {
	catalog := staticdata.Default()
	mapID := ""
	if r.cfg != nil {
		mapID = r.cfg.MapID
	}
	if mapID == "" {
		mapID = catalog.DefaultMapID()
	}
	baseMap, err := maploader.LoadMap(catalog, mapID)
	if err != nil {
		return fmt.Errorf("load map %s: %w", mapID, err)
	}

	world := donburi.NewWorld()
	playerIDs := r.participantIDs()
	usernames := r.participantUsernames()
	seed := time.Now().UnixNano()
	runtimeMap := maploader.GenerateProceduralMap(baseMap, len(playerIDs), seed)
	if runtimeMap == nil {
		return fmt.Errorf("generate procedural map for %s", mapID)
	}
	mapData := maploader.InitWorldFromMap(world, runtimeMap, playerIDs)
	r.state = domain.NewGameState(r.ID, playerIDs, usernames, mapData)
	r.state.World = world
	r.state.RefreshStructuredModel()
	r.resetObservations()
	if err := r.bootstrapStartingPlayers(); err != nil {
		return err
	}
	r.grantDevStartingResources()
	r.initializeCityStates()
	return r.sendBootstrapMessages()
}

func (r *Runtime) InitializePrepared(state *domain.GameState) error {
	if state == nil {
		return fmt.Errorf("prepared state is nil")
	}
	if state.World == nil {
		return fmt.Errorf("prepared state world is nil")
	}
	if state.Map == nil {
		return fmt.Errorf("prepared state map is nil")
	}

	r.state = state
	r.resetObservations()
	r.normalizePreparedState()
	r.state.InitializeMinisterRoster()
	r.state.RefreshStructuredModel()
	r.grantDevStartingResources()
	r.initializeCityStates()
	return r.sendBootstrapMessages()
}

func (r *Runtime) normalizePreparedState() {
	if r == nil || r.state == nil {
		return
	}
	if r.state.GameID == "" {
		r.state.GameID = r.ID
	}
	if r.state.NodeIndex == nil {
		r.state.NodeIndex = make(map[string]donburi.Entity)
	}
	if r.state.Map != nil && r.state.Map.NodeIndex != nil && len(r.state.NodeIndex) == 0 {
		for nodeID, entity := range r.state.Map.NodeIndex {
			r.state.NodeIndex[nodeID] = entity
		}
	}
}

func (r *Runtime) resetObservations() {
	if r.observations == nil {
		r.observations = gamequery.NewObservationStore()
		return
	}
	r.observations.Reset()
}
