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

func NewDomesticPipeline() *Pipeline {
	return NewPipeline(
		&production.BuildSystem{},
		&production.FlowSystem{},
		&production.ProductionSystem{},
		&production.UpkeepSystem{},
		&production.RechargeSystem{},
	)
}

func NewCombatPipeline() *Pipeline {
	return NewPipeline(
		&combat.MovementSystem{},
		&combat.ConflictSystem{},
		&combat.BattleSystem{},
		&combat.SiegeSystem{},
		&combat.RangedSystem{},
		&combat.DestroySystem{},
		&combat.CombatUpkeepSystem{},
	)
}
