package combat

import (
	"math"

	"github.com/elebirds/panoptes/internal/algo/geometry"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type RangedSystem struct{}

func (s *RangedSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	hpAfter := make(map[string]int)
	events := make([]event.Event, 0)

	ecs.RangedUnits(world).Each(world, func(attackerEntry *donburi.Entry) {
		attacker := ecs.UnitStatsC.Get(attackerEntry)
		ar := ecs.RangedAbilityC.Get(attackerEntry)
		apos := ecs.PositionC.Get(attackerEntry)

		var targetEntry *donburi.Entry
		minHP := int(^uint(0) >> 1)
		ecs.AllUnits(world).Each(world, func(defEntry *donburi.Entry) {
			if targetEntry != nil && minHP == 1 {
				return
			}
			def := ecs.UnitStatsC.Get(defEntry)
			if def.Faction == attacker.Faction {
				return
			}
			dpos := ecs.PositionC.Get(defEntry)
			dist := geometry.Manhattan(domain.Position{X: apos.X, Y: apos.Y}, domain.Position{X: dpos.X, Y: dpos.Y})
			if dist > ar.Range {
				return
			}
			hp := readHP(hpAfter, def.ID, def.HP)
			if hp <= 0 || hp >= minHP {
				return
			}
			minHP = hp
			targetEntry = defEntry
		})

		if targetEntry == nil {
			return
		}
		target := ecs.UnitStatsC.Get(targetEntry)
		tpos := ecs.PositionC.Get(targetEntry)
		dmg := computeRangedDamage(attacker.Attack, domain.Position{X: tpos.X, Y: tpos.Y}, state)
		nextHP := maxInt(0, readHP(hpAfter, target.ID, target.HP)-dmg)
		hpAfter[target.ID] = nextHP
		events = append(events, event.UnitDamagedEvent{UnitID: target.ID, Damage: dmg, HPAfter: nextHP, Source: "ranged"})
		if nextHP <= 0 {
			events = append(events, event.UnitDiedEvent{UnitID: target.ID, KillerID: attacker.ID, Pos: domain.Position{X: tpos.X, Y: tpos.Y}})
		}
	})

	return events
}

func computeRangedDamage(attack int, pos domain.Position, state *domain.GameState) int {
	if attack <= 0 {
		return 0
	}
	terrainFactor := 1.0
	if nodeEntry, ok := domain.GetNodeAt(state.World, pos); ok {
		node := ecs.NodeC.Get(nodeEntry)
		if terrain, ok := staticdata.Default().GetTerrain(string(node.Terrain)); ok {
			terrainFactor = math.Max(0.2, 1-terrain.DefenseBonus+terrain.AttackPenalty)
		}
	}
	dmg := int(math.Round(float64(attack) * terrainFactor))
	if dmg < 1 {
		return 1
	}
	return dmg
}
