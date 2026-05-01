# Client Composition Root Phase 3 Stores

## Goal

Finish Phase 3 of the client reactive presentation architecture by adding the
initial Core Store / Read Model layer for migrated modules. Stores should expose
read-only reactive snapshots and become the dependency surface that Phase 4
ViewModels consume.

## What I Already Know

- Phase 3 target is documented in
  `docs/2026-05-02-client-reactive-presentation-architecture-implementation-plan.md`.
- Phase 2 already added `ProjectLifetimeScope`, `GameLifetimeScope`, and a real
  `Game Composition` scene root in `Assets/Scenes/Game.unity`.
- `IReadOnlyStore<TState>` already exists under
  `Core/Application/Stores` and uses R3 `Observable<TState>`.
- Legacy `GameStateCache`, `PlanningDraftCache`, and `StaticCatalogCache` are
  MonoBehaviour singletons and must not be registered as compatibility services
  in the new VContainer scopes.
- Presentation must not reference generated Protocol, and new migrated code
  must not call legacy `*.Instance` APIs.

## Requirements

- Add initial Store / Read Model types:
  - `StaticCatalogStore`
  - `GameStateStore`
  - `PlanningDraftStore`
  - `SelectionStore`
  - `TurnStore`
- Store state is mirror/read-model data only; it must not compute gameplay
  legality or resource affordability.
- Store snapshots must return immutable or cloned data so consumers cannot
  mutate internal store state.
- Store write APIs must be internal/application-facing rather than public
  UI-facing mutators.
- Store state uses independent Core read-model DTOs; do not expose generated
  Protocol messages or nested legacy cache JSON classes as the Store API.
- Register the new stores in final VContainer scopes:
  - project scope: static catalog store
  - game scope: game state, planning draft, selection, and turn stores
- Do not register legacy cache facades in `GameLifetimeScope`.
- Add EditMode tests for snapshot immutability and update flow.

## Acceptance Criteria

- [x] Store classes and snapshot state types compile under `Panoptes.Core`.
- [x] `ClientCompositionInstaller` registers final stores without registering
      legacy cache singletons.
- [x] Store tests cover update flow and defensive snapshot behavior.
- [x] Unity batchmode compile succeeds.
- [x] `dotnet build client/Panoptes.Tests.EditMode.csproj` succeeds.
- [x] Trellis context validates.
- [x] `git diff --check` succeeds.
- [x] Generated protocol files are untouched.
- [x] Boundary checks remain green:
      Presentation does not reference generated Protocol, Presentation UI does
      not call `NetworkManager.Instance`, and new composition/store paths do
      not call legacy singleton APIs.

## Verification

- `dotnet build client/Panoptes.Tests.EditMode.csproj`
- `Unity -batchmode -quit -projectPath client -logFile /tmp/panoptes-unity-phase3-final.log`
- Manual NUnit invocation through a temporary runner executed
  `StoreReadModelTests` and `ClientCompositionBoundaryTests` methods; all 10
  passed.
- `python3 ./.trellis/scripts/task.py validate .trellis/tasks/05-02-client-composition-root-phase-3-stores`
- `git diff --check`
- Static boundary searches confirmed no generated Protocol references under
  Presentation, no `NetworkManager.Instance` calls in Presentation UI, no
  legacy cache or singleton use in Store/Composition paths, and no generated
  protocol file changes.

## Out of Scope

- Migrating `UnitInfoPanel` or any concrete UI binder; that starts in Phase 4.
- Wiring legacy message handlers to feed stores automatically.
- Removing existing legacy caches or singleton-backed panels.
- Adding UI Toolkit panels.
- Editing generated protocol files.

## Technical Notes

- Store write APIs can be `internal` and covered by
  `InternalsVisibleTo("Panoptes.Tests.EditMode")`.
- Tests may construct stores directly; they do not need a Unity scene or
  VContainer runtime.
- If Store snapshots wrap DTO/reference data, clone objects and collections on
  both write and read boundaries.
