package resolution

import (
	buildingorchestration "github.com/elebirds/panoptes/internal/building/orchestration"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine"
	"github.com/elebirds/panoptes/internal/engine/economy"
	"github.com/elebirds/panoptes/internal/event"
)

type RunnerHooks struct {
	PlanningCommitEvents func(*domain.GameState) []event.Event
	FreezeOrders         func(*domain.GameState)
	RefreshActiveMarches func(*domain.GameState)
	MapActionEvents      func(*domain.GameState) []event.Event
}

type RunnerContext struct {
	State     *domain.GameState
	Collector *Collector
	Hooks     RunnerHooks
}

type StageOutcome struct {
	Stop bool
}

// ResolutionStage is one step in the authoritative resolving pipeline.
// Stages run in TurnResolutionRunner order and may stop the rest of the turn
// by returning Stop=true. Fatal turns use that contract after combat or any
// later stage sets state.IsOver, preserving already-applied events while
// skipping unresolved map, building, and economy work that follows.
type ResolutionStage interface {
	Run(*RunnerContext) StageOutcome
}

type TurnResolutionRunner struct {
	stages []ResolutionStage
}

type PlanningCommitStage struct{}

type OrderFreezeStage struct{}

type UnitResolutionStage struct{}

type MapActionStage struct{}

type BuildingStage struct{}

type EconomyStage struct{}

// NewTurnResolutionRunner returns the fixed M1 resolving contract:
//  1. PlanningCommitStage applies planning lock-in events.
//  2. OrderFreezeStage freezes planning unit directives into resolving orders.
//  3. UnitResolutionStage applies combat first, then upkeep only if non-fatal.
//  4. MapActionStage refreshes active marches and applies map action events.
//  5. BuildingStage applies building lifecycle events.
//  6. EconomyStage applies economy runner events.
//
// Do not insert M2+ systems here without first updating this contract and its
// tests. In particular, fatal combat must continue to skip later stages.
func NewTurnResolutionRunner() *TurnResolutionRunner {
	return &TurnResolutionRunner{
		stages: []ResolutionStage{
			PlanningCommitStage{},
			OrderFreezeStage{},
			UnitResolutionStage{},
			MapActionStage{},
			BuildingStage{},
			EconomyStage{},
		},
	}
}

func (r *TurnResolutionRunner) Run(state *domain.GameState, hooks RunnerHooks) *Collector {
	collector := NewCollector()
	if state == nil {
		return collector
	}
	ctx := &RunnerContext{
		State:     state,
		Collector: collector,
		Hooks:     hooks,
	}
	for _, stage := range r.stages {
		if outcome := stage.Run(ctx); outcome.Stop {
			break
		}
	}
	return collector
}

func (PlanningCommitStage) Run(ctx *RunnerContext) StageOutcome {
	if ctx.Hooks.PlanningCommitEvents != nil {
		ctx.Collector.ApplyNow(ChannelPlanning, ctx.State.World, ctx.State, ctx.Hooks.PlanningCommitEvents(ctx.State)...)
	}
	return StageOutcome{}
}

func (OrderFreezeStage) Run(ctx *RunnerContext) StageOutcome {
	if ctx.Hooks.FreezeOrders != nil {
		ctx.Hooks.FreezeOrders(ctx.State)
	}
	return StageOutcome{}
}

func (UnitResolutionStage) Run(ctx *RunnerContext) StageOutcome {
	runner := engine.NewUnitResolutionRunner()
	combatEvents := runner.ResolveCombat(ctx.State.World, ctx.State)
	ctx.Collector.ApplyNow(ChannelUnit, ctx.State.World, ctx.State, combatEvents...)
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	upkeepEvents := runner.ResolveUpkeep(ctx.State.World, ctx.State)
	ctx.Collector.ApplyNow(ChannelUnit, ctx.State.World, ctx.State, upkeepEvents...)
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	return StageOutcome{}
}

func (MapActionStage) Run(ctx *RunnerContext) StageOutcome {
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	if ctx.Hooks.RefreshActiveMarches != nil {
		ctx.Hooks.RefreshActiveMarches(ctx.State)
	}
	if ctx.Hooks.MapActionEvents != nil {
		ctx.Collector.ApplyNow(ChannelMap, ctx.State.World, ctx.State, ctx.Hooks.MapActionEvents(ctx.State)...)
	}
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	return StageOutcome{}
}

func (BuildingStage) Run(ctx *RunnerContext) StageOutcome {
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	ctx.Collector.ApplyNow(ChannelEconomy, ctx.State.World, ctx.State, (&buildingorchestration.LifecycleSystem{}).Run(ctx.State.World, ctx.State)...)
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	return StageOutcome{}
}

func (EconomyStage) Run(ctx *RunnerContext) StageOutcome {
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	economy.NewRunner().RunWithApplier(ctx.State.World, ctx.State, func(_ economy.Stage, events []event.Event) {
		ctx.Collector.ApplyNow(ChannelEconomy, ctx.State.World, ctx.State, events...)
	})
	return StageOutcome{}
}
