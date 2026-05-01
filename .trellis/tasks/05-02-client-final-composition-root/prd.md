# Client Final Composition Root

## Goal

Implement the next architecture slice after the reactive presentation foundation:
introduce real VContainer lifetime scopes that new C0a modules can depend on
directly, without building a compatibility wrapper around legacy singleton
lookups.

## What I already know

- The final client architecture is documented in
  `docs/2026-05-02-client-reactive-presentation-architecture-implementation-plan.md`.
- Phase 0 and the first part of Phase 1 are already committed.
- VContainer, R3, UniTask, and UI Toolkit are installed and policy-approved.
- The current client still creates legacy singletons through
  `AppManager.EnsureManagersBootstrap()`.
- This task should establish final scope classes and the first non-singleton
  injectable infrastructure services, not migrate a whole UI panel yet.

## Assumptions

- This slice can introduce `ProjectLifetimeScope` and `GameLifetimeScope`
  scripts without immediately rewiring all scenes and prefabs.
- The first scope should register only dependencies that can be resolved without
  calling legacy `*.Instance` APIs.
- Existing legacy singleton components may remain for old modules, but new
  services introduced here must not call singleton lookup.

## Requirements

- Add final `ProjectLifetimeScope` and `GameLifetimeScope` classes under
  `Presentation/Composition`.
- Add reusable composition installer helpers so scope registration is explicit
  and testable.
- Add an injectable message sender service that depends on a `NetworkManager`
  instance rather than calling `NetworkManager.Instance`.
- Register the first final project-scope services through VContainer:
  `AuthService`, `NetworkManager`, `MessageDispatcher`, `SessionManager`, and
  the injectable message sender.
- Keep game-scope registration empty or marker-only until Phase 3 stores exist;
  do not register `GameStateCache`, `StaticCatalogCache`, or
  `PlanningDraftCache` as compatibility dependencies.
- Add static EditMode coverage for the new composition rules.

## Acceptance Criteria

- [x] `ProjectLifetimeScope` and `GameLifetimeScope` compile.
- [x] `Panoptes.Presentation` references VContainer explicitly.
- [x] New composition code contains no `*.Instance` singleton lookup.
- [x] No generated protocol files are modified.
- [x] Presentation still does not reference generated Protocol.
- [x] UI still does not call `NetworkManager.Instance`.
- [x] Unity batchmode script compile succeeds.
- [x] Trellis context validates.

## Verification

- `python3 ./.trellis/scripts/task.py validate .trellis/tasks/05-02-client-final-composition-root`
- Unity `6000.4.1f1` batchmode compile:
  `/Applications/Unity/Hub/Editor/6000.4.1f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath /Users/hhm/code/panoptes/client -logFile /tmp/panoptes-unity-composition-root.log`
- `dotnet build client/Panoptes.Presentation.csproj`
- `dotnet build client/Panoptes.Tests.EditMode.csproj`
- Static boundary checks:
  - `rg -n "Panoptes\\.Protocol" client/Assets/Scripts/Runtime/Presentation -g '*.cs'`
  - `rg -n "NetworkManager\\.Instance" client/Assets/Scripts/Runtime/Presentation/UI -g '*.cs'`
  - `rg -n "\\.Instance" client/Assets/Scripts/Runtime/Presentation/Composition client/Assets/Scripts/Runtime/Core/Infrastructure/Network/NetworkMessageSender.cs`
  - `rg -n "GameStateCache|PlanningDraftCache|StaticCatalogCache" client/Assets/Scripts/Runtime/Presentation/Composition -g '*.cs'`
- Generated protocol dirty check:
  `git status --porcelain -- server/internal/gen/proto client/Assets/Scripts/Protocol`

## Definition of Done

- Tests or static checks added/updated for new architecture rules.
- Lint / typecheck / Unity compile gate green where available.
- Docs/notes updated if the composition strategy changes.
- Work committed as a coherent architecture slice.

## Out of Scope

- Migrating UnitInfo, BuildCommandPanel, RecipeSynthesisPanel, or map input to
  ViewModel/Binder.
- Deleting existing legacy singleton caches.
- Registering cache facades as Store replacements.
- Editing generated protocol files.

## Technical Notes

- `ProjectLifetimeScope` should live in `Presentation/Composition` because it is
  Unity scene/prefab-facing infrastructure.
- `NetworkMessageSender` should live in `Core/Infrastructure/Network` because
  it talks to `NetworkManager`.
- Phase 3 will add actual R3 Stores; this task only reserves the game scope and
  avoids false compatibility registrations.
