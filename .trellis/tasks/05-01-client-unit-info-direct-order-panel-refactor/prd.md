# Client Unit Info Direct Order Panel Refactor

## Goal

Continue the UnitInfo final-structure cleanup by extracting direct-order button
creation, listener binding, and render-state application from
`UnitInfoPanelController`.

## What I Already Know

- Generic action-list behavior is now in `UnitInfoActionListBinder`.
- Direct-order behavior is still owned by the controller:
  - `BindDirectOrderButtons()`;
  - `RefreshPlanningUi()` direct-order root/button visibility;
  - `SetDirectOrderButtonState()`;
  - `CreateDirectOrderButton()`.
- Direct-order availability is already derived by
  `UnitInfoDirectOrderStateResolver`; this task should not change those rules.

## Assumptions

- Keep existing serialized button fields (`moveButton`, `attackButton`,
  `holdButton`, `chargeButton`) and root field names stable.
- Keep `MapPlanningInputController` as the command target for now.
- Keep planning-summary rendering in its existing presenter for now; only move
  direct-order panel wiring/state.

## Requirements

- Extract direct-order button listener binding and state application into a
  focused helper beside `UnitInfoPanelController`.
- Extract runtime direct-order button creation/default visual setup when doing
  so does not risk serialized field compatibility.
- Preserve labels, visibility, and interactability semantics for move, attack,
  hold, and charge.
- Preserve `ActionLock` behavior.
- Add focused EditMode coverage for the extracted helper.
- Do not change generated protocol files or add packages.

## Acceptance Criteria

- `UnitInfoPanelController` is smaller and delegates direct-order UI mechanics.
- The controller still decides high-level planning context from
  `GameStateCache`, current unit ownership, and `UnitInfoDirectOrderState`.
- Direct-order buttons still call `BeginMoveSelection`,
  `BeginAttackSelection`, `IssueHoldOrder`, and `BeginChargeSelection` through
  `MapPlanningInputController`.
- Static boundary checks and dotnet compile pass; Unity TestRunner limitations
  are documented if XML is not produced.

## Out Of Scope

- Moving `MapPlanningInputController` itself.
- Changing direct-order availability rules.
- Full ViewModel/R3 migration.
