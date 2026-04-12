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
