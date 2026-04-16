// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的配方结算逻辑。

package economy

import (
	"math"

	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

const recipeProgressScale = 1000

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

func runRecipeProgress(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	if state == nil {
		return events
	}
	// 这里使用 per-player 的模拟库存，而不是一边遍历一边直接扣权威状态。
	// 这样同回合多个建筑会按遍历顺序共享同一份预算，但直到事件 Apply 前都不会把 ECS 写脏。
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
		buildingComp := ecs.BuildingC.Get(entry)
		nodeID := ecs.NodeC.Get(entry).ID
		operation := ecs.BuildingOperationC.Get(entry)
		selectedRecipeID := operation.SelectedRecipeID
		requiredTurns := operation.RequiredTurns
		if selectedRecipeID == "" {
			return
		}
		if !domain.BuildingOperationalAtTurn(entry, state.Turn) {
			appendRecipeDisabled(&events, nodeID, selectedRecipeID, operation, requiredTurns, "building_disabled")
			return
		}
		if serviceCityID := ecs.ResolveServiceCityID(entry); serviceCityID != "" && !state.IsCityOnlineForPlayer(buildingComp.Owner, serviceCityID) {
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
		if !state.IsRecipeUnlocked(buildingComp.Owner, selectedRecipeID) {
			appendRecipeBlocked(&events, nodeID, selectedRecipeID, operation, requiredTurns, "invalid_recipe_selection")
			return
		}

		recipe, ok := staticdata.Default().GetRecipe(selectedRecipeID)
		if !ok {
			appendRecipeBlocked(&events, nodeID, selectedRecipeID, operation, requiredTurns, "invalid_recipe_selection")
			return
		}

		resourceCost := state.ApplyResourceModifiers(buildingComp.Owner, string(staticdata.ModifierTriggerRecipeResourceInput), recipe.ID, toResourceBag(recipe.ResourceInputs))
		pointCost := state.ApplyPointModifiers(buildingComp.Owner, string(staticdata.ModifierTriggerRecipePointInput), recipe.ID, toPointBag(recipe.PointInputs))
		requiredProgress := state.ApplyScalarModifier(buildingComp.Owner, string(staticdata.ModifierTriggerRecipeWorkAmount), recipe.ID, "", recipe.WorkAmount)
		if requiredProgress <= 0 {
			requiredProgress = 1
		}
		wasBlocked := operation.BlockedReason != ""
		resourceRatio := affordabilityRatioResources(simulatedResources[buildingComp.Owner], resourceCost)
		pointRatio := affordabilityRatioPoints(simulatedPoints[buildingComp.Owner], pointCost)
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
		if !simulatedResources[buildingComp.Owner].CanAfford(resourceDelta) || !simulatedPoints[buildingComp.Owner].CanAfford(pointDelta) {
			blockedReason := blockedReasonForRatios(resourceRatio, pointRatio, resourceCost, pointCost)
			if blockedReason == "" {
				blockedReason = "insufficient_resources"
			}
			appendRecipeBlocked(&events, nodeID, selectedRecipeID, operation, requiredProgress, blockedReason)
			return
		}
		simulatedResources[buildingComp.Owner] = simulatedResources[buildingComp.Owner].Sub(resourceDelta)
		for _, key := range pointDelta.Keys() {
			simulatedPoints[buildingComp.Owner].AddAmount(key, -pointDelta.Get(key))
			events = append(events, event.PointSpentEvent{
				PlayerID: buildingComp.Owner,
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
				Owner:         buildingComp.Owner,
				CityID:        building.ResolveCityID(entry),
				RequiredTurns: requiredProgress,
				Cost:          resourceCost,
				Resources:     state.ApplyResourceModifiers(buildingComp.Owner, string(staticdata.ModifierTriggerRecipeResourceOutput), recipe.ID, toResourceBag(recipe.Outputs.Resources)),
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

func appendRecipeBlocked(events *[]event.Event, nodeID string, recipeID string, operation *ecs.BuildingOperationComp, requiredTurns int, reason string) {
	if events == nil || operation == nil {
		return
	}
	if requiredTurns <= 0 {
		requiredTurns = max(operation.RequiredTurns, 1)
	}
	// blocked 会同时留下两类事件：
	// 1. RecipeSkippedEvent 说明这回合没法继续运行；
	// 2. RecipeProgressedEvent 回传当前累计进度与阻塞原因，方便 settlement 与客户端看到“停在什么位置”。
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
	// disabled 和 blocked 都不会推进进度，但 disabled 不额外改 building status，
	// 因为建筑生命周期阶段已经决定了它当前处于 disabled/takeover/contested 等运行态。
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
	// 返回“所有输入资源里最短板的可支付比例”，用于和 point ratio 共同决定 recipe 的低效推进速度。
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
	// 把“完整 recipe 的总成本”按当前累计进度折算成“理论上此刻应累计消耗多少”。
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
