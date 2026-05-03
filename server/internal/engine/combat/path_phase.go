// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 瀹炵幇鍗曚綅缁撶畻寮曟搸鐨勮矾寰勫睍寮€闃舵閫昏緫銆?

package combat

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
)

type PathPlanningPhase struct{}

func (PathPlanningPhase) Apply(ctx *ResolutionContext) {
	for _, unitID := range ctx.UnitIDs() {
		unit := ctx.Snapshot.Units[unitID]
		resolver, ok := ctx.OrderResolvers[unit.Order.Action]
		if !ok {
			resolver = HoldResolver{}
		}
		// 瑙勫垝闃舵鐨勮緭鍑哄彧鏈?OrderPlan锛屼笉浜х敓浠讳綍姝ｅ紡鐘舵€佷慨鏀广€?
		// 杩欐牱 conflict / movement / damage 閮借兘鍥寸粫鍚屼竴浠藉€欓€夎鍒掔户缁鍐炽€?
		ctx.Plans[unitID] = resolver.Plan(ctx, unit)
	}
}

type HoldResolver struct{}

func (HoldResolver) Action() domain.UnitResolutionAction { return domain.UnitResolutionActionHold }

func (HoldResolver) Plan(_ *ResolutionContext, unit SnapshotUnit) *OrderPlan {
	// hold 涓嶇Щ鍔紝浣嗕粛鐒朵骇鍑烘爣鍑嗗寲璁″垝锛屽悗缁啿绐?浼ゅ闃舵灏变笉闇€瑕佸尯鍒嗏€滄湁娌℃湁璁″垝鈥濄€?
	return &OrderPlan{
		UnitID:    unit.UnitID,
		Action:    domain.UnitResolutionActionHold,
		Start:     unit.Position,
		Path:      []domain.Position{unit.Position},
		Candidate: unit.Position,
		Fallback:  unit.Position,
	}
}

type MoveResolver struct{}

func (MoveResolver) Action() domain.UnitResolutionAction { return domain.UnitResolutionActionMove }

func (MoveResolver) Plan(ctx *ResolutionContext, unit SnapshotUnit) *OrderPlan {
	return planMovement(ctx, unit, resolveTargetNode(ctx, unit.Order.TargetNodeID), false)
}

type AttackResolver struct{}

func (AttackResolver) Action() domain.UnitResolutionAction { return domain.UnitResolutionActionAttack }
func (AttackResolver) Plan(ctx *ResolutionContext, unit SnapshotUnit) *OrderPlan {
	attackTarget := resolveAttackTarget(ctx, unit.Order)
	if len(unit.Order.PathNodeIDs) == 0 {
		return &OrderPlan{
			UnitID:       unit.UnitID,
			Action:       domain.UnitResolutionActionAttack,
			Start:        unit.Position,
			Path:         []domain.Position{unit.Position},
			Candidate:    unit.Position,
			Fallback:     unit.Position,
			AttackTarget: attackTarget,
		}
	}

	goal := resolveChargeGoal(ctx, unit.Order)
	plan := planMovement(ctx, unit, goal, false)
	plan.Action = domain.UnitResolutionActionAttack
	plan.AttackTarget = attackTarget
	return plan
}

type ChargeResolver struct{}

func (ChargeResolver) Action() domain.UnitResolutionAction { return domain.UnitResolutionActionCharge }

func (ChargeResolver) Plan(ctx *ResolutionContext, unit SnapshotUnit) *OrderPlan {
	goal := resolveChargeGoal(ctx, unit.Order)
	return planMovement(ctx, unit, goal, true)
}

func planMovement(ctx *ResolutionContext, unit SnapshotUnit, goal domain.Position, allowCharge bool) *OrderPlan {
	// 瑙勫垝闃舵鍙畻鈥滃€欓€夌粨鏋溾€濓紝涓嶅湪杩欓噷鍋氬啿绐佽鍐炽€?
	// 杩欐牱 move 鍜?charge 鑳藉叡鐢ㄥ悓涓€濂楄矾寰?闃绘柇閫昏緫锛屽啀浜ょ粰鍚庣画鍐茬獊闃舵缁熶竴澶勭悊銆?
	plan := &OrderPlan{
		UnitID:    unit.UnitID,
		Action:    unit.Order.Action,
		Start:     unit.Position,
		Path:      []domain.Position{unit.Position},
		Candidate: unit.Position,
		Fallback:  unit.Position,
	}
	if goal == unit.Position {
		return plan
	}

	path, ok := plannedOrderPath(ctx, unit)
	if !ok {
		path, ok = ctx.RoutePlanner.FindPath(ctx.World, unit.Position, goal, unit.Movement)
	}
	if !ok || len(path) == 0 {
		return plan
	}

	plan.Path = path
	candidate, fallback := ctx.TurnPlanner.Reachable(ctx.World, path, unit.Movement)
	candidateIndex := indexOfPosition(path, candidate)
	if candidateIndex < 0 {
		candidateIndex = 0
	}
	for i := 1; i < len(path); i++ {
		source, blocked := ctx.BlockRule.SourceFor(ctx, unit, path[i])
		if !blocked {
			continue
		}
		blockedPos := path[i]
		plan.BlockedAt = &blockedPos
		// charge 鍙厑璁告妸璺緞涓婄殑绗竴澶勬晫鏂瑰崟浣嶆帴鏁岀偣璁颁负鍐查攱鐩爣锛?
		// 涓嶈兘绌胯繃绗竴閬撴晫绾垮幓鍛戒腑鍚庢帓銆?
		// 杩欎篃鏄负浠€涔?BlockRule 浼氫紭鍏堣繑鍥炲悓鏍煎崟浣嶈€屼笉鏄缓绛戙€?
		if allowCharge && source.Kind == "unit" {
			plan.ChargeTargetID = source.UnitID
		}
		candidateIndex = i - 1
		break
	}

	plan.Candidate = path[candidateIndex]
	if candidateIndex > 0 {
		plan.Fallback = path[candidateIndex-1]
	} else {
		plan.Fallback = fallback
	}
	// 娌℃壘鍒板悎娉曟帴鏁岀洰鏍囨椂锛宑harge 鑷姩閫€鍖栦负鏅€?move銆?
	if plan.Action == domain.UnitResolutionActionCharge && plan.ChargeTargetID == "" {
		plan.Action = domain.UnitResolutionActionMove
	}
	return plan
}

func plannedOrderPath(ctx *ResolutionContext, unit SnapshotUnit) ([]domain.Position, bool) {
	if ctx == nil || len(unit.Order.PathNodeIDs) == 0 {
		return nil, false
	}

	path, _, ok := resolvePreviewPathFromNodeIDs(ctx.World, ctx.State, unit.UnitID, unit.Order.PathNodeIDs)
	if !ok || len(path) == 0 || path[0] != unit.Position {
		return nil, false
	}

	return path, true
}

func indexOfPosition(path []domain.Position, target domain.Position) int {
	for i, pos := range path {
		if pos == target {
			return i
		}
	}
	return -1
}

func resolveTargetNode(ctx *ResolutionContext, nodeID string) domain.Position {
	entry, ok := ctx.State.GetNode(nodeID)
	if !ok {
		return domain.Position{}
	}
	pos := ecs.PositionC.Get(entry)
	return domain.Position{Q: pos.Q, R: pos.R}
}

func resolveChargeGoal(ctx *ResolutionContext, order domain.UnitResolutionOrder) domain.Position {
	// charge 浼樺厛鏀寔鈥滄寚瀹氱洰鏍囧崟浣嶁€濓紝杩欐牱鍚庣画鎺?AI 鎴栧墠绔攣瀹氱洰鏍囨椂涓嶉渶瑕佹敼鍗忚銆?
	if order.TargetUnitID != "" {
		if unit, ok := ctx.Snapshot.Units[order.TargetUnitID]; ok {
			return unit.Position
		}
	}
	return resolveTargetNode(ctx, order.TargetNodeID)
}

func resolveAttackTarget(ctx *ResolutionContext, order domain.UnitResolutionOrder) CombatTargetRef {
	if order.TargetUnitID != "" {
		return CombatTargetRef{Kind: CombatTargetKindUnit, UnitID: order.TargetUnitID}
	}
	if order.TargetNodeID == "" {
		return CombatTargetRef{}
	}
	if structure, ok := ctx.Structure(order.TargetNodeID); ok {
		kind := CombatTargetKindStructure
		if structure.IsCityCore {
			kind = CombatTargetKindCityCore
		}
		return CombatTargetRef{Kind: kind, NodeID: order.TargetNodeID}
	}
	return CombatTargetRef{Kind: CombatTargetKindStructure, NodeID: order.TargetNodeID}
}
