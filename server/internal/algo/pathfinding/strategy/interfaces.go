package strategy

import (
	"math"

	pf "github.com/elebirds/panoptes/internal/algo/pathfinding"
)

// ---------------------------------------------------------------------------
// 接口定义
// ---------------------------------------------------------------------------

type StrategyDecider interface {
	Decide(world WorldState, unit UnitState) (StrategyDecision, error)
}

type CostBuilder interface {
	Build(world WorldState, unit UnitState, decision StrategyDecision) (pf.Grid, error)
}

type MovementPlanner interface {
	Plan(world WorldState, unit UnitState, decision StrategyDecision) (MovementPlanResult, error)
}

// MaxInt is a sentinel for unreachable cost.
const MaxInt = math.MaxInt32
