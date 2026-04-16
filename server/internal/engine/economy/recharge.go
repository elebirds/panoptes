// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的点数回充结算逻辑。

package economy

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type RechargeSystem struct{}

func (s *RechargeSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	// 这是旧版回充系统的保留实现。
	// 现在主链预算刷新已经统一交给 BudgetStage，这里默认不再接入 resolving 顺序。
	rules := staticdata.Default().Rules()
	events := make([]event.Event, 0, len(state.Players))
	for playerID := range state.Players {
		events = append(events, event.IndustryOutputRefreshedEvent{PlayerID: playerID, Amount: state.EffectiveIndustryOutput(playerID)})
		events = append(events, event.ResearchProgressAppliedEvent{PlayerID: playerID, Amount: max(state.EffectiveResearchOutput(playerID), rules.BaseResearchOutputPerTurn)})
	}
	return events
}

func max(a int, b int) int {
	if a > b {
		return a
	}
	return b
}
