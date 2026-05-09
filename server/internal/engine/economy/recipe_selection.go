// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Recipe selection settlement stage.

package economy

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

// recipe 选择阶段只做两件事：
// 1. 校验本回合切配方是否合法；
// 2. 产出 RecipeSelectionChangedEvent，让新的 operation state 先落地。
// 真正的资源消耗和进度推进放到后续 RecipeProgressStage。
func collectRecipeSelectionEvents(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	if state == nil {
		return events
	}
	for _, selection := range state.TurnRuntime.Planning.RecipeSelections {
		if strings.TrimSpace(selection.RecipeID) == "" {
			validation := ValidateRecipeCancellation(state, selection.PlayerID, selection.NodeID)
			if !validation.OK {
				events = append(events, event.RecipeSkippedEvent{
					NodeID: selection.NodeID,
					Reason: validation.ErrorCode,
				})
				continue
			}
			events = append(events, event.RecipeSelectionChangedEvent{
				NodeID: selection.NodeID,
			})
			events = append(events, event.BuildingStatusChangedEvent{
				NodeID: selection.NodeID,
				Status: domain.BuildingStatusIdle,
			})
			continue
		}
		validation := ValidateRecipeSelection(state, selection.PlayerID, selection.NodeID, selection.RecipeID)
		if !validation.OK {
			events = append(events, event.RecipeSkippedEvent{
				NodeID:   selection.NodeID,
				RecipeID: selection.RecipeID,
				Reason:   validation.ErrorCode,
			})
			continue
		}
		building := ecs.BuildingC.Get(validation.NodeEntry)
		requiredTurns := state.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerRecipeWorkAmount), validation.Recipe.ID, "", validation.Recipe.WorkAmount)
		if requiredTurns <= 0 {
			requiredTurns = 1
		}
		events = append(events, event.RecipeSelectionChangedEvent{
			NodeID: selection.NodeID, RecipeID: selection.RecipeID, RequiredTurns: requiredTurns,
		})
		events = append(events, event.BuildingStatusChangedEvent{
			NodeID: selection.NodeID,
			Status: "active",
		})
	}
	return events
}
