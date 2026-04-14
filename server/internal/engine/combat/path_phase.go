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
		ctx.Plans[unitID] = resolver.Plan(ctx, unit)
	}
}

type HoldResolver struct{}

func (HoldResolver) Action() domain.CombatAction { return domain.CombatActionHold }

func (HoldResolver) Plan(_ *ResolutionContext, unit SnapshotUnit) *OrderPlan {
	// hold 不移动，但仍然产出标准化计划，后续冲突/伤害阶段就不需要区分“有没有计划”。
	return &OrderPlan{
		UnitID:    unit.UnitID,
		Action:    domain.CombatActionHold,
		Start:     unit.Position,
		Path:      []domain.Position{unit.Position},
		Candidate: unit.Position,
		Fallback:  unit.Position,
	}
}

type MoveResolver struct{}

func (MoveResolver) Action() domain.CombatAction { return domain.CombatActionMove }

func (MoveResolver) Plan(ctx *ResolutionContext, unit SnapshotUnit) *OrderPlan {
	return planMovement(ctx, unit, resolveTargetNode(ctx, unit.Order.TargetNodeID), false)
}

type AttackResolver struct{}

func (AttackResolver) Action() domain.CombatAction { return domain.CombatActionAttack }

func (AttackResolver) Plan(_ *ResolutionContext, unit SnapshotUnit) *OrderPlan {
	// attack 的位移计划固定为原地，实际是否命中留到伤害窗口按最终位置判定。
	return &OrderPlan{
		UnitID:         unit.UnitID,
		Action:         domain.CombatActionAttack,
		Start:          unit.Position,
		Path:           []domain.Position{unit.Position},
		Candidate:      unit.Position,
		Fallback:       unit.Position,
		AttackTargetID: unit.Order.TargetUnitID,
	}
}

type ChargeResolver struct{}

func (ChargeResolver) Action() domain.CombatAction { return domain.CombatActionCharge }

func (ChargeResolver) Plan(ctx *ResolutionContext, unit SnapshotUnit) *OrderPlan {
	goal := resolveChargeGoal(ctx, unit.Order)
	return planMovement(ctx, unit, goal, true)
}

func planMovement(ctx *ResolutionContext, unit SnapshotUnit, goal domain.Position, allowCharge bool) *OrderPlan {
	// 规划阶段只算“候选结果”，不在这里做冲突裁决。
	// 这样 move 和 charge 能共用同一套路径/阻断逻辑，再交给后续冲突阶段统一处理。
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
		// charge 只允许把路径上的第一处敌方单位接敌点记为冲锋目标，
		// 不能穿过第一道敌线去命中后排。
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
	// 没找到合法接敌目标时，charge 自动退化为普通 move。
	if plan.Action == domain.CombatActionCharge && plan.ChargeTargetID == "" {
		plan.Action = domain.CombatActionMove
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
	return domain.Position{X: pos.X, Y: pos.Y}
}

func resolveChargeGoal(ctx *ResolutionContext, order domain.CombatOrder) domain.Position {
	// charge 优先支持“指定目标单位”，这样后续接 AI 或前端锁定目标时不需要改协议。
	if order.TargetUnitID != "" {
		if unit, ok := ctx.Snapshot.Units[order.TargetUnitID]; ok {
			return unit.Position
		}
	}
	return resolveTargetNode(ctx, order.TargetNodeID)
}
