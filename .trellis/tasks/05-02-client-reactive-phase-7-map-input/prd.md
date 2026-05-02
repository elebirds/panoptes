# client reactive phase 7 map input rearchitecture

## Goal

Implement Phase 7 of the client reactive presentation architecture: move map
planning input state toward final Store + Service + ViewModel ownership, with
`MapPlanningInputController` acting as a Unity scene adapter instead of the
long-term owner of tool and selection state.

## What I Already Know

* Phase 7 is defined in
  `docs/2026-05-02-client-reactive-presentation-architecture-implementation-plan.md`.
* The final target shape is `MapInputBinder -> PlanningToolViewModel /
  SelectionStore / PlanningDraftStore -> command services`.
* Phase 6 already moved map planning commands through `PlanningIntentService`.
* `SelectionStore` and `SelectionService` already exist and are registered in
  `ClientCompositionInstaller.RegisterGame`.
* `MapPlanningInputController` is still a large scene-facing controller. It
  owns Unity raycast, world-object resolution, ghost presenters, overlays, and
  legacy cache/event bridging.

## Assumptions

* This phase should not rewrite raycast, overlay, or ghost rendering all at
  once; those remain map adapter / presenter responsibilities.
* Client-side legality decisions must not be introduced. Existing UI guards may
  remain as presentation affordances, but new state services must not validate
  game rules.
* The first final owner for map tool state should live in Core and expose only
  read-model style state to Presentation.

## Requirements

* Add a Core `PlanningToolStore` and immutable `PlanningToolState`.
* Add a Core `PlanningToolService` that owns local map tool mode transitions:
  build placement, move targeting, attack targeting, charge targeting, hover
  preview targets, and clearing.
* Add a Presentation `PlanningToolViewModel` that projects tool state plus
  `SelectionStore` into UI-facing prompt/action state.
* Register the store, service, and ViewModel in final game composition.
* Wire `MapPlanningInputController` to publish selection through
  `SelectionService` and tool state through `PlanningToolService`.
* Keep raycast and Unity object resolution inside the map scene adapter.
* Add EditMode coverage for planning tool state, ViewModel projection, and
  composition/boundary rules.

## Acceptance Criteria

* [x] `PlanningToolStore` is the final read model for map tool mode state.
* [x] `PlanningToolService` owns mode/preview target mutations.
* [x] `PlanningToolViewModel` does not reference Protocol, legacy caches, or
      Unity UI controls.
* [x] `MapPlanningInputController` publishes selected unit IDs to
      `SelectionStore` through `SelectionService`.
* [x] Map planning commands still go through `PlanningIntentService`.
* [x] Final composition registers the new map input state stack.
* [x] No generated protocol files are modified.
* [x] Unity compile / dotnet build gates pass.

## Definition Of Done

* Tests added or updated for Core state and Presentation projection.
* Static boundary checks remain green.
* Trellis task context validates.
* Work committed as a coherent Phase 7 slice.

## Out Of Scope

* Deleting every legacy cache dependency from `MapPlanningInputController`.
* Rewriting map raycast or world overlay rendering.
* Moving gameplay legality to the client.
* Replacing all scene prefabs or requiring manual prefab edits in this slice.

## Technical Notes

* New Core state uses domain-neutral enums and strings, not Presentation types.
* `MapPlanningInputController` may keep `UnitView` references as scene adapter
  handles, but selected IDs must be mirrored into `SelectionStore`.
* Preview target state belongs in `PlanningToolStore`; actual preview rendering
  continues to read server/draft feedback.
