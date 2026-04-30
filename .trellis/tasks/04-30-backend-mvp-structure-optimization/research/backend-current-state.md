# Backend Current State Analysis

## Summary

The backend has already moved beyond the older two-phase design and now runs on the Turn V2 `planning -> resolving` model. The strongest MVP closing opportunity is not another large rewrite; it is to reduce the remaining `game.GameRoom` knowledge of resolving internals and make the already-created runtime packages easier to extend safely.

## Current Shape

* Runtime documentation in `docs/SERVER_RUNTIME_ARCHITECTURE.md` says the current loop is `Coordinator.Start -> PlanningStartRunner -> planning.Service -> TurnResolutionRunner -> MsgGameSync -> next planning`.
* `server/internal/game/resolution` already owns the top-level resolving runner and stage order.
* `server/internal/game/session` owns initialization, bootstrap, planning-start state, chat state, observations, minister draft preparation, and transport sending.
* `server/internal/game/planning` owns command validation and draft writes.
* `server/internal/game/query` and `server/internal/game/projection` own client-facing views and sync/report projection.
* `server/internal/building/orchestration` owns building lifecycle rules, and `engine/economy` owns economy stages.

## Observed Structure Risks

* `server/internal/game/settlement.go` still keeps resolving-specific helpers on `GameRoom`: planning commit event construction, order freeze, map action events, broadcast, game-over check, and runtime cleanup.
* `RunTurnResolution` clears many `TurnRuntime` fields manually in `game`, which means the orchestration layer knows the internal shape of planning/resolving scratch state.
* `planningCommitEvents` only needs `*domain.GameState`; keeping it as a `GameRoom` method makes the room boundary look more central than it needs to be.
* `server/internal/game/session/runtime.go` is very large and multi-responsibility, but much of that size is already mitigated by side files such as `planning_start.go`, `minister_draft.go`, `minister_prompt.go`, and `controller.go`.
* Some large files remain intentional rule surfaces for now (`game/ai/rulebot.go`, `game/planning/service.go`, `game/scenario/scenario.go`, `engine/maploader/procedural.go`). Splitting them is lower value unless a concrete feature touches them.

## Dependency Notes

* `transport/websocket` does not import `game`, matching the project rule; websocket routes through transport abstractions and dispatch.
* `game/resolution` does not import root `game`; it imports `engine`, `building/orchestration`, `engine/economy`, `domain`, and `event`.
* `debug` and `transport/http` import `game`, which is expected for dev/debug surfaces rather than core transport websocket coupling.
* `engine` packages still follow the state-change pattern through events for settlement behavior.

## Recommended MVP Optimization Scope

1. Move planning commit event construction out of `GameRoom` into `game/resolution`.
2. Add a domain-level helper to clear turn scratch state after resolving, then call it from `RunTurnResolution`.
3. Keep route preview/order freezing and broadcast/game-over behavior in `game` for this pass, because they still need room/session behavior.
4. Add focused tests for the extracted commit-event helper and scratch-state reset.
5. Run backend tests to confirm this is behavior-preserving.

## Out of Scope For This Pass

* Splitting `game/session.Runtime` into a new object graph.
* Moving command validation out of `game/planning`.
* Changing proto messages or generated files.
* Introducing new third-party dependencies.
* Reworking AI rulebot behavior.
