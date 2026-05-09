package economy

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type DemolishSystem struct{}

func (s *DemolishSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	if state == nil {
		return events
	}

	orders := append([]domain.DemolishOrder{}, state.TurnRuntime.Planning.DemolishOrders...)
	reservedNodes := make(map[string]struct{}, len(orders))

	for _, order := range orders {
		if _, ok := state.Players[order.PlayerID]; !ok {
			continue
		}
		if _, reserved := reservedNodes[order.NodeID]; reserved {
			events = append(events, event.DemolishSkippedEvent{
				PlayerID:     order.PlayerID,
				NodeID:       order.NodeID,
				BuildingType: order.BuildingType,
				Reason:       "invalid_target",
			})
			continue
		}
		validation := ValidateDemolishOrder(state, order.PlayerID, order.NodeID)
		if !validation.OK {
			events = append(events, event.DemolishSkippedEvent{
				PlayerID:     order.PlayerID,
				NodeID:       order.NodeID,
				BuildingType: order.BuildingType,
				Reason:       validation.ErrorCode,
			})
			continue
		}
		reservedNodes[order.NodeID] = struct{}{}
		events = append(events, event.BuildingDemolishedEvent{
			NodeID:       order.NodeID,
			Owner:        order.PlayerID,
			BuildingType: string(validation.Building.Type),
			Reason:       "demolish_order",
		})
	}
	return events
}
