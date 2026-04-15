// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的配方结算逻辑。

package production

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type RecipeSystem struct{}

func (s *RecipeSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	// 先结算“玩家这轮切了什么配方”，再按切换后的运行态推进生产。
	applySelections(world, state, &events)

	ecs.NodesWithBuilding(world).Each(world, func(entry *donburi.Entry) {
		if !entry.HasComponent(ecs.BuildingOperationC) {
			return
		}
		building := ecs.BuildingC.Get(entry)
		operation := ecs.BuildingOperationC.Get(entry)
		if operation.SelectedRecipeID == "" || !state.IsRecipeUnlocked(building.Owner, operation.SelectedRecipeID) {
			return
		}

		recipe, ok := staticdata.Default().GetRecipe(operation.SelectedRecipeID)
		if !ok {
			return
		}

		cost := state.ApplyResourceModifiers(building.Owner, string(staticdata.ModifierTriggerRecipeResourceInput), recipe.ID, toResourceBag(recipe.ResourceInputs))
		requiredProgress := state.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerRecipeWorkAmount), recipe.ID, "", recipe.WorkAmount)
		if requiredProgress <= 0 {
			requiredProgress = 1
		}
		if !state.CanAffordFromCastle(building.Owner, building.CastleID, cost) {
			events = append(events, event.RecipeProgressedEvent{
				NodeID:        ecs.NodeC.Get(entry).ID,
				ProgressTurns: operation.ProgressTurns,
				RequiredTurns: requiredProgress,
				BlockedReason: "insufficient_resources",
			})
			return
		}

		progressStep := state.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerRecipeBaseProgress), recipe.ID, "", recipe.BaseProgress)
		if progressStep <= 0 {
			progressStep = 1
		}
		progress := operation.ProgressTurns + progressStep
		if progress >= requiredProgress {
			events = append(events, event.RecipeCompletedEvent{
				NodeID:        ecs.NodeC.Get(entry).ID,
				Owner:         building.Owner,
				CastleID:      building.CastleID,
				RequiredTurns: requiredProgress,
				Cost:          cost,
				Resources:     state.ApplyResourceModifiers(building.Owner, string(staticdata.ModifierTriggerRecipeResourceOutput), recipe.ID, toResourceBag(recipe.Outputs.Resources)),
				Units:         append([]string(nil), recipe.Outputs.Units...),
			})
			return
		}

		events = append(events, event.RecipeProgressedEvent{
			NodeID: ecs.NodeC.Get(entry).ID, ProgressTurns: progress, RequiredTurns: requiredProgress,
		})
	})

	return events
}

func applySelections(world donburi.World, state *domain.GameState, events *[]event.Event) {
	if state == nil {
		return
	}
	for _, selection := range state.TurnRuntime.Planning.RecipeSelections {
		if !state.IsRecipeUnlocked(selection.PlayerID, selection.RecipeID) {
			continue
		}
		recipe, ok := staticdata.Default().GetRecipe(selection.RecipeID)
		if !ok {
			continue
		}
		entry, ok := state.GetNode(selection.NodeID)
		if !ok || !entry.HasComponent(ecs.BuildingC) {
			continue
		}
		building := ecs.BuildingC.Get(entry)
		if building.Owner != selection.PlayerID {
			continue
		}
		requiredTurns := state.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerRecipeWorkAmount), recipe.ID, "", recipe.WorkAmount)
		if requiredTurns <= 0 {
			requiredTurns = 1
		}
		*events = append(*events, event.RecipeSelectionChangedEvent{
			NodeID: selection.NodeID, RecipeID: selection.RecipeID, RequiredTurns: requiredTurns,
		})
	}
}
