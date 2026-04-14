// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现单位结算引擎的补给与维护结算逻辑。

package combat

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type CombatUpkeepSystem struct{}

func (s *CombatUpkeepSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	unitsByPlayer := make(map[string][]string)
	foodNeedByPlayer := make(map[string]int)

	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		stats := ecs.UnitStatsC.Get(entry)
		unitsByPlayer[stats.Faction] = append(unitsByPlayer[stats.Faction], stats.ID)
		unitCfg, ok := staticdata.Default().GetUnit(string(stats.Type))
		if !ok {
			foodNeedByPlayer[stats.Faction]++
			return
		}
		foodNeedByPlayer[stats.Faction] += unitCfg.Upkeep["food"]
	})

	for playerID, playerState := range state.Players {
		consumed := foodNeedByPlayer[playerID]
		events = append(events, event.UpkeepPaidEvent{PlayerID: playerID, FoodConsumed: consumed})
		if playerState.Resources.Get(domain.ResourceFood) >= consumed {
			continue
		}
		for _, unitID := range unitsByPlayer[playerID] {
			events = append(events, event.UnitStarvingEvent{UnitID: unitID, DamagePerTurn: 1})
		}
	}

	return events
}
