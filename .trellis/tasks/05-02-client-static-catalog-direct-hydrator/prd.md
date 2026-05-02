# client static catalog direct store hydrator

## Goal

Move static catalog Store hydration into the project scope so catalog snapshots
and section sync completion update `StaticCatalogStore` from the AppManager
static catalog boundary without waiting for the game-scoped legacy cache bridge.

## What I Already Know

* `StaticCatalogStore` is registered in `ProjectLifetimeScope`.
* Game runtime Stores now use `StoreMessageHydrator`.
* `StoreHydrationCacheBridge` still hydrates static catalog data from
  `StaticCatalogCache` when the Game scope is built.
* `MsgStaticCatalogSnapshot` is a project/global catalog message and may arrive
  before any active game session exists.
* `GameEventSessionGate` currently allows catalog manifest/chunk/complete but
  does not explicitly allow `StaticCatalogSnapshot`.

## Assumptions

* First direct catalog slice should hydrate `MsgStaticCatalogSnapshot`.
* Section chunk JSON sync remains in `StaticCatalogCache` for now because it
  has richer local bundle behavior and atomic chunk assembly.
* The protocol snapshot schema is narrower than the generated local JSON
  catalog, so direct mapping should fill only available Core DTO fields.

## Requirements

* Add Core mapping from `StaticCatalogSnapshot` to `StaticCatalogState`.
* Add a project-scope Store hydrator for static catalog snapshots/cache data.
* Register the hydrator through VContainer `ProjectLifetimeScope` and attach it
  to `AppManager`.
* Allow static catalog snapshots through the session gate before Game init.
* Keep Presentation free of protocol and legacy cache dependencies.
* Add EditMode tests for mapper, hydrator register/unregister, and gate behavior.

## Acceptance Criteria

* [x] `MsgStaticCatalogSnapshot` hydrates `StaticCatalogStore` in project scope.
* [x] Direct static catalog hydrator does not call legacy cache singletons.
* [x] Static catalog snapshots pass `GameEventSessionGate` without active game
      session.
* [x] Legacy `StaticCatalogCache` handling remains intact for local bundle and
      section sync behavior.
* [x] Tests cover mapper and Store hydration behavior.
* [x] `dotnet build`, `dotnet test`, static boundary checks, Unity batchmode
      compile, and focused Unity EditMode tests pass.

## Definition Of Done

* Tests added/updated.
* Docs/plan updated.
* Static boundary checks pass.
* Unity script import/compile succeeds.
* Trellis context validates.
* Work committed.

## Out Of Scope

* Removing `StaticCatalogCache`.
* Directly hydrating Store from static catalog section chunks.
* Extending protocol schema.
* Reworking UI catalog layouts.

## Technical Notes

* Target direct flow:
  `MessageDispatcher -> AppManager static catalog handlers ->
  StaticCatalogCache -> StaticCatalogStoreHydrator -> StaticCatalogStore`.
* Existing game-scope bridge remains for cache/local bundle fallback:
  `StaticCatalogCache -> StoreHydrationCacheBridge -> StaticCatalogStore`.
