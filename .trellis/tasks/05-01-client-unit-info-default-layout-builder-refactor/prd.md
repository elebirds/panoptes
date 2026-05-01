# Client Unit Info Default Layout Builder Refactor

## Goal

Move UnitInfo's runtime default uGUI layout construction out of
`UnitInfoPanelController`, so the controller remains a prefab-facing facade and
layout fallback code lives in a focused builder/helper.

## What I Already Know

- UnitInfo action list, direct-order panel, portrait lifecycle, slide animator,
  HP binding, and planning summary are already extracted.
- `UnitInfoPanelController.EnsureDefaultLayout()` still creates the default
  canvas, panel, background, roots, icon, text, slider, HP text, action slots,
  and direct-order buttons.
- The project prefers manually-authored prefabs for final UI, but runtime
  fallback layout remains useful for editor/default prefab repair.

## Assumptions

- Preserve all serialized fields and public MonoBehaviour entry points on
  `UnitInfoPanelController`.
- Keep `UnitInfoPanelLayoutBuilder` as the shared place for default uGUI layout
  helpers, expanding it if appropriate.
- Do not move portrait camera lifecycle or action/direct-order behavior back
  into the controller.
- Do not introduce packages or edit generated protocol files.

## Requirements

- Extract default layout construction into a helper that returns/updates the
  needed references without renaming serialized fields.
- Keep `UnitInfoPanelController` responsible for orchestration and assigning
  returned references.
- Preserve default sizes, anchors, colors, labels, and component wiring.
- Add or update EditMode tests for the extracted layout creation behavior.
- Keep static boundary and compile checks green.

## Acceptance Criteria

- `UnitInfoPanelController.EnsureDefaultLayout()` becomes a thin delegation or
  small orchestration wrapper.
- Runtime/default layout still creates:
  - HUD canvas if no parent canvas exists;
  - background;
  - action/direct-order roots;
  - unit icon, portrait placeholder, name/description/planning summary text;
  - HP slider and HP text;
  - default action slots and direct-order buttons through existing binders.
- `UnitInfoPanelController` line count decreases materially.
- Tests/compile/static boundary checks pass or Unity TestRunner limitations are
  documented.

## Out Of Scope

- Full UI Toolkit migration.
- Removing runtime fallback layout support.
- Changing final prefab/manual-authoring strategy.
