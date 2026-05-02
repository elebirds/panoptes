# phase8 complete management ui migration

## Goal

Complete Phase 8 by giving each planned management panel a migrated
`Store -> ViewModel -> UI Toolkit Binder` slice while keeping existing uGUI
prefabs intact for compatibility.

## What I Already Know

* Phase 7 is complete.
* Phase 8 order is `TurnSummary`, `BuildCommandPanel`, `TechTree`,
  `RecipeSynthesis`, `MinisterReport`, `Policy/NationalFocus`, and
  `NationalLedger`.
* `TurnSummary` and the initial `BuildCatalog` slice already have UI Toolkit
  binders and ViewModels.
* Remaining panels can share a common management-list state/rendering shape,
  with panel-specific ViewModels projecting from existing Stores.

## Assumptions

* This phase can complete as migrated runtime slices without deleting old uGUI
  panels or requiring hand-authored production UXML/USS in this commit.
* UI Toolkit binders render explicit fallback trees and expose callbacks for
  commands; full UX polish and authored prefabs can follow as asset work.
* The client remains presentation-only and must not compute gameplay legality.

## Requirements

* Add UI Toolkit migrated slices for TechTree, RecipeSynthesis,
  MinisterReport, Policy/NationalFocus, and NationalLedger.
* Reuse common management panel render state/helpers where reasonable.
* Register all migrated ViewModels and binders in the final game composition.
* Add focused tests for representative ViewModel projections and binder
  rendering.
* Update the Phase 8 plan status to complete after checks pass.

## Acceptance Criteria

* [x] All Phase 8 planned panels have a ViewModel and UI Toolkit Binder.
* [x] Migrated ViewModels consume Store state, not generated Protocol or
      legacy cache singletons.
* [x] Binders use explicit UI Toolkit rendering and named elements.
* [x] Existing uGUI panels remain intact.
* [x] Tests cover representative migrated panel projection/rendering.
* [x] dotnet build/test, static boundary checks, and Unity compile pass.
* [x] Phase 8 plan status is updated as complete.

## Definition Of Done

* Tests added/updated.
* Static boundary checks pass.
* Unity script import/compile succeeds.
* Trellis task context validates.
* Work committed.

## Out Of Scope

* Removing old uGUI prefabs/controllers.
* Authoring final UXML/USS assets by hand.
* Adding client-side gameplay legality validation.
