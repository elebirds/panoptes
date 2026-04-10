package production

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type FlowSystem struct{}

func (s *FlowSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	ecs.NodesWithBuilding(world).Each(world, func(entry *donburi.Entry) {
		node := ecs.NodeC.Get(entry)
		if !node.IsResource || node.ResourceType == "" {
			return
		}
		building := ecs.BuildingC.Get(entry)
		cfg, ok := staticdata.Default().GetBuilding(string(building.Type))
		if !ok {
			return
		}
		for resType, amount := range cfg.Production.Output {
			if amount <= 0 {
				continue
			}
			events = append(events, event.ResourceProducedEvent{NodeID: node.ID, ResourceType: resType, Amount: amount, Owner: building.Owner})
			bag := domain.NewResourceBag()
			bag.Set(domain.ResourceKey(resType), amount)
			events = append(events, event.ResourceFlowedEvent{FromNodeID: node.ID, ToNodeID: node.ID, Resources: bag})
		}
	})
	return events
}
