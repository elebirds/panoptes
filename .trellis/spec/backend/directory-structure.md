# Directory Structure

> How backend code is organized in this project.

---

## Overview

The backend is organized as a layered Go service under `server/internal`.
The current MVP runtime uses Turn V2: `planning -> resolving -> next planning`.
Keep package ownership narrow and move logic to the lowest layer that has
enough information to perform it.

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

---

## Naming Conventions

Name backend files after the trigger or ownership surface they implement:

* `planning_commit.go` for planning decisions committed at resolving start.
* `turn_runtime.go` for helpers on `domain.TurnRuntime`.
* `*_test.go` beside the package that owns the helper being tested.

---

## Examples

Good current examples:

* `server/internal/game/resolution/runner.go` owns the resolving stage order without importing root `game`.
* `server/internal/game/resolution/planning_commit.go` builds planning commit events from `*domain.GameState`.
* `server/internal/domain/turn_runtime.go` owns clearing post-resolution scratch state.
* `server/internal/game/settlement.go` remains the room-level orchestration bridge for broadcast, debug dump, game-over checks, and room-dependent hooks.
