# Panoptes M1 后端规则底座长期化计划

> 状态：M1 已完成  
> 日期：2026-04-30  
> 输入：M0 基线冻结  
> 范围：规则底座长期化，不实现物流/大臣/迷雾主功能

## 1. M1 目标

M1 的目标是让现有 MVP 后端成为终版国家机器的稳定内核。它不追求新增玩法，而是让后续 M2-M9 可以安全接上。

M1 成功后，应当更清楚地回答：

- resolving 每个 stage 能做什么，不能做什么
- 什么是真实状态写入口
- 哪些事件必须审计
- 哪些命令只是预留，当前显式拒绝
- headless 验收如何覆盖核心回合链

## 2. M1 非目标

M1 不做：

- 道路建造/修复接线
- 本地仓储和物流 flow
- min-cost flow 算法
- 工程单位
- 大臣默认执行
- 信息失真、战争迷雾、内政迷雾
- 战略崩溃或经济胜利
- 客户端 UI

## 3. 工作包

### M1.1 Resolving Stage Contract

目标：把 `TurnResolutionRunner` 的 stage 合同固化为文档和测试。

内容：

- 明确 `PlanningCommitStage`
- 明确 `OrderFreezeStage`
- 明确 `UnitResolutionStage`
- 明确 `MapActionStage`
- 明确 `BuildingStage`
- 明确 `EconomyStage`
- 明确 fatal turn 的短路规则

验收：

- 有 stage contract 文档或测试命名说明
- 有测试覆盖 stage 顺序
- fatal turn 不执行后续 map/building/economy 规则

### M1.2 Event Audit Contract

目标：定义真实状态变化必须如何进入事件审计。

内容：

- 梳理当前 `event` 包事件族
- 标记哪些事件会写状态，哪些只是报告/投影
- 明确事件命名、kind、data、typed projection 的最低要求
- 确认 lock-in、combat、map、building、economy 事件分类

验收：

- 事件审计规则文档化
- 关键状态变化都有事件来源
- 新增测试或检查覆盖主要事件 kind

### M1.3 Reserved Command Boundary

目标：让预留命令的当前行为明确、稳定、可测试。

内容：

- `build_road / repair_road / build_improvement / repair_improvement`
- `set_minister_directive`
- `set_war_zone / war_zone_directive`
- 任何协议存在但不进入默认主链的入口

当前 owner：

| Reserved command | Stable M1 behavior | Future owner |
|---|---|---|
| `build_road` / `repair_road` | `invalid_directive`; no planning snapshot or resolving order write | M2 road/network rules |
| `build_improvement` / `repair_improvement` | `invalid_directive`; no planning snapshot or resolving order write | M2 city network/resource facility rules |
| `set_minister_directive` | `invalid_directive`; minister proposal display may exist, but this command cannot approve or write drafts | M7 minister default execution layer |
| `set_war_zone` / `war_zone_directive` | `invalid_directive`; no player war-zone or planning directive write | M7 military minister/default execution, after M6 war substrate exists |

验收：

- 当前未接线命令返回稳定错误
- 不写入 planning snapshot
- 不进入 resolving state
- 对未来 M2/M7 留下 TODO 或文档定位

### M1.4 Headless Acceptance Harness

目标：把无客户端验证变成后端路线标准入口。

内容：

- 整理现有 debug harness 能覆盖的场景
- 给 M2-M6 预留场景命名规则
- 梳理 deterministic scenario 的输入/输出
- 明确状态摘要和结算记录器的验收口径

验收：

- 有 M1 headless 场景清单
- 能用现有测试稳定验证一局核心流程
- 输出能定位 planning、resolution、event、sync 问题

### M1.5 Durable State vs Runtime State

目标：避免后续物流/信息层把临时态和长期态混在一起。

内容：

- 梳理 `GameState`
- 梳理 `TurnRuntime.Planning`
- 梳理 `TurnRuntime.Resolving`
- 标注未来本地仓储、物流图、优先级 profile 应该落在哪一层
- 明确 `truth / observed / reported` 暂不实现，但接口方向要避免冲突

验收：

- 有状态责任表
- M2/M3 新状态有推荐落点
- 不引入客户端本地状态责任

### M1.6 Backend Regression Gate

目标：建立进入 M2 前的质量门。

内容：

- `cd server && go test ./...`
- 必要时 `cd server && go build ./...`
- 对当前文档和测试说明做一次 review
- 保证 M1 不引入未说明的 proto/data 变更

验收：

- 测试通过
- dirty diff 只包含 M1 范围内文件
- M2 可基于 M1 输出直接开任务

## 4. 建议任务拆分

| 子任务 | 优先级 | 类型 | 主要输出 |
|---|---:|---|---|
| M1.1 stage contract | P1 | docs + tests | resolving stage 合同和顺序验证 |
| M1.2 event audit | P1 | docs + tests | 事件审计规则和 kind 覆盖 |
| M1.3 reserved commands | P1 | tests + small fixes | 未接线命令稳定拒绝 |
| M1.4 headless harness | P2 | docs + tests | 无客户端验收清单 |
| M1.5 state responsibility | P1 | docs | durable/runtime/truth 边界 |
| M1.6 regression gate | P1 | verification | M1 质量门 |

## 5. M1 完成定义

- M1 子任务全部完成或明确延期
- 后端测试通过
- 未接线系统仍不会静默影响裁决
- M2 的道路/连通工作可以直接从明确边界开始

## 6. M1 完成记录

M1 已于 2026-04-30 完成。验收记录见 `docs/2026-04-30-backend-m1-regression-gate.md`。

完成输出：

- Resolving stage contract 已固化为测试和架构文档。
- Event audit contract 已固化为文档、测试和 projection 过滤规则。
- Reserved command boundary 已对道路、改良、大臣指令和 war-zone 预留入口形成稳定拒绝。
- Headless acceptance conventions 已归档，可作为 M2-M6 后端-only 验收入口。
- Durable/runtime/truth 状态责任已归档，避免后续物流和信息系统混写权威状态。
- 回归门通过：全量测试、构建、vet、buf lint 等价检查和 diff 空白检查均通过。
