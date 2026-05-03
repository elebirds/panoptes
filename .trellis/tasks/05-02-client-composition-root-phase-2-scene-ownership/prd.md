# Client Composition Root Phase 2 Scene Ownership

## Goal

Finish Phase 2 of the client reactive presentation architecture by making the
final VContainer composition root concrete in scene assets, not only available
as scripts.

## What I already know

- Phase 2 target is documented in
  `docs/2026-05-02-client-reactive-presentation-architecture-implementation-plan.md`.
- The previous slice added `ProjectLifetimeScope`, `GameLifetimeScope`,
  `ClientCompositionInstaller`, and `PanoptesCompositionBootstrap`.
- `Boot.unity` is the first build scene, and `AppManager` /
  `PanoptesCompositionBootstrap` already create the persistent `Managers`
  object before scene load.
- Adding another serialized `Managers` object to `Boot.unity` would risk
  duplicate manager objects because the runtime bootstrap runs before the first
  scene loads.
- A safer Phase 2 completion is to keep Project scope runtime-owned by bootstrap
  and make the `Game` scene own a real `GameLifetimeScope`.

## Requirements

- Add a real `GameLifetimeScope` component to `client/Assets/Scenes/Game.unity`
  through Unity APIs.
- Do not register legacy `GameStateCache`, `PlanningDraftCache`, or
  `StaticCatalogCache` in `GameLifetimeScope`.
- Keep `ProjectLifetimeScope` bootstrap-owned for now to avoid duplicating the
  existing `Managers` runtime object.
- Add or update static EditMode coverage so Phase 2 scene ownership is checked.
- Keep existing hard boundaries:
  - Presentation does not reference generated Protocol.
  - Presentation UI does not call `NetworkManager.Instance`.
  - New composition code does not call legacy `*.Instance`.

## Acceptance Criteria

- [x] `Game.unity` contains a serialized `GameLifetimeScope`.
- [x] The test suite has a static check for scene ownership of
      `GameLifetimeScope`.
- [x] Unity batchmode compile succeeds.
- [x] `dotnet build client/Panoptes.Tests.EditMode.csproj` succeeds.
- [x] Trellis context validates.
- [x] `git diff --check` succeeds.
- [x] Generated protocol files are untouched.

## Verification

- `python3 ./.trellis/scripts/task.py validate .trellis/tasks/05-02-client-composition-root-phase-2-scene-ownership`
- `dotnet build client/Panoptes.Tests.EditMode.csproj`
- `Unity -batchmode -quit -projectPath client -logFile /tmp/panoptes-unity-composition-phase2-final.log`
- `git diff --check`
- Static boundary searches confirmed no generated Protocol references under
  Presentation, no `NetworkManager.Instance` calls in Presentation UI, no
  legacy singleton calls in new composition/message-sender paths, and no
  generated protocol file changes.

## Out of Scope

- Migrating actual stores or panel ViewModels; that starts in Phase 3.
- Adding legacy cache facades to the VContainer scopes.
- Removing `AppManager.EnsureManagersBootstrap()`.
- Editing generated protocol files.

## Technical Notes

- Scene editing should be done through Unity editor APIs rather than hand-writing
  large YAML blocks.
- Static scene ownership can be asserted by reading the `GameLifetimeScope`
  script GUID from its `.meta` file and checking `Assets/Scenes/Game.unity`.
