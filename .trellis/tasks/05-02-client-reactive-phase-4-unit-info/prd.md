# client reactive phase 4 unit info ugui migration

## Goal

Implement Phase 4 of the client reactive presentation architecture: the first
complete uGUI migration using `UnitInfoPanel` as the target slice.

## What I Already Know

* Phase 4 is defined in
  `docs/2026-05-02-client-reactive-presentation-architecture-implementation-plan.md`.
* The target flow is `GameStateStore + SelectionStore + StaticCatalogStore ->
  UnitInfoViewModel -> UnitInfoUguiBinder -> existing uGUI controls`.
* Phase 3 introduced final Store read models and registered them in
  VContainer scopes.
* `UnitInfoPanelController` is still the prefab-facing facade. It owns
  serialized references, slide animation, portrait camera lifecycle, default
  layout repair, action registry integration, and selection event wiring.
* Store hydration from server messages is not implemented yet, so this task
  should not introduce a compatibility composition root around legacy cache
  singletons.

## Assumptions

* Keep existing scene/prefab compatibility by preserving
  `UnitInfoPanelController` serialized fields and public methods.
* Move panel projection into a ViewModel and rendering into a Binder without
  changing generated protocol files.
* Existing legacy action button behavior may remain in the facade until command
  service migration.

## Requirements

* Add `UnitInfoState`.
* Add `UnitInfoViewModel` that derives panel display state from final Stores.
* Add `UnitInfoUguiBinder` that renders TMP/Image/Slider/Button state and does
  not inspect Protocol or call cache singletons.
* Register `UnitInfoViewModel` in the game composition scope.
* Wire `UnitInfoPanelController` to the Binder/ViewModel path where safe while
  preserving existing UI behavior.
* Add focused EditMode coverage for ViewModel projection and Binder rendering.

## Acceptance Criteria

* [x] UnitInfo display projection lives in `UnitInfoViewModel`.
* [x] Binder only renders Unity UI controls from `UnitInfoState`.
* [x] Binder does not reference Protocol and does not call legacy cache
      singletons.
* [x] Composition registers `UnitInfoViewModel`.
* [x] Existing `UnitInfoPanelController` public surface and prefab serialized
      fields remain compatible.
* [x] Unity compile / dotnet test project build passes.
* [x] Static boundary checks stay green.

## Definition of Done

* Tests added/updated.
* Lint/typecheck/build checks pass or limitations are recorded.
* Docs/specs updated if a lasting convention is added.
* Task validated and completed.

## Out Of Scope

* Full server-message-to-store hydration.
* Map input re-architecture.
* Replacing action registrars and direct order command routing; that belongs to
  later command/input phases.
* UI Toolkit migration.

## Technical Notes

* Relevant specs read: frontend directory structure, component guidelines, state
  management, quality guidelines, cross-layer and code reuse guides.
* Existing helper classes to preserve/reuse:
  `UnitInfoHpBinder`, `UnitInfoHpStateResolver`,
  `UnitInfoDirectOrderPanelBinder`, `UnitInfoDirectOrderStateResolver`,
  `UnitInfoPlanningSummaryPresenter`, `UnitInfoDefaultLayoutBuilder`.
