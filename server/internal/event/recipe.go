// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现事件模型的配方结算逻辑。

package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

type RecipeSelectionChangedEvent struct {
	NodeID        string
	RecipeID      string
	RequiredTurns int
}

func (e RecipeSelectionChangedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok {
		return
	}
	if !entry.HasComponent(ecs.BuildingOperationC) {
		entry.AddComponent(ecs.BuildingOperationC)
	}
	ecs.BuildingOperationC.SetValue(entry, ecs.BuildingOperationComp{
		SelectedRecipeID: e.RecipeID,
		RequiredTurns:    e.RequiredTurns,
	})
}

func (e RecipeSelectionChangedEvent) Kind() string { return "recipe_selected" }

func (e RecipeSelectionChangedEvent) String() string {
	return fmt.Sprintf("RecipeSelectionChangedEvent node=%s recipe=%s", e.NodeID, e.RecipeID)
}

type RecipeDelayedEvent struct {
	NodeID        string
	DelayTurns    int
	RequiredTurns int
	Reason        string
}

func (e RecipeDelayedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !entry.HasComponent(ecs.BuildingOperationC) {
		return
	}
	operation := ecs.BuildingOperationC.Get(entry)
	// 延时是累加态，不重置已有 progress；这样“差一点完成但断料”不会白做。
	operation.DelayTurns += e.DelayTurns
	operation.RequiredTurns = e.RequiredTurns
	operation.BlockedReason = e.Reason
	ecs.BuildingOperationC.SetValue(entry, *operation)
}

func (e RecipeDelayedEvent) Kind() string { return "recipe_delayed" }

func (e RecipeDelayedEvent) String() string {
	return fmt.Sprintf("RecipeDelayedEvent node=%s delay=%d reason=%s", e.NodeID, e.DelayTurns, e.Reason)
}

type RecipeProgressedEvent struct {
	NodeID        string
	ProgressTurns int
	RequiredTurns int
	BlockedReason string
}

func (e RecipeProgressedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !entry.HasComponent(ecs.BuildingOperationC) {
		return
	}
	operation := ecs.BuildingOperationC.Get(entry)
	operation.ProgressTurns = e.ProgressTurns
	operation.RequiredTurns = e.RequiredTurns
	operation.BlockedReason = e.BlockedReason
	ecs.BuildingOperationC.SetValue(entry, *operation)
}

func (e RecipeProgressedEvent) Kind() string { return "recipe_progressed" }

func (e RecipeProgressedEvent) String() string {
	return fmt.Sprintf("RecipeProgressedEvent node=%s progress=%d", e.NodeID, e.ProgressTurns)
}

type RecipeCompletedEvent struct {
	NodeID        string
	Owner         string
	CastleID      string
	RequiredTurns int
	Cost          domain.ResourceBag
	Resources     domain.ResourceBag
	Units         []string
}

func (e RecipeCompletedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if ok && entry.HasComponent(ecs.BuildingOperationC) {
		operation := ecs.BuildingOperationC.Get(entry)
		operation.ProgressTurns = 0
		operation.DelayTurns = 0
		operation.RequiredTurns = e.RequiredTurns
		operation.BlockedReason = ""
		ecs.BuildingOperationC.SetValue(entry, *operation)
	}
	// 配方完成时才统一扣输入、发输出，避免“进度走了一半先扣料”带来额外回滚问题。
	if e.Cost != nil {
		state.ConsumeResources(e.Owner, e.CastleID, e.Cost)
	}
	for _, key := range e.Resources.Keys() {
		state.AddResourceToCastle(e.Owner, e.CastleID, key, e.Resources.Get(key))
	}
	if len(e.Units) == 0 || !ok {
		return
	}
	pos := ecs.PositionC.Get(entry)
	for _, unitType := range e.Units {
		ecs.CreateUnit(world, unitType, e.Owner, domain.Position{X: pos.X, Y: pos.Y})
	}
}

func (e RecipeCompletedEvent) Kind() string { return "recipe_completed" }

func (e RecipeCompletedEvent) String() string {
	return fmt.Sprintf("RecipeCompletedEvent node=%s owner=%s", e.NodeID, e.Owner)
}
