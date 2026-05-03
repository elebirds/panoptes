# phase7 complete map input final split

## Goal

Finish Phase 7 by reducing `MapPlanningInputController` to the current map
Binder/Adapter boundary: serialized Unity references, scene raycast input, and
delegation to focused presentation/application helpers. Planning mode,
preview, and command responsibilities should no longer live as scattered
MonoBehaviour fields.

## What I Already Know

* Phase 7 already introduced final Core ownership for planning tool and
  selection state through stores/services/ViewModels.
* The controller already delegates routing, build click order, node info proxy
  creation, catalog resolution, move/combat feedback, pending deploy ghosts,
  and territory highlights.
* The controller still owns build mode fields, pending build bookkeeping,
  damage popup HP tracking, and cache-event orchestration inline.
* Prefab-facing serialized fields and public methods must remain stable unless
  scene/prefab assets are updated and verified.

## Assumptions

* "Phase 7 complete" means no remaining planning-mode bookkeeping scattered in
  `MapPlanningInputController`, while keeping the current scene component as
  the adapter entry point until prefab assets can be renamed safely.
* This task does not change backend/protocol behavior.
* Extracted helpers stay under `Presentation/Map` and use Core DTO/cache APIs,
  not generated protocol messages.

## Requirements

* Move build placement state, preview request bookkeeping, ghost rendering, and
  pending build rollback/resolve behavior into a focused helper.
* Move unit damage fallback HP/popup bookkeeping out of the controller.
* Keep map input click routing, command submission, and cache update behavior
  equivalent.
* Keep generated protocol files untouched and Presentation protocol-free.
* Add or update focused EditMode coverage for extracted helpers.
* Mark Phase 7 complete in the persisted architecture plan only after checks
  pass.

## Acceptance Criteria

* [x] `MapPlanningInputController` has no raw build mode/pending build preview
      state fields.
* [x] Unit damage popup tracking is delegated to a helper.
* [x] Public scene-facing APIs continue to compile.
* [x] Tests cover extracted helper behavior where practical.
* [x] Static Presentation boundary checks pass.
* [x] dotnet build/test and Unity compile pass.
* [x] Phase 7 plan status is updated as complete.

## Definition Of Done

* Tests added/updated.
* Static boundary checks pass.
* Unity script import/compile succeeds.
* Trellis task context validates.
* Work committed.

## Out Of Scope

* Renaming or deleting the serialized scene component.
* Protocol/backend changes.
* Full Phase 8 management UI migration.
