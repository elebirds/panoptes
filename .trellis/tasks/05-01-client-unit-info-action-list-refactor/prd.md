# Client Unit Info Action List Refactor

## Goal

Continue moving `UnitInfoPanelController` toward the final facade shape by
extracting action-list slot ownership, runtime button creation, visual repair,
and click binding out of the controller.

## What I Already Know

- The previous UnitInfo pass extracted slide/docking animation and portrait
  camera lifecycle.
- `UnitInfoPanelController` still directly owns `ActionButtonSlot`, action slot
  expansion, default button creation, action-button visual repair, and
  `RefreshActionButtons()`.
- Existing public/serialized facade compatibility matters more than aggressive
  renaming.

## Assumptions

- Keep existing serialized field names on `UnitInfoPanelController` where
  possible, including `actionButtons`, `actionButtonsRoot`,
  `defaultActionButtonSize`, and `defaultActionButtonColor`.
- Extract helpers beside the facade under
  `client/Assets/Scripts/Runtime/Presentation/UI/HUD/`.
- Do not introduce new packages or change generated protocol files.

## Requirements

- Move action slot data and behavior into a focused helper or nested-compatible
  public/helper type that Unity serialization can still use.
- Move default action button creation and action button visual repair out of
  `UnitInfoPanelController`.
- Move action button click binding/visibility refresh out of
  `UnitInfoPanelController`.
- Add focused EditMode coverage for any extracted non-trivial helper behavior.
- Keep Presentation using Core DTO/cache/intents, not protocol messages.

## Acceptance Criteria

- `UnitInfoPanelController` is materially smaller and remains prefab-facing
  orchestration.
- Action button behavior remains equivalent:
  - missing required slots are created;
  - labels and listeners are refreshed from `UnitInfoActionRegistry`;
  - unavailable actions are hidden;
  - visual repair still applies layout, fallback sprite, and label font.
- Existing direct-order buttons are not accidentally folded into generic action
  list behavior unless that can be done without changing semantics.
- `git diff --check`, dotnet compile, static boundary checks, and relevant
  Unity/EditMode tests pass or limitations are documented.

## Out Of Scope

- Full ViewModel/R3/VContainer migration.
- UI Toolkit conversion.
- Changing action registry/provider semantics.
- Renaming serialized fields without prefab verification.
