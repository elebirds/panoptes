package production

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type BuildSystem struct{}

func (s *BuildSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	orders := append([]domain.BuildOrder{}, state.PendingBuilds...)
	orders = append(orders, state.MinisterBuildOrders...)

	for _, order := range orders {
		playerState, ok := state.Players[order.PlayerID]
		if !ok {
			continue
		}
		cfg, ok := staticdata.Default().GetBuilding(order.BuildingType)
		if !ok {
			continue
		}
		cost := toResourceBag(cfg.BuildCost)
		if !playerState.Resources.CanAfford(cost) {
			continue
		}
		events = append(events, event.BuildingBuiltEvent{
			NodeID:       order.NodeID,
			BuildingType: order.BuildingType,
			Owner:        order.PlayerID,
			Cost:         cost,
		})
	}
	return events
}

func toResourceBag(amounts map[string]int) domain.ResourceBag {
	bag := domain.NewResourceBag()
	for key, value := range amounts {
		bag.Set(domain.ResourceKey(key), value)
	}
	return bag
}
