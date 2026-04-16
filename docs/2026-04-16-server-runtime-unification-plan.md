# Panoptes 服务端运行时统一化设计（MVP 核心版）

> 日期：2026-04-16  
> 状态：已落地到当前服务端主链  
> 适用范围：`server/internal/game/*`、`server/internal/engine/*`、`server/internal/event/*`  
> 配套现状文档：`docs/SERVER_RUNTIME_ARCHITECTURE.md`

## 1. 这份文档解决什么

这不是“代码现状说明”，而是本轮服务端运行时统一化改造的目标设计与裁决记录。它回答的是：

- 为什么顶层不再把通用 `Pipeline` 当作运行时核心抽象。
- 为什么 MVP 主链改为 `PlanningStartRunner -> TurnResolutionRunner -> SettlementProjector`。
- 每个 stage 的职责边界是什么。
- 为什么 `settle_city` 要改为领域事件驱动。
- 为什么非 MVP 的 war zone 指令要显式拒绝，而不是继续写进 snapshot 或 runtime。

如果你要看“当前代码现在怎么跑”，请读 `docs/SERVER_RUNTIME_ARCHITECTURE.md`。  
如果你要看“为什么要这样改、后续扩展应遵守什么边界”，请读本文。

## 2. 设计裁决

本轮统一化改造的硬裁决如下：

1. 顶层回合编排以 `Runner` 为核心，不再以通用 `Pipeline` 作为真实主链抽象。
2. 顶层主链固定为 `PlanningStartRunner -> TurnResolutionRunner -> SettlementProjector`。
3. 正式状态写回统一收敛到 `event.Apply()`；业务 stage 不再直接拼 `pb.TurnEvent`。
4. `settle_city` 改为 `MapActionStage -> CityFoundedEvent.Apply()` 的事件链，不再在 `game/map_actions.go` 里直接写世界。
5. `set_war_zone`、`war_zone_directive` 在 MVP 期显式拒绝，不再进入 planning snapshot、runtime 或结算链。
6. `minister` 与 `war zone` 不属于 MVP，本轮不为它们保留核心运行时位置。

## 3. 为什么顶层不用通用 Pipeline

### 3.1 Pipeline 与 Runner 的分工

两者的区别不在于“是否顺序执行”，而在于“抽象约束是否统一”。

- `Pipeline`
  - 适合一组共享统一语义的 system
  - 强调统一接口、统一收集、统一 apply
  - 适合规则简单、阶段边界稳定的子系统
- `Runner`
  - 适合真实业务时序本身就有条件跳转、立即 apply、fatal stop、延后生效等差异
  - 强调显式编排，而不是把所有步骤硬塞进统一 system 协议

Panoptes 当前的服务端结算链天然带有这些时序特征：

- planning lock-in 需要先写入 active state
- combat fatal turn 会中止后续 map/economy
- economy 需要“阶段间立即 apply”
- 科技完成需要“本回合完成、下回合生效”

因此：

- 顶层回合主链必须是 `Runner`
- 子系统内部是否继续使用 `Pipeline`，取决于该子系统自身是否满足统一 apply 语义

### 3.2 当前保留的原则

这并不意味着彻底否定 `Pipeline`。

更准确的原则是：

- `Runner` 是运行时主链抽象
- `Pipeline` 是子系统内部的可选工具

## 4. 顶层运行时模型

### 4.1 PlanningStartRunner

职责：只处理“回合开始时才正式生效”的逻辑。

当前 stage：

1. `TechnologyActivationStage`
2. `InstitutionPromotionStage`
3. `PlanningRefreshStage`

其中：

- `TechnologyActivationStage`
  - 负责把上回合已完成的科技提升为 active
  - 应用显式效果
  - 解锁 building / recipe / policy candidate / institution slot
- `InstitutionPromotionStage`
  - 负责把 `PendingPolicyIDs` 提升为 `ActivePolicyIDs`
- `PlanningRefreshStage`
  - 负责“开回合刷新”类规则的统一入口
  - 本轮已明确：**token 在 MVP 中不在 planning start 自动刷新**

### 4.2 TurnResolutionRunner

职责：只处理“planning 输入锁定后的本回合裁决”。

当前固定 stage 顺序：

1. `PlanningCommitStage`
2. `OrderFreezeStage`
3. `UnitResolutionStage`
4. `MapActionStage`
5. `EconomyStage`

语义：

- `PlanningCommitStage`
  - 生成并 apply 国策、科研目标、institution loadout 的 lock-in 事件
- `OrderFreezeStage`
  - 把 planning 单位命令冻结到 resolving 订单
- `UnitResolutionStage`
  - 执行 combat + combat upkeep
  - 若 `state.IsOver`，直接 stop
- `MapActionStage`
  - 执行地图动作
  - MVP 当前只保留 `settle_city`
- `EconomyStage`
  - 执行建筑生命周期、点数预算、科研推进、建造、配方

### 4.3 SettlementProjector

职责：统一把领域事件与最终权威状态投影成 `MsgTurnSettlement`。

关键裁决：

- 业务 stage 只产出 `event.Event`
- `pb.TurnEvent` 只在投影层构造
- `SettlementSection` 继续保持 `unit / map / economy`
- `planning` lock-in 事件并入 `economy` section 对外展示

## 5. Stage 合同与 Collector

### 5.1 Stage 合同

顶层 stage 统一遵守如下语义：

- 接收同一个 `ResolutionContext`
- 由 stage 自己决定是立即 apply，还是只先记录
- 通过 `StageOutcome.Stop` 告诉顶层 runner 是否中止后续 stage

这保证了：

- 顶层编排只有一个真相
- 每个子系统仍可保留自己适合的时序细节

### 5.2 ResolutionCollector

`ResolutionCollector` 的职责不是替代权威状态，而是记录“本回合各 channel 发生了哪些领域事件”。

当前 channel：

- `planning`
- `unit`
- `map`
- `economy`

当前能力：

- `ApplyNow()`
  - 记录事件
  - 立即执行 `event.Apply()`
- `AppendDeferred()`
  - 只记录事件
  - 用于“事件已在子系统内部 apply，但仍需进入结算投影”的情况

设计目的：

- 统一 settlement 分组来源
- 避免业务层直接拼 protobuf
- 保持 headless 测试可以直接断言领域事件序列

## 6. event-only state mutation 原则

本轮明确收紧为：

- Planning 阶段只允许写 `TurnRuntime.Planning`
- PlanningStart 只允许做回合开始 promotion/refresh
- Resolving 阶段的正式世界变化必须经由 `event.Apply()`

这条原则的核心价值在于：

- 减少世界状态的写入口
- 提升 headless 回归与事件断言能力
- 让 settlement 投影可以只依赖事件和最终状态，不依赖隐藏 side effect

## 7. 为什么 `settle_city` 要事件化

原先 `settle_city` 在 `game/map_actions.go` 中直接：

- 改 territory owner
- 放 `city_core`
- 写 `CityState`
- 删除 settler
- 同时手写 `pb.TurnEvent`

这会带来三个问题：

1. map action 成为“绕过事件层”的例外路径
2. 业务规则和 protobuf 投影混在一起
3. 测试只能从最终状态和 protobuf 侧面验证，不能直接验证领域事件

本轮改造后：

- `MapActionStage` 只生成 `CityFoundedEvent` / `CityFoundingFailedEvent`
- 真正世界写回在 `CityFoundedEvent.Apply()`
- settlement 的 `city_founded / settle_city_failed` 由投影层统一生成

## 8. 非 MVP 入口的处理策略

### 8.1 为什么要显式拒绝

对于 `set_war_zone`、`war_zone_directive` 这类已经出现在 proto 中、但不在 MVP 裁决链内的入口，如果继续：

- 接受输入
- 回显到 snapshot
- 但 resolving 永远不消费

那么客户端、调试和测试都会得到错误暗示：看起来像“功能存在，只是还没完全实现”，实际上它会制造状态歧义和产品误导。

因此本轮改为：

- 协议字段保留
- 服务端 planning 阶段直接返回 `invalid_directive`
- 不写入 `TurnRuntime`
- 不出现在 planning snapshot 主字段中

### 8.2 当前范围

本轮已明确按非 MVP 处理：

- `set_war_zone`
- `war_zone_directive`

本轮仍保留但不视为 MVP 闭环的一类输入：

- `minister` 相关入口

原因是这部分协议/输入仍在仓库中承担一定实验性或占位职责，但不再进入本轮“核心运行时统一化”的设计口径。

## 9. token 语义的收口

本轮专门对 token 做出代码级裁决：

- MVP 当前语义：**token 不在 planning start 自动恢复**
- `PlanningRefreshStage` 作为唯一显式入口，当前保持 no-op，并在文档中写明

这项裁决的意义不在于“永久决定 token 规则”，而在于先消除模糊状态：

- 代码不再让人误以为每回合自动刷新
- 文档不再出现“看起来应该刷新、但代码没有统一入口”的歧义

## 10. 与现状文档的关系

本文不是现状说明。

当前仓库需要同时保留两类文档：

- `docs/SERVER_RUNTIME_ARCHITECTURE.md`
  - 记录“现在的代码真实如何运行”
- `docs/2026-04-16-server-runtime-unification-plan.md`
  - 记录“本轮统一化设计为什么这样收口、哪些边界是硬裁决”

两者必须同时维护，避免出现：

- 现状文档写的是旧结构
- 设计文档写的是目标蓝图
- 代码跑的是第三种东西

## 11. 本轮落地结果

本轮已经落地的关键点：

1. `PlanningStartRunner` 已成为 planning start 的显式时机入口。
2. `TurnResolutionRunner` 已成为 resolving 的统一顶层编排器。
3. `ResolutionCollector` 已成为 settlement 分组的统一事件来源。
4. `settle_city` 已改为 `CityFoundedEvent` 驱动。
5. `BuildTurnSettlement()` 已改为按 channel 消费领域事件。
6. war zone 相关 planning 命令已改为显式拒绝。
7. economy 默认主链已不再宣称执行空壳 `Flow / Production / production.Upkeep`。

## 12. 后续扩展约束

后续如果要继续扩展运行时，必须遵守下面两条：

1. 新的 MVP 内地图动作，优先接入 `MapActionStage -> event.Apply()`，不要再回到 `game` 层直接改世界。
2. 非 MVP 的实验性输入，不要再以“接受并回显但不执行”的方式接入 planning 主链。

一句话总结：

Panoptes 当前服务端的目标不是“做一个抽象最漂亮的通用 pipeline 框架”，而是“让 MVP 主链只有一个明确真相，并且每个正式状态变化都能被事件和 headless 回归稳定捕获”。
