// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现单位结算引擎的快照阶段逻辑。

package combat

import (
	"sort"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type SnapshotPhase struct{}

func (SnapshotPhase) Apply(ctx *ResolutionContext) {
	snapshot := CombatSnapshot{
		Units:          make(map[string]SnapshotUnit),
		Structures:     make(map[string]SnapshotStructure),
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
			caps = inferCapabilities(entry, stats.Type)
		}

		// 快照阶段就把缺省指令归一成 hold，避免后续阶段重复兜底。
		order, ok := ctx.State.TurnRuntime.Resolving.UnitOrders[stats.ID]
		if !ok {
			order = domain.UnitResolutionOrder{PlayerID: stats.Faction, UnitID: stats.ID, Action: domain.UnitResolutionActionHold}
		}
		order = order.Normalized()
		attack := effectiveUnitAttack(ctx.State, stats.Faction, stats.Type, stats.Attack)
		moveRange := effectiveUnitMoveRange(ctx.State, stats.Faction, stats.Type, stats.Speed)

		unit := SnapshotUnit{
			UnitID:       stats.ID,
			PlayerID:     stats.Faction,
			Type:         stats.Type,
			Position:     domain.Position{X: pos.X, Y: pos.Y},
			HP:           stats.HP,
			MaxHP:        stats.MaxHP,
			Attack:       attack,
			AttackRange:  stats.AttackRange,
			MoveRange:    moveRange,
			Movement:     buildMovementProfile(stats.Type, caps, moveRange),
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
		cityID := ecs.ResolveCityID(entry)
		isCityCore := entry.HasComponent(ecs.CityCoreC)
		isCapitalCore := false
		if isCityCore && ctx.State != nil {
			if ownerState, ok := ctx.State.Players[building.Owner]; ok && ownerState != nil && cityID != "" && cityID == ownerState.CapitalCityID {
				isCapitalCore = true
			}
		}
		snapshot.Structures[node.ID] = SnapshotStructure{
			NodeID:        node.ID,
			PlayerID:      building.Owner,
			CityID:        cityID,
			Type:          building.Type,
			Position:      domain.Position{X: pos.X, Y: pos.Y},
			HP:            building.HP,
			MaxHP:         building.MaxHP,
			IsCityCore:    isCityCore,
			IsCapitalCore: isCapitalCore,
		}
		ctx.CurrentStructureHP[node.ID] = building.HP
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

func inferCapabilities(entry *donburi.Entry, unitType domain.UnitType) domain.UnitCapabilities {
	// 兼容旧存档/旧测试中尚未挂 UnitCapabilities 组件的单位。
	// 这是迁移保护逻辑，正常新单位应优先使用静态数据生成后的能力组件。
	canAttackStructures := false
	if cfg, ok := staticdata.Default().GetUnit(string(unitType)); ok {
		canAttackStructures = cfg.Flags.CanAttackStructures
	}
	return domain.UnitCapabilities{
		Melee:               !entry.HasComponent(ecs.RangedAbilityC),
		Ranged:              entry.HasComponent(ecs.RangedAbilityC),
		Charge:              entry.HasComponent(ecs.ChargeAbilityC),
		Siege:               entry.HasComponent(ecs.SiegeAbilityC),
		CanAttackStructures: canAttackStructures,
		DestroyRoad:         entry.HasComponent(ecs.DestroyAbilityC),
	}
}
