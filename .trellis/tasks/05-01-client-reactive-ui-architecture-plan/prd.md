# persist client reactive UI architecture plan

## Goal

Persist the long-term client UI/script architecture decision before C0a starts, so future implementation agents can distinguish the current C0p facade/helper baseline from the desired final architecture.

## What I Already Know

- The user wants to introduce UI Toolkit for selected information-heavy panels because the team is mostly new to Unity and UXML/USS is more AI-friendly than serialized uGUI prefab data.
- The user agrees with a future stack where VContainer owns lifecycle, R3 owns state propagation, UniTask owns async flows, ViewModels own projection/commands, and explicit Binders connect UI assets.
- The current AGENTS/frontend docs still forbid VContainer/R3/UniTask-style dependencies, so this task must persist a plan and note the dependency policy amendment required before package installation.
- C0p completed the preflight cleanup: Presentation no longer references generated protocol types, Presentation UI no longer calls `NetworkManager.Instance`, and large scripts have been moved toward facade/helper shapes.

## Assumptions

- C0a should not immediately install all new packages. It should start with a UI Toolkit pilot and preserve the existing client boundary tests.
- uGUI remains valid for map/HUD/world-space presentation even if UI Toolkit is introduced for management panels.
- Runtime data binding should be considered, but explicit Binder + reactive ViewModel is the default initial integration path.

## Requirements

- Persist a full architecture plan under `docs/`.
- List the current script refactor roadmap from the C0p state to the target state.
- Update frontend specs so future agents see the target direction and the migration guardrails.
- Do not modify generated protocol files or Unity packages in this task.

## Acceptance Criteria

- [x] A dated client architecture plan exists under `docs/`.
- [x] The plan includes stack choices, layer responsibilities, UI Toolkit/uGUI boundaries, data binding policy, and rollout phases.
- [x] The plan includes a concrete script refactor roadmap for current large files.
- [x] Frontend Trellis specs point to the new target without pretending it is already fully implemented.
- [x] Task validation passes.

## Definition of Done

- Documentation updated.
- Trellis task context curated.
- No generated files touched.
- Changes committed.

## Out of Scope

- Installing VContainer, R3, UniTask, DOTween, or new Unity packages.
- Migrating any existing panel to UI Toolkit.
- Refactoring Unity scripts in this task.
- Updating AGENTS.md dependency bans directly; this plan records the proposed policy change first.

## Technical Notes

- Current package manifest includes uGUI and UIElements modules, but no VContainer/R3/UniTask package entries.
- Current static boundary tests are in `client/Assets/Scripts/Tests/EditMode/StaticClientBoundaryTests.cs`.
- C0p completion note: `docs/2026-05-01-client-c0p-preflight-refactor-gate.md`.
