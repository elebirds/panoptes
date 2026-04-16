package session

import "github.com/elebirds/panoptes/internal/domain"

type PlanningStartRunner struct {
	stages []planningStartStage
}

type planningStartStage interface {
	Run(state *domain.GameState)
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

func (r *PlanningStartRunner) Run(state *domain.GameState) {
	if state == nil {
		return
	}
	for _, playerState := range state.Players {
		if playerState == nil {
			continue
		}
		playerState.Research.EnsureProgressMaps()
		playerState.Institutions.EnsureMaps()
	}
	for _, stage := range r.stages {
		stage.Run(state)
	}
}

func (TechnologyActivationStage) Run(state *domain.GameState) {
	activatePendingTechnologies(state)
}

func (InstitutionPromotionStage) Run(state *domain.GameState) {
	promoteInstitutionLoadouts(state)
}

func (PlanningRefreshStage) Run(state *domain.GameState) {
	_ = state
	// 当前 MVP 保持 token 只在使用时扣减，不在 planning start 自动恢复。
}
