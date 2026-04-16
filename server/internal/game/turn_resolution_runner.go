package game

import (
	buildingorchestration "github.com/elebirds/panoptes/internal/building/orchestration"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine"
	"github.com/elebirds/panoptes/internal/engine/economy"
	"github.com/elebirds/panoptes/internal/event"
	gameresolution "github.com/elebirds/panoptes/internal/game/resolution"
)

type ResolutionContext struct {
	Room      *GameRoom
	State     *domain.GameState
	Collector *gameresolution.Collector
}

// TurnResolutionRunner 是 resolving 期的顶层真相。
// 它不直接实现具体规则，而是只固定主链顺序，并决定哪些阶段会在 fatal state 后提前停止。
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

type BuildingStage struct{}

type EconomyStage struct{}

func NewTurnResolutionRunner() *TurnResolutionRunner {
	return &TurnResolutionRunner{
		stages: []ResolutionStage{
			// 先把 planning 草案 lock-in 成正式输入，再冻结单位命令。
			PlanningCommitStage{},
			OrderFreezeStage{},
			// 单位与地图动作跑完以后，经济阶段才能读取这回合已经稳定下来的占领与建筑状态。
			UnitResolutionStage{},
			MapActionStage{},
			BuildingStage{},
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

func (BuildingStage) Run(ctx *ResolutionContext) StageOutcome {
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	// 建筑阶段先于经济阶段执行，保证 budget / build / recipe 读取到的都是
	// 已经完成接管、城市陷落与 pending_activation 解释后的稳定建筑状态。
	ctx.Collector.ApplyNow(gameresolution.ChannelEconomy, ctx.State.World, ctx.State, (&buildingorchestration.LifecycleSystem{}).Run(ctx.State.World, ctx.State)...)
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	return StageOutcome{}
}

func (EconomyStage) Run(ctx *ResolutionContext) StageOutcome {
	if ctx.State.IsOver {
		return StageOutcome{Stop: true}
	}
	// 经济 runner 内部仍按 stage 运行，但这里要求它把每个 stage 的事件立即写入 collector。
	// 这样 settlement 投影拿到的是已经按真实时序生效过的 economy 事件流。
	economy.NewRunner().RunWithApplier(ctx.State.World, ctx.State, func(_ economy.Stage, events []event.Event) {
		ctx.Collector.ApplyNow(gameresolution.ChannelEconomy, ctx.State.World, ctx.State, events...)
	})
	return StageOutcome{}
}
