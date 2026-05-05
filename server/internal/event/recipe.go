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

type RecipeSkippedEvent struct {
	NodeID   string
	RecipeID string
	Reason   string
}

func (e RecipeSkippedEvent) Apply(donburi.World, *domain.GameState) {}

func (e RecipeSkippedEvent) Kind() string { return "recipe_skipped" }

func (e RecipeSkippedEvent) String() string {
	return fmt.Sprintf("RecipeSkippedEvent node=%s recipe=%s reason=%s", e.NodeID, e.RecipeID, e.Reason)
}

func (e RecipeSelectionChangedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok {
		return
	}
	if !entry.HasComponent(ecs.BuildingOperationC) {
		entry.AddComponent(ecs.BuildingOperationC)
	}
	// 切换 recipe 会重置 operation state。
	// 旧 recipe 的累计进度、累计消耗和阻塞原因都不会继承给新 recipe。
	ecs.BuildingOperationC.SetValue(entry, ecs.BuildingOperationComp{
		SelectedRecipeID:  e.RecipeID,
		RequiredTurns:     e.RequiredTurns,
		ConsumedResources: domain.NewResourceBag(),
		ConsumedPoints:    domain.NewPointBag(),
	})
}

func (e RecipeSelectionChangedEvent) Kind() string { return "recipe_selected" }

func (e RecipeSelectionChangedEvent) String() string {
	return fmt.Sprintf("RecipeSelectionChangedEvent node=%s recipe=%s", e.NodeID, e.RecipeID)
}

type BuildingStatusChangedEvent struct {
	NodeID       string
	Status       string
	Reason       string
	OnlineOnTurn int
}

func (e BuildingStatusChangedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok {
		return
	}
	// building status 描述的是建筑当前能否运行及原因，本身不承载 recipe 的进度值。
	domain.SetBuildingLifecycleState(entry, e.Status, e.Reason, e.OnlineOnTurn)
}

func (e BuildingStatusChangedEvent) Kind() string { return "building_status_changed" }

func (e BuildingStatusChangedEvent) String() string {
	return fmt.Sprintf("BuildingStatusChangedEvent node=%s status=%s reason=%s", e.NodeID, e.Status, e.Reason)
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
	NodeID            string
	ProgressTurns     int
	RequiredTurns     int
	BlockedReason     string
	ProgressRemainder int
	ConsumedResources domain.ResourceBag
	ConsumedPoints    domain.PointBag
	ResourceDelta     domain.ResourceBag
}

func (e RecipeProgressedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !entry.HasComponent(ecs.BuildingOperationC) {
		return
	}
	operation := ecs.BuildingOperationC.Get(entry)
	// progressed 直接写回一整份 operation 快照，
	// 这样低效推进、阻塞、累计消耗这些状态都能在一个事件里保持一致。
	operation.ProgressTurns = e.ProgressTurns
	operation.RequiredTurns = e.RequiredTurns
	operation.BlockedReason = e.BlockedReason
	operation.ProgressRemainder = e.ProgressRemainder
	if e.ConsumedResources != nil {
		operation.ConsumedResources = e.ConsumedResources.Clone()
	}
	if e.ConsumedPoints != nil {
		operation.ConsumedPoints = e.ConsumedPoints.Clone()
	}
	ecs.BuildingOperationC.SetValue(entry, *operation)
	if state != nil && e.ResourceDelta != nil && !e.ResourceDelta.IsZero() {
		// 资源只扣本回合新增 delta，不会重复扣 operation 里已经累计过的历史消耗。
		state.ConsumeResources(ecs.BuildingC.Get(entry).Owner, ecs.ResolveServiceCityID(entry), e.ResourceDelta)
	}
}

func (e RecipeProgressedEvent) Kind() string { return "recipe_progressed" }

func (e RecipeProgressedEvent) String() string {
	return fmt.Sprintf("RecipeProgressedEvent node=%s progress=%d", e.NodeID, e.ProgressTurns)
}

type RecipeCompletedEvent struct {
	NodeID        string
	Owner         string
	CityID        string
	RequiredTurns int
	Cost          domain.ResourceBag
	Resources     domain.ResourceBag
	Units         []string
}

func (e RecipeCompletedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if ok && entry.HasComponent(ecs.BuildingOperationC) {
		operation := ecs.BuildingOperationC.Get(entry)
		// recipe 完成后会把 operation state 清回初始值，下一轮生产重新从 0 开始累计。
		operation.ProgressTurns = 0
		operation.DelayTurns = 0
		operation.RequiredTurns = e.RequiredTurns
		operation.BlockedReason = ""
		operation.ProgressRemainder = 0
		operation.ConsumedResources = domain.NewResourceBag()
		operation.ConsumedPoints = domain.NewPointBag()
		ecs.BuildingOperationC.SetValue(entry, *operation)
	}
	state.AddResourcesToCity(e.Owner, e.CityID, e.Resources)
	if len(e.Units) == 0 || !ok {
		return
	}
	pos := ecs.PositionC.Get(entry)
	origin := domain.Position{Q: pos.Q, R: pos.R}
	for _, unitType := range e.Units {
		spawnPos, ok := domain.ResolveUnitSpawnPosition(state, origin)
		if !ok {
			continue
		}
		ecs.CreateUnit(world, unitType, e.Owner, spawnPos)
	}
}

func (e RecipeCompletedEvent) Kind() string { return "recipe_completed" }

func (e RecipeCompletedEvent) String() string {
	return fmt.Sprintf("RecipeCompletedEvent node=%s owner=%s", e.NodeID, e.Owner)
}
