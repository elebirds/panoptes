package strategy

import pf "github.com/elebirds/panoptes/internal/algo/pathfinding"

// ---------------------------------------------------------------------------
// 执行目标
// ---------------------------------------------------------------------------

type ExecutionTargetType string

const (
	ExecutionTargetGoal ExecutionTargetType = "goal"
)

type ExecutionTarget struct {
	Type   ExecutionTargetType `json:"type"`
	Target pf.Point            `json:"target"`
	Reason string              `json:"reason"`
}
