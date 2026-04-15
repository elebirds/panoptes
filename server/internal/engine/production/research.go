// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现经济结算引擎的科研结算逻辑。

package production

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type ResearchSystem struct{}

func (s *ResearchSystem) Run(_ donburi.World, state *domain.GameState) []event.Event {
	if state == nil {
		return nil
	}
	events := make([]event.Event, 0, len(state.TurnRuntime.Planning.ResearchOrders))
	spentByPlayer := make(map[string]int)

	for _, order := range state.TurnRuntime.Planning.ResearchOrders {
		playerState, ok := state.Players[order.PlayerID]
		if !ok || playerState == nil {
			continue
		}
		technology, ok := staticdata.Default().GetTechnology(order.TechnologyID)
		if !ok || playerState.Research.HasTechnology(order.TechnologyID) {
			continue
		}
		available := playerState.Research.CurrentProgress - spentByPlayer[order.PlayerID]
		if available < technology.ResearchCost {
			continue
		}
		prereqsMet := true
		for _, prereq := range technology.Prerequisites {
			if prereq.Type == "technology_unlocked" && !playerState.Research.HasTechnology(prereq.TargetID) {
				prereqsMet = false
				break
			}
		}
		if !prereqsMet {
			continue
		}
		spentByPlayer[order.PlayerID] += technology.ResearchCost
		events = append(events, event.TechnologyUnlockedEvent{
			PlayerID: order.PlayerID, TechnologyID: order.TechnologyID, Cost: technology.ResearchCost,
		})
		for _, effect := range technology.ExplicitEffects {
			if effect.Type != "grant" {
				continue
			}
			// grant 不走 recipe，而是和科技解锁一起排进事件流，
			// 由 Event.Apply 负责真正加资源/刷单位。
			events = append(events, event.TechnologyGrantAppliedEvent{
				PlayerID:   order.PlayerID,
				Resources:  toResourceBag(effect.GrantResources),
				UnitTypes:  append([]string(nil), effect.GrantUnits...),
				SourceTech: technology.ID,
			})
		}
	}

	return events
}
