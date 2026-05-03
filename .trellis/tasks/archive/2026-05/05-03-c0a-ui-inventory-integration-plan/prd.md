# brainstorm: C0a UI Inventory and Integration Plan

## Goal

Before C0a starts, define which Unity UI surfaces must exist, which ones
already exist, what each surface displays, what player operations it can emit,
and whether it should be authored as uGUI prefab or UI Toolkit UXML/USS.

## What I already know

- The user wants this inventory before starting C0a.
- The final client architecture standard is Store/Service -> ViewModel ->
  Binder -> authored uGUI prefab or UI Toolkit UIDocument.
- Existing uGUI prefabs cover most map, HUD, lobby, overlay, and world-space
  surfaces.
- Existing UI Toolkit authored assets currently cover only `TurnSummary`.
- Management panel ViewModel/Binder slices already exist for TurnSummary,
  BuildCatalog, TechTree, RecipeSynthesis, MinisterReport, PolicyFocus, and
  NationalLedger.
- Several management panels still rely on composition-created fallback
  UIDocument objects until authored prefabs exist.

## Requirements

- Inventory existing UI assets.
- Inventory needed C0a UI surfaces.
- For every major surface, identify displayed information.
- For every major surface, identify emitted player operations.
- Classify every surface as uGUI prefab, UI Toolkit, or keep-existing.
- Identify current implementation gaps before C0a.
- Recommend C0a implementation order.

## Deliverable

- `docs/2026-05-03-c0a-ui-inventory-and-integration-plan.md`

## Acceptance Criteria

- [x] Existing UI assets are listed.
- [x] Needed UI surfaces are listed.
- [x] Each surface has technology ownership: uGUI prefab or UI Toolkit.
- [x] Each surface lists information displayed and player operations.
- [x] C0a order is explicit.
- [x] Known current gaps are captured.

## Out of Scope

- Implementing C0a UI assets.
- Editing Unity scenes or prefabs.
- Removing existing uGUI HUD surfaces.
- Reopening minister interaction requirements.

## Technical Notes

- Inspected current UI assets under `client/Assets/Prefabs`,
  `client/Assets/Resources/Prefabs`, and `client/Assets/UI/Toolkit`.
- Inspected final architecture notes in
  `docs/2026-05-02-client-reactive-presentation-architecture-implementation-plan.md`.
- Inspected current Presentation ViewModels, UI Toolkit binders, HUD
  controllers, composition registration, and planning/game command services.

