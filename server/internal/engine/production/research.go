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
	events := make([]event.Event, 0, len(state.PendingResearchOrders))
	spentByPlayer := make(map[string]int)

	for _, order := range state.PendingResearchOrders {
		playerState, ok := state.Players[order.PlayerID]
		if !ok || playerState == nil {
			continue
		}
		technology, ok := staticdata.Default().GetTechnology(order.TechnologyID)
		if !ok || playerState.Research.HasTechnology(order.TechnologyID) {
			continue
		}
		available := playerState.Research.TechPoints - spentByPlayer[order.PlayerID]
		if available < technology.TechPointCost {
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
		spentByPlayer[order.PlayerID] += technology.TechPointCost
		events = append(events, event.TechnologyUnlockedEvent{
			PlayerID: order.PlayerID, TechnologyID: order.TechnologyID, Cost: technology.TechPointCost,
		})
		for _, effect := range technology.Effects {
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
