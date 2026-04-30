// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的行军同步与路径辅助。

package game

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/combat"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
)

func (r *GameRoom) buildRoutePreview(unitID string, destinationNodeID string) (domain.RoutePreview, bool) {
	state := r.State()
	if r == nil || state == nil {
		return domain.RoutePreview{}, false
	}
	planner := combat.NewWeightedRoutePlanner(combat.DefaultTerrainCostPolicy{})
	return planner.BuildPreview(state.World, state, unitID, destinationNodeID)
}

func (r *GameRoom) buildRoutePreviewFromPathNodeIDs(unitID string, pathNodeIDs []string) (domain.RoutePreview, bool) {
	state := r.State()
	if r == nil || state == nil {
		return domain.RoutePreview{}, false
	}
	planner := combat.NewWeightedRoutePlanner(combat.DefaultTerrainCostPolicy{})
	return planner.BuildPreviewFromPathNodeIDs(state.World, state, unitID, pathNodeIDs)
}

func (r *GameRoom) routePreviewCallbacks() gameorders.RoutePreviewCallbacks {
	if r == nil {
		return gameorders.RoutePreviewCallbacks{}
	}
	return gameorders.RoutePreviewCallbacks{
		ByDestination: r.buildRoutePreview,
		ByPath:        r.buildRoutePreviewFromPathNodeIDs,
	}
}
