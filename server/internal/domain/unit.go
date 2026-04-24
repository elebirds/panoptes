// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义领域模型的单位领域模型。

package domain

import "github.com/yohamta/donburi"

func IsAlive(unitEntry *donburi.Entry) bool {
	return unitEntry != nil && UnitStatsC.Get(unitEntry).HP > 0
}

func CanSiege(unitEntry *donburi.Entry) bool {
	return unitEntry != nil && unitEntry.HasComponent(SiegeAbilityC)
}

func CanDestroy(unitEntry *donburi.Entry) bool {
	return unitEntry != nil && unitEntry.HasComponent(DestroyAbilityC)
}

func IsRanged(unitEntry *donburi.Entry) bool {
	return unitEntry != nil && unitEntry.HasComponent(RangedAbilityC)
}

func GetFaction(unitEntry *donburi.Entry) string {
	if unitEntry == nil {
		return ""
	}
	return UnitStatsC.Get(unitEntry).Faction
}

func GetPosition(unitEntry *donburi.Entry) Position {
	if unitEntry == nil {
		return Position{}
	}
	pos := PositionC.Get(unitEntry)
	return Position{Q: pos.Q, R: pos.R}
}

func GetUnitsByFaction(world donburi.World, faction string) []*donburi.Entry {
	units := make([]*donburi.Entry, 0)
	unitQuery.Each(world, func(entry *donburi.Entry) {
		if UnitStatsC.Get(entry).Faction == faction {
			units = append(units, entry)
		}
	})
	return units
}

func GetUnitsByNode(world donburi.World, pos Position) []*donburi.Entry {
	units := make([]*donburi.Entry, 0)
	unitQuery.Each(world, func(entry *donburi.Entry) {
		p := PositionC.Get(entry)
		if p.Q == pos.Q && p.R == pos.R {
			units = append(units, entry)
		}
	})
	return units
}

func UnitsByFactionAtNode(world donburi.World, pos Position) map[string][]*donburi.Entry {
	grouped := make(map[string][]*donburi.Entry)
	for _, unit := range GetUnitsByNode(world, pos) {
		faction := UnitStatsC.Get(unit).Faction
		grouped[faction] = append(grouped[faction], unit)
	}
	return grouped
}
