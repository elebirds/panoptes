# M2 networked improvements and projections

## Goal

Complete the remaining M2 backend scope after road connectivity landed: unit-driven improvements, city/resource/building network explanation, and projection fields that let clients/headless tests see road and network status without implementing M3 logistics.

## What I Already Know

* M2 first slice was committed as `9cac996 feat: implement m2 road connectivity`.
* Road build/repair is now validated, event-applied, and queryable through `domain.RoadConnected` / `PlayerRoadNetworkStatus`.
* Full M2 roadmap still requires resource facilities, new cities, and buildings to explain whether they are connected to the national network.
* `build_improvement` / `repair_improvement` were intentionally left reserved in the first M2 slice.
* Existing `MsgIssueUnitOrder.params` can carry improvement details without adding a new command message.
* Existing `NodeView` has `has_road`, `city_id`, and `service_city_id`, but lacks explicit road/network status fields.
* Generated files must only change through proto source + `make gen`.

## Decisions

* Implement `build_improvement` / `repair_improvement` in this slice.
* `build_improvement` uses `params["building_type_id"]` when supplied; otherwise resource nodes infer `farm`, `mine`, or `lumber` from resource type.
* `repair_improvement` repairs an existing building on the target node by restoring HP and clearing ruined/disabled repair state through an authoritative event.
* M2 network state remains explanatory only: connectivity and projection status, not capacity, storage, or allocation.

## Requirements

* `build_improvement` must be accepted for a valid civilian/engineering unit, valid target node, valid improvement building type, and valid city context.
* `repair_improvement` must be accepted for a valid owned/controlled building that can be repaired.
* Improvement commands must write authoritative state only through events during map action resolution.
* Invalid improvement commands must return stable existing error codes without writing planning/resolving/node state.
* `build_road`, `repair_road`, `build_improvement`, and `repair_improvement` must all remain map actions handled by `game/orders`.
* Node projection must expose explicit road/network status and connected city explanation.
* City core, in-city building, out-of-city resource facility, and raw resource node network status must be deterministic and backend-owned.
* No M3 local inventory, logistics capacity, min-cost flow, policy priority, information asymmetry, or minister execution is introduced.

## Acceptance Criteria

* [x] Valid `build_improvement` produces `BuildingBuiltEvent` via map action resolution.
* [x] Valid `repair_improvement` produces a repair event and restores building HP/state via event application.
* [x] Invalid improvement commands fail without planning/resolving/node state writes.
* [x] `NodeView` exposes road status, network status, and connected city ID for relevant nodes.
* [x] Tests cover connected/disconnected resource facilities, city cores, and buildings.
* [x] Proto changes, if any, are generated with `make gen`.
* [x] `cd server && go test ./...` passes.
* [x] `mise exec -- make lint` passes.

## Out of Scope

* Local city storage or network inventory.
* Logistics capacity allocation, persistent shipments, or min-cost flow.
* New client UI.
* Minister execution or information distortion.
* Strategic collapse or new victory conditions.

## Technical Notes

* Road/network base: `server/internal/domain/road_network.go`.
* Improvement command path: `server/internal/game/orders/validation.go`, `planning_state.go`, `map_actions.go`.
* Building rules: `server/internal/building/rules.go`, `server/internal/event/production_building.go`.
* Projection path: `protocol/panoptes/proto/v1/game_state.proto`, `server/internal/game/query/views.go`, `server/internal/game/projection/sync.go`.

## Verification

* Added `NodeView.road_status`, `network_status`, `network_city_id`, and `is_network_connected`; regenerated server/client protocol output with `make gen`.
* Added backend network explanation via `domain.NodeNetworkStatusForPlayer`.
* Connected `build_improvement` and `repair_improvement` through planning/order validation and `BuildMapActionEvents`.
* Added `event.BuildingRepairedEvent` and projection/audit/kind coverage.
* Updated backend code-spec for M2 map actions and network projection fields.
* Focused tests passed: `go test -count=1 ./internal/domain ./internal/event ./internal/game/orders ./internal/game/query ./internal/game/projection ./internal/game/planning`.
* Full tests passed: `cd server && go test -count=1 ./...`.
* Lint passed: `PATH="/opt/homebrew/opt/protobuf/bin:$HOME/go/bin:$PATH" mise exec -- make lint`.
* M2 regression record archived at `docs/2026-05-01-backend-m2-regression-gate.md`.
