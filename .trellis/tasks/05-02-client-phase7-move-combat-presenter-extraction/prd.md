# client phase7 move combat presenter extraction

## Goal

Continue reducing `MapPlanningInputController` toward a final map input
Binder/Adapter by moving move-preview rendering and combat targeting helpers
out of the scene controller.

## What I Already Know

* `MapPlanningInputCoordinator` now owns top-level click routing.
* `MapBuildPlacementCoordinator` owns build hover/commit routing.
* `MapNodeInfoProxyFactory` owns node info proxy construction.
* `MapBuildingCatalogResolver` owns building catalog alias resolution.
* `MapPlanningInputController` still owns move preview overlay orchestration,
  attack range highlight resolution, and several combat targeting helpers.

## Assumptions

* The prefab-facing `MapPlanningInputController` class name remains stable for
  now.
* This slice should avoid protocol changes and generated files.
* Extracted helpers should stay beside the controller under
  `Presentation/Map`.

## Requirements

* Extract at least one meaningful move/combat presentation responsibility from
  `MapPlanningInputController`.
* Keep extracted helpers protocol-free and command-free where feasible.
* Add focused EditMode coverage for extracted logic.
* Preserve Unity serialized field compatibility.

## Acceptance Criteria

* [x] Move preview overlay bookkeeping or combat targeting projection lives in
      a dedicated helper.
* [x] `MapPlanningInputController` loses more inline move/combat helper logic.
* [x] Tests cover the extracted helper.
* [x] Static Presentation boundary checks pass.
* [x] dotnet build/test and Unity compile pass.

## Definition Of Done

* Tests added/updated.
* Static boundary checks pass.
* Unity script import/compile succeeds.
* Trellis task context validates.
* Work committed.

## Out Of Scope

* Full deletion/rename of `MapPlanningInputController`.
* Protocol or backend changes.
* New client-side gameplay legality rules.
