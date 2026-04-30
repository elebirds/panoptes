// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载领域事件写入逻辑拆分后的事件族或事件辅助逻辑。

package event

import (
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

type FacilityTakeoverProgressedEvent struct {
	NodeID             string
	ControllerPlayerID string
	Progress           int
	Required           int
	Status             string
	Reason             string
}

func (e FacilityTakeoverProgressedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !entry.HasComponent(ecs.BuildingC) {
		return
	}
	if !entry.HasComponent(ecs.FacilityTakeoverC) {
		entry.AddComponent(ecs.FacilityTakeoverC)
		ecs.FacilityTakeoverC.SetValue(entry, ecs.FacilityTakeoverComp{})
	}
	takeover := ecs.FacilityTakeoverC.Get(entry)
	takeover.Progress = e.Progress
	if e.Required > 0 {
		takeover.Required = e.Required
	}
	takeover.Completed = false
	takeover.ControllerPlayerID = strings.TrimSpace(e.ControllerPlayerID)

	node := ecs.NodeC.Get(entry)
	switch {
	case strings.TrimSpace(e.ControllerPlayerID) != "":
		node.Owner = strings.TrimSpace(e.ControllerPlayerID)
	case domain.NormalizeBuildingStatus(e.Status) == domain.BuildingStatusContested:
		node.Owner = ""
	default:
		node.Owner = ecs.BuildingC.Get(entry).Owner
	}

	status := domain.NormalizeBuildingStatus(e.Status)
	if status == domain.BuildingStatusIdle && strings.TrimSpace(e.ControllerPlayerID) != "" {
		status = domain.BuildingStatusTakeover
	}
	domain.SetBuildingLifecycleState(entry, status, e.Reason, 0)
}

func (e FacilityTakeoverProgressedEvent) Kind() string { return "facility_takeover_progressed" }

func (e FacilityTakeoverProgressedEvent) String() string {
	return fmt.Sprintf("FacilityTakeoverProgressedEvent node=%s controller=%s progress=%d", e.NodeID, e.ControllerPlayerID, e.Progress)
}

type FacilityTakeoverCompletedEvent struct {
	NodeID        string
	NewOwnerID    string
	ServiceCityID string
	OnlineOnTurn  int
}

func (e FacilityTakeoverCompletedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !entry.HasComponent(ecs.BuildingC) {
		return
	}
	currentBuilding := ecs.BuildingC.Get(entry)
	currentBuilding.Owner = e.NewOwnerID

	node := ecs.NodeC.Get(entry)
	node.Owner = e.NewOwnerID
	node.TerritoryOwner = e.NewOwnerID

	building.Rebind(entry, e.ServiceCityID, e.ServiceCityID)
	if entry.HasComponent(ecs.FacilityTakeoverC) {
		takeover := ecs.FacilityTakeoverC.Get(entry)
		takeover.Progress = 0
		takeover.ControllerPlayerID = e.NewOwnerID
		takeover.Completed = true
	}
	domain.SetBuildingLifecycleState(entry, domain.BuildingStatusDisabled, "pending_activation", e.OnlineOnTurn)
}

func (e FacilityTakeoverCompletedEvent) Kind() string { return "facility_takeover_completed" }

func (e FacilityTakeoverCompletedEvent) String() string {
	return fmt.Sprintf("FacilityTakeoverCompletedEvent node=%s owner=%s city=%s", e.NodeID, e.NewOwnerID, e.ServiceCityID)
}

type BuildingRuinedEvent struct {
	NodeID     string
	NewOwnerID string
	Reason     string
}

func (e BuildingRuinedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !entry.HasComponent(ecs.BuildingC) {
		return
	}
	if strings.TrimSpace(e.NewOwnerID) != "" {
		building := ecs.BuildingC.Get(entry)
		building.Owner = e.NewOwnerID
		node := ecs.NodeC.Get(entry)
		node.Owner = e.NewOwnerID
		node.TerritoryOwner = e.NewOwnerID
	}
	reason := strings.TrimSpace(e.Reason)
	if reason == "" {
		reason = "city_captured"
	}
	domain.SetBuildingLifecycleState(entry, domain.BuildingStatusRuined, reason, 0)
}

func (e BuildingRuinedEvent) Kind() string { return "building_ruined" }

func (e BuildingRuinedEvent) String() string {
	return fmt.Sprintf("BuildingRuinedEvent node=%s owner=%s", e.NodeID, e.NewOwnerID)
}
