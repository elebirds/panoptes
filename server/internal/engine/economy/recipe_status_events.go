// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Recipe status event helpers.

package economy

import (
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
)

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

func appendRecipeProgressBlocked(events *[]event.Event, nodeID string, requiredTurns int, requiredProgress int, operation *ecs.BuildingOperationComp, blockedReason string, wasBlocked bool) {
	if events == nil || operation == nil {
		return
	}
	*events = append(*events, event.RecipeProgressedEvent{
		NodeID:            nodeID,
		ProgressTurns:     operation.ProgressTurns,
		RequiredTurns:     max(requiredTurns, requiredProgress),
		BlockedReason:     blockedReason,
		ProgressRemainder: operation.ProgressRemainder,
		ConsumedResources: cloneResourceBag(operation.ConsumedResources),
		ConsumedPoints:    clonePointBag(operation.ConsumedPoints),
	})
	if !wasBlocked || operation.BlockedReason != blockedReason {
		*events = append(*events, event.BuildingStatusChangedEvent{
			NodeID: nodeID,
			Status: "blocked",
			Reason: blockedReason,
		})
	}
}
