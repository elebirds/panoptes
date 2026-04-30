# M1.4 headless acceptance harness

## Goal

Standardize backend-only validation so future milestones can prove rules without waiting for Unity client integration.

## Requirements

* Inventory existing debug harness and scenario coverage.
* Define naming and acceptance conventions for M2-M6 scenarios.
* Identify gaps in deterministic scenario setup, state summary, and command result recording.
* Add lightweight docs/tests where useful.

## Acceptance Criteria

* [x] M1 headless scenario inventory exists.
* [x] Future scenario naming and acceptance conventions are documented.
* [x] Current core flow can be validated backend-only.

## Out of Scope

* New gameplay mechanics.
* Client automation.

## Technical Notes

* Parent roadmap: `docs/2026-04-30-backend-m1-rule-foundation-plan.md`.
* Current debug package: `server/internal/debug`.
