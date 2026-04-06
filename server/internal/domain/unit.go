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
	return Position{X: pos.X, Y: pos.Y}
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
		if p.X == pos.X && p.Y == pos.Y {
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
