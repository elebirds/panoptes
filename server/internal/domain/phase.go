// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义领域模型的阶段枚举与语义定义。

package domain

type TurnPhase string

const (
	PhasePlanning  TurnPhase = "planning"
	PhaseResolving TurnPhase = "resolving"
	PhaseTurnReport TurnPhase = "turn_report"
)

func (p TurnPhase) String() string {
	return string(p)
}

func (p TurnPhase) IsPlanning() bool {
	return p == PhasePlanning
}
