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

## Out of Scope

- Rewriting every UI prefab in one pass.
- Removing Core legacy caches that are still used by message handlers or
  unmigrated UI.
- Server changes.
