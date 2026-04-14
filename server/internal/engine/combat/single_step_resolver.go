// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现单位结算引擎的单步解析逻辑。

package combat

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type SingleStepResolver struct {
	phases []ResolutionPhase
}

// NewSingleStepResolver 对应当前 V1 规格的“单步 WEGO”实现。
// 这里把阶段顺序显式固定下来，方便和设计文档逐项对照，也为未来的 MultiStepResolver 预留替换点。
func NewSingleStepResolver() *SingleStepResolver {
	return &SingleStepResolver{
		phases: []ResolutionPhase{
			&SnapshotPhase{},
			&PathPlanningPhase{},
			&ConflictPhase{},
			&MovementApplyPhase{},
			&DamagePhase{},
			&CleanupPhase{},
		},
	}
}

func (r *SingleStepResolver) Resolve(world donburi.World, state *domain.GameState) []event.Event {
	// 所有策略对象都挂在上下文上，而不是散落在各阶段内部，
	// 这样后续要替换 block / retaliation / damage 策略时不需要重写主 resolver。
	ctx := &ResolutionContext{
		World:             world,
		State:             state,
		Plans:             make(map[string]*OrderPlan),
		ActualPositions:   make(map[string]domain.Position),
		CurrentHP:         make(map[string]int),
		DeadUnits:         make(map[string]bool),
		EdgeConflictUnits: make(map[string]bool),
		NodeConflictUnits: make(map[string]bool),
		OrderResolvers: map[domain.UnitResolutionAction]OrderResolver{
			domain.UnitResolutionActionMove:   MoveResolver{},
			domain.UnitResolutionActionAttack: AttackResolver{},
			domain.UnitResolutionActionHold:   HoldResolver{},
			domain.UnitResolutionActionCharge: ChargeResolver{},
		},
		BlockRule:         StaticSnapshotBlockRule{},
		ConflictDetectors: []ConflictDetector{EdgeConflictDetector{}, NodeConflictDetector{}},
		RetaliationPolicy: DefaultRetaliationPolicy{},
		DamageResolver:    DefaultDamageResolver{},
		RoutePlanner:      NewWeightedRoutePlanner(DefaultTerrainCostPolicy{}),
		TurnPlanner:       DefaultTurnSegmentPlanner{CostPolicy: DefaultTerrainCostPolicy{}},
	}

	for _, phase := range r.phases {
		phase.Apply(ctx)
	}

	return ctx.Events
}

func (r *SingleStepResolver) Run(world donburi.World, state *domain.GameState) []event.Event {
	return r.Resolve(world, state)
}

type CleanupPhase struct{}

// CleanupPhase 目前作为扩展挂点保留。
// V1 的实际状态收尾主要由 event.Apply 完成，这里先不重复写状态变更逻辑。
func (CleanupPhase) Apply(*ResolutionContext) {}
