# Panoptes GDD V1 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **状态更新（2026-04-16）**：M1 / Chunk 1 已完成，Chunk 2 已按“最小收口”定义完成，Chunk 3、Chunk 4 与 Chunk 6 已按 revised/final plan 落地到 `main`。当前仓库已补齐主城 bootstrap、建城/建筑放置统一校验、配方低效推进、城市陷落与设施延时接管，以及基于 `MsgIssueUnitOrderResult + can_attack_structures + Targetable combat` 的主线战争闭环；本文档中的 Chunk 1-4、Chunk 6 勾选状态已同步到当前实现现状。

**Goal:** 在现有 `planning / resolving`、`orders / turn / settlement` 骨架之上，落地 [2026-04-15-panoptes-gdd-v1-structured.md](./gdd/2026-04-15-panoptes-gdd-v1-structured.md) 的当前基线（MVP），并为中期、长期系统预留稳定扩展接口。

**Architecture:** 以 `data/` 为唯一玩法作者源，`protocol/*.proto` 为跨端契约，`server/internal/domain + ecs + engine + event` 为服务端权威规则实现，`client/Assets/Scripts/Runtime/*` 为纯展示与输入层。实施顺序采用“静态数据与协议先对齐，服务端规则闭环先成立，客户端垂直切片随后跟进”的路线，不在 MVP 阶段硬做完整物流、双层迷雾、大臣接管和 PVE。

**Tech Stack:** Go 服务端、donburi ECS、protobuf/buf、静态数据生成链、Unity 2022.3 LTS 客户端、Transport V2。

---

## 1. 本文档解决什么

本文档不是系统创意说明，而是面向工程落地的主实施计划。它回答四个问题：

1. 以新版 GDD 为准，当前应当先做哪些系统，不做哪些系统。
2. 现有 `TURN_V2_REFACTOR_PLAN.md` 完成后，下一阶段应按什么顺序继续推进。
3. 服务端、协议、客户端和静态数据分别需要改哪些边界。
4. 哪些内容是 MVP 必须兑现的，哪些内容只保留接口与演进方向。

## 2. 前置条件与硬裁决

### 2.1 前置文档

本计划以以下文档为输入：

- [GDD 结构版](./gdd/2026-04-15-panoptes-gdd-v1-structured.md)
- [GDD 细化版](./gdd/2026-04-15-panoptes-gdd-v1-detailed.md)
- [回合重构计划](./TURN_V2_REFACTOR_PLAN.md)
- [无客户端调试与规则验证方案](./2026-04-15-headless-debug-and-rule-validation.md)

### 2.2 硬裁决

1. 当前实施目标只覆盖 GDD 的 `当前基线`。`中期扩展` 与 `长期目标` 只做接口预留，不做功能兑现。
2. `TURN_V2_REFACTOR_PLAN.md` 视为前置基线，不重复规划双阶段到统一回合的重构。
3. 玩法数据必须继续走 `data/ -> data-gen -> proto/gen` 的统一链路，不允许在服务端或客户端硬编码系统内容。
4. 客户端仍是纯展示层，任何合法性判断、经济计算、科技推进、战斗结果和建筑归属都在服务端完成。
5. 当前版本的玩家控制模式仍是直接指挥；大臣系统仅保留数据接口与命令接管插口。
6. 当前版本的资源逻辑仍使用全局虚空库存；道路不承担经济物流，只承担地图骨架与未来网络接口。
7. 政策系统继续按“双层设计”落地：国策保持 same-turn，制度后端已具备候选池、槽位、loadout、next-turn 激活与统一 modifier 接入；正式制度 UI 与复杂切换成本仍留待后续。

### 2.3 术语归正策略

新版 GDD 的正式术语是“城市 / 城市核心 / 步兵 / 点数”。当前仓库已采用破坏式归正策略，规则如下：

- 作者源、schema、proto、生成代码、服务端视图与客户端运行时脚本统一使用新版公开语义，不保留 `castle -> city`、`warrior -> infantry`、`build_points/tech_points -> points` 的兼容壳。
- 旧公开字段、旧公开 ID 和旧结算事件名必须在运行时代码中直接删除，而不是继续桥接。
- 本轮允许暂时保留 `Castle*` 这类 Prefab、资源路径和表现层类名；它们属于资产与命名清扫范围，不再影响协议与玩法语义。

## 3. 当前实施范围冻结

### 3.1 MVP 必做

- 统一 `planning / resolving` 回合下的完整可玩闭环
- 主城开局存在，开拓者自由建城
- 一级原始资源
- 全局虚空库存
- 两类点数：科研产出、建造/工业产出
- 统一修正系统与显式效果/修正效果分层
- 科技目标选择、进度累积、次回合生效
- 国策层可用，制度后端已打通；制度正式 UI 暂不做
- 建筑统一底座、城市内建筑/城外设施双轨
- 单建筑单激活配方
- 输入不足时低效推进或停滞
- 当前单位仅开拓者、步兵
- 当前军事建筑仅被动防御
- 主城核心摧毁判负
- 客户端可完成研究、建筑、配方、建城、单位命令和结算回放

### 3.2 MVP 不做

- 二级资源链的完整实装
- 工程单位
- 制度槽位完整玩法
- “连通才激活”经济规则
- 本地仓储、道路运量、真实物流
- 战争迷雾、内政迷雾、失真汇报
- 大臣默认接管命令
- PVE

### 3.3 必须预留的接口

- 政策双层结构
- 城市辖区扩展
- 建筑状态细化与接管参数
- 连通与物流判定层
- 大臣命令生成与审阅层
- 信息可见性与汇报层
- PVE 复用玩家规则底座

## 4. 交付顺序与里程碑

本计划按四个里程碑推进：

| 里程碑 | 目标 | 完成标志 |
|---|---|---|
| M1 数据与协议对齐（已完成） | 让 GDD 当前基线拥有稳定作者源和跨端契约 | `make data-validate`、`make gen` 稳定通过 |
| M2 服务端规则闭环 | 服务端能够独立跑通一局 MVP | `go test ./...` 通过；无头对局 harness 能稳定跑通场景化对局 |
| M3 客户端垂直切片 | Unity 客户端可完成一局基本对局 | 可从房间进入游戏，完成研究、建造、建城、战斗与结算 |
| M4 内容与验收 | 内容、数值、提示、结算信息达到可试玩标准 | 数据包定版，完成手工冒烟与规则回归 |

里程碑必须顺序推进。不得在 M1 尚未稳定时切客户端玩法 UI；不得在 M2 未闭环时接入复杂中期系统。

### 4.1 当前进度（2026-04-16）

- `main` 已集成“无客户端调试与规则验证”首批服务端实现，包含 prepared room、deterministic scenario、headless harness、结构化状态摘要、结算记录器与 `DEV_MODE` HTTP 调试入口。
- `M2` 相关基础设施已明显前移：服务端现在可以在不依赖客户端的情况下，用规则级测试与 harness 场景测试复现 `planning -> settlement` 的关键裁决。
- Chunk 2 已按“最小收口”完成：`player.Resources` 已回归唯一权威库存，`planning` 与长期状态已分离，城市/建筑状态与服务城市语义已正式进入 `domain + ecs + query` 边界，`planning snapshot` 也已按唯一键草案语义稳定回显研究/国策/建筑/配方/战区指令。
- `planning -> resolving` 边界的 lock-in 现在会把 `national_policy_changed` 与 `research_target_changed` 放入 `MsgTurnSettlement.economy` 事件流，而不再只是静默改状态。
- Chunk 3 已按 revised plan 完成主干收口：经济 resolving 改为单一 orchestrator，点数预算已从 `ResourceBag` 拆分为 resolving 内部 `PointBag`，`PlayerView.points` 保持“有效产出预览”语义，建筑来源修正已正式纳入 `staticdata + datagen + content + generated bundle` 链路。
- 统一修正公式现已切到 `((base*(1+percent))+flat)*multiplier`，并补上了 combat regression 与结算事件映射；点数刷新、点数消耗、建造跳过/配方阻塞都能进入 settlement/report。
- Chunk 4 已按 revised plan 完成主干收口：runtime 程序化开局会主动 bootstrap 主城，`CapitalCityID / OnlineOnTurn / building lifecycle status` 已成为统一运行时状态；建城、建筑放置、配方低效推进、非主城城市陷落、设施延时接管与 settlement/report 可观测性均已接入主线。
- Chunk 6 已按 final plan 完成主干收口：`IssueUnitOrder` 现在会先返回 typed `MsgIssueUnitOrderResult`，`flags.can_attack_structures` 已贯通作者源、schema、proto、staticdata、ECS 与客户端本地 catalog，`infantry` 可通过 `attack + target_node_id` 命中敌方建筑与主城核心。
- 主线单位结算已切到 shared `Targetable` combat：`lockPlanningInputs()` 继续在 combat 之前执行，fatal `city_core_destroyed` 会短路 `CombatUpkeep / marches / map / lifecycle / economy`，但 settlement 仍保留 `lockInEvents` 到 `economy` section；非 fatal 回合的 `CombatUpkeep` 仍归入 `unit` section。
- Chunk 7 的规则矩阵与 harness 也已覆盖到 Chunk 6：当前测试与场景可验证非法 node-target attack 的 typed result、步兵伤害普通建筑、步兵摧毁主城核心并判负、fatal turn 跳过后续 resolving，以及非 fatal 回合继续执行 upkeep。
- Chunk 7 的无头验证场景已继续扩到 Chunk 4：当前规则级测试与 harness 场景已能验证研究解锁次回合建造、共享工业点预算耗尽、settlement 重校验建造草案、低效推进/阻塞、开拓者建城、设施接管完成转移归属、非主城城市陷落、普通建筑废墟化与主城摧毁判负。
- `M4` 中“开发态调试接口”已提前落地，但这不代表 Chunk 6/7 以外的玩法内容已整体完成。
- 当前主工作区验证结果：`make gen` 与 `cd server && go test ./...` 已于 2026-04-16 在 `main` 上通过。
- Windows Unity batchmode 当前已可完成脚本编译并退出，但 `-runTests -testPlatform EditMode` 仍未稳定产出 `-testResults` XML，因此客户端 EditMode 自动化验证暂时仍记为“未最终确认通过”。

## 5. 推荐排期

### 5.1 排期假设

以下排期以“一名主后端/玩法程序、一名客户端程序半职、一名策划/数据半职”为基线估算。若由单人全职推进，建议将整体周期放宽至 12-14 周；若由两名以上后端并行推进且客户端有独立负责，则可争取压缩至 6-7 周，但前提仍是严格控制范围，不在 MVP 中途夹带中期系统。

### 5.2 总体阶段表

| 阶段 | 时间 | 对应任务 | 主要产物 | 验收标准 |
|---|---|---|---|---|
| Phase 0 | 第 0 周，2-3 天 | 文档冻结 | GDD、实现计划、无客户端验证方案冻结 | 范围、术语与 MVP 边界不再漂移 |
| Phase 1 | 第 1 周 | Task 1-3 | 数据作者源、schema、proto、catalog 对齐 | `make data-validate`、`make gen` 稳定通过 |
| Phase 2 | 第 2-3 周 | Task 4-6、Task 18-19 起步 | 领域状态、planning 输入模型、规则级测试矩阵、无头 harness 雏形 | 服务端可完成 `planning -> submit -> settlement` 基本循环 |
| Phase 3 | 第 4-5 周 | Task 7-12 | 资源/点数/修正、城市、建筑、配方、建筑状态与接管闭环 | 无头场景可跑通建城、配方、接管与基础产出 |
| Phase 4 | 第 6 周 | Task 13-17、Task 20 | 科技持续研究、科技效果、国策 MVP、单位/战争闭环、开发态调试接口 | 服务器可独立按新版 GDD 规则跑完一局 MVP |
| Phase 5 | 第 7 周 | Task 21-23 | 客户端缓存、规划 UI、地图展示、结算回放 | Unity 客户端可完整打一局基本流程 |
| Phase 6 | 第 8 周 | Task 24-26 | 内容定版、服务端回归、客户端冒烟 | 可试玩 MVP 冻结，进入收尾与缺陷清单阶段 |

### 5.3 周计划

| 周次 | 本周重点 | 输入 | 输出 | 风险点 |
|---|---|---|---|---|
| 第 0 周 | 冻结文档与范围 | GDD、实现计划、验证方案 | 冻结版文档 | 范围继续膨胀 |
| 第 1 周 | 数据与协议 | 静态数据表、proto、目录结构 | 可生成的 catalog 与协议 | 数据语义和协议字段反复变动 |
| 第 2 周 | 领域状态收口 | 新协议、GDD 当前基线 | `domain / ecs / planning` 骨架 | 继续沿用旧 `castle` 语义导致边界混乱 |
| 第 3 周 | 无头验证起步 | 房间主循环、planning 服务 | 规则级测试矩阵、harness 雏形 | 过早依赖客户端验证 |
| 第 4 周 | 经济基础闭环 | 资源、点数、修正定义 | 资源/点数/修正统一结算 | 把点数重新做成可囤积资源 |
| 第 5 周 | 城市/建筑/配方闭环 | 经济基础层、地图动作链 | 建城、建筑归属、配方推进、接管规则 | 城内建筑/城外设施边界失真 |
| 第 6 周 | 科技/政策/战争闭环 | 前五周稳定底座 | 服务端完整一局 | 科技时序与政策生效口径冲突 |
| 第 7 周 | 客户端垂直切片 | 稳定 proto、稳定 settlement | 可玩的 Unity 端基本流程 | 客户端倒逼服务端改规则 |
| 第 8 周 | 内容定版与验收 | 服务端闭环、客户端可玩 | MVP 冻结版 | 最后一周仍在做结构性改动 |

### 5.4 阶段交付物

| 阶段 | 必须交付 |
|---|---|
| Phase 1 | 可验证的数据包、可生成的协议、更新后的静态目录 |
| Phase 2 | 服务端长期状态模型、统一 planning 输入、初始规则级测试 |
| Phase 3 | 城市/建筑/配方主闭环、资源点数修正系统、基础无头场景 |
| Phase 4 | 科技与国策 MVP、主城摧毁判负、调试接口、无头可跑整局 |
| Phase 5 | 客户端 HUD、规划面板、地图展示、结算回放 |
| Phase 6 | 内容定版、回归记录、冒烟结果与缺陷分类清单 |

### 5.5 关键路径

本计划的关键路径只有一条：

```text
数据与协议冻结
-> 领域状态收口
-> 经济/城市/建筑/配方闭环
-> 科技/政策/战争闭环
-> 无头完整一局
-> 客户端垂直切片
-> 内容定版
```

其中最不能拖延的节点是：

1. 数据与协议冻结
2. 领域状态模型收口
3. 无头对局 harness 建立
4. 城市/建筑/配方闭环

这四项任意一项延误，都会直接推迟后续所有工作。

### 5.6 资源建议

若项目按 8 周推进，推荐分工如下：

| 角色 | 主要负责 |
|---|---|
| 主后端/玩法程序 | `domain / ecs / engine / event / planning / settlement / harness` |
| 客户端程序 | `cache / mapper / HUD / map / settlement playback` |
| 策划/数据 | `data/content / data/ui / 数值调试 / 内容对账 / 场景化回归样本` |

如果只有一名主开发者，则建议仍按相同阶段顺序推进，但不要在第 7 周前尝试把客户端和服务端两条线同时铺开，应优先拿到“无头可跑完一局”的里程碑。

## 6. Chunk 1：静态数据、协议与运行时收口（已完成）

### 6.0 完成说明（2026-04-15）

Chunk 1 在实际落地时采用了“静态数据与协议先对齐，再补最小运行时消费链路”的收口方式，当前已经完成以下内容：

- 作者源、schema 与生成链已切到新版 MVP 基线；所有作者源 JSON 顶层都带 `$schema`，并新增 `points`、`policies` 目录。
- 公开契约已按破坏式归正切换到新版术语：`city_core`、`infantry`、`research_output`、`industry_output`、`expansion / war_preparedness / recovery / reorganization`。
- `data_types.proto`、`data_catalog.proto`、`game_state.proto`、`orders.proto`、`turn.proto`、`settlement.proto` 已对齐新版字段与结果消息。
- 服务端已真实填充 `PlayerView.research.current_target_technology_id`、`required_progress`、`NodeView.city_id`、`service_city_id`、`building_status`、`takeover_*` 等字段。
- `MsgSetPolicyResult`、`MsgBuildStructureResult`、`MsgResearchResult` 的运行时语义已接入；settlement 已纳入 `building_status_changed`。
- 客户端运行时脚本已改为从 `PointBag` 读取 `industry_output`，并消费新的玩家/节点视图字段与结果消息。
- 规则字段 `tokens_recuperation_bonus` 已统一归正为 `bonus_tokens_per_turn`。

本 Chunk 的完成验证口径为：

- `make data-validate`
- `make gen`
- `cd server && go test ./...`

范围说明：

- Chunk 1 已完成到“跨端契约与最小运行时消费链路闭环”的口径。
- Unity Prefab、Scene、资源路径和表现层类名中的 `Castle*` 仍允许暂存，留待单独的资产与命名清扫轮次处理。

### Task 1: 建立 GDD 当前基线的静态数据作者源

**Files:**
- Modify: `data/content/buildings/buildings.json`
- Modify: `data/content/recipes/recipes.json`
- Modify: `data/content/technologies/technologies.json`
- Modify: `data/content/units/units.json`
- Modify: `data/content/rules/rules.json`
- Modify: `data/registry/resources.json`
- Create: `data/content/policies/policies.json`
- Modify: `data/ui/catalogs/buildings.json`
- Modify: `data/ui/catalogs/recipes.json`
- Modify: `data/ui/catalogs/technologies.json`
- Modify: `data/ui/catalogs/units.json`
- Create: `data/ui/catalogs/policies.json`
- Create: `data/schema/content/policies.schema.json`
- Create: `data/schema/ui/policies.schema.json`
- Modify: `data/registry/manifest.json`

- [x] **Step 1: 以 GDD 当前基线为准，收敛 MVP 内容集**
- [x] **Step 2: 在作者源中补齐三种原始资源、两类点数、两种单位、城市核心、基础资源建筑、基础生产建筑、基础国策与代表性科技**
- [x] **Step 3: 用数据字段表达建筑标签、放置规则、配方工作量、科技显式效果、修正效果和占领参数**
- [x] **Step 4: 在规则表中加入建筑接管回合数 `N`、科研/建造基础收入、主城核心生命等可调参数**

### Task 2: 扩展静态数据模型与生成链

**Files:**
- Modify: `server/internal/staticdata/model.go`
- Modify: `server/internal/staticdata/catalog.go`
- Modify: `server/internal/datagen/schema.go`
- Modify: `server/internal/datagen/validate.go`
- Modify: `server/internal/datagen/generator.go`
- Modify: `server/internal/staticdata/catalog_test.go`
- Modify: `server/internal/datagen/generator_test.go`

- [x] **Step 1: 为政策、点数、建筑状态、城市内建筑/城外设施语义补齐静态数据模型**
- [x] **Step 2: 让 schema 与语义验证能校验建筑放置规则、科技解锁目标、政策依赖、配方边界和修正 trigger**
- [x] **Step 3: 让 generator 把新增内容合并进服务端 bundle 和客户端 UI catalog**
- [x] **Step 4: 用测试覆盖政策目录、科技效果、建筑与配方的交叉引用**

### Task 3: 对齐跨端协议视图

**Files:**
- Modify: `protocol/data_types.proto`
- Modify: `protocol/data_catalog.proto`
- Modify: `protocol/game_state.proto`
- Modify: `protocol/orders.proto`
- Modify: `protocol/turn.proto`
- Modify: `protocol/settlement.proto`
- Modify: `Makefile`
- Modify: `server/internal/transport/dispatch/generated_commands.go`
- Modify: `server/internal/gen/proto/*.pb.go`
- Modify: `client/Assets/Scripts/Protocol/*.cs`

- [x] **Step 1: 在目录快照中加入政策目录、点数展示所需结构，以及科技/建筑/配方所需的最小 UI 数据**
- [x] **Step 2: 在 `game_state.proto` 中补齐当前科技目标、科技进度、玩家点数、城市核心信息、建筑状态、服务城市与建筑运作视图**
- [x] **Step 3: 在 `orders/turn/settlement` 中补齐研究目标切换、国策切换、建城结果、建筑状态变化、科技完成和配方推进结果的统一消息**
- [x] **Step 4: 运行 `make gen`，保证 Go 与 C# 生成代码、命令分发代码同步更新**

## 7. Chunk 2：领域模型与运行时基础

### 7.0 当前状态（2026-04-15）

Chunk 2 当前已经完成了“领域长期状态收口 + ECS/query 最小正式化 + planning 输入模型主线 + reconnect/settlement 补口”的收尾工作，主要包括：

- `player.Resources` 已成为唯一权威库存；城市库存双权威已移除，建造、配方、研究 grant 与工业点刷新都已改走玩家级库存。
- `TurnRuntime.Planning` 现在只保存研究目标、国策、建筑/配方、单位命令等草案；研究目标与国策不会在 planning 阶段直接污染 active state。
- `planning snapshot` 已扩展并稳定回显当前研究目标、国策、建筑放置、配方切换、战区指令和单位有效命令；`planning` 阶段重连会补发带 `snapshot` 的 `MsgPlanningStart`。
- `planning -> resolving` 的 lock-in 已接入 settlement 事件流；客户端与调试侧现在能看到 `national_policy_changed` / `research_target_changed`，不再只看到终态。
- 城市、城市核心、服务城市、设施绑定、takeover runtime 元数据和单位类别组件已正式进入 ECS；查询层可以直接回答“某建筑属于哪座城市”“某节点是否可建城”“某设施当前是否 disabled / takeover 中”等规则问题，`NodeView.city_id / service_city_id / building_status / takeover_*` 不再由视图层临时猜测。
- `PlanningInputs` 已补齐唯一键 upsert 语义：同节点建造草案、同节点配方草案和同战区指令会保留最后一次提交；同节点建造草案替换不重复扣 token。

当前说明：

- Chunk 2 已完成到“最小收口”口径；原先留给 Chunk 3-4 的接管推进、城市玩法扩展和 richer city gameplay 主干现已在 `main` 落地，后续只剩 Chunk 5-6 与更后续阶段的扩展项。
- 客户端 EditMode 自动化结果文件仍未稳定落盘，因此客户端验证口径目前以“脚本编译通过 + 关键缓存行为测试已补”为主，不把 Unity 测试整体验证记为完成。

### Task 4: 重塑领域状态以承载 GDD 当前基线

**Files:**
- Modify: `server/internal/domain/state.go`
- Modify: `server/internal/domain/types.go`
- Modify: `server/internal/domain/node.go`
- Modify: `server/internal/domain/unit.go`
- Create: `server/internal/domain/city.go`
- Create: `server/internal/domain/policy.go`
- Create: `server/internal/domain/economy.go`
- Modify: `server/internal/domain/state_runtime_test.go`
- Modify: `server/internal/domain/types_test.go`

- [x] **Step 1: 在领域层明确玩家资源、玩家点数、科技进度、当前研究目标、国策状态和解锁状态**
- [x] **Step 2: 建立城市、城市核心、服务城市、辖区基础值和建筑状态的领域模型**
- [x] **Step 3: 把“显式效果”和“修正效果”的计算入口统一到领域层，而不是散落到各个系统**
- [x] **Step 4: 保持 `TurnRuntime.Planning` 与 `TurnRuntime.Resolving` 只承载回合临时态，不反向污染长期状态**

### Task 5: 扩展 ECS 组件与查询层

**Files:**
- Modify: `server/internal/ecs/components.go`
- Modify: `server/internal/ecs/query.go`
- Modify: `server/internal/ecs/factory.go`
- Modify: `server/internal/ecs/factory_test.go`
- Modify: `server/internal/game/query/views.go`
- Modify: `server/internal/game/query/planning.go`

- [x] **Step 1: 为城市核心、建筑状态、设施绑定、建筑运行态、单位类别和未来可见性插口补齐 ECS 组件**
- [x] **Step 2: 让查询层能够直接回答“某建筑属于哪座城市”“某节点是否可建城”“某设施是否已被接管”等规则问题**
- [x] **Step 3: 让视图构建函数能输出玩家所需的城市、建筑、科技和配方运行信息**
- [x] **Step 4: 保持查询层只做读取与拼装，不在查询过程中修改状态**

### Task 6: 收口规划态输入模型

**Files:**
- Modify: `server/internal/game/planning/service.go`
- Modify: `server/internal/game/orders/types.go`
- Modify: `server/internal/game/command_handler.go`
- Modify: `server/internal/game/turn/coordinator.go`
- Modify: `server/internal/game/planning/service_test.go`
- Modify: `server/internal/game/turn_v2_test.go`

- [x] **Step 1: 把研究目标切换、国策切换、建城、建筑放置、配方切换和单位命令统一收口到 `planning` 输入模型**
- [x] **Step 2: 移除“研究即消费整笔科技点”的旧语义，改为“设定目标、回合末自动推进”**
- [x] **Step 3: 让 `planning snapshot` 能正确回显当前研究目标、国策、建筑配方和单位命令**
- [x] **Step 4: 用测试覆盖重复提交、非法切换、回合外提交和快照回显行为**

> **备注（Task 6 / Step 4）**：当前已经补齐 `planning` 阶段不污染 active state、回合外提交拒绝、`planning snapshot` 回显、reconnect 补发 snapshot、lock-in 进入 settlement，以及 build/recipe/war directive 按唯一键替换草案的回归测试。

## 8. Chunk 3：经济基础、点数与修正系统

### 8.0 当前状态（2026-04-15）

Chunk 3 当前已经完成了“经济预算从资源库存拆分 + 单轮经济结算顺序收口 + 修正来源统一聚合”的主干改造，主要包括：

- `ResourceBag` 已回归纯物质库存；科研/工业点数不再伪装成资源库存条目，而是在 `TurnRuntime.Resolving.PointBudgets` 中以独立 `PointBag` 管理。
- 经济结算不再依赖通用 `Pipeline.Run` 的“先收集后 Apply”语义；`NewEconomyPipeline()` 现在会进入单一 orchestrator，按“预算刷新 -> 科研推进 -> 建造 -> 配方 -> 其余生产/维护”的固定顺序执行同回合共享预算。
- `PlayerView.points` 与客户端 HUD 继续展示 `EffectiveResearchOutput / EffectiveIndustryOutput` 的预览值，而不是 resolving 内部剩余预算；预算刷新与消费改由 settlement economy 事件显式表达。
- 建筑修正来源已前移到正式数据链路：`BuildingDefinition`、schema、validator、作者源和生成 bundle 现在都支持 `explicit_effects / modifier_effects` 字段，本轮作者源先以空数组和最小样例口径收口。
- 统一修正公式已切到 `((base*(1+percent))+flat)*multiplier`，并通过同一入口同时服务经济与 combat；科技、国策、已建成且未停用建筑都可作为活跃修正来源。
- 结算报告已补齐 `point_budget_refreshed`、`point_spent`、`building_skipped`、`recipe_skipped` 等事件映射；建造会在 settlement 重新校验可建性，失效配方选择也会显式进入 skipped/blocked，而不是静默失败。
- 道路仍未接入 Chunk 3 的统一预算与 map action 结算；当前已显式禁止部长 `repair_road` 直接落图，避免绕过点数账本，后续若恢复道路玩法必须另行接入正式预算链。

当前说明：

- Chunk 3 现在可按 revised plan 的范围定义视为完成；原先挂到 Chunk 4 的“低效推进”“更完整城市/接管语义”已在当前主线实现，不再作为 Chunk 3/4 之间的悬而未决项。
- 客户端本轮只同步了“建造成功=草案已记录”的提示语义与断言；Unity EditMode 自动化结果仍未在主工作区重新确认通过。

### Task 7: 建立当前基线的资源与点数回合结算

**Files:**
- Modify: `server/internal/engine/production/recharge.go`
- Modify: `server/internal/engine/production/upkeep.go`
- Modify: `server/internal/engine/production/flow.go`
- Modify: `server/internal/engine/production/production.go`
- Modify: `server/internal/event/production.go`
- Modify: `server/internal/game/resolution/report/report.go`
- Modify: `server/internal/game/resolution/report/report_test.go`

- [x] **Step 1: 明确当前 MVP 的资源库存口径为玩家级全局虚空库存，并在结算链中统一读写**
- [x] **Step 2: 为科研产出与建造/工业产出建立统一回合收入、统一修正入口和统一结算展示**
- [x] **Step 3: 让资源产出、点数产出、维护消耗和库存变化都进入统一事件流与结算报告**
- [x] **Step 4: 保证道路当前不参与经济物流判定，但在模型中保留后续接入点**

### Task 8: 收敛统一修正系统

**Files:**
- Modify: `server/internal/domain/state.go`
- Modify: `server/internal/event/research.go`
- Modify: `server/internal/engine/combat/modifiers.go`
- Modify: `server/internal/engine/production/build.go`
- Modify: `server/internal/engine/production/recipe.go`
- Modify: `server/internal/engine/production/research.go`
- Modify: `server/internal/engine/combat/modifier_integration_test.go`
- Modify: `server/internal/engine/production/research_system_test.go`

- [x] **Step 1: 统一 `flat / percent / multiplier` 的计算顺序，并明确触发域与目标域**
- [x] **Step 2: 让科技、政策和建筑都通过同一修正入口影响科研、建造、单位和配方**
- [x] **Step 3: 让“显式效果”与“修正效果”分层执行，避免在修正路径中偷做解锁行为**
- [x] **Step 4: 补齐集成测试，保证同一修正不会在不同系统里出现不同结果**

## 9. Chunk 4：城市、建筑与配方主闭环

### 9.0 当前状态（2026-04-16）

Chunk 4 当前已经完成了“主城/新城统一建模 + 建筑放置/归属双轨正式化 + 配方低效推进 + 建筑生命周期与接管闭环”的主干改造，主要包括：

- runtime 程序化开局会在最终 spawn 点主动 bootstrap 主城；prepared/scenario 路径仍可扫描已有 `city_core`。`PlayerState.CapitalCityID`、`CityState.OnlineOnTurn` 和建筑 `OnlineOnTurn` 已成为统一时序状态。
- `settle_city` 现在只创建 `city_core + 初始辖区`，不再赠送免费建筑；新城、新建筑和新接管资产统一采用“本回合落地、次回合上线”口径。
- 建城与建筑放置统一收口到服务端唯一合法性入口；非核心建筑要求 `city_id`，城内建筑受城市固定辖区约束，城外设施必须匹配资源点并绑定服务城市；最小城距已按 Manhattan 距离进入 `CanFoundCityAt()`。
- 战斗后果已拆分为普通建筑受损/废墟、非主城城市陷落、主城核心摧毁判负三条路径；城市陷落会同步迁移 `node.Owner / TerritoryOwner`、`CityState` 目录和后续城市归属。
- 建筑生命周期系统已替换旧 territory disable 语义；设施会先 `contested/takeover` 再延时接管，城市陷落时 `production / processing / storage` 建筑进入接管池，`defense / governance` 建筑进入 `ruined`。
- 配方推进已切到“工作量 / 推进率 / 运行效率”模型；资源与点数按累计进度比例扣除，输入不足时会低效推进或停滞，不再在完工瞬间一次性扣料。
- `NodeView.building_status`、settlement report、新增 lifecycle 事件以及规则级 / 场景化测试已覆盖上述语义。

当前说明：

- Chunk 4 现在可按 revised plan 的范围定义视为完成；剩余主线工作已转入 Chunk 5-6 的科技/政策收尾与单位/战争闭环，不再把城市/接管尾差挂在本 Chunk 名下。

### Task 9: 落地主城、开拓者建城与城市核心

**Files:**
- Modify: `server/internal/engine/maploader/loader.go`
- Modify: `server/internal/engine/maploader/procedural.go`
- Modify: `server/internal/game/map_actions.go`
- Modify: `server/internal/event/game.go`
- Modify: `server/internal/event/production.go`
- Modify: `server/internal/game/room.go`
- Modify: `server/internal/game/query/views.go`
- Modify: `server/internal/engine/maploader/loader_test.go`
- Modify: `server/internal/game/turn_v2_test.go`

- [x] **Step 1: 让主城在初始化时以“主城核心建筑 + 城市记录”的形式成立**
- [x] **Step 2: 让开拓者的 `settle_city` 动作创建新城市核心，并初始化基础辖区和服务关系**
- [x] **Step 3: 统一主城核心与普通城市核心的生命、归属和判负语义**
- [x] **Step 4: 用测试覆盖开局主城存在、建城合法性、非法地块建城和新城次回合生效口径**

### Task 10: 落地建筑放置规则与归属双轨

**Files:**
- Modify: `server/internal/game/planning/service.go`
- Modify: `server/internal/engine/production/build.go`
- Modify: `server/internal/event/production.go`
- Modify: `server/internal/ecs/factory.go`
- Modify: `server/internal/game/query/views.go`
- Modify: `server/internal/engine/production/research_system_test.go`

- [x] **Step 1: 让建筑放置规则显式区分城市内建筑与资源点设施，并由服务端统一校验**
- [x] **Step 2: 让城市内建筑必须处于辖区内，城外设施必须绑定服务城市**
- [x] **Step 3: 让科技解锁和建筑标签决定玩家能建什么、能在哪建，而不是只靠节点类型硬编码**
- [x] **Step 4: 用测试覆盖未解锁建筑、辖区外建造、错误资源点建造和服务城市绑定**

### Task 11: 对齐配方推进模型

**Files:**
- Modify: `server/internal/engine/production/recipe.go`
- Modify: `server/internal/engine/production/flow.go`
- Modify: `server/internal/event/recipe.go`
- Modify: `server/internal/game/query/views.go`
- Modify: `server/internal/engine/production/research_system_test.go`

- [x] **Step 1: 保持单建筑单激活配方，但把推进逻辑明确为“工作量/推进率/运行效率”模型**
- [x] **Step 2: 让输入不足时进入低效推进或停滞，而不是回滚进度**
- [x] **Step 3: 让配方产出严格限制在资源、单位、点数推进和局部状态边界内**
- [x] **Step 4: 在查询层和结算报告中暴露配方选择、当前进度、需要回合和阻塞原因**

### Task 12: 落地建筑状态、停用与接管

**Files:**
- Modify: `server/internal/engine/combat/destroy.go`
- Modify: `server/internal/engine/combat/siege.go`
- Modify: `server/internal/event/production.go`
- Modify: `server/internal/event/game.go`
- Modify: `server/internal/game/query/views.go`
- Modify: `server/internal/game/resolution/report/report.go`
- Create: `server/internal/event/building_state.go`
- Create: `server/internal/engine/combat/capture.go`

- [x] **Step 1: 建立建筑的激活、停用、争夺、接管、废墟状态语义**
- [x] **Step 2: 让城外设施采用“先失效、后接管”的延时接管规则，并读取可调参数 `N`**
- [x] **Step 3: 让城市陷落时执行“部分接管、部分废墟”的基础分类，而不是整城原样翻面**
- [x] **Step 4: 在结算报告和地图视图中给出建筑状态变化，便于客户端播放与提示**

## 10. Chunk 5：科技与政策闭环

### Task 13: 把科技从“一次性购买”迁移到“持续研究”

**Files:**
- Modify: `server/internal/domain/state.go`
- Create: `server/internal/domain/research_progress.go`
- Modify: `server/internal/game/planning/service.go`
- Modify: `server/internal/engine/production/research.go`
- Modify: `server/internal/event/research.go`
- Modify: `server/internal/game/query/views.go`
- Modify: `protocol/game_state.proto`
- Modify: `server/internal/engine/production/research_system_test.go`

- [x] **Step 1: 为玩家建立“当前研究目标 + 每项科技进度”的长期状态，而不是自由科技点购买模型**
- [x] **Step 2: 把 `MsgSetResearchTarget` 改为只切目标，不在下达命令时直接消费整笔科技点**
- [x] **Step 3: 在回合末自动把科研产出灌入目标科技，并在科技完成时发出“本回合显示、下回合生效”的事件**
- [x] **Step 4: 用测试覆盖切线保留进度、前置依赖、连续回合推进和完成延迟生效**

### Task 14: 落地科技显式效果链

**Files:**
- Modify: `server/internal/event/research.go`
- Modify: `server/internal/staticdata/model.go`
- Modify: `server/internal/datagen/validate.go`
- Modify: `server/internal/game/query/views.go`
- Modify: `server/internal/engine/production/research_system_test.go`

- [x] **Step 1: 让科技支持解锁建筑、解锁配方、解锁兵种、解锁政策候选、授予单位和修正效果**
- [x] **Step 2: 保证显式效果只在科技正式生效时进入玩家可用边界**
- [x] **Step 3: 在玩家视图中清楚呈现当前目标、已解锁科技和完成后新增的可用项**
- [x] **Step 4: 用测试覆盖科技完成前不可建、科技完成后次回合可建的时序规则**

### Task 15: 落地政策系统的 MVP 形态

**Files:**
- Modify: `server/internal/domain/policy.go`
- Modify: `server/internal/game/planning/service.go`
- Modify: `server/internal/event/minister.go`
- Modify: `server/internal/game/query/views.go`
- Modify: `server/internal/staticdata/model.go`
- Modify: `protocol/game_state.proto`
- Modify: `protocol/orders.proto`
- Create: `server/internal/engine/production/policy.go`
- Create: `server/internal/engine/production/policy_test.go`

- [x] **Step 1: 用静态数据落地国策目录、候选可见性与修正效果**
- [x] **Step 2: 让当前版本的玩家可切换国策，并在当回合结算开始时生效**
- [x] **Step 3: 为制度层建立数据模型、候选池和协议接口，但不在 MVP 强做完整制度玩法**
- [x] **Step 4: 保证政策修正能进入统一修正系统，而不是单独写一套特殊分支**

当前状态：
- 研究状态已改为 `current target + per-tech saved progress + completed/pending_activation/active` 三态模型；`completed_technology_ids` 明确只表示“研究已完成”，不再等价于“已正式生效”。
- 科技完成当回合只记录 completion；建筑/配方/制度候选/制度槽位/resource grant/unit grant/modifier effect 全部统一在下一回合 `planning_start` 前激活。
- `MsgPlanningStart` 已扩展为 planning 边界的唯一 active-state 同步载体，bootstrap 与常规回合都复用同一条 turn-start activation 链，并携带 `my_player + nodes + units + snapshot`。
- 制度层后端已具备 `candidate pool + slot_count + active loadout + pending loadout/activation turn`，支持 `MsgSetInstitutionLoadout`、planning snapshot 回显、next-turn 激活，以及通过统一 modifier 入口正式生效。
- 客户端已补齐非 UI 管线：`planning_start` 走静默 cache refresh，不复用 settlement 回放；planning draft、debug raw sender、日志与快捷动作已能观察和发送制度 loadout。

## 11. Chunk 6：单位、战争与胜负

### 11.0 完成说明（2026-04-16）

Chunk 6 实际落地时采用了“typed 命令反馈 + 作者源结构攻击资格 + shared Targetable combat”的最终收口方案，当前已经完成以下内容：

- `IssueUnitOrder` 的业务反馈已从通用 `Problem` 分离，改为 typed `MsgIssueUnitOrderResult`；非法 `attack + target_node_id` 会返回 `success=false` 且不覆盖旧草案、不发送新 snapshot。
- 单位结构攻击资格已由作者源字段 `flags.can_attack_structures` 统一驱动，并贯通 `schema -> datagen -> staticdata -> generated bundle -> proto contract -> ECS -> client local catalog`；当前主线只有 `infantry` 具备该能力。
- 主线战斗已改为 shared `Targetable` 结算：单位目标攻击与节点目标攻击共用同一条伤害投递链，建筑仍阻挡移动，`attack + target_node_id` 为 stationary attack，不引入 move-and-attack。
- turn settlement 继续保留 Chunk 5 的 planning lock-in 语义：`lockPlanningInputs()` 先于 combat 执行，fatal `city_core_destroyed` 只短路 `CombatUpkeep / marches / map / lifecycle / economy`，不会丢掉 lock-in 事件；fatal turn 的 `economy` section 只保留 `lockInEvents`。
- 客户端已补齐最小垂直切片：本地 `StaticCatalogCache` 可读取 `can_attack_structures`，地图攻击模式可对敌方建筑/城市核心下发 `attack + target_node_id`，并能消费 `MsgIssueUnitOrderResult` 进行即时反馈。

本 Chunk 的完成验证口径为：

- `make gen`
- `cd server && go test ./...`
- subagent review 已复核 `GameEvent` tag 兼容性、客户端建筑攻击点击链路与 `SiegeSystem` 的主线依赖边界；当前结论是 Chunk 6 主线已完成，`SiegeSystem` 仅剩扩展骨架用途，不再参与 MVP 主线结算。

范围说明：

- `CanSiege` / `SiegeSystem` 保留为未来扩展骨架，但主线作者源、主线场景和主线 turn settlement 已不再依赖它们。
- 当前主线被动防御仍限定为 authored HP、阻挡与主城核心胜负语义；`wall_level` / `tower counterfire` 等数值效果继续留待后续数据链支持后再启用。
- 客户端侧当前已完成最小命令与反馈接线，但 Unity EditMode 自动化结果仍未在本轮作为最终通过口径锁定。

### Task 16: 收敛 MVP 单位谱系与初始对局配置

**Files:**
- Modify: `data/content/units/units.json`
- Modify: `data/ui/catalogs/units.json`
- Modify: `server/internal/ecs/factory.go`
- Modify: `server/internal/engine/maploader/loader.go`
- Modify: `server/internal/game/query/views.go`
- Modify: `server/internal/ecs/factory_test.go`

- [x] **Step 1: 当前对局只保留开拓者与步兵两类单位，并清理与未启用谱系强耦合的默认内容**
- [x] **Step 2: 让单位目录与地图初始编成严格匹配 GDD 当前基线**
- [x] **Step 3: 保证开拓者与步兵在协议、视图、动画和交互层都能被清晰区分**
- [x] **Step 4: 用测试覆盖初始编成、单位工厂和目录映射**

当前状态：主线作者源与 UI catalog 仍只暴露 `settler / infantry`，`scenario.capital_destroy_gameover` 已改为 `infantry + attack(target_node)` 路径；`factory_test` 已补齐 `can_attack_structures` 的作者源映射断言，客户端现有交互也已按“开拓者建城 / 步兵战斗”最小语义收口。

### Task 17: 对齐战争闭环与被动防御

**Files:**
- Modify: `server/internal/engine/combat/*.go`
- Modify: `server/internal/game/room_march.go`
- Modify: `server/internal/game/room.go`
- Modify: `server/internal/game/turn/coordinator.go`
- Modify: `server/internal/game/query/views.go`
- Modify: `server/internal/engine/combat/single_step_resolver_test.go`
- Modify: `server/internal/engine/combat/route_planner_test.go`

- [x] **Step 1: 保持当前直接指挥战斗闭环，但把城市核心、防御建筑和建筑状态纳入统一战斗后果**
- [x] **Step 2: 让军事建筑在 MVP 只承担被动防御、耐久和守城修正，不提前接入主动攻击循环**
- [x] **Step 3: 用主城核心生命归零作为默认判负规则，并在结算报告中明确给出失败原因**
- [x] **Step 4: 让战斗、建筑受损、设施停用与胜负检测共享同一结算顺序**

当前状态：主线 combat 已通过 typed target reference 支持 `unit / structure / city_core` 三类目标，`infantry` 可在最终邻接校验通过后伤害普通建筑与主城核心；fatal `city_core_destroyed` 会在保留 planning lock-in settlement 的前提下短路后续 resolving，非 fatal 回合仍继续执行 `CombatUpkeep` 并把其事件并入 `unit` section。当前 MVP 不启用建筑主动攻击循环，也不引入未数据化的墙/塔数值反击。

## 12. Chunk 7：无客户端调试与规则验证基础设施

> 详细方案见：[2026-04-15-headless-debug-and-rule-validation.md](./2026-04-15-headless-debug-and-rule-validation.md)

### Task 18: 建立规则级回归矩阵

**Files:**
- Modify: `server/internal/engine/production/research_system_test.go`
- Modify: `server/internal/game/turn_v2_test.go`
- Modify: `server/internal/engine/combat/*_test.go`
- Create: `server/internal/game/planning/service_rules_test.go`
- Create: `server/internal/event/building_state_test.go`
- Create: `server/internal/game/scenario/scenario_test.go`

- [x] **Step 1: 把 GDD 当前基线中的关键裁决拆成规则级测试矩阵，覆盖科技、建城、建筑放置、配方阻塞、建筑接管和主城判负**
- [x] **Step 2: 让规则级测试尽量只依赖 `domain / engine / event`，不把网络与客户端状态卷入断言**
- [x] **Step 3: 为每一类关键规则建立可读的场景名称，使失败信息能直接对应到 GDD 条目**
- [x] **Step 4: 保证“单条规则失败”与“整局链路失败”能够在测试层级上被区分定位**

当前状态：科技推进、研究解锁次回合建造、建筑放置、共享工业点预算、配方阻塞/低效推进、设施接管完成并转移归属、非主城城市陷落与主城判负均已进入规则级矩阵；Step 1 的覆盖范围已按当前 GDD 基线补齐。

### Task 19: 抽象无头对局 Harness

**Files:**
- Create: `server/internal/debug/capture_transport.go`
- Create: `server/internal/debug/harness.go`
- Create: `server/internal/game/scenario/scenario.go`
- Create: `server/internal/debug/harness_test.go`
- Modify: `server/internal/debug/integration_test.go`
- Modify: `server/internal/game/room.go`
- Modify: `server/internal/game/session/runtime.go`
- Modify: `server/internal/game/turn/coordinator.go`

- [x] **Step 1: 从现有 `captureTransport + GameRoom` 联调测试中抽出可复用的无头对局运行器**
- [x] **Step 2: 让 harness 支持装配静态数据、地图、玩家、注入 planning 命令、自动 submit 和等待 settlement**
- [x] **Step 3: 让 harness 能导出每回合消息、状态摘要和关键结算 sections，用于断言与复盘**
- [x] **Step 4: 建立一组场景化无头对局用例，验证研究解锁、点数预算消耗、结算重校验、开拓者建城、配方阻塞/失效、设施接管和主城摧毁**

当前状态：9 个场景化无头用例已经落地，除原有研究解锁、建城、配方阻塞与主城摧毁外，新增覆盖了共享工业点预算耗尽、settlement 阶段建造重校验、点数预览保持有效产出语义、disabled recipe 的 `recipe_skipped` 反馈，以及设施接管进度/完成后的归属与服务城市转移断言。

### Task 20: 落地开发态调试接口与状态转储

**Files:**
- Modify: `server/internal/debug/state_dumper.go`
- Modify: `server/internal/debug/middleware.go`
- Create: `server/internal/debug/settlement_recorder.go`
- Create: `server/internal/transport/http/debug_handler.go`
- Modify: `server/internal/transport/http/server.go`
- Modify: `server/cmd/server/app/server.go`

- [x] **Step 1: 扩展状态转储，让开发者能查看当前回合、玩家、城市、建筑、单位、研究和点数摘要**
- [x] **Step 2: 记录最近一次结算输出，支持导出 `MsgTurnSettlement` 与关键事件 sections**
- [x] **Step 3: 在 `DEV_MODE` 下提供最小调试入口，用于注入 planning 命令、强制 submit 和单步推进回合**
- [x] **Step 4: 保证这些接口只服务调试与复现，不承担规则正确性的主验证职责**

## 13. Chunk 8：客户端垂直切片

### Task 21: 更新客户端缓存、DTO 与消息处理

**Files:**
- Modify: `client/Assets/Scripts/Runtime/Core/Application/Handler/GameMessageHandler.cs`
- Modify: `client/Assets/Scripts/Runtime/Core/Application/Cache/GameStateCache.cs`
- Modify: `client/Assets/Scripts/Runtime/Core/Application/Cache/PlanningDraftCache.cs`
- Modify: `client/Assets/Scripts/Runtime/Core/Application/Cache/StaticCatalogCache.cs`
- Modify: `client/Assets/Scripts/Runtime/Core/Infrastructure/Mapper/SettlementMapper.cs`
- Modify: `client/Assets/Scripts/Runtime/Core/Infrastructure/Mapper/NodeMapper.cs`
- Modify: `client/Assets/Scripts/Runtime/Core/Infrastructure/Mapper/UnitMapper.cs`
- Create: `client/Assets/Scripts/Runtime/Core/Foundation/Domain/TechnologyDto.cs`
- Create: `client/Assets/Scripts/Runtime/Core/Foundation/Domain/PolicyDto.cs`
- Create: `client/Assets/Scripts/Runtime/Core/Foundation/Domain/CityDto.cs`
- Create: `client/Assets/Scripts/Runtime/Core/Foundation/Domain/BuildingDto.cs`

- [ ] **Step 1: 让客户端缓存能读取新的玩家点数、科技目标、科技进度、城市/建筑状态和政策信息**
- [ ] **Step 2: 让映射层以纯展示方式消费 `game_state`、`planning snapshot` 和 `settlement` 新字段**
- [ ] **Step 3: 保持客户端不做任何合法性推断，只显示服务端给出的可见状态**
- [ ] **Step 4: 用消息处理测试和运行时检查确认旧字段删除后仍能完成进入游戏与回合同步**

### Task 22: 补齐 MVP 规划 UI

**Files:**
- Modify: `client/Assets/Scripts/Runtime/Presentation/UI/HUD/ResourceHUD.cs`
- Modify: `client/Assets/Scripts/Runtime/Presentation/UI/HUD/TurnHUD.cs`
- Modify: `client/Assets/Scripts/Runtime/Presentation/UI/HUD/UnitInfoPanelController.cs`
- Modify: `client/Assets/Scripts/Runtime/Presentation/UI/HUD/UnitInfoActionRegistry.cs`
- Create: `client/Assets/Scripts/Runtime/Presentation/UI/HUD/ResearchPanel.cs`
- Create: `client/Assets/Scripts/Runtime/Presentation/UI/HUD/PolicyPanel.cs`
- Create: `client/Assets/Scripts/Runtime/Presentation/UI/HUD/BuildingOperationPanel.cs`
- Create: `client/Assets/Scripts/Runtime/Presentation/UI/HUD/CityPanel.cs`
- Modify: `client/Assets/Scripts/Runtime/Presentation/UI/Game/GameSceneController.cs`

- [ ] **Step 1: 在 HUD 中展示当前资源、点数、当前研究目标、国策和主城核心状态**
- [ ] **Step 2: 为节点和建筑提供建造、切配方、查看科技解锁与当前城市信息的入口**
- [ ] **Step 3: 为单位信息面板保留开拓者建城和步兵基础指令，并隐藏未实现谱系动作**
- [ ] **Step 4: 让“提交回合”前的草稿状态可见且可撤销，确保玩家理解自己本回合做了什么**

### Task 23: 地图展示与结算回放

**Files:**
- Modify: `client/Assets/Scripts/Runtime/Presentation/Map/MapRenderer.cs`
- Modify: `client/Assets/Scripts/Runtime/Presentation/Map/BuildingView.cs`
- Modify: `client/Assets/Scripts/Runtime/Presentation/Map/NodeView.cs`
- Modify: `client/Assets/Scripts/Runtime/Presentation/Map/SettlementPlaybackController.cs`
- Modify: `client/Assets/Scripts/Runtime/Presentation/UI/Turn/SettlementTimeline.cs`
- Modify: `client/Assets/Scripts/Runtime/Presentation/UI/Turn/TurnReportPanel.cs`

- [ ] **Step 1: 在地图上区分城市核心、普通建筑、城外设施、建筑状态和主城/新城差异**
- [ ] **Step 2: 让结算回放能播放建城、建筑落地、战斗、设施停用/接管、科技完成和胜负事件**
- [ ] **Step 3: 保证“地图本回合可见、持续收益次回合生效”的反馈在表现层上清晰可理解**
- [ ] **Step 4: 用手工冒烟覆盖一整回合的规划、提交、结算和地图刷新流程**

## 14. Chunk 9：内容定版与验收

### Task 24: 定版 MVP 内容包

**Files:**
- Modify: `data/content/**`
- Modify: `data/ui/catalogs/**`
- Modify: `data/generated/server/catalog.bundle.json`
- Modify: `server/internal/staticdata/catalog_test.go`

- [ ] **Step 1: 为 MVP 定版最小可玩内容集，包括资源、单位、建筑、配方、科技、国策和规则参数**
- [ ] **Step 2: 让内容包能够支撑“主城开局 -> 采集/建造 -> 研究 -> 建城 -> 训练 -> 战斗 -> 判负”的完整闭环**
- [ ] **Step 3: 统一图标 key、prefab key、展示排序和文本描述，避免出现数据存在但客户端无表现的情况**
- [ ] **Step 4: 重新生成并校验服务端 bundle，保证 catalog 测试稳定通过**

### Task 25: 后端验证与回归

**Files:**
- Modify: `server/internal/game/turn_v2_test.go`
- Create: `server/internal/game/planning/service_rules_test.go`
- Modify: `server/internal/engine/production/research_system_test.go`
- Modify: `server/internal/engine/combat/*_test.go`
- Modify: `server/internal/event/building_state_test.go`
- Modify: `server/internal/debug/harness_test.go`
- Modify: `server/internal/debug/integration_test.go`

- [ ] **Step 1: 补齐一局完整 MVP 的服务端回归用例**
- [ ] **Step 2: 覆盖研究推进、建筑解锁、配方阻塞、建城、建筑接管、主城摧毁等关键裁决**
- [x] **Step 3: 运行 `cd server && go test ./...` 与 `cd server && go build ./...`**
- [x] **Step 4: 保持所有规则性断言都在服务端测试中可复现，不把关键验证留给客户端手点**

当前状态：服务端回归已覆盖研究推进、研究解锁后的次回合建造、配方阻塞、开拓者建城、设施停用/失效与主城摧毁判负；完整 MVP 整局回归与“接管完成后归属转移”仍待后续玩法内容补齐。

### Task 26: 客户端与联机冒烟

**Files:**
- Modify: `client/Assets/Scripts/Runtime/Core/Infrastructure/Debug/IntegrationChecker.cs`
- Modify: `client/Assets/Scripts/Runtime/Core/Infrastructure/Debug/DebugPanel.cs`
- Modify: `client/Assets/Scripts/Runtime/Presentation/UI/Game/GameSceneController.cs`

- [ ] **Step 1: 建立最小客户端联机冒烟清单：进房、开局、规划、提交、结算、胜负**
- [ ] **Step 2: 验证研究目标切换、国策切换、建筑放置、配方切换、开拓者建城和战斗回放**
- [ ] **Step 3: 验证断线重连或重复消息情况下，客户端展示仍以服务端最新状态为准**
- [ ] **Step 4: 把剩余问题按“规则 bug / 协议 bug / 表现 bug / 内容 bug”分类记录**

## 15. 延后实施清单

以下内容不进入当前主实施计划，只保留接口和后续路线：

### 中期扩展候选

- 二级加工资源与五大核心单位谱系
- 工程单位
- 制度槽位完整玩法
- 连通才激活的网络规则
- 更细的建筑状态与修复逻辑

### 长期目标候选

- 本地仓储与真实物流
- 双层迷雾
- 大臣默认命令与失真汇报
- 忠诚、性格、亲政机制
- PVE 势力

这些内容只有在当前基线闭环稳定之后，才允许另立子计划推进。不得在主计划尚未完成时并行插入。

## 16. 完成定义

本计划完成时，应满足以下标准：

1. 服务端能以新版 GDD 当前基线规则独立跑完一局，并通过无头对局 harness 完成场景化验证。
2. 客户端能完成研究、建筑、配方、建城、单位命令和基础战斗的完整回合循环。
3. 静态数据、协议、服务端规则和客户端展示对同一批内容使用同一套标识与语义。
4. 未实现的中期、长期内容不影响 MVP 规则闭环，但其接口边界已经清晰。
5. 规则时序、建筑归属、科技推进、政策生效和建筑状态变化都能在规则级测试、无头对局回放和客户端结算回放中被明确观察。

如果把这份计划压缩成一句话，那么本阶段真正要完成的，不是“把 GDD 里每个词都写成代码”，而是把新版 GDD 收敛成一条可靠的可玩闭环：先让国家能被组织起来，再让城市、建筑、科技、政策和战争在同一个回合语言里稳定运行。
