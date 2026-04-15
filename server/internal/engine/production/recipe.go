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
	if state == nil {
		return events
	}
	// 先结算“玩家这轮切了什么配方”，再按切换后的运行态推进生产。
	selectionOverrides := applySelections(world, state, &events)
	simulatedResources := make(map[string]domain.ResourceBag, len(state.Players))
	simulatedPoints := make(map[string]domain.PointBag, len(state.Players))
	for playerID, playerState := range state.Players {
		if playerState == nil {
			continue
		}
		simulatedResources[playerID] = playerState.Resources.Clone()
		simulatedPoints[playerID] = state.EnsurePointBudget(playerID).Clone()
	}

	ecs.NodesWithBuilding(world).Each(world, func(entry *donburi.Entry) {
		if !entry.HasComponent(ecs.BuildingOperationC) {
			return
		}
		building := ecs.BuildingC.Get(entry)
		nodeID := ecs.NodeC.Get(entry).ID
		operation := ecs.BuildingOperationC.Get(entry)
		selectedRecipeID := operation.SelectedRecipeID
		requiredTurns := operation.RequiredTurns
		if override, ok := selectionOverrides[nodeID]; ok {
			selectedRecipeID = override.RecipeID
			requiredTurns = override.RequiredTurns
		}
		if selectedRecipeID == "" {
			return
		}
		if entry.HasComponent(ecs.BuildingStateC) && ecs.BuildingStateC.Get(entry).Disabled {
			events = append(events, event.RecipeSkippedEvent{
				NodeID:   nodeID,
				RecipeID: selectedRecipeID,
				Reason:   "building_disabled",
			})
			return
		}
		if !state.IsRecipeUnlocked(building.Owner, selectedRecipeID) {
			appendRecipeBlocked(&events, nodeID, selectedRecipeID, operation, requiredTurns, "invalid_recipe_selection")
			return
		}

		recipe, ok := staticdata.Default().GetRecipe(selectedRecipeID)
		if !ok {
			appendRecipeBlocked(&events, nodeID, selectedRecipeID, operation, requiredTurns, "invalid_recipe_selection")
			return
		}

		resourceCost := state.ApplyResourceModifiers(building.Owner, string(staticdata.ModifierTriggerRecipeResourceInput), recipe.ID, toResourceBag(recipe.ResourceInputs))
		pointCost := state.ApplyPointModifiers(building.Owner, string(staticdata.ModifierTriggerRecipePointInput), recipe.ID, toPointBag(recipe.PointInputs))
		requiredProgress := state.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerRecipeWorkAmount), recipe.ID, "", recipe.WorkAmount)
		if requiredProgress <= 0 {
			requiredProgress = 1
		}
		wasBlocked := operation.BlockedReason != ""
		blockedReason := ""
		if !simulatedResources[building.Owner].CanAfford(resourceCost) {
			blockedReason = "insufficient_resources"
		} else if !simulatedPoints[building.Owner].CanAfford(pointCost) {
			blockedReason = "insufficient_points"
		}
		if blockedReason != "" {
			events = append(events, event.RecipeProgressedEvent{
				NodeID:        nodeID,
				ProgressTurns: operation.ProgressTurns,
				RequiredTurns: max(requiredTurns, requiredProgress),
				BlockedReason: blockedReason,
			})
			if !wasBlocked || operation.BlockedReason != blockedReason {
				events = append(events, event.BuildingStatusChangedEvent{
					NodeID: nodeID,
					Status: "blocked",
					Reason: blockedReason,
				})
			}
			return
		}

		for _, key := range pointCost.Keys() {
			simulatedPoints[building.Owner].AddAmount(key, -pointCost.Get(key))
			events = append(events, event.PointSpentEvent{
				PlayerID: building.Owner,
				Key:      key,
				Amount:   pointCost.Get(key),
				Reason:   "recipe_progress",
			})
		}
		progressStep := state.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerRecipeBaseProgress), recipe.ID, "", recipe.BaseProgress)
		if progressStep <= 0 {
			progressStep = 1
		}
		progress := operation.ProgressTurns + progressStep
		if progress >= requiredProgress {
			simulatedResources[building.Owner] = simulatedResources[building.Owner].Sub(resourceCost)
			events = append(events, event.RecipeCompletedEvent{
				NodeID:        nodeID,
				Owner:         building.Owner,
				CityID:        building.CityID,
				RequiredTurns: requiredProgress,
				Cost:          resourceCost,
				Resources:     state.ApplyResourceModifiers(building.Owner, string(staticdata.ModifierTriggerRecipeResourceOutput), recipe.ID, toResourceBag(recipe.Outputs.Resources)),
				Units:         append([]string(nil), recipe.Outputs.Units...),
			})
			events = append(events, event.BuildingStatusChangedEvent{
				NodeID: nodeID,
				Status: "idle",
			})
			return
		}

		events = append(events, event.RecipeProgressedEvent{
			NodeID: nodeID, ProgressTurns: progress, RequiredTurns: requiredProgress,
		})
		if wasBlocked {
			events = append(events, event.BuildingStatusChangedEvent{
				NodeID: nodeID,
				Status: "active",
			})
		}
	})

	return events
}

type recipeSelectionOverride struct {
	RecipeID      string
	RequiredTurns int
}

func applySelections(world donburi.World, state *domain.GameState, events *[]event.Event) map[string]recipeSelectionOverride {
	overrides := make(map[string]recipeSelectionOverride)
	if state == nil {
		return overrides
	}
	for _, selection := range state.TurnRuntime.Planning.RecipeSelections {
		if !state.IsRecipeUnlocked(selection.PlayerID, selection.RecipeID) {
			*events = append(*events, event.RecipeSkippedEvent{
				NodeID:   selection.NodeID,
				RecipeID: selection.RecipeID,
				Reason:   "invalid_recipe_selection",
			})
			continue
		}
		recipe, ok := staticdata.Default().GetRecipe(selection.RecipeID)
		if !ok {
			*events = append(*events, event.RecipeSkippedEvent{
				NodeID:   selection.NodeID,
				RecipeID: selection.RecipeID,
				Reason:   "invalid_recipe_selection",
			})
			continue
		}
		entry, ok := state.GetNode(selection.NodeID)
		if !ok || !entry.HasComponent(ecs.BuildingC) {
			*events = append(*events, event.RecipeSkippedEvent{
				NodeID:   selection.NodeID,
				RecipeID: selection.RecipeID,
				Reason:   "invalid_target",
			})
			continue
		}
		building := ecs.BuildingC.Get(entry)
		if building.Owner != selection.PlayerID {
			*events = append(*events, event.RecipeSkippedEvent{
				NodeID:   selection.NodeID,
				RecipeID: selection.RecipeID,
				Reason:   "unauthorized",
			})
			continue
		}
		requiredTurns := state.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerRecipeWorkAmount), recipe.ID, "", recipe.WorkAmount)
		if requiredTurns <= 0 {
			requiredTurns = 1
		}
		overrides[selection.NodeID] = recipeSelectionOverride{
			RecipeID:      selection.RecipeID,
			RequiredTurns: requiredTurns,
		}
		*events = append(*events, event.RecipeSelectionChangedEvent{
			NodeID: selection.NodeID, RecipeID: selection.RecipeID, RequiredTurns: requiredTurns,
		})
		*events = append(*events, event.BuildingStatusChangedEvent{
			NodeID: selection.NodeID,
			Status: "active",
		})
	}
	return overrides
}

func appendRecipeBlocked(events *[]event.Event, nodeID string, recipeID string, operation *ecs.BuildingOperationComp, requiredTurns int, reason string) {
	if events == nil || operation == nil {
		return
	}
	if requiredTurns <= 0 {
		requiredTurns = max(operation.RequiredTurns, 1)
	}
	*events = append(*events, event.RecipeSkippedEvent{
		NodeID:   nodeID,
		RecipeID: recipeID,
		Reason:   reason,
	})
	*events = append(*events, event.RecipeProgressedEvent{
		NodeID:        nodeID,
		ProgressTurns: operation.ProgressTurns,
		RequiredTurns: requiredTurns,
		BlockedReason: reason,
	})
	if operation.BlockedReason == reason {
		return
	}
	*events = append(*events, event.BuildingStatusChangedEvent{
		NodeID: nodeID,
		Status: "blocked",
		Reason: reason,
	})
}
