package combat

import (
	"math"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type DestroySystem struct{}

func (s *DestroySystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	ecs.DestroyUnits(world).Each(world, func(entry *donburi.Entry) {
		stats := ecs.UnitStatsC.Get(entry)
		ability := ecs.DestroyAbilityC.Get(entry)
		pos := ecs.PositionC.Get(entry)
		center := domain.Position{X: pos.X, Y: pos.Y}
		positions := append([]domain.Position{center}, center.Neighbors()...)

		for _, targetPos := range positions {
			nodeEntry, ok := domain.GetNodeAt(world, targetPos)
			if !ok {
				continue
			}
			node := ecs.NodeC.Get(nodeEntry)
			if node.Owner == stats.Faction {
				continue
			}
			if node.HasRoad {
				events = append(events, event.RoadDestroyedEvent{FromNode: node.ID, ToNode: node.ID, DestroyerID: stats.ID})
			}
			if nodeEntry.HasComponent(ecs.BuildingC) {
				building := ecs.BuildingC.Get(nodeEntry)
				attack := effectiveUnitAttack(state, stats.Faction, stats.Type, stats.Attack)
				multiplier := effectiveUnitDestroyMultiplier(state, stats.Faction, stats.Type, ability.Multiplier)
				dmg := int(math.Round(float64(attack) * multiplier))
				if dmg < 1 {
					dmg = 1
				}
				nextHP := maxInt(0, building.HP-dmg)
				events = append(events, event.BuildingDamagedEvent{NodeID: node.ID, Damage: dmg, HPAfter: nextHP})
			}
		}
	})
	return events
}
