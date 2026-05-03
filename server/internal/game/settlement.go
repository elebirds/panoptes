// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的回合结算编排逻辑。

package game

import (
	"github.com/elebirds/panoptes/internal/domain"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	gameresolution "github.com/elebirds/panoptes/internal/game/resolution"
)

// RunTurnResolution executes the unified Turn V2 resolving pipeline.
func RunTurnResolution(room *GameRoom) {
	if room == nil || room.State() == nil {
		return
	}

	collector := gameresolution.NewTurnResolutionRunner().Run(room.State(), gameresolution.RunnerHooks{
		PlanningCommitEvents: gameresolution.BuildPlanningCommitEvents,
		FreezeOrders: func(state *domain.GameState) {
			gameorders.BuildResolvingUnitOrders(state, room.routePreviewCallbacks())
		},
		RefreshActiveMarches: func(state *domain.GameState) {
			gameorders.RefreshActiveMarchesAfterSettlement(state, room.routePreviewCallbacks())
		},
		MapActionEvents: gameorders.BuildMapActionEvents,
	})
	room.broadcastGameSync(collector)
	if room.IsDevMode() {
		if hooks := currentDebugHooks(); hooks.DumpStateSummary != nil {
			hooks.DumpStateSummary(room.State())
		}
	}
	room.checkGameOver()

	room.State().TurnRuntime.ClearPostResolutionScratch()
}
