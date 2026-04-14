// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现单位结算引擎的战斗判定逻辑。

package combat

import (
	"math"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type BattleSystem struct{}

func (s *BattleSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	hpAfter := make(map[string]int)
	events := make([]event.Event, 0)
	for _, conflict := range state.TurnRuntime.Resolving.Conflicts {
		aEntry, okA := findUnit(world, conflict.UnitAID)
		bEntry, okB := findUnit(world, conflict.UnitBID)
		if !okA || !okB {
			continue
		}
		aStats := ecs.UnitStatsC.Get(aEntry)
		bStats := ecs.UnitStatsC.Get(bEntry)

		aHP := readHP(hpAfter, aStats.ID, aStats.HP)
		bHP := readHP(hpAfter, bStats.ID, bStats.HP)
		if aHP <= 0 || bHP <= 0 {
			continue
		}

		dmgToB := computeDamage(aStats.Type, bStats.Type, aStats.Attack, conflict.Location, state)
		dmgToA := computeDamage(bStats.Type, aStats.Type, bStats.Attack, conflict.Location, state)

		bHPAfter := maxInt(0, bHP-dmgToB)
		aHPAfter := maxInt(0, aHP-dmgToA)
		hpAfter[bStats.ID] = bHPAfter
		hpAfter[aStats.ID] = aHPAfter

		events = append(events, event.UnitDamagedEvent{UnitID: bStats.ID, Damage: dmgToB, HPAfter: bHPAfter, Source: "combat"})
		events = append(events, event.UnitDamagedEvent{UnitID: aStats.ID, Damage: dmgToA, HPAfter: aHPAfter, Source: "combat"})

		if bHPAfter <= 0 {
			events = append(events, event.UnitDiedEvent{UnitID: bStats.ID, KillerID: aStats.ID, Pos: conflict.Location})
		}
		if aHPAfter <= 0 {
			events = append(events, event.UnitDiedEvent{UnitID: aStats.ID, KillerID: bStats.ID, Pos: conflict.Location})
		}
	}
	return events
}

func findUnit(world donburi.World, unitID string) (*donburi.Entry, bool) {
	var found *donburi.Entry
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found != nil {
			return
		}
		if ecs.UnitStatsC.Get(entry).ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}

func readHP(m map[string]int, unitID string, defaultHP int) int {
	if hp, ok := m[unitID]; ok {
		return hp
	}
	return defaultHP
}

func computeDamage(attackerType, defenderType domain.UnitType, attack int, pos domain.Position, state *domain.GameState) int {
	if attack <= 0 {
		return 0
	}
	multiplier := 1.0
	if cfg, ok := staticdata.Default().GetUnit(string(attackerType)); ok {
		if m, ok := cfg.Multipliers[string(defenderType)]; ok && m > 0 {
			multiplier = m
		}
	}
	terrainFactor := 1.0
	if nodeEntry, ok := domain.GetNodeAt(state.World, pos); ok {
		node := ecs.NodeC.Get(nodeEntry)
		if terrain, ok := staticdata.Default().GetTerrain(string(node.Terrain)); ok {
			terrainFactor = math.Max(0.2, 1-terrain.DefenseBonus+terrain.AttackPenalty)
		}
	}
	dmg := int(math.Round(float64(attack) * multiplier * terrainFactor))
	if dmg < 1 {
		return 1
	}
	return dmg
}
