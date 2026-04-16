package economy

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
)

// refreshPointBudgets 负责把“这一回合玩家理论可用的点数产出”写成临时预算。
// 预算不是跨回合资源，只在当前 resolving 期内用于 build / research / recipe 等系统竞争消耗。
func refreshPointBudgets(state *domain.GameState) []event.Event {
	if state == nil {
		return nil
	}
	events := make([]event.Event, 0, len(state.Players)*2)
	for playerID := range state.Players {
		events = append(events,
			event.PointBudgetRefreshedEvent{
				PlayerID: playerID,
				Key:      domain.PointResearchOutput,
				Amount:   state.EffectiveResearchOutput(playerID),
			},
			event.PointBudgetRefreshedEvent{
				PlayerID: playerID,
				Key:      domain.PointIndustryOutput,
				Amount:   state.EffectiveIndustryOutput(playerID),
			},
		)
	}
	return events
}

// applyResearchProgress 只做 research_output -> 当前科技进度 的投入。
// 它不负责判定科技是否完成，完成判定留给后续 ResearchCompletionStage。
func applyResearchProgress(state *domain.GameState) []event.Event {
	if state == nil {
		return nil
	}
	events := make([]event.Event, 0, len(state.Players)*2)
	for playerID, playerState := range state.Players {
		if playerState == nil || playerState.Research.CurrentTargetTechnologyID == "" {
			continue
		}
		amount := state.EnsurePointBudget(playerID).Get(domain.PointResearchOutput)
		if amount <= 0 {
			continue
		}
		events = append(events,
			event.PointSpentEvent{
				PlayerID: playerID,
				Key:      domain.PointResearchOutput,
				Amount:   amount,
				Reason:   "research_progress",
			},
			event.ResearchProgressAppliedEvent{
				PlayerID: playerID,
				Amount:   amount,
			},
		)
	}
	return events
}
