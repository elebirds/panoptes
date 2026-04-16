package session

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
)

type PlanningStartResult struct {
	Events []event.Event
}

// PlanningStartRunner 只处理“上一回合已完成，但这一回合开始才正式生效”的内容。
// 它和经济 runner 分工明确：经济负责完成，planning start 负责激活。
type PlanningStartRunner struct {
	stages []planningStartStage
}

type planningStartStage interface {
	Run(*planningStartContext)
}

type planningStartContext struct {
	state  *domain.GameState
	result *PlanningStartResult
}

type TechnologyActivationStage struct{}

type InstitutionPromotionStage struct{}

type PlanningRefreshStage struct{}

func NewPlanningStartRunner() *PlanningStartRunner {
	return &PlanningStartRunner{
		stages: []planningStartStage{
			TechnologyActivationStage{},
			InstitutionPromotionStage{},
			PlanningRefreshStage{},
		},
	}
}

func (r *PlanningStartRunner) Run(state *domain.GameState) *PlanningStartResult {
	result := &PlanningStartResult{}
	if state == nil {
		return result
	}
	for _, playerState := range state.Players {
		if playerState == nil {
			continue
		}
		playerState.Research.EnsureProgressMaps()
		playerState.Institutions.EnsureMaps()
	}
	ctx := &planningStartContext{state: state, result: result}
	for _, stage := range r.stages {
		stage.Run(ctx)
	}
	return result
}

func (ctx *planningStartContext) apply(events ...event.Event) {
	if ctx == nil || ctx.state == nil {
		return
	}
	for _, evt := range events {
		if evt == nil {
			continue
		}
		// planning start 事件需要同时满足两件事：
		// 1. 真实写回权威状态；
		// 2. 保留下来投影进 MsgPlanningStart，让客户端明确看到 activation 边界。
		ctx.result.Events = append(ctx.result.Events, evt)
		evt.Apply(ctx.state.World, ctx.state)
	}
}

func (TechnologyActivationStage) Run(ctx *planningStartContext) {
	for _, evt := range activatePendingTechnologies(ctx.state) {
		ctx.apply(evt)
	}
}

func (InstitutionPromotionStage) Run(ctx *planningStartContext) {
	for _, evt := range promoteInstitutionLoadouts(ctx.state) {
		ctx.apply(evt)
	}
}

func (PlanningRefreshStage) Run(_ *planningStartContext) {
	// 当前 MVP 保持 token 只在使用时扣减，不在 planning start 自动恢复。
	// 这里保留为空 stage，是为了把“开回合刷新语义”的位置固定住，后续若新增刷新规则可直接落在这里。
}
