# client script architecture refactor phase 3

## Goal

Continue the client script architecture refactor by shrinking `UnitInfoPanelController` and moving direct order button state derivation into a testable helper.

## What I Already Know

- `UnitInfoPanelController` is a high-priority refactor candidate in the reactive UI architecture plan.
- Existing helper style includes `UnitInfoActionButtonBinder`, `UnitInfoPlanningSummaryPresenter`, and `UnitInfoPortraitPresenter`.
- The direct order button logic currently mixes UI rendering with unit catalog/tag checks for move, attack, hold, and charge visibility.

## Requirements

- Extract direct order state derivation from `UnitInfoPanelController`.
- Preserve existing command button behavior: civilians can move only, military units can attack/hold, charge requires the `charge` tag, buildings/resources have no direct orders.
- Add EditMode tests for the extracted resolver.
- Keep Presentation protocol and UI NetworkManager boundaries clean.

## Acceptance Criteria

- [x] `UnitInfoPanelController` line count decreases.
- [x] Direct order state derivation is owned by a helper.
- [x] Extracted helper has focused EditMode coverage.
- [x] Static boundary searches remain clean.
- [x] No generated protocol files or package manifests are touched.

## Verification

- `git diff --check`: passed.
- `task.py validate`: passed.
- Unity batchmode script import/compile: passed with exit code 0 and no `error CS` entries.
- Unity TestRunner XML was not produced at `/tmp/panoptes-refactor-phase3-editmode-results.xml`.
- `rg "Panoptes\\.Protocol" client/Assets/Scripts/Runtime/Presentation -g '*.cs'`: no matches.
- `rg "NetworkManager\\.Instance" client/Assets/Scripts/Runtime/Presentation/UI -g '*.cs'`: no matches.
- `rg "Panoptes\\.Protocol|Protocol\\.V1|MinisterDraftView" client/Assets/Scripts/Runtime/Core/Foundation -g '*.cs'`: no matches.

## Out of Scope

- Moving the whole UnitInfo panel to ViewModels.
- Refactoring portrait camera or slide layout.
- Installing UI Toolkit/VContainer/R3/UniTask.
