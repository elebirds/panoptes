# Client Build Placement Feedback Presenter Refactor

## Problem

After extracting move preview ghosts, `MapPlanningInputController` still owns
build-placement hover ghost creation, preview recoloring, and cleanup. This is
runtime presentation feedback, not input intent routing. Keeping it inside the
map input facade makes the next C0a planning/binding work more likely to add
more visual object lifecycle code to the controller.

## Goal

Extract build placement hover ghost ownership into a focused feedback presenter
under `Presentation/Planning/Feedback`, while preserving all serialized fields
and public `MapPlanningInputController` entry points.

## Acceptance Criteria

- `MapPlanningInputController` delegates build hover ghost creation,
  placement-ghost recoloring, and cleanup to a helper/presenter.
- Existing serialized build fields stay on the controller for scene/prefab
  compatibility.
- The presenter exposes deterministic cleanup for the runtime hover ghost.
- No gameplay placement legality or affordability rules are added client-side.
- Presentation still has no direct `Panoptes.Protocol` references.
- UI scripts still do not call `NetworkManager.Instance` directly.
- No generated protocol files are modified.
- Client Presentation/EditMode projects compile.

## Non-Goals

- Do not change build command semantics, preview request semantics, or server
  validation behavior.
- Do not introduce new packages or UI framework changes.
- Do not rename or move scene-facing MonoBehaviours.
