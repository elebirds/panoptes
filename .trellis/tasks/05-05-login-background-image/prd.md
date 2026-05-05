# Implement login background image

## Goal

Replace only the large background visual of the existing login area with the user-provided artwork, while preserving the current login input fields, buttons, serialized references, and runtime behavior.

## What I already know

- The target prefab exists at `client/Assets/Prefabs/UI/LoginPanel.prefab`.
- The authored login scene exists at `client/Assets/Scenes/Login.unity`.
- `LoginPanel.cs` already owns the input/button behavior and does not need logic changes for this task.
- The current Unity Editor session has an unrelated dirty unnamed scene, so implementation and verification should avoid scene-destructive operations.
- The provided source image is `C:\Users\27902\Downloads\ChatGPT Image 2026年5月5日 13_01_23.png`.

## Requirements

- Import a processed version of the provided image under `client/Assets/Art/UI/MainMenu/`.
- If needed, remove the black outer background so the imported asset can be used as a UI sprite cleanly.
- Update the existing login UI background visuals only; do not alter login/register behavior.
- Preserve the current input fields, buttons, and serialized script wiring.
- Prefer prefab-level changes so the scene stays in sync through the prefab reference.
- Avoid disturbing unrelated unsaved Unity scene state.

## Acceptance Criteria

- [ ] A new login background asset exists under `client/Assets/Art/UI/MainMenu/`.
- [ ] `LoginPanel.prefab` references the new asset for its large background visual.
- [ ] Login input/button components and `LoginPanel` serialized references remain intact.
- [ ] Unity reports no new console errors from the asset import / prefab update path.
- [ ] Verification is recorded without overwriting unrelated dirty scene state.

## Definition of Done

- Task-scoped files are updated only where needed.
- Unity asset import and prefab references are verified as far as possible without saving unrelated scene changes.
- A summary of changed files and verification steps is ready for handoff.

## Out of Scope

- Reworking login flow logic or validation behavior.
- Redesigning the entire login layout.
- Touching unrelated main menu or scene presentation assets.

## Technical Approach

- Process the source PNG into a UI-ready sprite asset using local tooling.
- Import the generated asset into Unity and set UI-friendly texture import settings.
- Update the prefab background sprite reference and related visual-only properties through Unity-safe tooling.
- Verify via prefab inspection, console checks, and off-scene or non-destructive Unity validation where feasible.

## Technical Notes

- Prefab hierarchy confirmed through Unity MCP: `LoginPanel/Bakcground` is the large panel image; child input/button visuals exist separately beneath it.
- Relevant specs: `.trellis/spec/frontend/index.md`, `directory-structure.md`, `component-guidelines.md`, `quality-guidelines.md`.
