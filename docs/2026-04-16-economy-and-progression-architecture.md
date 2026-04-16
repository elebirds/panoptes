# 2026-04-16 经济与 Progression 架构归正说明

> 本文记录 2026-04-16 这轮重构后的目标落地结果，重点说明经济系统、科技完成/激活、PlanningStart 事件面与统一投影的现状。

## 1. 现在的边界

- `engine/economy` 只负责同回合经济裁决：
  - 建筑生命周期
  - 点数预算
  - 研究推进
  - 科技完成判定
  - 建造
  - 配方切换
  - 配方推进
- `game/session/PlanningStartRunner` 只负责“上一回合已完成、这一回合开始正式生效”的 progression：
  - `technology_activated`
  - `technology_grant_applied`
  - `institution_loadout_activated`

## 2. 经济 Runner

`engine/economy.NewRunner()` 固定暴露 7 个 stage，顺序不可变：

1. `lifecycle`
2. `budget`
3. `research_progress`
4. `research_completion`
5. `build`
6. `recipe_selection`
7. `recipe_progress`

每个 stage 都只产出领域事件；状态写回只发生在 `Event.Apply()`。  
`TurnResolutionRunner` 通过 `RunWithApplier()` 按 stage 把事件直接写进 resolution collector，因此 settlement 投影看到的事件顺序与真实 stage 顺序一致。

## 3. 科技完成与激活

### 3.1 同回合完成

`ResearchCompletionStage` 发出 `TechnologyCompletedEvent`，对外事件字符串是 `technology_completed`。

它只做三件事：

- 把研究进度补齐到 cost
- 记录 completed turn
- 清空当前研究目标

它**不会**：

- 解锁 building / recipe
- 增加 institution slot
- grant 资源或单位

### 3.2 下一回合开始激活

`PlanningStartRunner` 的 `TechnologyActivationStage` 读取 pending activation 科技，并发出：

- `TechnologyActivatedEvent`
- 必要时的 `TechnologyGrantAppliedEvent`

其中：

- `technology_activated` 只出现在 `MsgPlanningStart.planning_start_events`
- `technology_completed` 只出现在 `MsgTurnSettlement.sections[economy]`

这样客户端看到的外部事件时序与真实规则时序一致，不再把“下一回合才生效”的内容伪装成上一回合 settlement。

## 4. 统一投影

统一投影入口现在是 `server/internal/game/projection`：

- `ProjectTurnSettlement(...)`
- `ProjectPlanningStartEvents(...)`

`TurnEventFromEvent(...)` 只保留一份，PlanningStart 和 TurnSettlement 共用同一套 `event -> TurnEvent` 映射。

## 5. 共享校验器

经济相关输入校验现在集中在 `engine/economy/validation.go`：

- `ValidateResearchTarget`
- `ValidateBuildOrder`
- `ValidateRecipeSelection`

`planning.Service` 用它们做即时反馈，经济结算阶段用同一套规则做复核。  
这样 research/build/recipe 的授权与合法性不再由 planning 和 settlement 各写一套。
