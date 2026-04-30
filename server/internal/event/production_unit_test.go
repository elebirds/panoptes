// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载领域事件写入逻辑拆分后的事件族或事件辅助逻辑。

package event

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

func TestUnitStarvingEventApplyRemovesDeadUnitViaSharedMutation(t *testing.T) {
	world := donburi.NewWorld()
	unitEntity := ecs.CreateUnit(world, string(domain.UnitTypeSettler), "player-1", domain.Position{Q: 0, R: 0})
	unitEntry := world.Entry(unitEntity)
	unitID := ecs.UnitStatsC.Get(unitEntry).ID

	UnitStarvingEvent{UnitID: unitID, DamagePerTurn: 100}.Apply(world, nil)

	if _, ok := findUnitByID(world, unitID); ok {
		t.Fatalf("unit %s should be removed after lethal starvation damage", unitID)
	}
}
