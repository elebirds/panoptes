# client script architecture refactor phase 1

## Goal

Start the client script architecture refactor with a low-risk boundary cleanup that moves minister draft protocol conversion out of Core/Foundation and into the mapper layer.

## What I Already Know

- The target architecture says `Core/Foundation` should be pure DTO/value objects and should not reference generated protocol types.
- `MinisterDto.cs` currently imports `Panoptes.Protocol.V1` and exposes `MinisterDraftDto.FromView(MinisterDraftView view)`.
- `MinisterMapper` already exists under `Core/Infrastructure/Mapper`, which is the correct protocol conversion layer.
- `PlanningDraftCache` is the only runtime caller of `MinisterDraftDto.FromView`.

## Assumptions

- This task should not install UI Toolkit, VContainer, R3, or UniTask yet.
- Existing minister draft behavior must remain unchanged.
- A static boundary test should lock the new Foundation rule.

## Requirements

- Move `MinisterDraftView -> MinisterDraftDto` conversion into `MinisterMapper`.
- Remove generated protocol and `UnityEngine.JsonUtility` dependencies from `MinisterDto`.
- Update `PlanningDraftCache` to call the mapper.
- Add/extend tests so Core/Foundation cannot reintroduce protocol references.

## Acceptance Criteria

- [x] `rg "Panoptes\\.Protocol" client/Assets/Scripts/Runtime/Core/Foundation` has no matches.
- [x] Existing Presentation boundary searches remain clean.
- [x] Minister draft integration path is updated to use `MinisterMapper`.
- [x] No generated protocol files are touched.

## Verification

- `git diff --check`: passed.
- `rg "Panoptes\\.Protocol|Protocol\\.V1|MinisterDraftView" client/Assets/Scripts/Runtime/Core/Foundation -g '*.cs'`: no matches.
- `rg "Panoptes\\.Protocol" client/Assets/Scripts/Runtime/Presentation -g '*.cs'`: no matches.
- `rg "NetworkManager\\.Instance" client/Assets/Scripts/Runtime/Presentation/UI -g '*.cs'`: no matches.
- Generated protocol and package manifest paths were unchanged.
- Unity batchmode could not run because another Unity instance already had the project open.

## Out of Scope

- UI Toolkit migration.
- Package dependency changes.
- Large Map/UnitInfo/BuildCommand refactors.
