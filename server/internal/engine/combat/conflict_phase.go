// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现单位结算引擎的冲突阶段结算逻辑。

package combat

import "github.com/elebirds/panoptes/internal/domain"

type ConflictPhase struct{}

func (ConflictPhase) Apply(ctx *ResolutionContext) {
	conflicts := make([]domain.Conflict, 0)
	for _, detector := range ctx.ConflictDetectors {
		found := detector.Detect(ctx)
		conflicts = append(conflicts, found...)
	}
	sortConflictSlice(conflicts)
	ctx.Conflicts = conflicts
	for _, conflict := range conflicts {
		switch conflict.ConflictType {
		case "edge":
			ctx.EdgeConflictUnits[conflict.UnitAID] = true
			ctx.EdgeConflictUnits[conflict.UnitBID] = true
		case "node":
			ctx.NodeConflictUnits[conflict.UnitAID] = true
			ctx.NodeConflictUnits[conflict.UnitBID] = true
		}
	}
}

type EdgeConflictDetector struct{}

func (EdgeConflictDetector) Detect(ctx *ResolutionContext) []domain.Conflict {
	conflicts := make([]domain.Conflict, 0)
	unitIDs := ctx.UnitIDs()
	for i := 0; i < len(unitIDs); i++ {
		a := ctx.Snapshot.Units[unitIDs[i]]
		aPlan := ctx.Plans[a.UnitID]
		for j := i + 1; j < len(unitIDs); j++ {
			b := ctx.Snapshot.Units[unitIDs[j]]
			if a.PlayerID == b.PlayerID {
				continue
			}
			bPlan := ctx.Plans[b.UnitID]
			// V1 的边冲突定义为：双方都把对方起始格视为自己的首个阻断点，并且彼此相邻。
			if aPlan.BlockedAt == nil || bPlan.BlockedAt == nil {
				continue
			}
			if *aPlan.BlockedAt != b.Position || *bPlan.BlockedAt != a.Position {
				continue
			}
			if a.Position.DistanceTo(b.Position) != 1 {
				continue
			}
			conflicts = append(conflicts, domain.Conflict{
				UnitAID:      a.UnitID,
				UnitBID:      b.UnitID,
				Location:     midpoint(a.Position, b.Position),
				ConflictType: "edge",
			})
		}
	}
	return conflicts
}

type NodeConflictDetector struct{}

func (NodeConflictDetector) Detect(ctx *ResolutionContext) []domain.Conflict {
	grouped := make(map[domain.Position][]string)
	for _, unitID := range ctx.UnitIDs() {
		plan := ctx.Plans[unitID]
		if ctx.EdgeConflictUnits[unitID] {
			grouped[plan.Start] = append(grouped[plan.Start], unitID)
			continue
		}
		grouped[plan.Candidate] = append(grouped[plan.Candidate], unitID)
	}

	conflicts := make([]domain.Conflict, 0)
	for pos, unitIDs := range grouped {
		if len(unitIDs) < 2 {
			continue
		}
		// V1 明确不引入三方冲突专门规则，因此这里只取首个异阵营配对。
		// 若未来支持多野怪/多方会战，可替换成 ConflictGroupResolver。
		a := ctx.Snapshot.Units[unitIDs[0]]
		b := ctx.Snapshot.Units[unitIDs[1]]
		if a.PlayerID == b.PlayerID {
			continue
		}
		conflicts = append(conflicts, domain.Conflict{
			UnitAID:      a.UnitID,
			UnitBID:      b.UnitID,
			Location:     pos,
			ConflictType: "node",
		})
	}
	return conflicts
}
