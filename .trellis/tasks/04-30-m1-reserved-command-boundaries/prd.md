# M1.3 reserved command boundaries

## Goal

Ensure protocol-visible but not-yet-implemented commands remain explicit, stable, and non-mutating until their planned milestones.

## Requirements

* Verify reserved road/improvement commands are rejected or feature-gated.
* Verify minister directive and war-zone commands do not affect default resolving.
* Ensure rejected commands do not write planning snapshot or resolving state.
* Record which milestone owns each reserved command.

## Acceptance Criteria

* [x] Reserved commands return stable errors.
* [x] Reserved commands do not mutate planning/resolving state.
* [x] Future owner milestones are documented.

## Out of Scope

* Implementing roads, improvements, ministers, or war zones.
* Proto removal or breaking command shape.

## Technical Notes

* Parent roadmap: `docs/2026-04-30-backend-m1-rule-foundation-plan.md`.
* Current planning command entry: `server/internal/game/planning/service.go`.
* Protocol-visible `set_minister_directive` is reserved until M7 and now returns `invalid_directive` without mutating draft state or minister memory.
* Reserved road/improvement map actions are guarded in `game/orders.ApplyPlanningUnitOrder`, while `settle_city` remains allowed.
* Verification completed by subagents:
  * `go test ./internal/game/planning ./internal/game/orders ./internal/game/resolution`
  * `go test ./internal/game/...`
  * `go test ./...` from `server/`
  * `go vet ./...`
  * `git diff --check`
