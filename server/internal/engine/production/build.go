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
	orders := append([]domain.BuildOrder{}, state.TurnRuntime.Planning.BuildOrders...)
	orders = append(orders, state.TurnRuntime.Planning.MinisterBuilds...)

	for _, order := range orders {
		if _, ok := state.Players[order.PlayerID]; !ok {
			continue
		}
		if !state.IsBuildingUnlocked(order.PlayerID, order.BuildingType) {
			continue
		}
		cfg, ok := staticdata.Default().GetBuilding(order.BuildingType)
		if !ok {
			continue
		}
		cost := state.ApplyResourceModifiers(order.PlayerID, string(staticdata.ModifierTriggerBuildingBuildCost), order.BuildingType, toResourceBag(cfg.BuildCost))
		if !state.CanAffordFromCastle(order.PlayerID, order.CastleID, cost) {
			continue
		}
		events = append(events, event.BuildingBuiltEvent{
			NodeID:       order.NodeID,
			BuildingType: order.BuildingType,
			Owner:        order.PlayerID,
			CastleID:     order.CastleID,
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
