# Fix build ghost tile color

## Goal

Fix the client-side build placement presentation so that both direct build commands and build commands created by accepting minister suggestions show the building as a green translucent ghost while the underlying map tile keeps its normal terrain/material color.

## Requirements

* Direct build placement must render only the building preview/queued building as a green ghost.
* Build orders that appear after accepting minister suggestions must use the same visual rule.
* The map node tile itself must not be recolored to white when a pending/queued build ghost is applied or restored.
* Preserve the client boundary: this is presentation-only rendering and must not add gameplay legality checks.
* Do not modify generated protocol files.

## Acceptance Criteria

* [ ] A direct build command creates/restores a green translucent building ghost on the selected node.
* [ ] Accepting a minister suggestion that creates a build order creates/restores the same green translucent building ghost.
* [ ] The affected node tile keeps its normal terrain color instead of becoming white.
* [ ] Renderer refresh/rebind paths still restore pending build ghosts without whitening tiles.
* [ ] Relevant static checks or targeted compile checks pass, or any environment limitation is documented.

## Definition of Done

* Client presentation code follows existing Map/Planning patterns.
* No generated files are edited.
* Quality check agent reviews the diff against frontend specs.
* Verification results are recorded in the final response.

## Technical Approach

Inspect the build visual path from `MapPlanningInputController` and `MapBuildPlacementSession` into `MapRenderer.ApplyBuildingPlacement`, `NodeView.SetBuildingGhost`, and `BuildingView.SetPlacementGhost`. Fix the layer that applies ghost coloring so the building instance is tinted while the tile/node renderer is not reset to white. Confirm queued build order sync after minister acceptance goes through the same pending build restoration path.

## Out of Scope

* New build legality rules or resource validation.
* Redesigning build UI, minister UI, or map tile art.
* Protocol/schema changes.

## Technical Notes

* Likely presentation files:
  * `client/Assets/Scripts/Runtime/Presentation/Map/MapPlanningInputController.cs`
  * `client/Assets/Scripts/Runtime/Presentation/Map/MapBuildPlacementSession.cs`
  * `client/Assets/Scripts/Runtime/Presentation/Map/MapRenderer.cs`
  * `client/Assets/Scripts/Runtime/Presentation/Map/NodeView.cs`
  * `client/Assets/Scripts/Runtime/Presentation/Map/BuildingView.cs`
* Relevant state path:
  * `client/Assets/Scripts/Runtime/Core/Application/Stores/PlanningDraftState.cs`
  * `client/Assets/Scripts/Runtime/Core/Application/Stores/StoreMessageHydrator.cs`
* Frontend spec notes: pending map ghosts are presentation-owned transient visuals and must be replayed after renderer refresh without mutating authoritative store snapshots.
