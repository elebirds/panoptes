# client script architecture refactor phase 2

## Goal

Continue the client script architecture refactor by shrinking `BuildCommandPanel` toward a prefab-facing facade and moving view plumbing into a helper.

## What I Already Know

- `BuildCommandPanel` is a high-priority refactor candidate in the reactive UI architecture plan.
- C0p already extracted list rendering into `BuildCommandListRenderer`.
- The next safe slice is prefab/ScrollRect/template resolution, which does not change gameplay behavior or require new packages.

## Requirements

- Extract build panel view reference resolution from `BuildCommandPanel`.
- Preserve serialized fields and existing public panel API.
- Keep Presentation protocol and UI NetworkManager boundaries clean.
- Add or update EditMode coverage for the extracted behavior.

## Acceptance Criteria

- [x] `BuildCommandPanel` line count decreases from the C0p post-state.
- [x] A helper owns build panel viewport/content/template resolution.
- [x] Existing build panel refresh behavior is preserved.
- [x] Static boundary searches remain clean.
- [x] No generated protocol files or package manifests are touched.

## Verification

- `git diff --check`: passed.
- `task.py validate`: passed.
- `rg "Panoptes\\.Protocol" client/Assets/Scripts/Runtime/Presentation -g '*.cs'`: no matches.
- `rg "NetworkManager\\.Instance" client/Assets/Scripts/Runtime/Presentation/UI -g '*.cs'`: no matches.
- `rg "Panoptes\\.Protocol|Protocol\\.V1|MinisterDraftView" client/Assets/Scripts/Runtime/Core/Foundation -g '*.cs'`: no matches.
- Unity batchmode could not run because another Unity instance already had the project open.

## Out of Scope

- Installing UI Toolkit/VContainer/R3/UniTask.
- Migrating `BuildCommandPanel` to UI Toolkit.
- Changing build placement behavior.
