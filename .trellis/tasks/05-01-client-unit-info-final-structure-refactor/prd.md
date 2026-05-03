# Client Unit Info Final Structure Refactor

## Goal

Move `UnitInfoPanelController` toward the intended final client UI shape:
the MonoBehaviour remains a prefab-facing facade, while runtime concerns are
owned by small helpers/binders.

## Scope

- Preserve all existing serialized field names and public MonoBehaviour entry
  points used by prefabs and scenes.
- Extract panel slide/docking animation state from `UnitInfoPanelController`.
- Extract unit portrait camera, render texture, and fill-light lifecycle from
  `UnitInfoPanelController`.
- Keep Presentation dependent on Core DTO/cache APIs only.
- Do not introduce new client packages or framework dependencies in this step.
- Do not edit generated protocol files.

## Acceptance Criteria

- `UnitInfoPanelController` is materially smaller and reads as orchestration.
- Extracted helpers live beside the facade under
  `client/Assets/Scripts/Runtime/Presentation/UI/HUD/`.
- Existing portrait, fallback icon, HP, planning summary, action button, and
  direct-order behavior is preserved.
- Unity script compile / EditMode gate passes, or any environment limitation is
  recorded with compiler-log verification.
- Static boundary checks remain green:
  - no `Panoptes.Protocol` usage in Presentation;
  - no direct `NetworkManager.Instance` usage in Presentation UI;
  - no protocol/view DTO leakage into Core Foundation.
