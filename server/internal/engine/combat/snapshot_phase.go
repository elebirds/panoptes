package combat

import (
	"sort"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

type SnapshotPhase struct{}

func (SnapshotPhase) Apply(ctx *ResolutionContext) {
	snapshot := CombatSnapshot{
		Units:          make(map[string]SnapshotUnit),
		BlockSources:   make(map[domain.Position]BlockSource),
		OrderedUnitIDs: make([]string, 0),
	}

	ecs.AllUnits(ctx.World).Each(ctx.World, func(entry *donburi.Entry) {
		stats := ecs.UnitStatsC.Get(entry)
		pos := ecs.PositionC.Get(entry)
		caps := domain.UnitCapabilities{}
		if entry.HasComponent(ecs.UnitCapabilitiesC) {
			caps = *ecs.UnitCapabilitiesC.Get(entry)
		} else {
			caps = inferCapabilities(entry)
		}

		// 快照阶段就把缺省指令归一成 hold，避免后续阶段重复兜底。
		order, ok := ctx.State.PendingCombatOrders[stats.ID]
		if !ok {
			order = domain.CombatOrder{PlayerID: stats.Faction, UnitID: stats.ID, Action: domain.CombatActionHold}
		}
		order = order.Normalized()

		unit := SnapshotUnit{
			UnitID:       stats.ID,
			PlayerID:     stats.Faction,
			Type:         stats.Type,
			Position:     domain.Position{X: pos.X, Y: pos.Y},
			HP:           stats.HP,
			MaxHP:        stats.MaxHP,
			Attack:       stats.Attack,
			AttackRange:  stats.AttackRange,
			MoveRange:    stats.Speed,
			Capabilities: caps,
			Order:        order,
		}
		snapshot.Units[stats.ID] = unit
		snapshot.OrderedUnitIDs = append(snapshot.OrderedUnitIDs, stats.ID)
		// 单位起始占位直接进入阻断快照。
		// 根据 V1 规格，这个阻断信息在整次结算中不会因为单位本回合移动而更新。
		snapshot.BlockSources[unit.Position] = BlockSource{
			Kind:     "unit",
			Owner:    unit.PlayerID,
			Position: unit.Position,
			UnitID:   unit.UnitID,
		}
		ctx.CurrentHP[unit.UnitID] = unit.HP
	})

	ecs.NodesWithBuilding(ctx.World).Each(ctx.World, func(entry *donburi.Entry) {
		building := ecs.BuildingC.Get(entry)
		if building.Owner == "" {
			return
		}
		pos := ecs.PositionC.Get(entry)
		node := ecs.NodeC.Get(entry)
		// 建筑阻断和单位阻断统一进入同一张表，后续规则只通过 BlockRule 读取。
		snapshot.BlockSources[domain.Position{X: pos.X, Y: pos.Y}] = BlockSource{
			Kind:     "building",
			Owner:    building.Owner,
			Position: domain.Position{X: pos.X, Y: pos.Y},
			NodeID:   node.ID,
		}
	})

	sort.Strings(snapshot.OrderedUnitIDs)
	ctx.Snapshot = snapshot
}

func inferCapabilities(entry *donburi.Entry) domain.UnitCapabilities {
	// 兼容旧存档/旧测试中尚未挂 UnitCapabilities 组件的单位。
	// 这是迁移保护逻辑，正常新单位应优先使用静态数据生成后的能力组件。
	return domain.UnitCapabilities{
		Melee:       !entry.HasComponent(ecs.RangedAbilityC),
		Ranged:      entry.HasComponent(ecs.RangedAbilityC),
		Charge:      entry.HasComponent(ecs.ChargeAbilityC),
		Siege:       entry.HasComponent(ecs.SiegeAbilityC),
		DestroyRoad: entry.HasComponent(ecs.DestroyAbilityC),
	}
}
