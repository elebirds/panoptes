// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载领域事件写入逻辑拆分后的事件族或事件辅助逻辑。

package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

type BuildingBuiltEvent struct {
	NodeID       string
	BuildingType string
	Owner        string
	CityID       string
	Cost         domain.ResourceBag
	OnlineOnTurn int
}

func (e BuildingBuiltEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok {
		return
	}
	if nodeEntry.HasComponent(ecs.BuildingC) {
		return
	}
	ecs.CreateBuilding(world, e.BuildingType, e.Owner, e.CityID, nodeEntry)
	if state != nil {
		state.RefreshBuildingMaxHPAtEntry(nodeEntry)
		onlineOnTurn := e.OnlineOnTurn
		if onlineOnTurn <= 0 {
			onlineOnTurn = state.Turn + 1
		}
		domain.SetBuildingLifecycleState(nodeEntry, domain.BuildingStatusDisabled, "pending_activation", onlineOnTurn)
		state.ConsumeResources(e.Owner, "", e.Cost)
	}
}

func (e BuildingBuiltEvent) Kind() string { return "building_built" }

func (e BuildingBuiltEvent) String() string {
	return fmt.Sprintf("BuildingBuiltEvent node=%s type=%s owner=%s city=%s", e.NodeID, e.BuildingType, e.Owner, e.CityID)
}

type BuildSkippedEvent struct {
	PlayerID     string
	NodeID       string
	BuildingType string
	Reason       string
}

type BuildingRepairedEvent struct {
	NodeID string
	Owner  string
}

func (e BuildingRepairedEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !nodeEntry.HasComponent(ecs.BuildingC) {
		return
	}
	building := ecs.BuildingC.Get(nodeEntry)
	if building.MaxHP <= 0 && state != nil {
		state.RefreshBuildingMaxHPAtEntry(nodeEntry)
	}
	if building.MaxHP > 0 {
		building.HP = building.MaxHP
	}
	domain.SetBuildingLifecycleState(nodeEntry, domain.BuildingStatusIdle, "", 0)
}

func (e BuildingRepairedEvent) Kind() string { return "building_repaired" }

func (e BuildingRepairedEvent) String() string {
	return fmt.Sprintf("BuildingRepairedEvent node=%s owner=%s", e.NodeID, e.Owner)
}

func (e BuildSkippedEvent) Apply(donburi.World, *domain.GameState) {}

func (e BuildSkippedEvent) Kind() string { return "building_skipped" }

func (e BuildSkippedEvent) String() string {
	return fmt.Sprintf("BuildSkippedEvent player=%s node=%s type=%s reason=%s", e.PlayerID, e.NodeID, e.BuildingType, e.Reason)
}
