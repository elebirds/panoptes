# 建筑拆除功能

## Goal

Add a full building demolition flow from protocol to frontend so a player can queue demolition for one of their owned buildings during planning and see it reflected in the planning snapshot and UI.

## What I already know

* The current build flow already exists end to end: protocol `MsgBuildStructure`, server planning validation, economy settlement event, and client command/service UI.
* Planning commands are carried through `ClientFrame -> GameCommand -> PlanningCommand`.
* `BuildSystem` in `server/internal/engine/economy/build.go` settles queued build orders during the economy stage.
* Client building actions are surfaced through `UnitInfoActionRegistry` and `UnitInfoPanelController`.
* UI scripts must not call `NetworkManager` directly; they should use injected services.

## Assumptions (temporary)

* Demolition applies only to owned non-city-core buildings.
* Demolition is queued during planning and resolved during economy settlement, like build orders.
* A demolished building is removed from the map and its node returns to an empty state.

## Open Questions

* None blocking.

## Requirements (evolving)

* Add protocol messages for demolition command/result and queued demolition snapshot data.
* Add server-side planning validation for demolition.
* Add settlement logic that turns queued demolition orders into authoritative events.
* Add client-side command sending and UI entry point for demolition.
* Keep generated proto files in sync via `make gen`.

## Acceptance Criteria (evolving)

* [ ] A player can queue demolition for an owned building.
* [ ] Invalid demolition attempts return a stable error code.
* [ ] The queued demolition appears in planning snapshot data.
* [ ] The demolition resolves into authoritative world/state mutation during settlement.
* [ ] The client can trigger the command from the building action UI.

## Definition of Done (team quality bar)

* Tests added/updated where behavior changes.
* Lint / typecheck / relevant test suites green.
* Generated protocol files updated only through the generator.
* Frontend remains a pure presentation layer.

## Out of Scope (explicit)

* Demolishing city cores.
* Adding a dedicated confirmation dialog or fancy animation pass.
* Allowing client-side legality checks.

## Technical Notes

* Relevant server files: `protocol/panoptes/proto/v1/orders.proto`, `protocol/panoptes/proto/v1/transport.proto`, `protocol/panoptes/proto/v1/turn.proto`, `server/internal/game/planning/*`, `server/internal/engine/economy/*`, `server/internal/event/*`, `server/internal/game/query/planning.go`.
* Relevant client files: `client/Assets/Scripts/Runtime/Core/Application/Services/PlanningIntentService.cs`, `client/Assets/Scripts/Runtime/Presentation/UI/HUD/UnitInfoPanelController.cs`, `client/Assets/Scripts/Runtime/Presentation/UI/HUD/*Action*`.
* Existing build flow is the closest pattern to mirror.
