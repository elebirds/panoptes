# client direct store hydrator migration

## Goal

Move the next slice of client runtime state migration from legacy cache event
mirroring toward direct Core message hydration. Game-scoped runtime server
messages should update reactive Stores through a Core-owned hydrator while the
existing cache bridge remains as the initial-scene and legacy-panel fallback.

## What I Already Know

* Phase 9 added `StoreHydrationHelper` and `StoreHydrationCacheBridge`.
* The bridge makes migrated ViewModels live, but still depends on legacy cache
  events.
* Current message registration is split between `AppManager` for global/game
  init/catalog messages and `GameMessageHandler` for in-game messages.
* `GameLifetimeScope` can resolve the project-owned `MessageDispatcher` and
  game-owned Stores.
* `MsgGameInit` may arrive before the Game scene scope exists, so the bridge
  must stay temporarily to seed Stores from cache after scene transition.

## Assumptions

* First direct hydrator slice should focus on game-runtime messages after the
  Game scene scope exists.
* Static catalog direct hydration can be a later project-scope slice.
* Legacy cache writes and legacy UI must keep working during this migration.
* No generated protocol files should change.

## Requirements

* Add a Core-owned direct message hydrator registered through VContainer.
* Hydrate `GameStateStore`, `PlanningDraftStore`, and `TurnStore` directly from
  representative in-game protocol messages.
* Keep direct hydrator code out of Presentation and avoid `*.Instance` cache
  lookups.
* Preserve the cache bridge as a bounded fallback for `MsgGameInit` already
  processed before the Game scope exists.
* Add EditMode coverage for direct message hydration and unregister behavior.

## Acceptance Criteria

* [x] In-game messages such as planning start, planning snapshot, game sync,
      token result, reveal result, preview responses, and game over can update
      Stores without waiting for cache event mirroring.
* [x] Direct hydrator is owned by `GameLifetimeScope`.
* [x] Direct hydrator does not call legacy cache singletons.
* [x] Legacy cache bridge remains available for initial seeding and old panels.
* [x] Presentation remains free of generated Protocol references and direct
      `NetworkManager.Instance` calls.
* [x] Tests cover representative direct hydration paths.
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

* Removing `StoreHydrationCacheBridge`.
* Removing `GameStateCache`, `PlanningDraftCache`, or legacy message handlers.
* Project-scope static catalog direct hydrator.
* Changing protocol or server behavior.

## Technical Notes

* Direct flow target:
  `MessageDispatcher -> StoreMessageHydrator -> StoreHydrationHelper -> Store`.
* Existing legacy flow remains:
  `MessageDispatcher -> GameMessageHandler/AppManager -> cache ->
  StoreHydrationCacheBridge -> Store`.
* Hydrator may merge partial messages with current Store snapshots because some
  responses only contain tokens or one changed node.
