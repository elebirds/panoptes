# C0a Management Host and National Overview

## Goal

Start implementing the approved C0a UI plan with the smallest real product
slice: a UI Toolkit management host and a read-only national overview surface
that consumes final Stores/ViewModels through VContainer and R3.

## Requirements

- Add a UI Toolkit `ManagementHost` runtime surface.
- Extend management panel visibility so the host can open/close/switch the
  first C0a panels.
- Add a `NationalOverview` read-only ViewModel projected from existing Stores.
- Render the overview through a UI Toolkit binder/host without Protocol or
  direct NetworkManager usage in Presentation.
- Keep map/HUD/world-space uGUI surfaces intact.
- Do not remove existing fallback management binders in this first batch.

## Acceptance Criteria

- [x] `ManagementPanelVisibilityStore` includes `NationalOverview`,
      `TurnSummary`, `NationalLedger`, and `PolicyFocus`.
- [x] A `ManagementHostUiToolkitBinder` exists and is registered from a
      prefab-backed composition path.
- [x] A `NationalOverviewViewModel` exists and projects turn, phase, tokens,
      resources, counts, planned research, planned policy, and recent events.
- [x] UI Toolkit UXML/USS assets exist for the management host.
- [x] EditMode tests cover national overview projection and host rendering /
      visibility behavior.
- [x] Boundary tests continue to protect Presentation from Protocol,
      NetworkManager singleton calls, and extra generated GameObject
      composition exceptions.

## Out of Scope

- Tech tree command wiring.
- Policy command wiring.
- Minister accept/reject flows.
- Removing legacy uGUI HUDs.
- Removing existing management fallback binders.

## Technical Notes

- Follow `docs/2026-05-03-c0a-ui-inventory-and-integration-plan-zh.md`.
- Follow frontend specs for Store/ViewModel/Binder direction and explicit UI
  Toolkit rendering.
