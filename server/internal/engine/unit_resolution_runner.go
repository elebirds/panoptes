package engine

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/combat"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type UnitResolutionRunner struct{}

func NewUnitResolutionRunner() *UnitResolutionRunner {
	return &UnitResolutionRunner{}
}

func (r *UnitResolutionRunner) ResolveCombat(world donburi.World, state *domain.GameState) []event.Event {
	if world == nil || state == nil {
		return nil
	}
	return combat.NewSingleStepResolver().Run(world, state)
}

func (r *UnitResolutionRunner) ResolveUpkeep(world donburi.World, state *domain.GameState) []event.Event {
	if world == nil || state == nil || state.IsOver {
		return nil
	}
	return (&combat.CombatUpkeepSystem{}).Run(world, state)
}

func (r *UnitResolutionRunner) Run(world donburi.World, state *domain.GameState) ([]event.Event, []event.Event) {
	if world == nil || state == nil {
		return nil, nil
	}

	combatEvents := r.ResolveCombat(world, state)
	for _, evt := range combatEvents {
		evt.Apply(world, state)
	}
	if state.IsOver {
		return combatEvents, nil
	}

	upkeepEvents := r.ResolveUpkeep(world, state)
	for _, evt := range upkeepEvents {
		evt.Apply(world, state)
	}
	return combatEvents, upkeepEvents
}
