# Client Map Planning Input Final Structure Refactor

## Problem

`MapPlanningInputController` is still a large prefab-facing scene facade. It
owns serialized map input wiring, selection, command intent routing, pending
planning state, build placement, combat targeting, previews, backend playback,
damage popups, and cleanup. This shape is acceptable for the current playable
baseline, but it is not a good foundation for C0a backend binding work.

The final direction is:

```text
Map scene facade
  -> planning tool / mode coordinator
  -> focused input modes
  -> preview and feedback presenters
  -> intent sender
```

The client must remain a presentation layer. It can request previews, render
server/cache state, and send player intents, but it must not perform gameplay
legality validation or become an alternative rules engine.

## Goal

Continue shrinking `MapPlanningInputController` toward the final structure
before C0a by extracting cohesive presentation collaborators while preserving
scene/prefab compatibility.

The first implementation slice extracts the move preview ghost lifecycle into a
dedicated presenter/helper owned by `Presentation/Planning/Feedback`. The
controller keeps its serialized fields and public API, but delegates runtime
ghost creation, animation, visual tinting, removal, and material cleanup.

## Acceptance Criteria

- `MapPlanningInputController` remains the scene-facing facade and keeps its
  public MonoBehaviour entry points, serialized field names, events, and
  singleton compatibility.
- Move preview ghost behavior is owned by a focused helper/presenter outside the
  controller.
- Extracted code exposes deterministic cleanup for runtime Unity objects and
  material resources.
- Presentation still has no direct `Panoptes.Protocol` references.
- UI scripts still do not call `NetworkManager.Instance` directly.
- No generated protocol files are modified.
- The Trellis task validates and the relevant client build/static checks pass or
  any environment limitation is recorded.

## Non-Goals

- Do not introduce VContainer, R3, UniTask, UI Toolkit, or new packages in this
  slice.
- Do not move scene/prefab-facing MonoBehaviour scripts or rename serialized
  fields.
- Do not change gameplay command semantics, range rules, resource checks, or
  server-authoritative validation.
