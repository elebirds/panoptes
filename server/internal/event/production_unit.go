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
	origin := domain.Position{Q: pos.Q, R: pos.R}
	for i := 0; i < e.Count; i++ {
		spawnPos, ok := domain.ResolveUnitSpawnPosition(state, origin)
		if !ok {
			continue
		}
		ecs.CreateUnit(world, e.UnitType, e.Faction, spawnPos)
	}
}

func (e UnitProducedEvent) Kind() string { return "unit_produced" }

func (e UnitProducedEvent) String() string {
	return fmt.Sprintf("UnitProducedEvent node=%s type=%s city=%s count=%d", e.NodeID, e.UnitType, e.CityID, e.Count)
}

type UnitStarvingEvent struct {
	UnitID        string
	DamagePerTurn int
}

func (e UnitStarvingEvent) Apply(world donburi.World, _ *domain.GameState) {
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
		removeUnitByID(world, e.UnitID)
	}
}

func (e UnitStarvingEvent) Kind() string { return "unit_starving" }

func (e UnitStarvingEvent) String() string {
	return fmt.Sprintf("UnitStarvingEvent unit=%s damage=%d", e.UnitID, e.DamagePerTurn)
}

func removeUnitByID(world donburi.World, unitID string) {
	entry, ok := findUnitByID(world, unitID)
	if !ok {
		return
	}
	world.Remove(entry.Entity())
}
