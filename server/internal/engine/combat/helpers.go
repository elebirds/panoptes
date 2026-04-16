// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-16 19:35:00 +0800
// Description: 提供战斗包内部共享的小型辅助函数。

package combat

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

func midpoint(a, b domain.Position) domain.Position {
	return domain.Position{
		X: (a.X + b.X) / 2,
		Y: (a.Y + b.Y) / 2,
	}
}

func readHP(overrides map[string]int, unitID string, base int) int {
	if hp, ok := overrides[unitID]; ok {
		return hp
	}
	return base
}

func maxInt(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func findUnit(world donburi.World, unitID string) (*donburi.Entry, bool) {
	var found *donburi.Entry
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found != nil {
			return
		}
		if ecs.UnitStatsC.Get(entry).ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}
