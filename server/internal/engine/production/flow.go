// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的资源流动结算逻辑。

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
			// 资源建筑的每回合产出沿用建筑上的 CastleID。
			// 这样“哪个城堡建的资源建筑，资源就归哪个城堡”的规则会在系统层面
			// 自然成立，而不需要再额外查询节点与城堡的映射关系。
			events = append(events, event.ResourceProducedEvent{
				NodeID:       node.ID,
				ResourceType: resType,
				Amount:       amount,
				Owner:        building.Owner,
				CastleID:     building.CastleID,
			})
			bag := domain.NewResourceBag()
			bag.Set(domain.ResourceKey(resType), amount)
			events = append(events, event.ResourceFlowedEvent{FromNodeID: node.ID, ToNodeID: node.ID, Resources: bag})
		}
	})
	return events
}
