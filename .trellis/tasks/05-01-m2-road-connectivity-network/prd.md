# M2 road connectivity and city network

## Goal

Make roads and the first version of the national city network real backend rule objects. M2 should turn the M1 reserved road commands into authoritative state changes, expose enough projection/status for headless validation, and create a clean foundation for later M3 local storage and logistics allocation.

## What I Already Know

* M1 is complete and committed as `3a08b98 feat: establish m1 backend rule foundation`.
* M1 stabilized reserved command behavior for `build_road`, `repair_road`, `build_improvement`, and `repair_improvement`.
* Final roadmap defines M2 as roads, connectivity, and city network.
* Existing backend already has `HasRoad` on nodes, static map road loading, road movement effects, `RoadBuiltEvent`, and `RoadDestroyedEvent`.
* Existing protocol already exposes `NodeView.has_road`; generated protocol files must not be edited manually.
* Current final roadmap expects road build/repair, city/facility/network connected checks, event flow, and projection status.
* M2 must not implement M3 logistics capacity allocation, local storage, priority profiles, information asymmetry, or minister default execution.

## Assumptions (Temporary)

* M2 should be backend-first and headless-testable before any client UI work.
* The first implementation slice should keep road state node-based unless code inspection proves an edge-level representation is already required.
* Repair can initially mean restoring `HasRoad` on the target road path/node after it was destroyed; richer damage levels can remain future work unless already supported cleanly.
* City network connectivity should start as deterministic graph/component analysis over owned city/network anchors and roaded/passable nodes, not full logistics flow.
* `build_improvement` / `repair_improvement` may remain out of the first M2 slice if road connectivity needs to land first.

## Decisions

* 2026-05-01: First M2 implementation slice includes only `build_road` / `repair_road` plus backend network connectivity. `build_improvement` / `repair_improvement` remain reserved for a later M2 slice or follow-up task.

## Requirements (Evolving)

* Convert `build_road` and `repair_road` from stable rejection into validated planning/resolving actions.
* Road commands must write authoritative state only through events.
* Road command failures must use the existing allowed error codes.
* Road state must be visible through existing or explicitly generated protocol/projection paths.
* Network connectivity must be deterministic and backend-owned.
* Headless tests must prove that road construction changes connectivity and that destroyed roads can break or degrade connectivity.
* M2 work must keep future M3 logistics capacity allocation separate from simple connectivity.
* M2 must not introduce client-side game authority.
* `build_improvement` and `repair_improvement` must keep their current explicit reserved-command behavior in this slice.

## Acceptance Criteria (Evolving)

* [x] `build_road` is accepted for a valid backend-owned case and produces a road state change through event application.
* [x] `repair_road` is accepted for a valid backend-owned case and restores road connectivity through event application.
* [x] Invalid road commands fail deterministically without writing planning or resolving state.
* [x] Road construction/destruction/repair affects a backend connectivity query or projection summary.
* [x] A headless scenario or focused backend test demonstrates connected versus disconnected city/network state.
* [x] `cd server && go test ./...` passes.
* [x] `mise exec -- make lint` passes.

## Definition of Done

* PRD and implementation context are complete.
* Tests cover command validation, event application, and connectivity behavior.
* Docs/specs are updated if M2 establishes new state ownership or projection conventions.
* No generated files are manually edited.
* M3 logistics remains explicitly out of scope.

## Out of Scope (Explicit)

* Local city storage and national network inventory.
* Capacity-constrained logistics allocation or min-cost flow.
* Policy priority profiles.
* Minister execution, minister reports, information distortion, or fog of war.
* Client UI or client-side legality checks.
* New victory conditions or strategic collapse.

## Technical Notes

* Roadmap: `docs/2026-04-30-backend-final-roadmap.md`.
* M1 regression gate: `docs/2026-04-30-backend-m1-regression-gate.md`.
* Current road state: `server/internal/domain/components.go`, `server/internal/domain/node.go`.
* Current road events: `server/internal/event/production_road.go`, `server/internal/event/combat.go`.
* Current reserved command boundary: `server/internal/game/orders/planning_state.go`, `server/internal/game/planning/service_rules_test.go`.
* Current map-action stage and order types should be inspected before implementation.

## Verification

* Implemented backend road connectivity queries in `server/internal/domain/road_network.go`.
* Enabled validated `build_road` and `repair_road` planning/map actions while keeping `build_improvement` and `repair_improvement` reserved.
* Added `RoadRepairedEvent` and projection/audit coverage for `road_repaired`.
* Updated backend code-spec with the road command/connectivity contract in `.trellis/spec/backend/directory-structure.md`.
* Implement check fixed missing invalid-endpoint/no-write test coverage.
* Focused tests passed: `cd server && go test ./internal/domain ./internal/event ./internal/game/orders ./internal/game/planning ./internal/game/projection`.
* Full tests passed: `cd server && go test ./...`.
* Lint passed: `mise exec -- make lint`.
