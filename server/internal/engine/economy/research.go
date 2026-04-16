// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的科研结算逻辑。

package economy

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type ResearchSystem struct{}

func (s *ResearchSystem) Run(_ donburi.World, state *domain.GameState) []event.Event {
	if state == nil {
		return nil
	}
	events := make([]event.Event, 0, len(state.Players))

	for playerID, playerState := range state.Players {
		if playerState == nil {
			continue
		}
		technologyID := playerState.Research.CurrentTargetTechnologyID
		if technologyID == "" {
			continue
		}
		// 这里故意只看“当前目标是否已经达到完成条件”，不在这里发 activation。
		// 这样 settlement 中只会出现 technology_completed，真正的激活效果统一挪到下一回合 planning start。
		validation := ValidateResearchTarget(state, playerID, technologyID)
		if !validation.OK || playerState.Research.HasCompletedTechnology(technologyID) {
			continue
		}
		if playerState.Research.CurrentTargetProgress() < validation.Technology.ResearchCost {
			continue
		}
		events = append(events, event.TechnologyCompletedEvent{
			PlayerID: playerID, TechnologyID: technologyID, Cost: validation.Technology.ResearchCost,
		})
	}

	return events
}
