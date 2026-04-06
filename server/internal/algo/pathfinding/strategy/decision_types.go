package strategy

import pf "github.com/elebirds/panoptes/internal/algo/pathfinding"

// ---------------------------------------------------------------------------
// 策略决策层（领域语义，不传入 A*）
// ---------------------------------------------------------------------------

type StrategyType string

const (
	StrategyAttack   StrategyType = "attack"
	StrategyPathfind StrategyType = "pathfind"
	StrategyDefend   StrategyType = "defend"
)

type StrategyDecision struct {
	Strategy StrategyType `json:"strategy"`
	Goal     pf.Point     `json:"goal"`
	Reason   string       `json:"reason"`
}

// ---------------------------------------------------------------------------
// 编排结果
// ---------------------------------------------------------------------------

type MovementPlanResult struct {
	Decision        StrategyDecision `json:"decision"`
	ExecutionTarget ExecutionTarget  `json:"execution_target"`
	Result          pf.PathResult    `json:"result"`
}
