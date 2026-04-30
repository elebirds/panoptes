# Directory Structure

> How backend code is organized in this project.

---

## Overview

The backend is organized as a layered Go service under `server/internal`.
The current MVP runtime uses Turn V2: `planning -> resolving -> next planning`.
Keep package ownership narrow and move logic to the lowest layer that has
enough information to perform it.

## 中文架构规范摘要

后端结构优化后的核心原则是：**root `game` 只做房间/会话/编排桥接，状态规则下沉到拥有该规则的包**。

新增或修改后端逻辑时按以下规则归位：

* 只依赖 `*domain.GameState` 的 resolving 输入构造，不放 `GameRoom`。
* planning 命令 oneof 适配、preview、响应投递顺序与 metadata 传播归 `game/planning`。
* 单位订单校验、草案写入、ActiveMarch 同步、resolving order freeze、结算后行军刷新和 map action 事件构造归 `game/orders`。
* 回合主循环归 `game/turn/coordinator.go`，命令分发归同包 `command_handler.go`。
* 客户端观察字段组合归 `game/projection.ObservedState`，不要在 session/sync 中重复拼 `MyPlayer/Nodes/Units/Snapshot`。
* 建筑专属 ECS 组件装配归 `building`，`ecs` 只保留实体创建与通用查询。
* Donburi `Query` 不做包级共享缓存；查询对象按调用创建，避免多房间/并发测试写同一份内部缓存。
* event `Apply()` 内不要嵌套调用另一个 event 的 `Apply()`；需要复用时抽私有 mutation helper。
* 大文件先按同包职责拆分，再考虑跨包 API 变动。

---

## Directory Layout

```
server/internal/
├── domain/              # Authoritative state models and state-local helpers
├── event/               # State mutation events; settlement writes flow through Apply
├── ecs/                 # ECS factories, component aliases, and common queries
├── engine/              # Rule systems that read state and emit events
├── building/            # Building rules and building lifecycle orchestration
├── game/
│   ├── planning/        # Planning command validation and draft writes
│   ├── query/           # Authoritative state -> player observations/views
│   ├── projection/      # Events/state -> client sync/report messages
│   ├── resolution/      # Turn resolving runner, stages, and state-only commit helpers
│   ├── session/         # Runtime/session lifecycle, bootstrap, observations, chat, minister prep
│   └── turn/            # Turn coordinator and phase command routing
├── transport/           # Protocol dispatch, codecs, HTTP/websocket transport boundaries
├── auth/, lobby/, store/, db/
└── gen/                 # Generated code; do not edit manually
```

---

## Module Organization

### Convention: Keep `GameRoom` Thin

**What**: `GameRoom` is the bridge for room lifecycle, transport sending,
debug hooks, and session/coordinator wiring. Do not add pure game-state
algorithms or resolving-stage policy to `GameRoom` when the logic can operate
on `*domain.GameState`.

**Why**: Older code made the room object the natural place for every turn
concern. MVP backend work is now closing that boundary so resolving behavior can
be tested without network/session setup.

**Correct**:

```go
collector := resolution.NewTurnResolutionRunner().Run(state, resolution.RunnerHooks{
	PlanningCommitEvents: resolution.BuildPlanningCommitEvents,
})
state.TurnRuntime.ClearPostResolutionScratch()
```

**Wrong**:

```go
events := room.planningCommitEvents()
room.clearTurnRuntimeScratch()
```

If a helper only needs `*domain.GameState`, prefer one of:

* `domain/` for state-local lifecycle helpers such as clearing transient runtime data.
* `game/resolution/` for resolving-specific event construction and stage helpers.
* `engine/<area>/` for rule systems that read world/state and emit events.

Keep behavior in `GameRoom` only when it truly needs room/session services:
transport sends, debug hooks, participant/session lookup, route preview helpers,
or coordinator interactions.

### Convention: Planning Owns Planning Command Adaptation

**What**: Conversions that turn planning-shaped protocol commands into planning
commands or planning intents belong in `game/planning`, even when the caller is
the turn coordinator.

**Why**: Batch commands and direct planning commands must stay behaviorally
identical. Keeping the `CommandEnvelope -> PlanningCommand` adapter in
`game/planning` prevents `game/turn` from duplicating planning oneof knowledge.

**Correct**:

```go
commands, ok := planning.AdaptCommandBatch(ctx, batch)
```

**Wrong**:

```go
// Do not add another CommandEnvelope oneof switch in turn.Coordinator.
cmd := planningCommandFromEnvelope(envelope)
```

### Convention: Map Actions Are Order/Resolution Inputs, Not Room Logic

**What**: State-only map action event construction belongs outside root
`game`. Current `settle_city` planning directives are translated by
`game/orders.BuildMapActionEvents(state)`.

**Why**: Map action translation only needs authoritative state and produces
domain events. Keeping it out of `GameRoom` makes the resolving pipeline easier
to test without session or transport setup.

**Correct**:

```go
MapActionEvents: orders.BuildMapActionEvents
```

**Wrong**:

```go
MapActionEvents: func(*domain.GameState) []event.Event {
	return room.plannedMapActionEvents()
}
```

Unit-order behavior follows the same rule: validation, planning-state
application/cancel, active-march synchronization, resolving-order freeze, and
post-settlement active-march refresh belong in `game/orders`. Root `game` may
provide route-preview callbacks because route preview still depends on room
state and combat planner wiring.

### Convention: Keep Turn Loop and Command Dispatch Separate

**What**: `game/turn/coordinator.go` owns turn progression: entering planning,
waiting for submit/timeout, running resolution, max-turn draw handling, and turn
increment. Game command dispatch methods live in a separate file in the same
package.

**Why**: The coordinator should be readable as the phase loop. Dispatch remains
in `game/turn` because it needs the coordinator services, but it should not
crowd the loop implementation.

### Convention: Planning Uses Ports and Delivery Helpers

**What**: `game/planning` keeps the exported `Service.HandleCommand` API, but
internally uses narrow ports and delivery helpers instead of one broad room-like
interface for every handler.

**Why**: Planning handlers should read as command execution, not transport
plumbing. Delivery helpers preserve message ordering and metadata so handlers do
not reimplement result-then-snapshot sending.

### Convention: Shared Observation Projection

**What**: When both planning start and game sync need `MyPlayer`, `Nodes`,
`Units`, or planning snapshot fields from the same observation, use
`game/projection.ObservedState`.

**Why**: Planning-start and game-sync wire shapes differ, but their observed
state composition should not drift.

### Convention: Building Owns Building Assembly

**What**: `ecs.CreateBuilding` creates the entity, but building-specific
component attachment and default operation/takeover wiring belongs to
`building`.

**Why**: ECS factories should not silently own building lifecycle policy.
`building.ValidatePlacement` is the canonical full placement rule; node-only
placement checks must be named as such.

### Convention: Donburi Queries Are Short-Lived

**What**: Do not store `*donburi.Query` as package-level shared state. Create a
query in the helper that uses it, or behind a small constructor such as
`newNodeQuery()`.

**Why**: Donburi queries keep internal archetype caches. Sharing one query
object across concurrent game worlds can turn read-style helper calls into
concurrent writes to the query cache.

### Convention: Events Do Not Nest Other Event Applies

**What**: Do not add new `OtherEvent.Apply(world, state)` calls inside event
application methods. Use shared private mutation helpers, or have producers emit
multiple reportable events explicitly through the collector.

**Why**: Nested event application hides semantically meaningful events from the
resolution collector and client reports.

### Convention: Large Packages Split By Concern First

**What**: For large same-package rule surfaces, prefer file-level splits by
ownership before changing package APIs.

**Examples**:

* `staticdata`: model subjects, default/load/index/query/hash.
* `datagen`: emit/json/map/ui/render/schema/validate concerns.
* `engine/economy`: recipe selection, progress, affordability, consumption, and event construction.
* `engine/maploader`: procedural terrain, resources, spawns, nodes, validation.
* `game/ai`: candidates, scoring, domestic/combat/expansion intents, owned-state helpers.
* `game/scenario`: definitions, catalog, maps, setup, placement.

---

## Naming Conventions

Name backend files after the trigger or ownership surface they implement:

* `planning_commit.go` for planning decisions committed at resolving start.
* `turn_runtime.go` for helpers on `domain.TurnRuntime`.
* `command_handler.go` for turn-level command dispatch adapters.
* `map_actions.go` for map-action event construction owned by `game/orders`.
* `ports.go` for narrow planning ports.
* `delivery.go` for planning response delivery/order helpers.
* `observed_state.go` for shared projection composition.
* `assembly.go` for building-owned ECS component assembly.
* `*_test.go` beside the package that owns the helper being tested.

---

## Examples

Good current examples:

* `server/internal/game/resolution/runner.go` owns the resolving stage order without importing root `game`.
* `server/internal/game/resolution/planning_commit.go` builds planning commit events from `*domain.GameState`.
* `server/internal/domain/turn_runtime.go` owns clearing post-resolution scratch state.
* `server/internal/game/planning/adapter.go` owns planning command and batch command adaptation.
* `server/internal/game/orders/map_actions.go` owns state-only map-action event construction.
* `server/internal/game/orders/planning_state.go` and `resolving_state.go` own unit-order lifecycle state.
* `server/internal/game/turn/command_handler.go` owns turn-level command dispatch while `coordinator.go` stays focused on the phase loop.
* `server/internal/game/projection/observed_state.go` shares observation composition between planning start and game sync.
* `server/internal/building/assembly.go` owns building-specific ECS assembly.
* `server/internal/game/settlement.go` remains the room-level orchestration bridge for broadcast, debug dump, game-over checks, and room-dependent hooks.
