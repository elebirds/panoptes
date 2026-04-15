package production

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type TerritoryControlSystem struct{}

func (s *TerritoryControlSystem) Run(world donburi.World, _ *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	ecs.NodesWithBuilding(world).Each(world, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		if entry.HasComponent(ecs.CityCoreC) {
			return
		}
		node := ecs.NodeC.Get(entry)
		building := ecs.BuildingC.Get(entry)
		territoryOwner := strings.TrimSpace(node.TerritoryOwner)
		if territoryOwner == "" || territoryOwner == strings.TrimSpace(building.Owner) {
			return
		}
		events = append(events, event.BuildingDeactivatedEvent{
			NodeID: node.ID,
			Reason: "outside_territory",
		})
	})
	return events
}
