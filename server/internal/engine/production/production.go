// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的生产结算逻辑。

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
		if !isMilitaryProducer(string(building.Type)) {
			return
		}
		cfg, ok := staticdata.Default().GetBuilding(string(building.Type))
		if !ok {
			return
		}
		cost := toResourceBag(cfg.Production.Input)
		// 军事生产的预扣费按 "playerID::castleID" 拆分资源池。
		// 这样可以避免不同城堡的兵营在同一轮结算里共享一份临时可用资源，
		// 也保证最终 Apply 时会落到对应城堡。
		poolKey := building.Owner + "::" + building.CastleID
		pool, ok := available[poolKey]
		if !ok {
			pool = available[building.Owner]
		}
		if !pool.CanAfford(cost) {
			return
		}
		available[poolKey] = pool.Sub(cost)
		for _, unitType := range cfg.ProducesUnits {
			events = append(events, event.UnitProducedEvent{
				NodeID:   node.ID,
				UnitType: unitType,
				Faction:  building.Owner,
				CastleID: building.CastleID,
				Count:    1,
				Cost:     cost.Clone(),
			})
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
