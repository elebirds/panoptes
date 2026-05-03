# Panoptes Turn V2 Refactor Plan

> 状态：实施基线  
> 日期：2026-04-14  
> 优先级：后端优先，前端随后跟进  
> 适用范围：回合结构、协议、服务端架构、客户端回合同步链路  
> 替代文档：
> - `docs/gdd/2026-04-14-turn-structure-v2-draft.md`
> - `docs/SERVER_RUNTIME_ARCHITECTURE.md`
> - `docs/combat/2026-04-12-manual-combat-v1-spec.md`
>
> 补充说明：运行时主链的最新收敛设计见 `docs/2026-04-16-server-runtime-unification-plan.md`。当前实现已经明确采用顶层 `Runner + Stage`，而不是把通用 `Pipeline` 作为服务端运行时核心。

## 1. 本文档解决什么

本文档不是概念讨论稿，而是 Panoptes 下一阶段的正式重构蓝图，用于统一回答以下问题：

- 新回合结构最终长什么样
- 旧的“内政 / 战斗”二阶段如何迁移掉
- 哪些 proto、后端代码、前端代码最终会被删除
- 后端现有结构混乱点如何拆分
- 为什么迁移顺序必须先后端，再前端

## 2. 最终裁决

以下内容作为本次重构的硬裁决：

1. 玩家侧不再保留“内政阶段 / 军事阶段”双主阶段。
2. 玩家侧统一为 `planning -> resolving -> next_turn`。
3. 所有真实状态变更统一在结算中发生，规划阶段只收集命令。
4. 单位按“动作类型”而不是“内政 / 军事身份”参与结算。
5. 坐城、修路、改良、修复都属于地图动作层，不再跨阶段硬塞。
6. 地图变化本回合立刻可见，新增经济收益默认下回合生效。
7. 后端必须先完成回合内核重构，再切前端协议与 UI。
8. 旧 proto 与旧二阶段代码不做长期兼容；迁移完成后直接删除。

## 3. 现状问题评估

当前代码已经出现明显的结构性失真。

### 3.1 回合状态机与房间对象耦合过深

当前 `server/internal/game/room.go` 同时负责：

- 房间生命周期
- 回合主循环
- submit 等待
- 阶段切换
- 命令暂存
- 结算广播
- 初始化地图和单位
- 构建客户端视图
- 部长生成触发

这导致：

- `GameRoom` 成为隐式 God Object
- 测试范围大而脆
- 任意改动回合逻辑时都容易碰到网络 / 初始化 / 视图拼装

### 3.2 二阶段假设已经渗透到协议、状态和客户端

当前存在明确的双阶段硬编码面：

- `protocol/domestic.proto`
- `protocol/combat.proto`
- `server/internal/game/phase/*`
- `server/internal/game/settlement.go`
- `server/internal/transport/websocket/router.go`
- `client/Assets/Scripts/Runtime/Core/Application/Handler/GameMessageHandler.cs`
- `client/Assets/Scripts/Runtime/Core/Application/Cache/GameStateCache.cs`
- `client/Assets/Scripts/Runtime/Core/Application/Cache/CombatDraftCache.cs`
- `client/Assets/Scripts/Runtime/Presentation/UI/Domestic/*`
- `client/Assets/Scripts/Runtime/Presentation/UI/Combat/*`

### 3.3 领域事件接口被客户端战斗消息绑死

当前 `server/internal/event/interface.go` 的 `Event` 接口暴露：

```go
ClientPayload() *pb.CombatEvent
```

这意味着：

- 服务端领域事件直接知道客户端 proto
- 非战斗事件也被迫用“是否能转成 CombatEvent”表达
- 统一回合结算几乎无法自然承载地图事件、经济事件、科研事件

这是必须拆掉的耦合点。

### 3.4 GameState 过胖且语义混杂

当前 `server/internal/domain/state.go` 同时承载：

- 持久对局状态
- 玩家资源与研究
- 城堡状态
- 回合临时命令队列
- 战斗临时冲突缓存
- ActiveMarch 与跨回合移动缓存
- 科技 modifier 计算

结果是：

- 持久状态与单回合临时态边界不清
- 迁移到统一 planning / resolving 时会持续冒出兼容 hack

### 3.5 现有开拓逻辑已经说明阶段边界失效

`server/internal/game/phase/domestic_expand.go` 当前的实现已经在说明一件事：

- “开拓”被挂在内政消息上
- 但它实际在改地图、删单位、发 reveal、借战斗结算消息表达移除

这不是一个局部 bug，而是旧模型已经无法自然容纳新玩法。

## 4. 目标回合模型

### 4.1 玩家可见阶段

最终玩家只看到两个主状态：

- `planning`
- `resolving`

可选附加显示态：

- `turn_report`

其中：

- `planning`：玩家查看汇报、调整政策、下达本回合全部命令
- `resolving`：服务端统一结算并推送结果
- `turn_report`：可选的结算摘要或下一回合汇报入口，不是独立操作阶段

### 4.2 服务端内部结算链

固定顺序如下：

1. 锁定输入
2. 战略状态切换
3. 单位动作结算
4. 战斗与冲突结算
5. 地图动作结算
6. 地图刷新
7. 经济与科研结算
8. 胜负检测与回合推进

### 4.3 生效时机总规则

- 地图存在变化：本回合立刻显示
- 单位位置与存亡：本回合立刻显示
- 新道路连通性：本回合末生效
- 新城 / 新改良产出：下回合生效
- 新单位 / 新建筑主动能力：下回合开放
- 科技完成：本回合显示完成，下回合正式生效

## 5. 目标协议结构

### 5.1 保留的 proto

以下 proto 保留，必要时只做字段调整：

- `protocol/panoptes/proto/v1/common.proto`
- `protocol/panoptes/proto/v1/auth.proto`
- `protocol/panoptes/proto/v1/lobby.proto`
- `protocol/panoptes/proto/v1/data_types.proto`
- `protocol/panoptes/proto/v1/data_catalog.proto`
- `protocol/panoptes/proto/v1/map_catalog.proto`
- `protocol/panoptes/proto/v1/game_state.proto`
- `protocol/panoptes/proto/v1/minister.proto`

### 5.2 删除的 proto

迁移完成后，以下 proto 直接删除：

- `protocol/domestic.proto`
- `protocol/combat.proto`

对应生成文件也一并删除：

- `server/internal/gen/proto/domestic.pb.go`
- `server/internal/gen/proto/combat.pb.go`
- `client/Assets/Scripts/Protocol/Domestic.cs`
- `client/Assets/Scripts/Protocol/Combat.cs`

### 5.3 新 proto 规划

新增三个 proto：

### `protocol/panoptes/proto/v1/orders.proto`

职责：承载玩家在 `planning` 阶段发出的所有命令。

建议消息集合：

- `MsgSetPolicy`
- `MsgSetResearchTarget`
- `MsgSetBuildingRecipe`
- `MsgSetWarZone`
- `MsgSetMinisterDirective`
- `MsgIssueUnitOrder`
- `MsgCancelUnitOrder`
- `MsgSubmitTurn`

说明：

- `MsgIssueUnitOrder` 统一承载 `move | hold | attack | charge | settle_city | build_road | repair_road | build_improvement | repair_improvement`
- 不再为“内政动作”和“战斗动作”拆不同消息域

### `protocol/panoptes/proto/v1/turn.proto`

职责：承载回合开始、规划开始、规划快照、回合结束等控制类消息。

建议消息集合：

- `MsgPlanningStart`
- `MsgPlanningSnapshot`
- `MsgTurnSettlement`
- `MsgTurnReport`
- `MsgGameOver`

说明：

- `MsgSubmitDomestic` 与 `MsgSubmitCombat` 被 `MsgSubmitTurn` 替代
- `MsgDomesticPhaseStart` 与 `MsgCombatPhaseStart` 被 `MsgPlanningStart` 替代
- `MsgDomesticSettlement` 与 `MsgCombatSettlement` 被 `MsgTurnSettlement` 替代

### `protocol/panoptes/proto/v1/settlement.proto`

职责：定义统一回合结算载荷。

建议结构：

- `SettlementSection`
- `TurnEvent`
- `UnitEvent`
- `MapEvent`
- `EconomyEvent`
- `ResearchEvent`
- `PlayerDelta`

说明：

- 客户端不再依赖“domestic changes + combat events”二分模型
- 服务端一次推送完整回合结果，由客户端按 section 播放

### 5.4 废弃消息映射

| 旧消息 | 新消息 |
|---|---|
| `MsgSubmitDomestic` | `MsgSubmitTurn` |
| `MsgSubmitCombat` | `MsgSubmitTurn` |
| `MsgDomesticPhaseStart` | `MsgPlanningStart` |
| `MsgCombatPhaseStart` | `MsgPlanningStart` |
| `MsgDomesticSettlement` | `MsgTurnSettlement` |
| `MsgCombatSettlement` | `MsgTurnSettlement` |
| `MsgCombatOrdersSnapshot` | `MsgPlanningSnapshot` |
| `MsgTokenExpandTerritory` | `MsgIssueUnitOrder(action=settle_city)` |
| `MsgTokenMicro` | `MsgIssueUnitOrder` |
| `MsgCombatOrder` | `MsgIssueUnitOrder` |

## 6. 目标后端结构

后端重构原则不是“把大文件机械拆小”，而是重建责任边界。

### 6.1 目标目录

建议目标结构如下：

```text
server/internal/game/
├── bootstrap/           # 对局启动、地图初始化、初始单位
├── session/             # 房间对象，薄封装，只负责玩家连接与对局持有
├── turn/                # TurnCoordinator、主循环、阶段推进
├── planning/            # 规划窗口、命令收集、提交控制、命令校验
├── orders/              # 统一命令模型与归一化
├── resolution/          # 回合统一结算协调器
│   ├── units/           # 移动、冲突、战斗
│   ├── mapactions/      # 坐城、修路、改良、修复
│   ├── economy/         # 资源流、生产、补给、科研
│   └── report/          # 结算结果组装
├── query/               # NodeView、PlayerView、PlanningSnapshot 查询构建
└── transport/adapter/   # 对 websocket/transport 暴露最小接口
```

### 6.2 GameRoom 的重构目标

当前 `GameRoom` 最终应缩成一个薄对象，仅负责：

- 标识和玩家列表
- 当前对局 session 持有
- 触发 TurnCoordinator
- 发送消息到 transport

以下职责必须搬走：

- `runLoop` -> `turn/Coordinator`
- `broadcastSettlement` -> `resolution/report`
- `buildNodeView/buildPlayerView/buildUnitViews` -> `query`
- `spawnInitialBaseVehicles/grantDevStartingResources/initializeCastleStates` -> `bootstrap`
- `prepareCombatOrders/applyPendingTerritoryDeploys/applyQueuedBuildOrdersAtDomesticStart` -> 删除或吸收到 `planning` / `resolution`

### 6.3 GameState 的拆分目标

`server/internal/domain/state.go` 必须拆分为至少以下几块：

- `state_game.go`：对局主状态
- `state_player.go`：玩家状态与资源
- `state_map.go`：地图与节点索引
- `state_research.go`：研究与科技效果
- `orders.go`：统一命令类型
- `turn_runtime.go`：单回合临时态
- `modifiers.go`：科技 / 政策 / 全局修正计算

核心原则：

- 持久态和单回合临时态分离
- query 辅助逻辑不继续堆进 state

### 6.4 Event 体系的重构目标

`server/internal/event` 保留“统一 Apply 入口”的思想，但去掉客户端 proto 耦合。

重构后建议：

```go
type Event interface {
    Apply(world donburi.World, state *domain.GameState)
    Kind() string
    String() string
}
```

客户端可播放数据不再由事件对象直接生成，而由：

- `game/projection`
- `query/settlement_mapper`

按最终协议进行映射。

### 6.5 旧 engine 包的去向

现有：

- `server/internal/engine/combat`
- `server/internal/engine/economy`

不再按“旧阶段名称”组织。

推荐迁移为：

- `resolution/units`：原 combat 中与单位移动、冲突、战斗直接相关部分
- `resolution/economy`：原 economy 中与资源、生产、补给、科研相关部分
- `resolution/mapactions`：把 `domestic_expand` 这类地图动作纳入统一结算

`engine/maploader` 与 `engine/minister` 可暂留，但建议后续分别并入：

- `bootstrap`
- `game/minister`

## 7. 目标前端结构

前端迁移在后端核心稳定后开始。

### 7.1 删除的客户端结构

最终删除：

- `client/Assets/Scripts/Runtime/Presentation/UI/Domestic/*`
- `client/Assets/Scripts/Runtime/Presentation/UI/Combat/*`
- `client/Assets/Scripts/Runtime/Core/Application/Cache/CombatDraftCache.cs`

以下文件需重写或大幅收缩：

- `client/Assets/Scripts/Runtime/Core/Application/Handler/GameMessageHandler.cs`
- `client/Assets/Scripts/Runtime/Core/Application/Cache/GameStateCache.cs`
- `client/Assets/Scripts/Runtime/Core/Application/Intents/GameIntents.cs`
- `client/Assets/Scripts/Runtime/Core/Infrastructure/Debug/DebugMessageRegistry.cs`
- `client/Assets/Scripts/Runtime/Core/Infrastructure/Debug/MessageLogger.cs`

### 7.2 新前端结构

建议收敛为：

```text
client/Assets/Scripts/Runtime/Presentation/UI/
├── Planning/
│   ├── PlanningPanel.cs
│   ├── StrategicPanel.cs
│   ├── UnitOrdersPanel.cs
│   ├── BuildQueuePanel.cs
│   └── MinisterInterventionPanel.cs
├── Resolution/
│   ├── ResolutionTimeline.cs
│   ├── SettlementSummaryPanel.cs
│   └── TurnReportPanel.cs
└── Common/
```

缓存建议收敛为：

- `TurnDraftCache`
- `GameStateCache`
- `SettlementPlaybackState`

## 8. 明确的删除清单

以下内容不是“考虑删除”，而是迁移完成后的硬删除清单。

### 8.1 Proto 与生成代码

- `protocol/domestic.proto`
- `protocol/combat.proto`
- `server/internal/gen/proto/domestic.pb.go`
- `server/internal/gen/proto/combat.pb.go`
- `client/Assets/Scripts/Protocol/Domestic.cs`
- `client/Assets/Scripts/Protocol/Combat.cs`

### 8.2 后端代码

- `server/internal/game/phase/combat.go`
- `server/internal/game/phase/combat_test.go`
- `server/internal/game/phase/domestic.go`
- `server/internal/game/phase/domestic_expand.go`
- `server/internal/game/phase/domestic_research_test.go`
- `server/internal/game/phase/interface.go`
- `server/internal/game/phase/doc.go`
- `server/internal/game/settlement.go`

以下文件不是原样保留，而是重写后删除旧版本：

- `server/internal/game/room.go`
- `server/internal/game/room_march.go`
- `server/internal/transport/websocket/router.go`
- `server/internal/event/interface.go`

### 8.3 前端代码

- `client/Assets/Scripts/Runtime/Presentation/UI/Domestic/*`
- `client/Assets/Scripts/Runtime/Presentation/UI/Combat/*`
- `client/Assets/Scripts/Runtime/Core/Application/Cache/CombatDraftCache.cs`

以下文件在切换新协议后重写并删除旧实现：

- `client/Assets/Scripts/Runtime/Core/Application/Handler/GameMessageHandler.cs`
- `client/Assets/Scripts/Runtime/Core/Application/Cache/GameStateCache.cs`
- `client/Assets/Scripts/Runtime/Core/Application/Intents/GameIntents.cs`

## 9. 迁移流程

本次迁移按“后端先定型，前端后切换，最后统一清理”的顺序推进。

### Phase 0：文档冻结

产出：

- 本文档
- 旧规划文档废弃标记

目标：

- 不再讨论是否保留二阶段
- 不再新增基于 `domestic/combat` 的新功能

### Phase 1：后端结构拆分，不改玩家协议

目标：

- 先把后端从 God Object 和双阶段硬耦合中拆出来
- 先不让前端立刻跟着崩

实施：

1. 新建 `turn/Coordinator`
2. 新建 `planning`、`orders`、`resolution`、`query` 包
3. 把 `GameRoom` 缩成 session 外壳
4. 拆 `GameState`
5. 去掉 `event.Event -> CombatEvent` 的客户端耦合

此阶段允许：

- 临时保留旧 proto
- 临时保留旧消息名

此阶段不允许：

- 再往 `game/phase` 增加新逻辑
- 再往 `room.go` 继续堆业务

### Phase 2：后端统一 planning / resolving 内核

目标：

- 服务端内部彻底不再按 domestic / combat 两套逻辑运行

实施：

1. 引入统一 `TurnCommandSet`
2. 引入统一 `PlanningWindow`
3. 引入统一 `ResolutionCoordinator`
4. 坐城 / 修路 / 改良迁入 `mapactions`
5. 经济与科研结算迁入 `resolution/economy`

完成标准：

- 后端内部已经不依赖 `game/phase/*`
- 回合结算已经按新顺序运行

### Phase 3：新 proto 切换

目标：

- 建立新协议，不再输出旧二阶段消息

实施：

1. 新增 `orders.proto`
2. 新增 `turn.proto`
3. 新增 `settlement.proto`
4. 调整 `game_state.proto` phase 字段口径
5. `make gen`

完成标准：

- 服务端只对外生成新 planning / settlement 消息
- 旧 `domestic/combat` proto 进入待删除状态

### Phase 4：前端切换

目标：

- 前端从二阶段 UI 和缓存模型迁移到 planning / resolution

实施：

1. 接入新 proto
2. 重写 `GameMessageHandler`
3. 将 `CombatDraftCache` 替换为 `TurnDraftCache`
4. 将 `Domestic UI` 与 `Combat UI` 合并为 `Planning UI`
5. 增加 `ResolutionTimeline`

完成标准：

- 客户端不再消费 `MsgDomestic*` / `MsgCombat*`
- 客户端能按 section 播放统一回合结算

### Phase 5：统一清理与删除

目标：

- 删除所有废弃协议、废弃代码、废弃测试与旧 UI

实施：

1. 删除 `protocol/domestic.proto` / `protocol/combat.proto`
2. `make gen`
3. 删除旧 generated code
4. 删除旧 phase 包
5. 删除旧 UI 目录
6. 清理 router 中旧消息名
7. 清理旧测试并补齐新测试

## 10. 测试与验收策略

### 10.1 后端验收重点

- 统一 planning 期间可正常收集政策、研究、生产、单位命令
- 单位移动、战斗、地图动作、经济结算按新顺序执行
- 坐城成功后本回合可见、下回合产出
- 新道路可影响本回合末补给
- 科技完成本回合显示、下回合生效

### 10.2 前端验收重点

- 一个 planning UI 能覆盖旧 domestic + combat 的主要操作
- 一个 settlement 播放链能覆盖单位、地图、经济、科研结果
- 缓存状态不再依赖双阶段切换

## 11. 执行纪律

从本文档生效后，以下规则立即生效：

1. 不再新增任何 `domestic_* / combat_*` 新消息。
2. 不再新增任何 `game/phase/*` 业务逻辑。
3. 不再向 `GameRoom` 增加新职责。
4. 不再让 `event.Event` 直接依赖客户端 proto。
5. 新的回合相关实现必须落到 `planning` / `resolution` / `orders` / `query` 的目标结构里。

## 12. 一句话总结

Panoptes 接下来不是在旧双阶段模型上打补丁，而是做一次明确的内核换代：

- 玩家侧统一 planning
- 服务端统一 resolution
- 后端先拆干净
- 前端随后切换
- 旧 proto、旧 phase、旧 UI 最终全部删除
