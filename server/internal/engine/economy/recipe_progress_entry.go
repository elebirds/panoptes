// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
package economy

import (
	"math"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func appendRecipeProgressForEntry(events *[]event.Event, entry *donburi.Entry, state *domain.GameState, budget recipeProgressBudget) {
	buildingComp := ecs.BuildingC.Get(entry)
	nodeID := ecs.NodeC.Get(entry).ID
	operation := ecs.BuildingOperationC.Get(entry)
	selectedRecipeID := operation.SelectedRecipeID
	requiredTurns := operation.RequiredTurns
	if selectedRecipeID == "" {
		return
	}
	if !domain.BuildingOperationalAtTurn(entry, state.Turn) {
		appendRecipeDisabled(events, nodeID, selectedRecipeID, operation, requiredTurns, "building_disabled")
		return
	}
	if serviceCityID := ecs.ResolveServiceCityID(entry); serviceCityID != "" && !state.IsCityOnlineForPlayer(buildingComp.Owner, serviceCityID) {
		appendRecipeDisabled(events, nodeID, selectedRecipeID, operation, requiredTurns, "building_disabled")
		return
	}
	serviceCityID := ecs.ResolveServiceCityID(entry)
	if !state.IsRecipeUnlocked(buildingComp.Owner, selectedRecipeID) {
		appendRecipeBlocked(events, nodeID, selectedRecipeID, operation, requiredTurns, "invalid_recipe_selection")
		return
	}

	recipe, ok := staticdata.Default().GetRecipe(selectedRecipeID)
	if !ok {
		appendRecipeBlocked(events, nodeID, selectedRecipeID, operation, requiredTurns, "invalid_recipe_selection")
		return
	}

	resourceCost := state.ApplyResourceModifiers(buildingComp.Owner, string(staticdata.ModifierTriggerRecipeResourceInput), recipe.ID, toResourceBag(recipe.ResourceInputs))
	pointCost := state.ApplyPointModifiers(buildingComp.Owner, string(staticdata.ModifierTriggerRecipePointInput), recipe.ID, toPointBag(recipe.PointInputs))
	requiredProgress := state.ApplyScalarModifier(buildingComp.Owner, string(staticdata.ModifierTriggerRecipeWorkAmount), recipe.ID, "", recipe.WorkAmount)
	if requiredProgress <= 0 {
		requiredProgress = 1
	}
	wasBlocked := operation.BlockedReason != ""
	availableResources := budget.availableResources(state, buildingComp.Owner, serviceCityID, resourceCost)
	resourceRatio := affordabilityRatioResources(availableResources, resourceCost)
	pointRatio := affordabilityRatioPoints(budget.points[buildingComp.Owner], pointCost)
	// efficiency 是这套 recipe 模型的核心：它不是“要么全速运行，要么停工”，
	// 而是允许资源或点数不足时按比例低效推进。
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
		appendRecipeProgressBlocked(events, nodeID, requiredTurns, requiredProgress, operation, blockedReason, wasBlocked)
		return
	}

	progressStep := state.ApplyScalarModifier(buildingComp.Owner, string(staticdata.ModifierTriggerRecipeBaseProgress), recipe.ID, "", recipe.BaseProgress)
	if progressStep <= 0 {
		progressStep = 1
	}
	// 进度使用定点整数而不是纯 float 保存：
	// ProgressTurns 表示已经完成的整回合进度，ProgressRemainder 表示不足 1 turn 的余量。
	// 这样既能支持比例推进，又能避免长期累计浮点误差。
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
	// 累计消耗是“按总进度比例反推目标总消耗”，再和 operation 上已消耗量做差。
	// 这样可以保证低效推进时，本回合只补扣新增那一部分消耗。
	resourceDelta := subtractResourceBags(targetConsumedResources, operation.ConsumedResources)
	pointDelta := subtractPointBags(targetConsumedPoints, operation.ConsumedPoints)
	if !budget.points[buildingComp.Owner].CanAfford(pointDelta) {
		blockedReason := blockedReasonForRatios(resourceRatio, pointRatio, resourceCost, pointCost)
		if blockedReason == "" {
			blockedReason = "insufficient_points"
		}
		appendRecipeBlocked(events, nodeID, selectedRecipeID, operation, requiredProgress, blockedReason)
		return
	}
	resourceFlows, resourcesOK := budget.consumeResources(state, buildingComp.Owner, serviceCityID, nodeID, resourceDelta)
	if !resourcesOK {
		blockedReason := blockedReasonForRatios(resourceRatio, pointRatio, resourceCost, pointCost)
		if blockedReason == "" {
			blockedReason = "insufficient_resources"
		}
		appendRecipeBlocked(events, nodeID, selectedRecipeID, operation, requiredProgress, blockedReason)
		return
	}
	*events = append(*events, resourceFlows...)
	for _, key := range pointDelta.Keys() {
		budget.points[buildingComp.Owner].AddAmount(key, -pointDelta.Get(key))
		*events = append(*events, event.PointSpentEvent{
			PlayerID: buildingComp.Owner,
			Key:      key,
			Amount:   pointDelta.Get(key),
			Reason:   "recipe_progress",
		})
	}

	progress := nextScaled / recipeProgressScale
	remainder := nextScaled % recipeProgressScale
	if nextScaled >= maxScaled {
		appendRecipeCompleted(events, recipeCompletionEventInput{
			entry:                   entry,
			state:                   state,
			nodeID:                  nodeID,
			owner:                   buildingComp.Owner,
			recipe:                  recipe,
			requiredProgress:        requiredProgress,
			resourceCost:            resourceCost,
			targetConsumedResources: targetConsumedResources,
			targetConsumedPoints:    targetConsumedPoints,
			resourceDelta:           resourceDelta,
			progress:                progress,
			remainder:               remainder,
		})
		return
	}

	*events = append(*events, event.RecipeProgressedEvent{
		NodeID:            nodeID,
		ProgressTurns:     progress,
		RequiredTurns:     requiredProgress,
		ProgressRemainder: remainder,
		ConsumedResources: targetConsumedResources,
		ConsumedPoints:    targetConsumedPoints,
		ResourceDelta:     resourceDelta,
	})
	if wasBlocked {
		*events = append(*events, event.BuildingStatusChangedEvent{
			NodeID: nodeID,
			Status: "active",
		})
	}
}
