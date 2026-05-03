# client phase7 final map input binder split

## Goal

Keep dismantling `MapPlanningInputController` toward the final
`MapInputBinder + PlanningInputCoordinator + presenters` architecture, without
stopping at cosmetic partial-class splits.

## What I Already Know

* `MapPlanningInputController` is still a large Unity scene facade.
* `MapPlanningInputStateAdapter` now owns tool/selection publication to final
  services/stores.
* The next highest-value responsibilities still inside the controller are click
  routing, mode dispatch, build placement orchestration, and node info proxy
  construction.

## Assumptions

* The scene-facing MonoBehaviour name should remain for now to avoid prefab
  GUID churn.
* A coordinator can depend on a narrow context interface implemented by the
  scene facade.
* World object resolution and Unity raycast primitives stay in the facade for
  this slice.

## Requirements

* Extract map input routing into a non-MonoBehaviour coordinator.
* Extract build placement routing into a non-MonoBehaviour coordinator.
* Extract node info proxy construction and building catalog lookup helpers out
  of the scene controller.
* Keep `MapPlanningInputController` as the Unity lifecycle/input polling
  facade.
* Do not reintroduce ViewModel command wrappers.
* Do not add client-side gameplay legality rules.
* Add focused EditMode tests for coordinator routing order.
* Preserve generated protocol files.

## Acceptance Criteria

* [x] `Update()` delegates build/combat click routing to
      `MapPlanningInputCoordinator`.
* [x] Coordinator has no Protocol references, no legacy cache references, and
      no Unity UI controls.
* [x] Controller implements a coordinator context instead of owning routing
      order inline.
* [x] Tests cover attack-priority click, move click, normal selection click,
      and cancel behavior.
* [x] Build placement routing is covered by focused coordinator tests.
* [x] Node info inspection projection has focused tests.
* [x] Client build / Unity compile gates pass.

## Definition Of Done

* Tests added/updated.
* Static boundary checks pass.
* Unity script import/compile succeeds.
* Trellis task context validates.
* Work committed.

## Out Of Scope

* Full deletion/rename of `MapPlanningInputController`.
* Moving all move/combat presenter details in this single commit.
* Protocol changes.
