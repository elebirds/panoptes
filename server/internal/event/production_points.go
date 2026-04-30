// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载领域事件写入逻辑拆分后的事件族或事件辅助逻辑。

package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/yohamta/donburi"
)

type PointBudgetRefreshedEvent struct {
	PlayerID string
	Key      domain.PointKey
	Amount   int
}

func (e PointBudgetRefreshedEvent) Apply(_ donburi.World, state *domain.GameState) {
	state.RefreshPointBudget(e.PlayerID, e.Key, e.Amount)
}

func (e PointBudgetRefreshedEvent) Kind() string { return "point_budget_refreshed" }

func (e PointBudgetRefreshedEvent) String() string {
	return fmt.Sprintf("PointBudgetRefreshedEvent player=%s key=%s amount=%d", e.PlayerID, e.Key, e.Amount)
}

type PointSpentEvent struct {
	PlayerID string
	Key      domain.PointKey
	Amount   int
	Reason   string
}

func (e PointSpentEvent) Apply(_ donburi.World, state *domain.GameState) {
	if e.Amount <= 0 {
		return
	}
	_ = state.ConsumePoints(e.PlayerID, domain.PointBag{
		e.Key: e.Amount,
	})
}

func (e PointSpentEvent) Kind() string { return "point_spent" }

func (e PointSpentEvent) String() string {
	return fmt.Sprintf("PointSpentEvent player=%s key=%s amount=%d reason=%s", e.PlayerID, e.Key, e.Amount, e.Reason)
}

type IndustryOutputRefreshedEvent struct {
	PlayerID string
	Amount   int
}

func (e IndustryOutputRefreshedEvent) Apply(_ donburi.World, state *domain.GameState) {
	if e.Amount <= 0 {
		return
	}
	state.RefreshPointBudget(e.PlayerID, domain.PointIndustryOutput, e.Amount)
}

func (e IndustryOutputRefreshedEvent) Kind() string { return "industry_output_refreshed" }

func (e IndustryOutputRefreshedEvent) String() string {
	return fmt.Sprintf("IndustryOutputRefreshedEvent player=%s amount=%d", e.PlayerID, e.Amount)
}
