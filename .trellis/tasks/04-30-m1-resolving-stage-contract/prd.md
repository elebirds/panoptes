# M1.1 resolving stage contract

## Goal

Document and verify the long-term contract of the backend resolving stages so later logistics, state, war, and information systems can attach without changing the basic turn semantics.

## Requirements

* Define the responsibility of each `TurnResolutionRunner` stage.
* Preserve the current planning lock-in, order freeze, unit resolution, map action, building, and economy order.
* Document fatal-turn short-circuit behavior.
* Add or update focused tests if current coverage is insufficient.

## Acceptance Criteria

* [x] Stage order and responsibility are documented.
* [x] Fatal turn behavior is explicitly tested or referenced by existing tests.
* [x] No M2+ gameplay systems are implemented in this task.

## Out of Scope

* Road logistics.
* New victory conditions.
* Minister or information-asymmetry logic.

## Technical Notes

* Parent roadmap: `docs/2026-04-30-backend-m1-rule-foundation-plan.md`.
* Current runtime reference: `docs/SERVER_RUNTIME_ARCHITECTURE.md`.
* Implementation added a fixed stage-order test and generic `StageOutcome.Stop` short-circuit test in `server/internal/game/resolution`.
* Verification completed by subagents:
  * `go test ./internal/game/resolution`
  * focused fatal/non-fatal game tests
  * `go test ./...` from `server/`
  * `go vet ./...`
  * `go run github.com/bufbuild/buf/cmd/buf@latest lint`
  * `git diff --check`
* `make lint` was attempted by check and failed only because local `buf` binary is not installed; the equivalent buf lint command above passed.
