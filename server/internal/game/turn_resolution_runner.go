package game

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine"
	gameresolution "github.com/elebirds/panoptes/internal/game/resolution"
)

type ResolutionContext struct {
	Room      *GameRoom
	State     *domain.GameState
	Collector *gameresolution.Collector
}

type StageOutcome struct {
	Stop bool
}

type ResolutionStage interface {
	Run(*ResolutionContext) StageOutcome
}

type TurnResolutionRunner struct {
	stages []ResolutionStage
}

type PlanningCommitStage struct{}

type OrderFreezeStage struct{}

type UnitResolutionStage struct{}

type MapActionStage struct{}

type EconomyStage struct{}

func NewTurnResolutionRunner() *TurnResolutionRunner {
	return &TurnResolutionRunner{
		stages: []ResolutionStage{
			PlanningCommitStage{},
			OrderFreezeStage{},
			UnitResolutionStage{},
			MapActionStage{},
			EconomyStage{},
		},
	}
}

func (r *TurnResolutionRunner) Run(room *GameRoom) *gameresolution.Collector {
	collector := gameresolution.NewCollector()
	if room == nil || room.State() == nil {
		return collector
	}
	ctx := &ResolutionContext{
		Room:      room,
		State:     room.State(),
		Collector: collector,
	}
	for _, stage := range r.stages {
		if outcome := stage.Run(ctx); outcome.Stop {
			break
		}
	}
	return collector
}

func (PlanningCommitStage) Run(ctx *ResolutionContext) StageOutcome {
	ctx.Collector.ApplyNow(gameresolution.ChannelPlanning, ctx.State.World, ctx.State, ctx.Room.planningCommitEvents()...)
	return StageOutcome{}
}

func (OrderFreezeStage) Run(ctx *ResolutionContext) StageOutcome {
	ctx.Room.lockUnitResolutionOrders()
	return StageOutcome{}
}

func (UnitResolutionStage) Run(ctx *ResolutionContext) StageOutcome {
	runner := engine.NewUnitResolutionRunner()
	combatEvents := runner.ResolveCombat(ctx.State.World, ctx.State)
	ctx.Collector.ApplyNow(gameresolution.ChannelUnit, ctx.State.World, ctx.State, combatEvents...)
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	upkeepEvents := runner.ResolveUpkeep(ctx.State.World, ctx.State)
	ctx.Collector.ApplyNow(gameresolution.ChannelUnit, ctx.State.World, ctx.State, upkeepEvents...)
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	return StageOutcome{}
}

func (MapActionStage) Run(ctx *ResolutionContext) StageOutcome {
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	ctx.Room.refreshActiveMarchesAfterSettlement()
	ctx.Collector.ApplyNow(gameresolution.ChannelMap, ctx.State.World, ctx.State, ctx.Room.plannedMapActionEvents()...)
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	return StageOutcome{}
}

func (EconomyStage) Run(ctx *ResolutionContext) StageOutcome {
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	ctx.Collector.AppendDeferred(gameresolution.ChannelEconomy, engine.NewEconomyPipeline().Run(ctx.State.World, ctx.State)...)
	return StageOutcome{}
}
