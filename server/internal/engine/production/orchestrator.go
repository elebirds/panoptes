package production

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

// EconomyRunner executes economy resolution in a fixed order while keeping
// the global pipeline semantics unchanged for other subsystems.
type EconomyRunner struct{}

func (r *EconomyRunner) Run(world donburi.World, state *domain.GameState) []event.Event {
	if state == nil {
		return nil
	}

	allEvents := make([]event.Event, 0)
	applyNow := func(events []event.Event) {
		if len(events) == 0 {
			return
		}
		allEvents = append(allEvents, events...)
		for _, evt := range events {
			if evt != nil {
				evt.Apply(world, state)
			}
		}
	}
	appendOnly := func(events []event.Event) {
		if len(events) == 0 {
			return
		}
		allEvents = append(allEvents, events...)
	}
	applyExisting := func(events []event.Event) {
		for _, evt := range events {
			if evt != nil {
				evt.Apply(world, state)
			}
		}
	}

	applyNow((&TerritoryControlSystem{}).Run(world, state))
	applyNow(refreshPointBudgets(state))
	applyNow(applyResearchProgress(state))

	deferredResearch := (&ResearchSystem{}).Run(world, state)
	applyNow((&BuildSystem{}).Run(world, state))
	applyNow((&RecipeSystem{}).Run(world, state))
	appendOnly(deferredResearch)
	applyExisting(deferredResearch)
	applyNow((&FlowSystem{}).Run(world, state))
	applyNow((&ProductionSystem{}).Run(world, state))
	applyNow((&UpkeepSystem{}).Run(world, state))

	state.ClearPointBudgets()
	return allEvents
}

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
