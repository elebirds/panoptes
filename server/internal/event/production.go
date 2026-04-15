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

// Apply creates the building and charges the city that issued the build.
//
// 这里的关键变化是：建造成本不再默认从玩家公共资源池扣除，而是优先从
// BuildOrder 绑定的 CityID 对应资源池扣除，这样城市看板上的数字会和
// “哪个城市造了这个建筑”保持一致。
func (e BuildingBuiltEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok {
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

// Apply settles one resource delta into the city-scoped resource model.
//
// Amount 可以是正数（产出）也可以是负数（upkeep 扣费）。当 CityID 非空时，
// 资源直接落入对应城市；随后会同步回 player.Resources 聚合视图，兼容仍然只
// 读取玩家总资源的消息与前端逻辑。
func (e ResourceProducedEvent) Apply(_ donburi.World, state *domain.GameState) {
	state.AddResourceToCity(e.Owner, e.CityID, domain.ResourceKey(e.ResourceType), e.Amount)
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

// Apply spends industry output through the shared city aggregate path.
//
// 道路当前还没有绑定明确的 cityID，所以这里走“玩家全部城市总池扣费”的
// 兼容分支。这样至少能保证玩家总资源和城堡看板汇总结果一致。
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

// Apply spawns units and charges the military production cost to the
// originating city.
//
// 这让兵营等建筑的生产输入可以和建筑归属的城市资源池绑定，避免多个
// 城市之间错误共用一份军事生产成本。
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

type IndustryOutputRefreshedEvent struct {
	PlayerID string
	Amount   int
}

// Apply refreshes the per-city industry budget snapshot used by the current
// runtime.
//
// Chunk 1 先把静态契约切到 point/output 语义；完整的“非库存工业点结算”会在后续
// chunk 完成。这里先把旧的内部库存语义约束成“每回合刷新到本回合可用
// 的工业产出”，避免继续出现跨回合累积的旧含义。
func (e IndustryOutputRefreshedEvent) Apply(_ donburi.World, state *domain.GameState) {
	if e.Amount <= 0 {
		return
	}
	state.RefreshIndustryOutput(e.PlayerID, e.Amount, e.Amount)
}

func (e IndustryOutputRefreshedEvent) Kind() string { return "industry_output_refreshed" }

func (e IndustryOutputRefreshedEvent) String() string {
	return fmt.Sprintf("IndustryOutputRefreshedEvent player=%s amount=%d", e.PlayerID, e.Amount)
}

type UpkeepPaidEvent struct {
	PlayerID     string
	FoodConsumed int
}

// Apply settles combat food upkeep through the city aggregate path.
//
// 战斗补给目前仍然没有精确到某一座城市，因此这里从玩家全部城市的总资源中扣除。
// 扣完后若总粮食为 0，则继续触发饥饿逻辑。
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
