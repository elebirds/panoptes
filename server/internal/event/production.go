// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现事件模型的生产结算逻辑。

package event

import (
	"fmt"
	"strconv"

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
}

// Apply creates the building and charges the player-global inventory.
func (e BuildingBuiltEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok {
		return
	}
	if nodeEntry.HasComponent(ecs.BuildingC) {
		return
	}
	ecs.CreateBuilding(world, e.BuildingType, e.Owner, e.CityID, nodeEntry)
	state.ConsumeResources(e.Owner, e.CityID, e.Cost)
}

func (e BuildingBuiltEvent) Kind() string { return "building_built" }

func (e BuildingBuiltEvent) String() string {
	return fmt.Sprintf("BuildingBuiltEvent node=%s type=%s owner=%s city=%s", e.NodeID, e.BuildingType, e.Owner, e.CityID)
}

type ResourceProducedEvent struct {
	NodeID       string
	ResourceType string
	Amount       int
	Owner        string
	CityID       string
}

func (e ResourceProducedEvent) Apply(_ donburi.World, state *domain.GameState) {
	state.AddResource(e.Owner, domain.ResourceKey(e.ResourceType), e.Amount)
}

func (e ResourceProducedEvent) Kind() string { return "resource_produced" }

func (e ResourceProducedEvent) String() string {
	return fmt.Sprintf("ResourceProducedEvent node=%s owner=%s city=%s %s=+%d", e.NodeID, e.Owner, e.CityID, e.ResourceType, e.Amount)
}

type ResourceFlowedEvent struct {
	FromNodeID string
	ToNodeID   string
	Resources  domain.ResourceBag
}

func (e ResourceFlowedEvent) Apply(donburi.World, *domain.GameState) {}

func (e ResourceFlowedEvent) Kind() string { return "resource_flowed" }

func (e ResourceFlowedEvent) String() string {
	return fmt.Sprintf("ResourceFlowedEvent from=%s to=%s", e.FromNodeID, e.ToNodeID)
}

type RoadBuiltEvent struct {
	FromNode string
	ToNode   string
	Owner    string
	Cost     int
}

func (e RoadBuiltEvent) Apply(world donburi.World, state *domain.GameState) {
	fromEntry, okFrom := findNodeByID(world, state, e.FromNode)
	toEntry, okTo := findNodeByID(world, state, e.ToNode)
	if okFrom {
		n := ecs.NodeC.Get(fromEntry)
		n.HasRoad = true
	}
	if okTo {
		n := ecs.NodeC.Get(toEntry)
		n.HasRoad = true
	}
	if okFrom && okTo {
		fromPos := ecs.PositionC.Get(fromEntry)
		toPos := ecs.PositionC.Get(toEntry)
		x, y := fromPos.X, fromPos.Y
		for x != toPos.X {
			if toPos.X > x {
				x++
			} else {
				x--
			}
			markRoadAt(world, domain.Position{X: x, Y: y})
		}
		for y != toPos.Y {
			if toPos.Y > y {
				y++
			} else {
				y--
			}
			markRoadAt(world, domain.Position{X: x, Y: y})
		}
	}
	state.ConsumeResources(e.Owner, "", domain.ResourceBag{domain.ResourceIndustryOutput: e.Cost})
}

func (e RoadBuiltEvent) Kind() string { return "road_built" }

func (e RoadBuiltEvent) String() string {
	return fmt.Sprintf("RoadBuiltEvent %s->%s owner=%s cost=%d", e.FromNode, e.ToNode, e.Owner, e.Cost)
}

type UnitProducedEvent struct {
	NodeID   string
	UnitType string
	Faction  string
	CityID   string
	Count    int
	Cost     domain.ResourceBag
}

func (e UnitProducedEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok {
		return
	}
	state.ConsumeResources(e.Faction, e.CityID, e.Cost)
	pos := ecs.PositionC.Get(nodeEntry)
	for i := 0; i < e.Count; i++ {
		ecs.CreateUnit(world, e.UnitType, e.Faction, domain.Position{X: pos.X, Y: pos.Y})
	}
}

func (e UnitProducedEvent) Kind() string { return "unit_produced" }

func (e UnitProducedEvent) String() string {
	return fmt.Sprintf("UnitProducedEvent node=%s type=%s city=%s count=%d", e.NodeID, e.UnitType, e.CityID, e.Count)
}

type PointBudgetRefreshedEvent struct {
	PlayerID string
	Key      domain.PointKey
	Amount   int
}

func (e PointBudgetRefreshedEvent) Apply(_ donburi.World, state *domain.GameState) {
	if e.Amount <= 0 {
		return
	}
	state.RefreshPointBudget(e.PlayerID, e.Key, e.Amount)
}

func (e PointBudgetRefreshedEvent) Kind() string { return "point_budget_refreshed" }

func (e PointBudgetRefreshedEvent) String() string {
	return fmt.Sprintf("PointBudgetRefreshedEvent player=%s key=%s amount=%d", e.PlayerID, e.Key, e.Amount)
}

type PointSpentEvent struct {
	PlayerID string
	Key      domain.PointKey
	Amount   int
	Reason   string
}

func (e PointSpentEvent) Apply(_ donburi.World, state *domain.GameState) {
	if e.Amount <= 0 {
		return
	}
	_ = state.ConsumePoints(e.PlayerID, domain.PointBag{
		e.Key: e.Amount,
	})
}

func (e PointSpentEvent) Kind() string { return "point_spent" }

func (e PointSpentEvent) String() string {
	return fmt.Sprintf("PointSpentEvent player=%s key=%s amount=%d reason=%s", e.PlayerID, e.Key, e.Amount, e.Reason)
}

type BuildSkippedEvent struct {
	PlayerID     string
	NodeID       string
	BuildingType string
	Reason       string
}

func (e BuildSkippedEvent) Apply(donburi.World, *domain.GameState) {}

func (e BuildSkippedEvent) Kind() string { return "building_skipped" }

func (e BuildSkippedEvent) String() string {
	return fmt.Sprintf("BuildSkippedEvent player=%s node=%s type=%s reason=%s", e.PlayerID, e.NodeID, e.BuildingType, e.Reason)
}

type IndustryOutputRefreshedEvent struct {
	PlayerID string
	Amount   int
}

func (e IndustryOutputRefreshedEvent) Apply(_ donburi.World, state *domain.GameState) {
	if e.Amount <= 0 {
		return
	}
	state.RefreshPointBudget(e.PlayerID, domain.PointIndustryOutput, e.Amount)
}

func (e IndustryOutputRefreshedEvent) Kind() string { return "industry_output_refreshed" }

func (e IndustryOutputRefreshedEvent) String() string {
	return fmt.Sprintf("IndustryOutputRefreshedEvent player=%s amount=%d", e.PlayerID, e.Amount)
}

type UpkeepPaidEvent struct {
	PlayerID     string
	FoodConsumed int
}

func (e UpkeepPaidEvent) Apply(world donburi.World, state *domain.GameState) {
	playerState, ok := state.Players[e.PlayerID]
	if !ok {
		return
	}
	state.ConsumeResources(e.PlayerID, "", domain.ResourceBag{domain.ResourceFood: e.FoodConsumed})
	if playerState.Resources.Get(domain.ResourceFood) == 0 {
		markFactionStarving(world, e.PlayerID)
	}
}

func (e UpkeepPaidEvent) Kind() string { return "upkeep_paid" }

func (e UpkeepPaidEvent) String() string {
	return fmt.Sprintf("UpkeepPaidEvent player=%s food=%d", e.PlayerID, e.FoodConsumed)
}

type UnitStarvingEvent struct {
	UnitID        string
	DamagePerTurn int
}

func (e UnitStarvingEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findUnitByID(world, e.UnitID)
	if !ok {
		return
	}
	if !entry.HasComponent(ecs.StarvingC) {
		entry.AddComponent(ecs.StarvingC)
		ecs.StarvingC.SetValue(entry, ecs.StarvingComp{TurnsStarving: 1})
	} else {
		s := ecs.StarvingC.Get(entry)
		s.TurnsStarving++
	}
	stats := ecs.UnitStatsC.Get(entry)
	stats.HP -= e.DamagePerTurn
	if stats.HP <= 0 {
		pos := ecs.PositionC.Get(entry)
		UnitDiedEvent{UnitID: e.UnitID, KillerID: "starvation", Pos: domain.Position{X: pos.X, Y: pos.Y}}.Apply(world, state)
	}
}

func (e UnitStarvingEvent) Kind() string { return "unit_starving" }

func (e UnitStarvingEvent) String() string {
	return fmt.Sprintf("UnitStarvingEvent unit=%s damage=%d", e.UnitID, e.DamagePerTurn)
}

type BuildingDeactivatedEvent struct {
	NodeID string
	Reason string
}

func (e BuildingDeactivatedEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok {
		return
	}
	if !nodeEntry.HasComponent(ecs.BuildingStateC) {
		nodeEntry.AddComponent(ecs.BuildingStateC)
	}
	ecs.BuildingStateC.SetValue(nodeEntry, ecs.BuildingStateComp{
		Disabled:       true,
		DisabledReason: e.Reason,
		Status:         "disabled",
	})
	if nodeEntry.HasComponent(ecs.BuildingOperationC) {
		operation := ecs.BuildingOperationC.Get(nodeEntry)
		operation.BlockedReason = e.Reason
	}
}

func (e BuildingDeactivatedEvent) Kind() string { return "building_deactivated" }

func (e BuildingDeactivatedEvent) String() string {
	return fmt.Sprintf("BuildingDeactivatedEvent node=%s reason=%s", e.NodeID, e.Reason)
}

func markRoadAt(world donburi.World, pos domain.Position) {
	entry, ok := domain.GetNodeAt(world, pos)
	if !ok {
		return
	}
	n := ecs.NodeC.Get(entry)
	n.HasRoad = true
}

func markFactionStarving(world donburi.World, faction string) {
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		stats := ecs.UnitStatsC.Get(entry)
		if stats.Faction != faction {
			return
		}
		if !entry.HasComponent(ecs.StarvingC) {
			entry.AddComponent(ecs.StarvingC)
			ecs.StarvingC.SetValue(entry, ecs.StarvingComp{TurnsStarving: 1})
			return
		}
		s := ecs.StarvingC.Get(entry)
		s.TurnsStarving++
	})
}

func formatResourceBagData(bag domain.ResourceBag) map[string]string {
	out := make(map[string]string, len(bag))
	for _, key := range bag.Keys() {
		out[string(key)] = strconv.Itoa(bag.Get(key))
	}
	return out
}
