// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现单位结算引擎的移动阶段结算逻辑。

package combat

import (
	"github.com/elebirds/panoptes/internal/event"
)

type MovementApplyPhase struct{}

func (MovementApplyPhase) Apply(ctx *ResolutionContext) {
	for _, unitID := range ctx.UnitIDs() {
		plan := ctx.Plans[unitID]
		actual := plan.Candidate
		// 冲突后的落点规则固定在这里：
		// edge 冲突回到起点，node 冲突退到最后合法非冲突格，伤害结算不会再给“继续穿过去”的机会。
		if ctx.EdgeConflictUnits[unitID] {
			actual = plan.Start
		} else if ctx.NodeConflictUnits[unitID] {
			actual = plan.Fallback
		}
		ctx.ActualPositions[unitID] = actual
		if actual != plan.Start {
			ctx.Events = append(ctx.Events, event.UnitMovedEvent{
				UnitID:    unitID,
				From:      plan.Start,
				To:        actual,
				Timestamp: len(ctx.Events),
			})
		}
	}
}
