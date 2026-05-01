# client script architecture refactor phase 4

## Goal

Continue the client script architecture refactor by extracting UnitInfo HP state resolution and binding from `UnitInfoPanelController`.

## What I Already Know

- `UnitInfoPanelController` remains a high-priority refactor candidate.
- The controller currently mixes cache lookup, building max HP fallback, Slider binding, and text binding in `RefreshUnitHpFromCache`.
- Existing helper style already separates action button binding and direct order state derivation.

## Requirements

- Extract HP state derivation from `UnitInfoPanelController`.
- Extract HP Slider/TMP binding into a small binder.
- Preserve unit HP, cached unit HP, building HP, and resource point behavior.
- Add focused EditMode coverage for HP state resolution.
- Keep Presentation protocol and UI NetworkManager boundaries clean.

## Acceptance Criteria

- [x] `UnitInfoPanelController` line count decreases.
- [x] HP state derivation is owned by a helper.
- [x] HP UI binding is owned by a binder.
- [x] Extracted helper has focused EditMode coverage.
- [x] Static boundary searches remain clean.
- [x] No generated protocol files or package manifests are touched.

## Verification

- `git diff --check`: passed.
- `task.py validate`: passed.
- Unity batchmode script import/compile: passed with exit code 0 and no `error CS` entries.
- Unity TestRunner XML was not produced at `/tmp/panoptes-refactor-phase4-editmode-results.xml`.
- `rg "Panoptes\\.Protocol" client/Assets/Scripts/Runtime/Presentation -g '*.cs'`: no matches.
- `rg "NetworkManager\\.Instance" client/Assets/Scripts/Runtime/Presentation/UI -g '*.cs'`: no matches.
- `rg "Panoptes\\.Protocol|Protocol\\.V1|MinisterDraftView" client/Assets/Scripts/Runtime/Core/Foundation -g '*.cs'`: no matches.

## Out of Scope

- Refactoring portrait camera ownership.
- Refactoring slide/docking layout.
- Installing UI Toolkit/VContainer/R3/UniTask.
