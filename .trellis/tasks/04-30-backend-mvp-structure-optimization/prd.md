# Comprehensive Backend Structure Optimization

## Goal

Analyze the current backend structure and perform a comprehensive, behavior-preserving structural optimization for MVP closure. The goal is to make the backend easier to extend after MVP by reducing oversized orchestration objects, clarifying package ownership, and moving state-only or subsystem-specific logic out of root `game` and other broad packages.

This task has one completed baseline commit already:

* Extracted planning commit event construction into `game/resolution`.
* Added `domain.TurnRuntime.ClearPostResolutionScratch()`.
* Recorded the `GameRoom` thin-boundary convention in backend spec.

## What I Already Know

* The project is a Go backend for a turn-based multiplayer city-building game using Turn V2 `planning -> resolving`.
* Generated proto files under `server/internal/gen/proto/` must not be edited manually.
* Engine settlement systems should mutate authoritative game state through events, with planning/runtime orchestration as explicit exceptions.
* Current runtime docs say resolving is already centralized in `server/internal/game/resolution.TurnResolutionRunner`.
* `server/internal/game/settlement.go` has already been slimmed for planning commit events and turn scratch cleanup.
* `server/internal/game/session/runtime.go`, `server/internal/game/planning/service.go`, `server/internal/game/ai/rulebot.go`, `server/internal/game/scenario/scenario.go`, `server/internal/datagen/*`, and several engine files were identified as large structure surfaces and have now been split by ownership.
* Research notes begin with `research/backend-current-state.md`; deeper structure research is being added under `research/`.

## Requirements

* Preserve current behavior and protocol shape.
* Keep the change backend-only unless tests reveal a necessary cross-layer fix.
* Optimize structure comprehensively across backend package boundaries, not just one helper extraction.
* Prioritize root `game` and `game/session` responsibilities, command/planning/projection boundaries, and domain/event/engine ownership.
* Split or relocate code only where ownership becomes clearer and tests can prove behavior preservation.
* Preserve generated files and proto compatibility.
* Add or update focused Go tests for extracted behavior.
* Do not touch generated code.
* Do not introduce new dependencies.

## Acceptance Criteria

* [x] Planning commit events can be built without a `GameRoom`.
* [x] End-of-resolution scratch cleanup is exposed through a domain-level helper instead of manual field clearing in `game`.
* [x] A comprehensive backend structure assessment is persisted under `research/`.
* [x] A phased refactor plan is captured in this PRD with explicit in-scope and out-of-scope boundaries.
* [x] At least one additional meaningful structure batch beyond the baseline commit is implemented and tested.
* [x] Existing Turn V2 resolving behavior remains unchanged.
* [x] Focused tests cover policy/research/institution planning commit events and scratch reset behavior.
* [x] `go test ./...` passes under `server/`.
* [x] `go vet ./...` passes under `server/`.
* [x] `buf lint` passes under `protocol/`.

## Definition of Done

* Tests added/updated where appropriate.
* Lint/typecheck/test command run, with result recorded.
* No manual edits to generated files.
* Any useful architecture lesson is considered for `.trellis/spec/` update before finish.

## Technical Approach

Use a staged comprehensive approach:

1. Assess structure by package slice and persist findings under `research/`.
2. Identify refactor batches that can be completed independently with tests.
3. Implement one batch at a time through Trellis implement/check, keeping commits behavior-preserving.
4. Prefer ownership moves and file splits over API redesign unless a testable boundary demands it.
5. Record durable package ownership conventions into `.trellis/spec/backend/`.

## Phased Refactor Plan

### Phase A: Command Adaptation and Dispatch Boundaries

**Goal**: reduce repeated oneof mapping and keep Turn coordinator focused on phase flow.

* [x] Move `CommandEnvelope -> PlanningCommand` adaptation out of `game/turn` and into `game/planning`.
* [x] Keep current request-id / participant-id mapping and public error behavior.
* [x] Add focused tests for the planning-owned batch adapter.

### Phase B: Map Action and Order Boundaries

**Goal**: move state-only map action and unit-order behavior away from root `game`.

* [x] Extract `settle_city` map action event construction from `GameRoom` into an order/map-action owned helper where it can operate on `*domain.GameState`.
* [x] Move unit-order validation/freeze/active-march synchronization into `game/orders` with route preview injected as a callback.
* [x] Keep root `GameRoom` as compatibility adapter until planning ports are narrowed.

### Phase C: Runtime and Coordinator File-Level Splits

**Goal**: reduce `session.Runtime` and `turn.Coordinator` file pressure without changing public APIs.

* [x] Split bootstrap/catalog sync helpers out of `session/runtime.go`.
* [x] Split static catalog payload helpers out of `session/runtime.go`.
* [x] Split coordinator command handler code out of `turn/coordinator.go`.
* [x] Split session initialization, participants, player bootstrap, city-state setup, and starting resources out of `session/runtime.go`.

### Phase D: Planning Service Decomposition

**Goal**: isolate read-only preview commands and delivery from mutating planning intents.

* [x] Move preview handling into a dedicated planning helper.
* [x] Introduce command result/delivery helpers while preserving current outbound message order and metadata.
* [x] Narrow `planning.Session` into internal ports after state-only order helpers moved.
* [x] Split concrete planning handlers by concern: policy, research, institution, build/recipe, unit order, minister, reveal/submit, preview unit.

### Phase E: Domain/Event/Engine Structural Cleanup

**Goal**: make core rule ownership explicit.

* [x] Split `domain/state.go` by state core, modifiers, unlocks, and building HP helpers.
* [x] Split large staticdata/datagen files by concern without changing package names or generated outputs.
* [x] Extract private mutation helpers inside event files before considering package moves.
* [x] Avoid nested `OtherEvent.Apply` calls in new code; producers should emit multiple reportable events explicitly.
* [x] Split AI rulebot, scenario fixtures, procedural map generation, and economy recipe settlement by concern.
* [x] Clarify building/ECS ownership by moving building attachment helpers to `building` and making placement checks explicit.
* [x] Reduce transport dispatch/logging duplication while preserving websocket problem behavior and metadata.

## Final Architecture State

The backend is now structurally ready for MVP follow-up work:

* Root `game` is primarily a room/session facade and resolving bridge, not a rule bucket.
* Turn flow, turn command dispatch, planning command adaptation, order state, resolving events, and client projection have distinct package owners.
* `game/orders` owns unit-order validation, planning-state application, active-march synchronization, resolving order freeze, post-settlement march refresh, and map-action event construction.
* `game/planning` owns command adaptation, ports, delivery ordering, preview handling, and per-command handlers.
* `game/session` owns runtime state/lifecycle but initialization, bootstrap sync, catalog payloads, participants, player bootstrap, city states, and resources are split into focused files.
* `domain`, `event`, `staticdata`, `datagen`, `engine/economy`, `engine/maploader`, `game/ai`, and `game/scenario` have been split by responsibility without changing exported behavior.
* Transport inbound dispatch now reuses the generated dispatcher path and websocket inbound logging uses proto oneof reflection.

### Completed Implementation Batches

Completed: baseline resolving extraction, Phase A, Phase B, Phase C, Phase D, Phase E, transport dispatch/logging cleanup, scenario split, procedural maploader split, AI rulebot split, and economy recipe split.

## Decision (ADR-lite)

**Context**: The backend has already done the large Turn V2 migration; the remaining risk is that several broad packages still concentrate too many responsibilities and hide future extension points.

**Decision**: Do comprehensive optimization in staged, testable batches rather than one risky repo-wide rewrite. The task should still be broad in ambition: root `game`, runtime/session, planning/projection, and domain/engine/event boundaries are all valid targets.

**Consequences**: This gives MVP a cleaner foundation while keeping each step reviewable. Some large files may remain large if their current ownership is correct; the point is package clarity, not line-count cosmetics.

## Out of Scope

* Changing game rules or AI behavior.
* Changing message protocol or static data generation.
* Introducing new frameworks or third-party dependencies.
* Large semantic rewrites without regression tests.

## Technical Notes

* `docs/SERVER_RUNTIME_ARCHITECTURE.md` is the best current-state reference for the runtime.
* `docs/TURN_V2_REFACTOR_PLAN.md` documents the historical reason for removing old domestic/combat phase assumptions.
* `server/internal/game/resolution/runner_architecture_test.go` already guards that resolution does not import root `game`.
* Relevant research: `research/backend-current-state.md`.
* Runtime orchestration research: `research/runtime-orchestration.md`.
* Command/projection boundary research: `research/command-projection-boundaries.md`.
* Domain/engine boundary research: `research/domain-engine-boundaries.md`.
