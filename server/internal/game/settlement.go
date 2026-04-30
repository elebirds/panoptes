// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的回合结算编排逻辑。

package game

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
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
		FreezeOrders: func(*domain.GameState) {
			room.lockUnitResolutionOrders()
		},
		RefreshActiveMarches: func(*domain.GameState) {
			room.refreshActiveMarchesAfterSettlement()
		},
		MapActionEvents: func(*domain.GameState) []event.Event {
			return room.plannedMapActionEvents()
		},
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

func (r *GameRoom) lockUnitResolutionOrders() {
	state := r.State()
	if state == nil {
		return
	}
	if state.TurnRuntime.Resolving.UnitOrders == nil {
		state.TurnRuntime.Resolving.UnitOrders = make(map[string]domain.UnitResolutionOrder)
	}
	clear(state.TurnRuntime.Resolving.UnitOrders)

	for unitID, march := range state.TurnRuntime.Resolving.ActiveMarches {
		state.TurnRuntime.Resolving.UnitOrders[unitID] = domain.UnitResolutionOrder{
			PlayerID:     march.PlayerID,
			UnitID:       unitID,
			Action:       domain.UnitResolutionActionMove,
			TargetNodeID: march.DestinationNodeID,
			PathNodeIDs:  append([]string(nil), march.LastPreview.PathNodeIDs...),
		}
	}

	for unitID, directive := range state.TurnRuntime.Planning.UnitOrders {
		order := gameorders.FromDirective(directive)
		if resolutionOrder, ok := order.ToResolutionOrder(); ok {
			if resolutionOrder.Action == domain.UnitResolutionActionMove {
				if march, ok := state.TurnRuntime.Resolving.ActiveMarches[unitID]; ok && len(march.LastPreview.PathNodeIDs) > 0 {
					resolutionOrder.TargetNodeID = march.DestinationNodeID
					resolutionOrder.PathNodeIDs = append([]string(nil), march.LastPreview.PathNodeIDs...)
				} else if preview, ok := r.buildRoutePreview(unitID, resolutionOrder.TargetNodeID); ok {
					resolutionOrder.PathNodeIDs = append([]string(nil), preview.PathNodeIDs...)
				}
			}
			state.TurnRuntime.Resolving.UnitOrders[unitID] = resolutionOrder.Normalized()
			continue
		}

		if gameorders.UnitAction(order.Action) == gameorders.ActionSettleCity && strings.TrimSpace(order.TargetNodeID) != "" {
			state.TurnRuntime.Resolving.UnitOrders[unitID] = domain.UnitResolutionOrder{
				PlayerID:     order.PlayerID,
				UnitID:       order.UnitID,
				Action:       domain.UnitResolutionActionMove,
				TargetNodeID: order.TargetNodeID,
			}
		}
	}
}
