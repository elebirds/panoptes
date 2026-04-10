package production

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type ProductionSystem struct{}

func (s *ProductionSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	available := make(map[string]domain.ResourceBag, len(state.Players))
	for playerID, p := range state.Players {
		available[playerID] = p.Resources.Clone()
	}

	ecs.NodesWithBuilding(world).Each(world, func(entry *donburi.Entry) {
		node := ecs.NodeC.Get(entry)
		building := ecs.BuildingC.Get(entry)
		if !isMilitaryProducer(string(building.Type)) {
			return
		}
		cfg, ok := staticdata.Default().GetBuilding(string(building.Type))
		if !ok {
			return
		}
		cost := toResourceBag(cfg.Production.Input)
		pool := available[building.Owner]
		if !pool.CanAfford(cost) {
			return
		}
		available[building.Owner] = pool.Sub(cost)
		for _, unitType := range cfg.ProducesUnits {
			events = append(events, event.UnitProducedEvent{NodeID: node.ID, UnitType: unitType, Faction: building.Owner, Count: 1, Cost: cost.Clone()})
		}
	})

	return events
}

func isMilitaryProducer(buildingType string) bool {
	switch buildingType {
	case "barracks", "stable", "engineer_camp":
		return true
	default:
		return false
	}
}
