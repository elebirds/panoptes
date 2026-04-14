// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的补给与维护结算逻辑。

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
	available := make(map[string]domain.ResourceBag)
	for playerID, p := range state.Players {
		if len(p.Castles) == 0 {
			available[playerID] = p.Resources.Clone()
			continue
		}
		for castleID, castle := range p.Castles {
			if castle == nil {
				continue
			}
			available[playerID+"::"+castleID] = castle.Resources.Clone()
		}
	}

	ecs.NodesWithBuilding(world).Each(world, func(entry *donburi.Entry) {
		node := ecs.NodeC.Get(entry)
		building := ecs.BuildingC.Get(entry)
		cfg, ok := staticdata.Default().GetBuilding(string(building.Type))
		if !ok || len(cfg.Upkeep) == 0 {
			return
		}
		owner := building.Owner
		// upkeep 也按建筑所属城堡预检查资源。
		// 如果某个城堡自己的资源不够，只会停用该城堡下的建筑，
		// 而不会错误消耗玩家其他城堡的资源来“补贴”它。
		poolKey := owner + "::" + building.CastleID
		pool, ok := available[poolKey]
		if !ok {
			pool = available[owner]
		}
		if !ok {
			return
		}
		cost := toResourceBag(cfg.Upkeep)
		if !pool.CanAfford(cost) {
			events = append(events, event.BuildingDeactivatedEvent{NodeID: node.ID, Reason: "insufficient_resources"})
			return
		}
		available[poolKey] = pool.Sub(cost)
		for _, key := range cost.Keys() {
			amount := cost.Get(key)
			if amount == 0 {
				continue
			}
			events = append(events, event.ResourceProducedEvent{
				NodeID:       node.ID,
				ResourceType: string(key),
				Amount:       -amount,
				Owner:        owner,
				CastleID:     building.CastleID,
			})
		}
	})

	return events
}
