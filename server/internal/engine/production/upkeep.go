package production

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type UpkeepSystem struct{}

func (s *UpkeepSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	available := make(map[string]domain.ResourceBag, len(state.Players))
	for playerID, p := range state.Players {
		available[playerID] = p.Resources.Clone()
	}

	ecs.NodesWithBuilding(world).Each(world, func(entry *donburi.Entry) {
		node := ecs.NodeC.Get(entry)
		building := ecs.BuildingC.Get(entry)
		cfg, ok := staticdata.Default().GetBuilding(string(building.Type))
		if !ok || len(cfg.Upkeep) == 0 {
			return
		}
		owner := building.Owner
		pool, ok := available[owner]
		if !ok {
			return
		}
		cost := toResourceBag(cfg.Upkeep)
		if !pool.CanAfford(cost) {
			events = append(events, event.BuildingDeactivatedEvent{NodeID: node.ID, Reason: "insufficient_resources"})
			return
		}
		available[owner] = pool.Sub(cost)
		for _, key := range cost.Keys() {
			amount := cost.Get(key)
			if amount == 0 {
				continue
			}
			events = append(events, event.ResourceProducedEvent{NodeID: node.ID, ResourceType: string(key), Amount: -amount, Owner: owner})
		}
	})

	return events
}
