# client store hydration migration

## Goal

Move the client runtime data flow from legacy cache-only hydration toward the
final reactive presentation architecture. Server pushes should hydrate Core
Stores, and migrated ViewModels/Binder slices should receive live runtime data
without depending on legacy cache singletons.

## What I Already Know

* Phase 0-8 established the target architecture: VContainer scopes, R3 Stores,
  ViewModels, explicit uGUI/UI Toolkit Binders, and command services.
* Migrated panels now read `GameStateStore`, `StaticCatalogStore`,
  `PlanningDraftStore`, `TurnStore`, `SelectionStore`, and `PlanningToolStore`.
* Real server messages still primarily hydrate legacy caches such as
  `GameStateCache`, `StaticCatalogCache`, and `PlanningDraftCache`.
* The biggest migration gap is the missing message-to-Store hydration path:
  `ServerFrame / MessageDispatcher -> Protocol Mapper -> Store Hydrator ->
  Store -> ViewModel -> Binder`.

## Assumptions

* This task should preserve legacy cache hydration until migrated views are
  proven live from Stores.
* Store hydration may initially mirror cache updates rather than delete caches.
* No generated protocol files should change.
* Presentation must not reference `Panoptes.Protocol` or
  `NetworkManager.Instance`.

## Requirements

* Identify server messages and legacy cache paths needed to hydrate current
  migrated Store consumers.
* Add Core-level hydration/mapper code so live runtime messages update Stores.
* Keep Store state as Core DTOs, not generated Protocol or legacy nested JSON
  types.
* Wire hydrators through final VContainer scopes rather than Presentation
  singleton lookups.
* Add tests for hydration from representative server events into Stores.
* Keep existing legacy UI/cache behavior intact during migration.

## Acceptance Criteria

* [x] `GameStateStore`, `PlanningDraftStore`, `StaticCatalogStore`, and
      `TurnStore` receive live data from runtime/server message flow or a
      clearly bounded bridge.
* [x] Migrated ViewModels no longer depend on manually seeded test-only Stores
      at runtime.
* [x] Legacy cache hydration continues to work for non-migrated panels.
* [x] New hydration code lives in Core/Application or Core/Infrastructure and
      is registered through VContainer.
* [x] Presentation remains free of generated Protocol references and direct
      `NetworkManager.Instance` calls.
* [x] EditMode/static boundary tests cover representative hydration paths.
* [x] `dotnet build`, `dotnet test`, static boundary checks, and Unity
      batchmode compile pass.

## Definition Of Done

* Tests added/updated.
* Static boundary checks pass.
* Unity script import/compile succeeds.
* Trellis context validates.
* Work committed.

## Out Of Scope

* Removing legacy caches entirely.
* Rewriting all old uGUI panels.
* Changing server protocol.
* Adding gameplay legality validation on the client.

## Technical Notes

* Key composition file:
  `client/Assets/Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs`
* Current legacy message registration is concentrated in `AppManager`,
  `GameMessageHandler`, and `LobbyMessageHandler`.
* Current migrated Stores live under
  `client/Assets/Scripts/Runtime/Core/Application/Stores`.
* First implementation uses a bounded `StoreHydrationCacheBridge`: it subscribes
  to legacy cache change events and hydrates current Store fields. It does not
  remove or replace legacy cache writes.
