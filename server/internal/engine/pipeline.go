// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现游戏引擎的流水线编排逻辑。

package engine

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/combat"
	"github.com/elebirds/panoptes/internal/engine/production"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type System interface {
	Run(world donburi.World, state *domain.GameState) []event.Event
}

type Pipeline struct {
	systems []System
}

func NewPipeline(systems ...System) *Pipeline {
	return &Pipeline{systems: systems}
}

func (p *Pipeline) Run(world donburi.World, state *domain.GameState) []event.Event {
	allEvents := make([]event.Event, 0)
	for _, sys := range p.systems {
		events := sys.Run(world, state)
		allEvents = append(allEvents, events...)
	}
	for _, e := range allEvents {
		e.Apply(world, state)
	}
	return allEvents
}

func NewEconomyPipeline() *production.EconomyRunner {
	return &production.EconomyRunner{}
}

func NewUnitResolutionPipeline() *Pipeline {
	return NewPipeline(
		combat.NewSingleStepResolver(),
		&combat.SiegeSystem{},
		&combat.CombatUpkeepSystem{},
	)
}
