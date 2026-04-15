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
	events := make([]event.Event, 0, len(state.Players))

	for playerID, playerState := range state.Players {
		if playerState == nil {
			continue
		}
		technologyID := playerState.Research.CurrentTargetTechnologyID
		if technologyID == "" {
			continue
		}
		technology, ok := staticdata.Default().GetTechnology(technologyID)
		if !ok || playerState.Research.HasCompletedTechnology(technologyID) {
			continue
		}
		if playerState.Research.CurrentTargetProgress() < technology.ResearchCost {
			continue
		}
		prereqsMet := true
		for _, prereq := range technology.Prerequisites {
			switch prereq.Type {
			case "technology_unlocked":
				if !state.HasTechnologyUnlocked(playerID, prereq.TargetID) {
					prereqsMet = false
				}
			case "policy_active":
				if !state.IsPolicyActive(playerID, prereq.TargetID) {
					prereqsMet = false
				}
			}
			if !prereqsMet {
				break
			}
		}
		if !prereqsMet {
			continue
		}
		events = append(events, event.TechnologyUnlockedEvent{
			PlayerID: playerID, TechnologyID: technologyID, Cost: technology.ResearchCost,
		})
	}

	return events
}
