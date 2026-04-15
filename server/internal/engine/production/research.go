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
		if !ok || playerState.Research.HasTechnology(technologyID) {
			continue
		}
		if playerState.Research.CurrentProgress < technology.ResearchCost {
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
		events = append(events, event.TechnologyUnlockedEvent{
			PlayerID: playerID, TechnologyID: technologyID, Cost: technology.ResearchCost,
		})
		for _, effect := range technology.ExplicitEffects {
			if effect.Type != "grant" {
				continue
			}
			// grant 不走 recipe，而是和科技解锁一起排进事件流，
			// 由 Event.Apply 负责真正加资源/刷单位。
			events = append(events, event.TechnologyGrantAppliedEvent{
				PlayerID:   playerID,
				Resources:  toResourceBag(effect.GrantResources),
				UnitTypes:  append([]string(nil), effect.GrantUnits...),
				SourceTech: technology.ID,
			})
		}
	}

	return events
}
