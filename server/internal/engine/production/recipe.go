// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的配方结算逻辑。

package production

import (
	"math"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type RecipeSystem struct{}

const recipeProgressScale = 1000

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
		if !domain.BuildingOperationalAtTurn(entry, state.Turn) {
			appendRecipeDisabled(&events, nodeID, selectedRecipeID, operation, requiredTurns, "building_disabled")
			return
		}
		if serviceCityID := ecs.ResolveServiceCityID(entry); serviceCityID != "" && !state.IsCityOnlineForPlayer(building.Owner, serviceCityID) {
			events = append(events, event.RecipeSkippedEvent{
				NodeID:   nodeID,
				RecipeID: selectedRecipeID,
				Reason:   "building_disabled",
			})
			events = append(events, event.RecipeProgressedEvent{
				NodeID:            nodeID,
				ProgressTurns:     operation.ProgressTurns,
				RequiredTurns:     max(requiredTurns, 1),
				BlockedReason:     "building_disabled",
				ProgressRemainder: operation.ProgressRemainder,
				ConsumedResources: cloneResourceBag(operation.ConsumedResources),
				ConsumedPoints:    clonePointBag(operation.ConsumedPoints),
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
		resourceRatio := affordabilityRatioResources(simulatedResources[building.Owner], resourceCost)
		pointRatio := affordabilityRatioPoints(simulatedPoints[building.Owner], pointCost)
		efficiency := math.Min(resourceRatio, pointRatio)
		if efficiency < 0 {
			efficiency = 0
		}
		if efficiency > 1 {
			efficiency = 1
		}
		if efficiency == 0 {
			blockedReason := blockedReasonForRatios(resourceRatio, pointRatio, resourceCost, pointCost)
			if blockedReason == "" {
				blockedReason = "building_disabled"
			}
			events = append(events, event.RecipeProgressedEvent{
				NodeID:            nodeID,
				ProgressTurns:     operation.ProgressTurns,
				RequiredTurns:     max(requiredTurns, requiredProgress),
				BlockedReason:     blockedReason,
				ProgressRemainder: operation.ProgressRemainder,
				ConsumedResources: cloneResourceBag(operation.ConsumedResources),
				ConsumedPoints:    clonePointBag(operation.ConsumedPoints),
			})
			if !wasBlocked || operation.BlockedReason != blockedReason {
				events = append(events, event.BuildingStatusChangedEvent{
					NodeID: nodeID,
					Status: "blocked",
					Reason: blockedReason,
				})
			}
			return
			return
		}

		progressStep := state.ApplyScalarModifier(building.Owner, string(staticdata.ModifierTriggerRecipeBaseProgress), recipe.ID, "", recipe.BaseProgress)
		if progressStep <= 0 {
			progressStep = 1
		}
		currentScaled := operation.ProgressTurns*recipeProgressScale + operation.ProgressRemainder
		maxScaled := requiredProgress * recipeProgressScale
		deltaScaled := int(math.Round(float64(progressStep*recipeProgressScale) * efficiency))
		if deltaScaled <= 0 && efficiency > 0 {
			deltaScaled = 1
		}
		nextScaled := currentScaled + deltaScaled
		if nextScaled > maxScaled {
			nextScaled = maxScaled
		}

		targetConsumedResources := proportionalResourceBag(resourceCost, nextScaled, maxScaled)
		targetConsumedPoints := proportionalPointBag(pointCost, nextScaled, maxScaled)
		resourceDelta := subtractResourceBags(targetConsumedResources, operation.ConsumedResources)
		pointDelta := subtractPointBags(targetConsumedPoints, operation.ConsumedPoints)
		if !simulatedResources[building.Owner].CanAfford(resourceDelta) || !simulatedPoints[building.Owner].CanAfford(pointDelta) {
			blockedReason := blockedReasonForRatios(resourceRatio, pointRatio, resourceCost, pointCost)
			if blockedReason == "" {
				blockedReason = "insufficient_resources"
			}
			appendRecipeBlocked(&events, nodeID, selectedRecipeID, operation, requiredProgress, blockedReason)
			return
		}
		simulatedResources[building.Owner] = simulatedResources[building.Owner].Sub(resourceDelta)
		for _, key := range pointDelta.Keys() {
			simulatedPoints[building.Owner].AddAmount(key, -pointDelta.Get(key))
			events = append(events, event.PointSpentEvent{
				PlayerID: building.Owner,
				Key:      key,
				Amount:   pointDelta.Get(key),
				Reason:   "recipe_progress",
			})
		}

		progress := nextScaled / recipeProgressScale
		remainder := nextScaled % recipeProgressScale
		if nextScaled >= maxScaled {
			events = append(events, event.RecipeProgressedEvent{
				NodeID:            nodeID,
				ProgressTurns:     progress,
				RequiredTurns:     requiredProgress,
				ProgressRemainder: remainder,
				ConsumedResources: targetConsumedResources,
				ConsumedPoints:    targetConsumedPoints,
				ResourceDelta:     resourceDelta,
			})
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
			NodeID:            nodeID,
			ProgressTurns:     progress,
			RequiredTurns:     requiredProgress,
			ProgressRemainder: remainder,
			ConsumedResources: targetConsumedResources,
			ConsumedPoints:    targetConsumedPoints,
			ResourceDelta:     resourceDelta,
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
		NodeID:            nodeID,
		ProgressTurns:     operation.ProgressTurns,
		RequiredTurns:     requiredTurns,
		BlockedReason:     reason,
		ProgressRemainder: operation.ProgressRemainder,
		ConsumedResources: cloneResourceBag(operation.ConsumedResources),
		ConsumedPoints:    clonePointBag(operation.ConsumedPoints),
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

func appendRecipeDisabled(events *[]event.Event, nodeID string, recipeID string, operation *ecs.BuildingOperationComp, requiredTurns int, reason string) {
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
		NodeID:            nodeID,
		ProgressTurns:     operation.ProgressTurns,
		RequiredTurns:     requiredTurns,
		BlockedReason:     reason,
		ProgressRemainder: operation.ProgressRemainder,
		ConsumedResources: cloneResourceBag(operation.ConsumedResources),
		ConsumedPoints:    clonePointBag(operation.ConsumedPoints),
	})
}

func affordabilityRatioResources(available domain.ResourceBag, total domain.ResourceBag) float64 {
	if total == nil || total.IsZero() {
		return 1
	}
	ratio := 1.0
	for _, key := range total.Keys() {
		required := total.Get(key)
		if required <= 0 {
			continue
		}
		current := available.Get(key)
		currentRatio := float64(current) / float64(required)
		if currentRatio < ratio {
			ratio = currentRatio
		}
	}
	if ratio < 0 {
		return 0
	}
	return ratio
}

func affordabilityRatioPoints(available domain.PointBag, total domain.PointBag) float64 {
	if total == nil || total.IsZero() {
		return 1
	}
	ratio := 1.0
	for _, key := range total.Keys() {
		required := total.Get(key)
		if required <= 0 {
			continue
		}
		current := available.Get(key)
		currentRatio := float64(current) / float64(required)
		if currentRatio < ratio {
			ratio = currentRatio
		}
	}
	if ratio < 0 {
		return 0
	}
	return ratio
}

func blockedReasonForRatios(resourceRatio float64, pointRatio float64, resources domain.ResourceBag, points domain.PointBag) string {
	if (resources != nil && !resources.IsZero()) && resourceRatio <= 0 {
		return "insufficient_resources"
	}
	if (points != nil && !points.IsZero()) && pointRatio <= 0 {
		return "insufficient_points"
	}
	if resourceRatio < pointRatio {
		return "insufficient_resources"
	}
	if pointRatio < resourceRatio {
		return "insufficient_points"
	}
	return ""
}

func proportionalResourceBag(total domain.ResourceBag, scaledProgress int, scaledRequired int) domain.ResourceBag {
	if total == nil || total.IsZero() || scaledRequired <= 0 {
		return domain.NewResourceBag()
	}
	out := domain.NewResourceBag()
	for _, key := range total.Keys() {
		amount := int(float64(total.Get(key)) * float64(scaledProgress) / float64(scaledRequired))
		if amount > total.Get(key) {
			amount = total.Get(key)
		}
		out.Set(key, amount)
	}
	return out
}

func proportionalPointBag(total domain.PointBag, scaledProgress int, scaledRequired int) domain.PointBag {
	if total == nil || total.IsZero() || scaledRequired <= 0 {
		return domain.NewPointBag()
	}
	out := domain.NewPointBag()
	for _, key := range total.Keys() {
		amount := int(float64(total.Get(key)) * float64(scaledProgress) / float64(scaledRequired))
		if amount > total.Get(key) {
			amount = total.Get(key)
		}
		out.Set(key, amount)
	}
	return out
}

func subtractResourceBags(total domain.ResourceBag, consumed domain.ResourceBag) domain.ResourceBag {
	out := domain.NewResourceBag()
	for _, key := range total.Keys() {
		delta := total.Get(key) - consumed.Get(key)
		out.Set(key, delta)
	}
	return out
}

func subtractPointBags(total domain.PointBag, consumed domain.PointBag) domain.PointBag {
	out := domain.NewPointBag()
	for _, key := range total.Keys() {
		delta := total.Get(key) - consumed.Get(key)
		out.Set(key, delta)
	}
	return out
}

func cloneResourceBag(src domain.ResourceBag) domain.ResourceBag {
	if src == nil {
		return domain.NewResourceBag()
	}
	return src.Clone()
}

func clonePointBag(src domain.PointBag) domain.PointBag {
	if src == nil {
		return domain.NewPointBag()
	}
	return src.Clone()
}
