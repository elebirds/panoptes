package strategy

import (
	pf "github.com/elebirds/panoptes/internal/algo/pathfinding"
)

// DefaultPlanner orchestrates the full movement pipeline:
//
//	CostBuilder → Grid
//	A* Search   → PathResult
//	Budget trim → current turn result
type DefaultPlanner struct {
	cost  CostBuilder
	astar *pf.AStar
}

func NewPlanner(cost CostBuilder) *DefaultPlanner {
	return &DefaultPlanner{
		cost:  cost,
		astar: pf.NewAStar(),
	}
}

func (p *DefaultPlanner) Plan(world WorldState, unit UnitState, decision StrategyDecision) (MovementPlanResult, error) {
	grid, err := p.cost.Build(world, unit, decision)
	if err != nil {
		return MovementPlanResult{}, err
	}

	// Search uses the strategy-adjusted total cost to choose the best overall route.
	start := pf.Point{X: unit.X, Y: unit.Y}
	pathResult, err := p.astar.Search(grid, start, decision.Goal)
	if err != nil {
		return MovementPlanResult{}, err
	}

	trimmed := trimPathByBudget(grid, pathResult, unit.MovePoints)
	execTarget := ExecutionTarget{
		Type:   ExecutionTargetGoal,
		Target: decision.Goal,
		Reason: "始终以主目标为执行目标，并沿全局最优路径按预算推进",
	}

	return MovementPlanResult{
		Decision:        decision,
		ExecutionTarget: execTarget,
		Result:          trimmed,
	}, nil
}

func trimPathByBudget(grid pf.Grid, pr pf.PathResult, budget int) pf.PathResult {
	if !pr.Found || len(pr.Path) <= 1 {
		return pr
	}

	// Movement budget tracks real movement points, so trimming only spends EnterCost.
	spent := 0
	cutIdx := 0
	for i := 1; i < len(pr.Path); i++ {
		cell := grid.CellAt(pr.Path[i])
		spent += cell.EnterCost
		if spent > budget {
			break
		}
		cutIdx = i
	}

	if cutIdx == 0 {
		return pf.PathResult{
			Found:    true,
			Cost:     0,
			Path:     pr.Path[:1],
			EndPoint: pr.Path[0],
		}
	}

	trimmed := pr.Path[:cutIdx+1]
	cost := 0
	for i := 1; i < len(trimmed); i++ {
		cost += grid.CellAt(trimmed[i]).EnterCost
	}

	return pf.PathResult{
		Found:         true,
		TargetReached: trimmed[len(trimmed)-1].Equal(pr.Path[len(pr.Path)-1]),
		Cost:          cost,
		Path:          trimmed,
		EndPoint:      trimmed[len(trimmed)-1],
	}
}
