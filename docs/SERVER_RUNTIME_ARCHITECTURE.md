# Panoptes 服务端回合结算架构说明

本文档说明当前服务端回合系统的实现架构，覆盖 Event、Engine Pipeline、回合状态机、内政/战斗结算、部长系统接入与消息路由。

## 1. 总体目标

当前实现将游戏主循环统一为：

1. 内政阶段收集指令
2. 内政结算（Pipeline）
3. 战斗阶段收集指令
4. 战斗结算（Pipeline）
5. 胜负检测 / 回合推进

并通过 Event 机制保证状态变更只在 Event.Apply 中发生，System 仅负责读状态并产出事件。

## 2. 核心分层

### 2.1 Event 层

文件：
- server/internal/event/interface.go
- server/internal/event/combat.go
- server/internal/event/production.go
- server/internal/event/minister.go
- server/internal/event/game.go

核心接口：

```go
type Event interface {
    Apply(world donburi.World, state *domain.GameState)
    ClientPayload() *pb.CombatEvent
    String() string
}
```

职责：
- Apply：执行真实状态变更（ECS 组件、GameState 字段、资源数值等）
- ClientPayload：将战斗事件序列化为客户端可播放动画事件
- String：用于日志和内政变更摘要

实现分类：
- combat 事件：UnitMoved/UnitDamaged/UnitDied/CastleDamaged/CastleDestroyed/RoadDestroyed/BuildingDamaged/ConflictResolved
- production 事件：BuildingBuilt/ResourceProduced/ResourceFlowed/RoadBuilt/UnitProduced/BuildPointsRecharged/UpkeepPaid/UnitStarving/BuildingDeactivated
- minister 事件：MinisterActed/PolicyChanged/TokenUsed
- game 事件：TurnStarted/PhaseChanged/GameOver/PlayerReconnected

### 2.2 Engine 层

文件：
- server/internal/engine/pipeline.go
- server/internal/engine/production/*.go
- server/internal/engine/combat/*.go

核心接口：

```go
type System interface {
    Run(world donburi.World, state *domain.GameState) []event.Event
}
```

Pipeline 行为：
1. 按顺序执行每个 System.Run 收集事件
2. 统一对事件调用 Apply
3. 返回完整事件列表供结算推送

内政 Pipeline 顺序：
- BuildSystem
- FlowSystem
- ProductionSystem
- UpkeepSystem
- RechargeSystem

战斗 Pipeline 顺序：
- MovementSystem
- ConflictSystem
- BattleSystem
- SiegeSystem
- RangedSystem
- DestroySystem
- CombatUpkeepSystem

## 3. GameState 扩展

文件：server/internal/domain/state.go

为了支持结算与跨系统临时数据，GameState 增加了：

- 对局终局字段：IsOver、WinnerID、OverReason、Narrative
- 指令队列：PendingBuilds、MinisterBuildOrders、MinisterMoveOrders
- 战斗临时数据：PendingMoves、PendingConflicts

这使系统间通信可通过 state 临时字段完成，而不是系统互相调用。

## 4. 回合状态机

文件：
- server/internal/game/phase/interface.go
- server/internal/game/phase/domestic.go
- server/internal/game/phase/combat.go
- server/internal/game/room.go
- server/internal/game/settlement.go

### 4.1 Phase 抽象

```go
type Phase interface {
    Name() string
    Enter(room Room)
    HandleMessage(room Room, playerID string, msgType string, payload []byte) error
    Timeout(room Room)
}
```

Room 在 phase 包内暴露最小能力接口，避免 phase 依赖具体实现细节。

### 4.2 DomesticPhase

职责：
- Enter 时重置提交状态并补充 tokens
- 处理 MsgSetPolicy / MsgTokenBuild / MsgTokenReveal / MsgMinisterDirective / MsgSubmitDomestic
- 令牌建造经过安全区、建筑占位、资源可支付校验
- 超时时通过 Submit("timeout") 结束等待

### 4.3 CombatPhase

职责：
- Enter 时重置提交与战区指令缓存
- 处理 MsgSetWarZone / MsgWarZoneDirective / MsgTokenVetoCombat / MsgTokenMicro / MsgSubmitCombat
- 令牌微操与否决操作会消耗 token
- 超时时通过 Submit("timeout") 结束等待

### 4.4 GameRoom 主循环

当前 runLoop：
1. 每回合开始触发部长汇报生成
2. 进入 DomesticPhase，等待提交或超时
3. RunDomesticSettlement
4. 若未结束，进入 CombatPhase，等待提交或超时
5. RunCombatSettlement
6. 回合 +1，检查 MaxTurns

同时加入默认超时兜底：
- domestic 默认 15s
- combat 默认 20s

避免配置为 0 时出现自旋。

### 4.5 Settlement

- RunDomesticSettlement：同步 room.pendingBuilds 到 state，执行内政 pipeline，推送内政结算，检测胜负
- RunCombatSettlement：先把指令写回 MoveIntent，再执行战斗 pipeline，推送战斗结算，检测胜负

## 5. 战斗系统实现摘要

文件：server/internal/engine/combat/*.go

### 5.1 MovementSystem

- 读取 MoveIntent 单位
- 若无路径则调用 A* 计算
- 按速度截断路径
- 产出 UnitMovedEvent
- 同时写入 state.PendingMoves 供冲突系统使用

### 5.2 ConflictSystem

按双单位组合检测：
- 边冲突（反向换边）
- 节点冲突（同终点）
- 追及冲突（同向追上）

并按规则排序：
- time_step 升序
- 同步内 max_speed 降序

结果写入 state.PendingConflicts，并产出 ConflictResolvedEvent。

### 5.3 BattleSystem

- 读取 PendingConflicts 逐条结算
- 伤害由 attack、克制系数、地形系数组合计算
- 产出 UnitDamagedEvent，HP<=0 追加 UnitDiedEvent

### 5.4 SiegeSystem

- Siege 单位攻击同格敌方城堡建筑
- 墙体减伤后产出 CastleDamagedEvent
- 城堡归零产出 CastleDestroyedEvent
- 箭塔反击产出 UnitDamagedEvent / UnitDiedEvent

### 5.5 RangedSystem

- 在射程内选 HP 最低敌方单位
- 计算远程伤害（含地形）
- 产出 UnitDamagedEvent / UnitDiedEvent

### 5.6 DestroySystem

- 对当前格及相邻格执行破坏逻辑
- 道路破坏：RoadDestroyedEvent
- 建筑破坏：BuildingDamagedEvent

### 5.7 CombatUpkeepSystem

- 统计每玩家单位粮耗
- 产出 UpkeepPaidEvent
- 粮不足时为全部单位产出 UnitStarvingEvent

## 6. 内政系统实现摘要

文件：server/internal/engine/production/*.go

- BuildSystem：消费 PendingBuilds + MinisterBuildOrders，校验资源并产出 BuildingBuiltEvent
- FlowSystem：资源点建筑产出 ResourceProducedEvent + ResourceFlowedEvent
- ProductionSystem：兵营类建筑满足输入资源时产出 UnitProducedEvent
- UpkeepSystem：建筑维持消耗，不足产出 BuildingDeactivatedEvent
- RechargeSystem：每回合为玩家产出 BuildPointsRechargedEvent

## 7. 部长系统接入

文件：server/internal/engine/minister/*.go

### 7.1 Memory

- MinisterMemory 维护最近 5 条记录
- Add/Recent/ToPromptString 均为并发安全（RWMutex）

### 7.2 Prompt

BuildMinisterPrompt 由：
- 角色设定
- 局势摘要
- 历史记忆
- 当前国策
- JSON 输出约束

组成 llm.CompletionRequest。

### 7.3 Parser + Actions

ParseMinisterResponse 解析 JSON 输出为：
- report
- metrics
- actions
- action_id

ExecuteActions 支持：
- build -> 进入 MinisterBuildOrders
- repair_road -> 直接 RoadBuiltEvent
- move_units -> 进入 MinisterMoveOrders
- redirect_flow -> 当前保留

### 7.4 Engine

- GenerateReports 为每玩家/部长生成汇报
- LLM 可用时异步并发流式发送 MsgMinisterReportChunk
- LLM 不可用时走同步 fallback，发送默认汇报
- 汇报结束发送 MsgMinisterMetrics
- actions 执行后写回状态并记录 MinisterActedEvent

## 8. 路由与消息入口

文件：server/internal/transport/websocket/router.go

新增阶段消息路由：
- MsgSetPolicy
- MsgTokenBuild
- MsgTokenReveal
- MsgMinisterDirective
- MsgSetWarZone
- MsgWarZoneDirective
- MsgTokenVetoCombat
- MsgTokenMicro

流程：
1. 根据 playerID 找 GameRoom
2. 调用 OnHumanMessage 转发给当前 Phase
3. 若找不到房间返回 game_not_found

## 9. 算法基础模块

为战斗与资源链路补充了可复用算法组件：

- server/internal/algo/pathfinding/astar.go：A* 寻路
- server/internal/algo/geometry/distance.go：曼哈顿距离
- server/internal/algo/graph/flow.go：最大流基础实现

## 10. 关键工程约束落地情况

- System 不直接改状态：通过返回 event 列表，由 pipeline 统一 Apply
- 结算推送统一由 room.broadcastSettlement 输出
- GameOver 统一由 room.checkGameOver / handleDraw 触发广播与注销
- 测试稳定性：
  - 默认回合超时兜底避免 0 值自旋
  - 部长记忆并发安全
  - 无 LLM 时同步汇报，避免测试 transport 并发写崩溃

## 11. 当前边界与后续建议

已实现完整可运行框架与真实基础结算；后续可继续增强：

- 将 ResourceFlowedEvent 从占位“同节点流”升级为真实图流路径
- 完善冲突系统对多单位链式冲突的二次迭代结算
- 将 redirect_flow 行动真正接入资源流配置
- 为事件和系统补充更细粒度单元测试（尤其 battle/siege/destroy 数值回归）
