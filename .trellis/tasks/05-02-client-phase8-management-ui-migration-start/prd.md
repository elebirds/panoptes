# phase8 management ui migration start

## Goal

Enter Phase 8 by starting the management UI migration with the first unfinished
panel after `TurnSummary`: the build command/catalog surface. Establish a UI
Toolkit `Store -> ViewModel -> Binder -> View` slice without breaking the
existing uGUI `BuildCommandPanel` prefab flow.

## What I Already Know

* Phase 7 is complete and committed.
* Phase 8 order is `TurnSummary`, `BuildCommandPanel`, `TechTree`,
  `RecipeSynthesis`, `MinisterReport`, `Policy/NationalFocus`,
  `NationalLedger`.
* `TurnSummary` already has `TurnSummaryViewModel` and
  `TurnSummaryUiToolkitBinder`.
* `BuildCommandPanel` is still a large uGUI facade that reads legacy
  `StaticCatalogCache`, `PlanningDraftCache`, and `GameStateCache` directly.
* Final architecture requires management panels to follow
  `Core store -> ViewModel -> Binder -> UI`, with explicit UI Toolkit binder
  rendering and no generated protocol references in Presentation.

## Assumptions

* This first Phase 8 slice should not delete or rename existing uGUI prefabs.
* The build catalog UI Toolkit slice can start as a catalog/state projection
  with explicit command callback hooks; full prefab replacement can happen in
  later Phase 8 tasks.
* Static catalog Store DTOs are the authoritative source for migrated build
  catalog rows.

## Requirements

* Add a build catalog ViewModel that projects `StaticCatalogStore` and
  `PlanningDraftStore` into UI-ready build groups/items.
* Add a UI Toolkit binder for the build catalog with explicit named element
  lookup/fallback tree rendering.
* Register the ViewModel and binder in the final game composition root.
* Add focused EditMode tests for projection and binder rendering.
* Update the Phase 8 plan status to document the migration start.

## Acceptance Criteria

* [x] `BuildCatalogViewModel` projects catalog buildings into grouped item
      state without using generated Protocol or legacy cache singletons.
* [x] Build rows reflect pending draft build orders/previews.
* [x] `BuildCatalogUiToolkitBinder` renders named UI Toolkit elements and
      exposes button commands through explicit callbacks.
* [x] `ClientCompositionInstaller.RegisterGame` registers the migrated slice.
* [x] Tests cover ViewModel projection and binder rendering.
* [x] dotnet build/test, static boundary checks, and Unity compile pass.

## Definition Of Done

* Tests added/updated.
* Static boundary checks pass.
* Unity script import/compile succeeds.
* Trellis task context validates.
* Work committed.

## Out Of Scope

* Removing the existing uGUI `BuildCommandPanel`.
* Re-authoring production UXML/USS assets by hand in this task.
* Gameplay legality validation or resource affordability checks on client.
