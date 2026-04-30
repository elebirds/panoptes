# M1.2 event audit contract

## Goal

Define and verify how backend domain events audit real state changes, so future logistics, ministers, information layers, and replay/debug tooling can trust the event stream.

## Requirements

* Classify events that write authoritative state versus events that only report or project.
* Document naming, `Kind()`, data payload, and typed projection expectations.
* Verify key state-changing event families have stable kind coverage.
* Preserve the existing `Event.Apply` authority model.

## Acceptance Criteria

* [x] Event audit expectations are documented.
* [x] Key event kind coverage is verified.
* [x] The task does not introduce new gameplay semantics.

## Out of Scope

* New event families for logistics or strategic collapse.
* Client playback UI.

## Technical Notes

* Parent roadmap: `docs/2026-04-30-backend-m1-rule-foundation-plan.md`.
* Current event package: `server/internal/event`.
* Implementation archived `docs/2026-04-30-backend-event-audit-contract.md`.
* `recipe_selected` is now explicitly internal-state-only and filtered from `DomainEventEnvelope`; clients observe its result through post-resolution node operation snapshots.
* Verification completed by subagents:
  * `go test ./internal/event ./internal/game/projection`
  * `go test ./internal/event ./internal/game/... ./internal/engine/... ./internal/building/...`
  * focused event/projection/resolution/game tests
  * `go test ./...` from `server/`
  * `go vet ./...`
  * `go run github.com/bufbuild/buf/cmd/buf@latest lint`
  * `git diff --check`
