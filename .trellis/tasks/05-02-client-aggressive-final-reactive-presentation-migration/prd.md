# client aggressive final reactive presentation migration

## Goal

Switch from incremental compatibility cleanup to a broad final-architecture
migration of the Unity client presentation layer. The target is the intended
Panoptes Client Reactive Presentation Architecture: VContainer owns lifetime,
Core Stores are the read model, ViewModels project UI state, and Binders/facades
render without reaching through legacy cache singletons.

## User Direction

- The previous migration is too slow.
- Move directly toward the final architecture instead of compatibility-first
  bridge work.
- It is acceptable for intermediate states to temporarily fail compilation while
  the broader migration is being applied.

## Migration Policy For This Task

- Prefer larger vertical migrations over tiny debt slices.
- Do not preserve compatibility wrappers just to keep old singleton flows alive.
- Presentation should receive Stores/ViewModels/services through VContainer.
- Presentation must not use generated Protocol messages.
- UI should not call `NetworkManager.Instance`.
- Legacy caches may remain in Core for unmigrated message handling, but they
  should stop being the preferred Presentation dependency.

## Initial Target Surface

- Simple HUD/game panels that subscribe to `GameStateCache`.
- Minister and turn/report panels that can consume existing Store/ViewModel
  state.
- Remaining static catalog legacy bridge only where bundle/chunk sync has no
  direct Core replacement yet.

## Acceptance Criteria

- [x] Multiple Presentation components are moved from legacy cache lookup to
      injected Store/ViewModel/Core service dependencies.
- [x] New code follows Store -> ViewModel -> Binder/facade where a ViewModel is
      warranted.
- [x] Any temporary compile breaks are either resolved before commit or clearly
      recorded with the next exact repair step.
- [x] Documentation records the aggressive migration approach.

## Batch 1 Result

- Added direct `GameChatStore` and `GameOverStore` hydration in
  `StoreMessageHydrator`.
- Extended `TurnStore` state for HUD rendering.
- Migrated `TurnHUD`, `GameChatPanelController`, `GameOverOverlay`, and
  `GameSceneController` display paths toward injected Store/service
  dependencies.
- Unity briefly failed during the broad edit, then compile was restored before
  commit.

## Batch 2 Result

- Added `SettlementStore` / `SettlementState` and direct `MsgGameSync`
  settlement hydration.
- Migrated `SettlementTimeline` and `TurnReportPanel` from
  `GameStateCache.OnTurnSettled` to injected Store subscriptions.
- Migrated the `GameSceneController` technology-completion toast projection to
  `SettlementStore + GameStateStore`.
- Extended local session reset and Store snapshot cloning to include settlement
  state.

## Batch 3 Result

- Added `GameplayFeedbackStore` / `GameplayFeedbackState` for toast-style
  runtime feedback.
- Extended `StoreMessageHydrator` to publish Problem frames, token failures,
  and planning command failures into the feedback Store.
- Migrated `GameIntentService` ActionLock release to `TurnStore + GameOverStore`
  subscriptions.
- Removed the remaining `GameStateCache` dependency from `GameSceneController`.

## Batch 4 Result

- Removed `MapPlanningCacheEventBridge` and its Unity meta file.
- Migrated `MapPlanningInputController` off `GameStateCache` /
  `PlanningDraftCache` event subscriptions and onto injected `GameStateStore`,
  `PlanningDraftStore`, `SettlementStore`, and `GameplayFeedbackStore`.
- Added sequence tracking for `SettlementStore` state so map settlement playback
  handles each pushed settlement once instead of relying on cache callbacks.
- Extended feedback Store details for token/build failures so map planning can
  rollback pending deploy/build/move presentation state without legacy planning
  result events.
- Moved map move/build placement helper preview reads from `PlanningDraftCache`
  to `PlanningDraftState` snapshots supplied by the controller.

## Batch 5 Result

- Removed the remaining Store compatibility seed/event-adapter classes and
  their game-scope composition registrations.
- Added a direct Core static catalog Store hydrator used by protocol snapshot
  hydration and AppManager static catalog sync boundaries.
- Updated AppManager static catalog handling to write `StaticCatalogStore`
  after local manifest comparison, section sync completion, and server snapshot
  application without subscribing a Store writer to cache change events.
- Replaced compatibility seed/event-adapter EditMode tests with direct static
  catalog Store hydrator tests.

## Batch 6 Result

- Removed `UnitInfoReactiveBridge` and its Unity meta file.
- Updated `UnitInfoPanelController` to directly own the
  `UnitInfoUguiBinder + UnitInfoViewModel` binding lifecycle without adding a
  replacement bridge or facade.
- Preserved the existing `UnitInfoPanelController` type and serialized field
  names while removing bridge-specific methods and fields.
- Updated migrated UnitInfo boundary coverage to scan only the ViewModel/Binder
  path for Protocol, legacy cache, and singleton `Instance` usage, plus an
  explicit guard that the bridge file stays deleted.

## Batch 7 Result

- Removed `SceneCommandServiceInjector` and its Unity meta file.
- Removed all `SceneCommandServiceInjector.InjectIfAvailable(...)` calls from
  migrated Presentation components instead of introducing another injection
  wrapper/helper/facade.
- Registered the current `[Inject]` scene/prefab-mounted Presentation
  components through `ClientCompositionInstaller.RegisterGame` with
  `RegisterComponentInHierarchy`, covering map planning, chat, game-over, turn
  HUD, recipe synthesis, settlement timeline/report, tech tree, minister, game
  scene, and unit info controllers.
- Removed the remaining `GameSceneController` resolver-based helper injection;
  any legacy runtime helper objects it creates are injected only through the
  registered VContainer scene component types.
- Removed `TechTreePanelBootstrap` and its Unity meta file. The final
  architecture requires `TechTreePanelController` to be mounted on the authored
  prefab or scene instance; runtime dynamic controller creation is no longer a
  supported compatibility path.
- Updated composition boundary coverage so the injection shell stays deleted
  and Presentation composition cannot reintroduce
  `LifetimeScope.Find<GameLifetimeScope>()` or `InjectGameObject` manual
  injection patterns.

## Batch 8 Result

- Removed `UnitInfoPanelController` direct `PlanningDraftCache`,
  `GameStateCache`, and `StaticCatalogCache` usage and its cache event
  subscriptions.
- Replaced UnitInfo fallback rendering with the final
  `UnitInfoViewModel + UnitInfoUguiBinder` render path for display text, HP,
  planning summary, and direct-order state.
- Injected `MapPlanningInputController` via VContainer, keeping the serialized
  field as the scene-authored override and dropping singleton lookup fallback.
- Removed legacy UnitInfo planning summary, HP resolver, and direct-order
  resolver files plus their tests, leaving only the binder state DTOs.
- Expanded UnitInfo boundary tests to cover `UnitInfoPanelController` against
  Protocol, legacy caches, singleton `Instance`, and direct network usage.

## Out of Scope

- Rewriting every UI prefab in one pass.
- Removing Core legacy caches that are still used by message handlers or
  unmigrated UI.
- Server changes.
