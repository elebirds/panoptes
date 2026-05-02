# client phase7 split map planning input controller

## Goal

Continue dismantling `MapPlanningInputController` toward the final
`MapInputBinder + Coordinator/Presenter` shape by extracting the map input
state bridge that connects Unity map input to final Core stores/services.

## What I Already Know

* `MapPlanningInputController` is still around 3000 lines and remains a
  scene-facing facade.
* Phase 7 moved tool state into `PlanningToolStore/Service` and selection into
  `SelectionStore/Service`.
* The last follow-up removed mutation wrappers from `PlanningToolViewModel`,
  leaving it as a read-only projection.
* The controller still directly holds service references, selected/tool state
  bridge methods, and conversion logic for build/combat tool state.

## Assumptions

* Do not rename or remove the MonoBehaviour yet because the scene/prefab script
  reference is tied to its `.meta` GUID.
* Extracting a plain C# adapter beside the map facade is the safest next slice.
* The adapter may be transitional, but it must not become a second rules engine
  or a hidden ViewModel command wrapper.

## Requirements

* Extract tool/selection publication logic out of `MapPlanningInputController`.
* Keep `PlanningToolViewModel` read-only.
* Keep command submission through existing Core services.
* Keep Unity raycast/world feedback in the scene-facing controller for this
  slice.
* Add focused EditMode coverage for the extracted helper.
* Preserve generated protocol files.

## Acceptance Criteria

* [x] `MapPlanningInputController` no longer directly stores
      `PlanningToolService`, `SelectionService`, or `PlanningToolViewModel`
      fields.
* [x] Tool mode, preview target, and selected unit publication are owned by an
      extracted helper.
* [x] The helper contains no Protocol references, no legacy cache references,
      and no Unity UI controls.
* [x] Boundary tests assert the controller delegates final store/service
      publication through the helper.
* [x] Client build / Unity compile gates pass.

## Definition Of Done

* Tests added/updated.
* Static boundary checks pass.
* Unity script import/compile succeeds.
* Trellis task context validates.
* Work committed as a Phase 7 follow-up.

## Out Of Scope

* Renaming the scene component to `MapInputBinder`.
* Splitting raycast, build placement, move, and combat presenters fully.
* Editing protocol or generated code.
