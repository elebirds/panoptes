// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现单位结算引擎的冲突阶段结算逻辑。

package combat

import (
	"sort"

	"github.com/elebirds/panoptes/internal/domain"
)

type ConflictPhase struct{}

func (ConflictPhase) Apply(ctx *ResolutionContext) {
	// 这一阶段只回答两件事：
	// 1. 哪些单位属于同一个冲突组
	// 2. 这些组属于 edge 还是 node
	// 真正的伤害与事件投影都留给 DamagePhase。
	groups := make([]ConflictGroup, 0)
	for _, detector := range ctx.ConflictDetectors {
		found := detector.Detect(ctx)
		groups = append(groups, found...)
	}
	sortConflictGroups(groups)
	ctx.ConflictGroups = groups
	for _, group := range groups {
		// 这里先把“命中过哪类冲突”记到单位级标记上，
		// MovementApplyPhase 会据此决定回起点还是退到 fallback。
		switch group.ConflictType {
		case "edge":
			for _, unitID := range group.Members {
				ctx.EdgeConflictUnits[unitID] = true
			}
		case "node":
			for _, unitID := range group.Members {
				ctx.NodeConflictUnits[unitID] = true
			}
		}
	}
}

type EdgeConflictDetector struct{}

func (EdgeConflictDetector) Detect(ctx *ResolutionContext) []ConflictGroup {
	groups := make([]ConflictGroup, 0)
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
			members := []string{a.UnitID, b.UnitID}
			sort.Strings(members)
			groups = append(groups, ConflictGroup{
				ConflictType: "edge",
				Location:     midpoint(a.Position, b.Position),
				Members:      members,
				// edge conflict 仍然保持严格二元定义，因此 hostile pair 与 members 一一对应。
				HostilePairs: []ConflictPair{{UnitAID: members[0], UnitBID: members[1]}},
			})
		}
	}
	return groups
}

type NodeConflictDetector struct{}

func (NodeConflictDetector) Detect(ctx *ResolutionContext) []ConflictGroup {
	grouped := make(map[domain.Position][]string)
	for _, unitID := range ctx.UnitIDs() {
		plan := ctx.Plans[unitID]
		if ctx.EdgeConflictUnits[unitID] {
			// 先命中 edge conflict 的单位，本回合节点归类按“回到起点后所在格”处理。
			grouped[plan.Start] = append(grouped[plan.Start], unitID)
			continue
		}
		grouped[plan.Candidate] = append(grouped[plan.Candidate], unitID)
	}

	groups := make([]ConflictGroup, 0)
	for pos, unitIDs := range grouped {
		if len(unitIDs) < 2 {
			continue
		}
		sort.Strings(unitIDs)
		pairs := buildHostilePairs(ctx, unitIDs)
		if len(pairs) == 0 {
			continue
		}
		groups = append(groups, ConflictGroup{
			ConflictType: "node",
			Location:     pos,
			Members:      append([]string(nil), unitIDs...),
			// 对外协议仍是二元 conflict event，因此 group 会在 DamagePhase 被展开为稳定 hostile pair 序列。
			HostilePairs: pairs,
		})
	}
	return groups
}

func buildHostilePairs(ctx *ResolutionContext, members []string) []ConflictPair {
	pairs := make([]ConflictPair, 0)
	for i := 0; i < len(members); i++ {
		a := ctx.Snapshot.Units[members[i]]
		for j := i + 1; j < len(members); j++ {
			b := ctx.Snapshot.Units[members[j]]
			if a.PlayerID == b.PlayerID {
				continue
			}
			// group conflict 采用“整组互殴，但只计算敌对 pair”：
			// A,A,B 会得到 A1-B 与 A2-B，不会生成 A1-A2。
			pairs = append(pairs, ConflictPair{
				UnitAID: a.UnitID,
				UnitBID: b.UnitID,
			})
		}
	}
	return pairs
}
