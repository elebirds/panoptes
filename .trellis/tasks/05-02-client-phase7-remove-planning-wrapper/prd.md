# client phase7 remove planning tool viewmodel command wrapper

## Goal

Remove the extra command/mutation wrapper from `PlanningToolViewModel` so it is
only a reactive projection layer. Map input and tests should mutate planning
tool state through Core services/stores directly, keeping the final data flow
clearer.

## What I Already Know

* Phase 7 added `PlanningToolStore`, `PlanningToolService`, and
  `PlanningToolViewModel`.
* The ViewModel currently exposes methods like `BeginMove`, `EnterBuild`, and
  `SelectUnit` that only forward to services.
* `MapPlanningInputController` already publishes tool state through
  `PlanningToolService` and selection through `SelectionService`.
* Store write APIs should remain `internal`; Presentation should not mutate
  stores directly.

## Assumptions

* The user's "this wrapper" refers to the ViewModel command pass-through layer,
  not the Core service boundary that protects Store write access.
* `PlanningToolViewModel` should remain because it projects
  `PlanningToolStore + SelectionStore` into UI-facing state.

## Requirements

* Remove service dependencies from `PlanningToolViewModel`.
* Remove `PlanningToolViewModel` command/pass-through methods.
* Keep `PlanningToolViewModel` as a read-only R3 state projection.
* Update tests to mutate through `PlanningToolService` and `SelectionService`
  when arranging state.
* Add/update a boundary test so PlanningTool ViewModel does not depend on
  services.

## Acceptance Criteria

* [x] `PlanningToolViewModel` constructor accepts stores only.
* [x] `PlanningToolViewModel` exposes no command/pass-through mutation methods.
* [x] ViewModel projection tests still cover selection, mode, and prompt state.
* [x] Composition still resolves `PlanningToolViewModel`.
* [x] Client build and boundary checks pass.

## Definition Of Done

* Tests updated.
* Static boundary checks pass.
* Unity script import/compile succeeds.
* Trellis task context validates.
* Work committed as a small follow-up to Phase 7.

## Out Of Scope

* Removing `PlanningToolService`.
* Rewriting `MapPlanningInputController` into separate binder/presenter files.
* Changing protocol or server behavior.
