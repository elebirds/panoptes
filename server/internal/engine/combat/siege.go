package combat

import (
	"math"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type SiegeSystem struct{}

func (s *SiegeSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	hpAfter := make(map[string]int)

	ecs.SiegeUnits(world).Each(world, func(unitEntry *donburi.Entry) {
		unit := ecs.UnitStatsC.Get(unitEntry)
		pos := ecs.PositionC.Get(unitEntry)
		nodeEntry, ok := domain.GetNodeAt(world, domain.Position{X: pos.X, Y: pos.Y})
		if !ok || !nodeEntry.HasComponent(ecs.BuildingC) {
			return
		}
		node := ecs.NodeC.Get(nodeEntry)
		building := ecs.BuildingC.Get(nodeEntry)
		if building.Owner == "" || building.Owner == unit.Faction {
			return
		}

		ability := ecs.SiegeAbilityC.Get(unitEntry)
		wallReduction := math.Min(float64(building.WallLevel)*0.08, 0.75)
		attack := effectiveUnitAttack(state, unit.Faction, unit.Type, unit.Attack)
		multiplier := effectiveUnitSiegeMultiplier(state, unit.Faction, unit.Type, ability.Multiplier)
		dmg := int(math.Round(float64(attack) * multiplier * (1 - wallReduction)))
		if dmg < 1 {
			dmg = 1
		}
		buildingKey := node.ID
		if buildingKey == "" {
			buildingKey = string(node.Terrain)
		}
		curHP := building.HP
		if v, ok := hpAfter[buildingKey]; ok {
			curHP = v
		}
		nextHP := maxInt(0, curHP-dmg)
		hpAfter[buildingKey] = nextHP
		events = append(events, event.CastleDamagedEvent{NodeID: node.ID, Damage: dmg, HPAfter: nextHP, AttackerID: unit.ID})
		if nextHP <= 0 {
			events = append(events, event.CastleDestroyedEvent{NodeID: node.ID, ConquerorFaction: unit.Faction})
			return
		}

		towerDamagePer := 1
		if cfg, ok := staticdata.Default().GetBuilding(string(building.Type)); ok && cfg.Combat.AttackPerTurn > 0 {
			towerDamagePer = cfg.Combat.AttackPerTurn
		}
		counterDmg := building.Towers * towerDamagePer
		if counterDmg <= 0 {
			return
		}
		unitHP := unit.HP - counterDmg
		if unitHP < 0 {
			unitHP = 0
		}
		events = append(events, event.UnitDamagedEvent{UnitID: unit.ID, Damage: counterDmg, HPAfter: unitHP, Source: "tower"})
		if unitHP <= 0 {
			events = append(events, event.UnitDiedEvent{UnitID: unit.ID, KillerID: node.ID, Pos: domain.Position{X: pos.X, Y: pos.Y}})
		}
	})

	return events
}
