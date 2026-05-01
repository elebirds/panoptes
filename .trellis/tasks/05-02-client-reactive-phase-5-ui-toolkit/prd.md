# client reactive phase 5 ui toolkit turn summary

## Goal

Implement Phase 5 of the client reactive presentation architecture: the first
read-only UI Toolkit migration, using `TurnSummary` as the pilot panel.

## What I Already Know

* Phase 5 target flow is `TurnStore / GameStateStore -> TurnSummaryViewModel ->
  TurnSummaryUiToolkitBinder -> UIDocument + UXML + USS`.
* The phase explicitly forbids Unity automatic data binding for gameplay state;
  use explicit Binder rendering.
* `TurnStore` already exposes turn, phase, token count, game-over state, and
  planning-start events.
* Existing `TurnHUD` and `TurnReportPanel` are legacy uGUI/cache-facing panels
  and should remain intact during this UI Toolkit pilot.
* `GameLifetimeScope` is the right owner for migrated gameplay presentation
  modules.

## Requirements

* Add `TurnSummaryState`.
* Add `TurnSummaryViewModel` that derives read-only state from `TurnStore` and
  `GameStateStore`.
* Add `TurnSummaryUiToolkitBinder` that renders a UIDocument explicitly using
  named VisualElements.
* Add UXML/USS assets with stable element names under the Toolkit Turn area.
* Register the ViewModel and Binder in game composition without introducing
  legacy cache singleton dependencies.
* Add EditMode coverage for projection, UI Toolkit rendering, and boundary
  rules.

## Acceptance Criteria

* [x] Runtime UI Toolkit panel can render from ViewModel state.
* [x] UXML/USS element names are stable and documented in tests/specs.
* [x] Binder uses explicit `Q<T>("name")` lookup and `Render(state)`.
* [x] No Unity automatic gameplay data binding is introduced.
* [x] TurnSummary ViewModel/Binder do not reference Protocol, legacy caches, or
      `NetworkManager.Instance`.
* [x] Existing uGUI Turn HUD/Report behavior remains untouched.
* [x] Unity compile / dotnet build passes.

## Definition Of Done

* Tests added/updated.
* Static boundary checks pass.
* Docs/specs updated for the Phase 5 UI Toolkit pilot.
* Trellis task validates and is marked completed.

## Out Of Scope

* Replacing `TurnHUD`.
* Submitting turns through the UI Toolkit panel.
* UI Toolkit automatic data binding.
* Full server-message-to-store hydration.
