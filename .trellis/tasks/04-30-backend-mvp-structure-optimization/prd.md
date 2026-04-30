# Backend MVP Structure Optimization

## Goal

Analyze the current backend structure and make a conservative, behavior-preserving structural optimization that helps close the MVP backend architecture: reduce remaining `GameRoom` resolving responsibilities and make future development less likely to couple room lifecycle, turn scratch state, and resolution stages.

## What I Already Know

* The project is a Go backend for a turn-based multiplayer city-building game using Turn V2 `planning -> resolving`.
* Generated proto files under `server/internal/gen/proto/` must not be edited manually.
* Engine settlement systems should mutate authoritative game state through events, with planning/runtime orchestration as explicit exceptions.
* Current runtime docs say resolving is already centralized in `server/internal/game/resolution.TurnResolutionRunner`.
* `server/internal/game/settlement.go` still keeps some resolution helper logic on `GameRoom`, including planning commit event construction and turn scratch cleanup.
* Research notes are captured in `research/backend-current-state.md`.

## Requirements

* Preserve current behavior and protocol shape.
* Keep the change backend-only unless tests reveal a necessary cross-layer fix.
* Reduce `GameRoom` knowledge of resolution internals where the logic can operate on `*domain.GameState`.
* Move only low-risk, clearly bounded structure: planning commit event construction and post-resolution scratch cleanup.
* Add or update focused Go tests for extracted behavior.
* Do not touch generated code.
* Do not introduce new dependencies.

## Acceptance Criteria

* [ ] Planning commit events can be built without a `GameRoom`.
* [ ] End-of-resolution scratch cleanup is exposed through a domain-level helper instead of manual field clearing in `game`.
* [ ] Existing Turn V2 resolving behavior remains unchanged.
* [ ] Focused tests cover policy/research/institution planning commit events and scratch reset behavior.
* [ ] `go test ./...` passes under `server/`.

## Definition of Done

* Tests added/updated where appropriate.
* Lint/typecheck/test command run, with result recorded.
* No manual edits to generated files.
* Any useful architecture lesson is considered for `.trellis/spec/` update before finish.

## Technical Approach

Use a minimal extraction approach:

* Add a resolution helper that accepts `*domain.GameState` and returns planning commit events.
* Add a method on the domain runtime state to reset transient planning/resolving data after a turn resolves.
* Update `game.RunTurnResolution` to delegate to these helpers while leaving route/order freezing, broadcast, debug hooks, and game-over checks in `game`.
* Add tests near the packages that own the new helpers.

## Decision (ADR-lite)

**Context**: The backend has already done the large Turn V2 migration; the remaining risk is lingering room-level ownership of details that now belong to resolution/domain state.

**Decision**: Optimize by extracting state-only logic, not by performing another large package reorganization.

**Consequences**: This keeps risk low for MVP, improves boundaries where they are already obvious, and leaves heavier refactors for a later task when a feature actually needs them.

## Out of Scope

* Splitting `server/internal/game/session/runtime.go`.
* Reworking AI planning or minister behavior.
* Changing message protocol or static data generation.
* Moving route preview/order freeze logic, because it still depends on room helpers.

## Technical Notes

* `docs/SERVER_RUNTIME_ARCHITECTURE.md` is the best current-state reference for the runtime.
* `docs/TURN_V2_REFACTOR_PLAN.md` documents the historical reason for removing old domestic/combat phase assumptions.
* `server/internal/game/resolution/runner_architecture_test.go` already guards that resolution does not import root `game`.
* Relevant research: `research/backend-current-state.md`.
