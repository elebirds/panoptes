package economy

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/yohamta/donburi"
)

type Stage interface {
	Name() string
	Run(world donburi.World, state *domain.GameState) []event.Event
}

// Runner 是经济结算的唯一编排器。
// 它把经济链固定成一组显式 stage，避免“一个大 Run 里隐式穿插多个语义”。
type Runner struct {
	stages []Stage
}

type BudgetStage struct{}

type ResearchProgressStage struct{}

type ResearchCompletionStage struct{}

type DemolishStage struct{}

type BuildStage struct{}

type RecipeSelectionStage struct{}

type RecipeProgressStage struct{}

func NewRunner() *Runner {
	return &Runner{
		stages: []Stage{
			BudgetStage{},
			// 科研分成“推进”和“完成判定”两段，便于把 technology_completed 与后续 activation 解耦。
			ResearchProgressStage{},
			ResearchCompletionStage{},
			// 拆除先于建造，确保同一回合内先清场再让后续经济阶段读取最新结构。
			DemolishStage{},
			// 建造先于配方，保证新建筑不会在落地当回合立刻投入生产。
			BuildStage{},
			// 配方切换和配方推进分成两段，确保切配方后的重置状态会先 Apply，再参与本回合推进。
			RecipeSelectionStage{},
			RecipeProgressStage{},
		},
	}
}

func (r *Runner) Stages() []Stage {
	if r == nil {
		return nil
	}
	return append([]Stage(nil), r.stages...)
}

func (r *Runner) Run(world donburi.World, state *domain.GameState) []event.Event {
	return r.RunWithApplier(world, state, nil)
}

func (r *Runner) RunWithApplier(world donburi.World, state *domain.GameState, applyFn func(Stage, []event.Event)) []event.Event {
	if r == nil || state == nil {
		return nil
	}
	allEvents := make([]event.Event, 0)
	for _, stage := range r.stages {
		events := stage.Run(world, state)
		if len(events) == 0 {
			continue
		}
		allEvents = append(allEvents, events...)
		if applyFn != nil {
			// TurnResolutionRunner 会走这里，把每个 stage 的事件立刻写进 collector；
			// 这样 settlement 能保留“按阶段已经生效”的真实事件序列。
			applyFn(stage, events)
			continue
		}
		// 默认分支仍保持 stage 内立即 Apply，确保后续 stage 读到的是前序阶段已经落地后的权威状态。
		applyEvents(world, state, events)
	}
	// 点数预算是 resolving 期的临时态，跑完整条经济链后统一清空。
	state.ClearPointBudgets()
	return allEvents
}

func applyEvents(world donburi.World, state *domain.GameState, events []event.Event) {
	for _, evt := range events {
		if evt == nil {
			continue
		}
		evt.Apply(world, state)
	}
}

func (BudgetStage) Name() string { return "budget" }

func (BudgetStage) Run(_ donburi.World, state *domain.GameState) []event.Event {
	return refreshPointBudgets(state)
}

func (ResearchProgressStage) Name() string { return "research_progress" }

func (ResearchProgressStage) Run(_ donburi.World, state *domain.GameState) []event.Event {
	return applyResearchProgress(state)
}

func (ResearchCompletionStage) Name() string { return "research_completion" }

func (ResearchCompletionStage) Run(world donburi.World, state *domain.GameState) []event.Event {
	return (&ResearchSystem{}).Run(world, state)
}

func (DemolishStage) Name() string { return "demolish" }

func (DemolishStage) Run(world donburi.World, state *domain.GameState) []event.Event {
	return (&DemolishSystem{}).Run(world, state)
}

func (BuildStage) Name() string { return "build" }

func (BuildStage) Run(world donburi.World, state *domain.GameState) []event.Event {
	return (&BuildSystem{}).Run(world, state)
}

func (RecipeSelectionStage) Name() string { return "recipe_selection" }

func (RecipeSelectionStage) Run(world donburi.World, state *domain.GameState) []event.Event {
	return collectRecipeSelectionEvents(world, state)
}

func (RecipeProgressStage) Name() string { return "recipe_progress" }

func (RecipeProgressStage) Run(world donburi.World, state *domain.GameState) []event.Event {
	return runRecipeProgress(world, state)
}
