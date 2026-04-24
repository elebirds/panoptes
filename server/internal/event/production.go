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
	OnlineOnTurn int
}

// Apply 负责真正创建建筑，并把建造成本扣到玩家全局库存。
// 新建筑不会立刻上线，而是统一进入 pending_activation，等到下一回合再转为可运行状态。
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
		state.ConsumeResources(e.Owner, e.CityID, e.Cost)
	}
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
		current := domain.Position{Q: fromPos.Q, R: fromPos.R}
		target := domain.Position{Q: toPos.Q, R: toPos.R}
		for current != target {
			next, ok := nextRoadStep(world, current, target)
			if !ok {
				break
			}
			current = next
			markRoadAt(world, current)
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
		ecs.CreateUnit(world, e.UnitType, e.Faction, domain.Position{Q: pos.Q, R: pos.R})
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
	// 刷新预算的语义是“设置到精确值”，包括 0。
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
	// 点数只影响本回合 resolving 里的临时预算，不会像资源那样成为跨回合持久库存。
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
	// 这是旧版 industry refresh 事件；当前主链已统一改用 PointBudgetRefreshedEvent。
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
		UnitDiedEvent{UnitID: e.UnitID, KillerID: "starvation", Pos: domain.Position{Q: pos.Q, R: pos.R}}.Apply(world, state)
	}
}

func (e UnitStarvingEvent) Kind() string { return "unit_starving" }

func (e UnitStarvingEvent) String() string {
	return fmt.Sprintf("UnitStarvingEvent unit=%s damage=%d", e.UnitID, e.DamagePerTurn)
}

func markRoadAt(world donburi.World, pos domain.Position) {
	entry, ok := domain.GetNodeAt(world, pos)
	if !ok {
		return
	}
	n := ecs.NodeC.Get(entry)
	n.HasRoad = true
}

func nextRoadStep(world donburi.World, current, target domain.Position) (domain.Position, bool) {
	bestDistance := current.DistanceTo(target)
	for _, candidate := range current.Neighbors() {
		if _, ok := domain.GetNodeAt(world, candidate); !ok {
			continue
		}
		distance := candidate.DistanceTo(target)
		if distance < bestDistance {
			return candidate, true
		}
	}
	return domain.Position{}, false
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
