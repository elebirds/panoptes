// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载规则 AI 规划器拆分后的候选、评分与意图生成逻辑。

package ai

import (
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

func (p *ruleBotPlanner) ownedUnitEntries() []*donburi.Entry {
	if p.req.State == nil || p.req.State.World == nil {
		return nil
	}
	out := make([]*donburi.Entry, 0)
	ecs.AllUnits(p.req.State.World).Each(p.req.State.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		if ecs.UnitStatsC.Get(entry).Faction == p.playerID {
			out = append(out, entry)
		}
	})
	sort.Slice(out, func(i, j int) bool {
		return ecs.UnitStatsC.Get(out[i]).ID < ecs.UnitStatsC.Get(out[j]).ID
	})
	return out
}

func (p *ruleBotPlanner) countOwnedUnitType(unitType string) int {
	count := 0
	for _, entry := range p.ownedUnitEntries() {
		if strings.EqualFold(string(ecs.UnitStatsC.Get(entry).Type), unitType) {
			count++
		}
	}
	return count
}

func (p *ruleBotPlanner) canExpandSoon() bool {
	return p.countOwnedUnitType("settler") > 0 || len(p.player.Cities) < 2
}

func (p *ruleBotPlanner) foodAmount() int {
	if p.player == nil {
		return 0
	}
	return p.player.Resources.Get(domain.ResourceFood)
}

func (p *ruleBotPlanner) countOwnedCombatUnits() int {
	count := 0
	for _, entry := range p.ownedUnitEntries() {
		stats := ecs.UnitStatsC.Get(entry)
		if isCivilianUnit(string(stats.Type)) {
			continue
		}
		count++
	}
	return count
}

func (p *ruleBotPlanner) hasOwnedBuildingType(buildingType string) bool {
	return p.countOwnedBuildingType(buildingType) > 0
}

func (p *ruleBotPlanner) countOwnedBuildingType(buildingType string) int {
	if p.req.State == nil || p.req.State.World == nil {
		return 0
	}
	count := 0
	ecs.NodesWithBuilding(p.req.State.World).Each(p.req.State.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		building := ecs.BuildingC.Get(entry)
		if building.Owner == p.playerID && strings.EqualFold(string(building.Type), buildingType) {
			count++
		}
	})
	return count
}

func (p *ruleBotPlanner) countOwnedCombatProductionBuildings() int {
	if p.req.State == nil || p.req.State.World == nil {
		return 0
	}
	count := 0
	ecs.NodesWithBuilding(p.req.State.World).Each(p.req.State.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		building := ecs.BuildingC.Get(entry)
		if building.Owner != p.playerID || !p.isCombatProductionBuilding(string(building.Type)) {
			return
		}
		count++
	})
	return count
}

func (p *ruleBotPlanner) countOwnedSettlerProductionBuildings() int {
	if p.req.State == nil || p.req.State.World == nil {
		return 0
	}
	count := 0
	ecs.NodesWithBuilding(p.req.State.World).Each(p.req.State.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		building := ecs.BuildingC.Get(entry)
		if building.Owner != p.playerID || !p.isSettlerProductionBuilding(string(building.Type)) {
			return
		}
		count++
	})
	return count
}
