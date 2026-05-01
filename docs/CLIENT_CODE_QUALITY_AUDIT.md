# Panoptes Client Code Quality Audit

> Baseline captured on branch `codex/axial-coordinates` after commit `0c4ada8`.
> Scope: `client/Assets/Scripts/Runtime`.

## Summary

The client runtime currently has 150 C# files and 43,440 lines. The Core /
Presentation assembly boundary is mostly intact: Presentation does not reference
`Panoptes.Protocol`, and UI scripts do not call `NetworkManager.Instance`
directly. The highest risks are concentrated in a small set of very large
Presentation components that combine scene bootstrapping, data lookup, runtime
UI construction, input modes, and presentation state.

## High-Risk Files

| File | Lines | Primary risk |
| --- | ---: | --- |
| `Presentation/Map/MapPlanningInputController.cs` | 3376 | Unity scene entry is now correctly named for map-scoped planning input, but it still coordinates selection, build placement, combat targeting, previews, and backend playback. |
| `Presentation/Map/MapRenderer.cs` | 2320 | Map source selection, debug map generation, node rendering, unit rendering, and camera context publishing are mixed. |
| `Presentation/UI/HUD/UnitInfoPanelController.cs` | 2169 | Panel orchestration, portrait rendering, runtime layout repair, action buttons, and planning summary are mixed. |
| `Presentation/UI/Domestic/BuildCommandPanel.cs` | 1881 | Catalog reading, fallback JSON parsing, list rendering, scroll state, tooltip binding, and command dispatch are mixed. |
| `Presentation/Map/SquadUnitVisualController.cs` | 1788 | Squad visual state and animation concerns are large enough to need a later focused pass. |
| `Core/Application/Cache/GameStateCache.cs` | 1542 | Central state cache is large, but it is the intended state mirror boundary and should be split only after Presentation risk is reduced. |

## Scene Lookup Hotspots

Presentation still contains scattered fallback scene lookup:

- `Resources.FindObjectsOfTypeAll<T>()` appears in `ResourceHUD`,
  `CityCoreBuildingActionRegistrar`, `TurnHUD`, and `TechTreePanelBootstrap`.
- `FindAnyObjectByType` / `FindObjectsByType` appears across map, HUD, domestic
  UI, and game scene bootstrap classes.
- Several callers need the same rule: fallback lookup may return only valid
  scene instances, never prefab/assets. This rule should live in one helper.

Priority: continue replacing fallback lookup with `SceneObjectFinder` when files
are touched, and prefer serialized scene references for stable scene wiring.

## Layer Boundary Checks

- `Presentation` to `Protocol`: no direct `Panoptes.Protocol` references found
  under `client/Assets/Scripts/Runtime/Presentation`.
- UI to network: no `NetworkManager.Instance` usage found under
  `client/Assets/Scripts/Runtime/Presentation/UI`.
- Existing command flow is still routed through Core services/intents such as
  `MessageSender`, `LobbyService`, and `GameIntents`.

These are healthy constraints and must be preserved during restructuring.

## Subscription and Lifecycle Risks

Several Presentation components subscribe directly to singleton cache events in
`OnEnable` and unsubscribe in `OnDisable`. The pattern is correct in intent but
implemented manually in many files, which increases the chance of duplicate
subscriptions or stale source references when singleton objects are recreated in
tests.

Priority targets:

- `UnitInfoPanelController`
- `BuildCommandPanel`
- `ResourceHUD`
- `TurnHUD`
- `RecipeSynthesisPanel`

## Test Baseline

Current EditMode coverage exists under `client/Assets/Scripts/Tests/EditMode`
and includes integration coverage for runtime scene bindings, build panels,
common overlays, tech tree state, feedback, fonts, map/grid behavior, and lobby
state reduction.

The restructuring should keep the existing Unity EditMode suite as the primary
regression gate. New tests should be added for new reusable helpers and for any
extracted collaborator with non-trivial behavior.

## Recommended Order

1. Centralize scene lookup helpers and replace duplicated fallback lookup.
2. Stabilize UI subscription/lifecycle patterns where they are already being
   touched.
3. Split `UnitInfoPanelController` into portrait, planning summary, action
   button, and layout collaborators.
4. Split `BuildCommandPanel` into model building, list rendering, scroll state,
   and fallback config parsing.
5. Continue slimming `MapPlanningInputController` by moving more mode behavior
   into `Presentation/Planning/Input`.
6. Split `MapRenderer` by source resolution, debug data generation, node
   rendering, unit rendering, and camera context publishing.

Every step must preserve public MonoBehaviour entry points and serialized field
compatibility unless scenes/prefabs are updated and verified in the same commit.

## Restructuring Outcome

Implemented through commits `f8d64cf`..`3716438`, the final MapRenderer pass,
and the later planning-input package cleanup:

- Added shared Presentation infrastructure:
  - `SceneObjectFinder` for scene-valid fallback lookup.
  - `EventSubscriptionBag` for deterministic UI event unsubscribe.
- Split UnitInfo responsibilities into dedicated helpers:
  - `UnitInfoPlanningSummaryPresenter`
  - `UnitInfoActionButtonBinder`
  - `UnitInfoPanelLayoutBuilder`
  - `UnitInfoPortraitPresenter`
- Split BuildCommandPanel responsibilities:
  - `BuildPanelScrollState`
  - `BuildConfigFallbackParser`
- Renamed the map-scoped player input entry from `MapInputHandler` to
  `MapPlanningInputController`.
- Removed the transitional `Presentation/Map/Input` package.
- Added `Presentation/Map/InputAdapter` for map-owned pointer, UI hit-test,
  raycast, and node/unit surface resolution.
- Added `Presentation/Planning` for planning-owned input helpers:
  - `Feedback/BuildPreviewPresenter`
  - `Feedback/MovePreviewPresenter`
  - `Feedback/MapInputTokens`
  - `Input/IPlanningInputMode`
  - `Input/PlanningInputCoordinator`
  - `Input/Intents/IPlanningIntentSender`
  - `Input/State/PendingMoveState`
  - `Input/State/PendingBuildState`
  - `Input/State/PendingDeployState`
- Split MapRenderer debug generation:
  - `DebugMapFactory`

Post-pass line-count snapshot:

| File | Before | After |
| --- | ---: | ---: |
| `Presentation/Map/MapPlanningInputController.cs` | 3565 | 3376 |
| `Presentation/Map/MapRenderer.cs` | 2320 | 2014 |
| `Presentation/UI/HUD/UnitInfoPanelController.cs` | 2169 | 2009 |
| `Presentation/UI/Domestic/BuildCommandPanel.cs` | 1881 | 1633 |
| `Presentation/UI/HUD/CityCoreBuildingActionRegistrar.cs` | 1105 | 1063 |

Layer boundary checks remain clean:

- No direct `Panoptes.Protocol` references under Presentation.
- No `NetworkManager.Instance` usage under Presentation UI.

Remaining technical debt:

- `MapPlanningInputController`, `MapRenderer`, `UnitInfoPanelController`, and
  `BuildCommandPanel` are still large. The current package structure now makes
  the next split direction explicit: map rendering and raycast adapters stay
  under `Presentation/Map`, while player planning input, pending state, intent
  ports, and feedback live under `Presentation/Planning`.
- `SquadUnitVisualController` remains a large untouched Presentation component
  and should receive a focused animation/visual-state pass later.
- Presentation still has fallback object lookup in several scene bootstrap paths;
  future work should replace more of these with serialized references or
  `SceneObjectFinder`.

## C0p Preflight Outcome

Implemented on 2026-05-01 as the C0a entry cleanup pass:

- Added static EditMode boundary coverage for:
  - no direct `Panoptes.Protocol` references under `Runtime/Presentation`;
  - no direct `NetworkManager.Instance` usage under `Runtime/Presentation/UI`;
  - high-risk Presentation line-count baseline.
- Improved prefab-ready lifecycle/binding:
  - `GameSceneController` now uses `SceneObjectFinder` for runtime helper lookup.
  - `ResourceHUD` and `TurnHUD` use `EventSubscriptionBag` for button/event binding.
- Added facade-friendly helpers:
  - `BuildCommandListRenderer`
  - `RecipeSynthesisRenderedItemRegistry`
  - `MoveSelectionInputMode` / `TerritoryDeployInputMode` helper methods
  - `MapSourceResolver`, `MapSourceSnapshot`, `MapCameraContextBuilder`, `MapRenderTokens`
  - `SquadUnitRenderBudgetPresenter`
  - `CityCoreBuildingActionResolver`
  - `GameStateCacheReadQueries`

Post-C0p line-count snapshot:

| File | Before C0p | After C0p |
| --- | ---: | ---: |
| `Presentation/Map/MapPlanningInputController.cs` | 3376 | 3308 |
| `Presentation/Map/MapRenderer.cs` | 2014 | 1514 |
| `Presentation/UI/Domestic/BuildCommandPanel.cs` | 1633 | 1497 |
| `Presentation/UI/Turn/RecipeSynthesisPanel.cs` | 1392 | 1377 |
| `Presentation/UI/HUD/CityCoreBuildingActionRegistrar.cs` | 1063 | 896 |
| `Presentation/Map/SquadUnitVisualController.cs` | 1788 | 1675 |
| `Core/Application/Cache/GameStateCache.cs` | 1542 | 1352 |

Remaining large files are now treated as prefab-facing facades. Future C0a work
should add new backend bindings through Core DTO/cache APIs and the extracted
helpers instead of re-expanding these MonoBehaviours.

## Reactive UI Target Plan

Recorded on 2026-05-01 in
`docs/2026-05-01-client-reactive-ui-architecture-plan.md`.

Long-term target:

- VContainer owns project/game scene lifetimes.
- R3 owns state propagation from stores to ViewModels.
- UniTask owns async login/connect/catalog/request flows.
- UI Toolkit is introduced for information-heavy management panels.
- uGUI remains for map HUD, overlays, and world-space UI.
- ViewModels own projection and commands; explicit Binders connect UXML/USS or
  prefab references to those ViewModels.

This is a target plan, not the current implemented stack. Current dependency
policy still needs an explicit AGENTS/frontend-doc amendment before new packages
are installed.

Current script refactor priority before and during C0a:

| Priority | Script / Area | Direction |
| --- | --- | --- |
| High | `MapPlanningInputController` | Split into planning tool coordinator, input modes, and preview presenter |
| High | `UnitInfoPanelController` | Split into ViewModel, stats binder, action list renderer, and selection binder |
| High | `MapRenderer` | Split into map render coordinator plus node/unit/building/fog/overlay renderers |
| High | `BuildCommandPanel` | Use as UI Toolkit/ViewModel pilot candidate or shrink behind a list binder |
| High | `MinisterDto` | Move protocol conversion out of Foundation before minister UI work |
| Medium | `SquadUnitVisualController` | Split health/flag/model/path/animation binders |
| Medium | `RecipeSynthesisPanel` | Move to ViewModel + item lifecycle binder, then consider UI Toolkit |
| Medium | `GameStateCache` / `StaticCatalogCache` | Split into domain stores/read models after R3 is introduced |

## Script Architecture Refactor Phase 1

Implemented on 2026-05-01 as the first post-plan cleanup slice:

- Moved minister draft protocol conversion from `MinisterDraftDto` into
  `MinisterMapper`.
- Removed `Panoptes.Protocol` and `JsonUtility` usage from
  `Core/Foundation/Domain/MinisterDto.cs`.
- Updated `PlanningDraftCache` to consume the mapper path.
- Extended static boundary coverage so `Runtime/Core/Foundation` cannot
  reintroduce generated protocol references.

Next script refactor candidates remain `MapPlanningInputController`,
`UnitInfoPanelController`, `MapRenderer`, and `BuildCommandPanel`.

## Script Architecture Refactor Phase 2

Implemented on 2026-05-01 as the second post-plan cleanup slice:

- Extracted `BuildCommandPanelViewResolver` from `BuildCommandPanel`.
- Moved tooltip, ScrollRect, viewport, content, group template, and item
  template resolution out of the panel facade.
- Added EditMode coverage for generating missing viewport/content references.
- Reduced `BuildCommandPanel.cs` from 1497 lines after C0p to 1307 lines.

`BuildCommandPanel` is still a UI Toolkit/ViewModel pilot candidate, but its
Unity view plumbing is now isolated from build item state construction.

## Script Architecture Refactor Phase 3

Implemented on 2026-05-01 as the third post-plan cleanup slice:

- Extracted `UnitInfoDirectOrderStateResolver` from `UnitInfoPanelController`.
- Moved direct order state derivation for move/attack/hold/charge out of the
  HUD facade.
- Added EditMode coverage for civilian, charge-capable military, and
  building/resource direct order states.
- Reduced `UnitInfoPanelController.cs` from 2009 lines after C0p to 1935 lines.

`UnitInfoPanelController` still needs further passes for stats binding, action
list rendering, portrait camera ownership, and slide/docking layout, but direct
order availability is now isolated and testable.

## Script Architecture Refactor Phase 4

Implemented on 2026-05-01 as the fourth post-plan cleanup slice:

- Extracted `UnitInfoHpStateResolver` from `UnitInfoPanelController`.
- Extracted `UnitInfoHpBinder` for Slider/TMP HP rendering.
- Added EditMode coverage for fallback HP clamping, building max HP, resource
  point max HP, and max HP not dropping below current HP.
- Reduced `UnitInfoPanelController.cs` from 1935 lines after Phase 3 to 1869
  lines.

`UnitInfoPanelController` still needs future passes for action list rendering,
portrait camera ownership, and slide/docking layout.

## UnitInfo Final Structure Refactor

Implemented on 2026-05-01 as the less-conservative UnitInfo facade pass:

- Extracted `UnitInfoPanelSlideAnimator` from `UnitInfoPanelController`.
- Moved slide/open/close, docking, hidden position, and external offset
  animation state out of the HUD facade.
- Extracted `UnitInfoPortraitCameraLifecycle` from `UnitInfoPanelController`.
- Moved portrait camera, render texture, fill light, camera pose, rendering,
  disable, and release lifecycle out of the HUD facade.
- Added EditMode coverage for slide positioning/offset behavior and portrait
  visibility/render texture release behavior.
- Reduced `UnitInfoPanelController.cs` from 1869 lines after Phase 4 to 1516
  lines.

`UnitInfoPanelController` now reads more like a prefab-facing orchestration
facade. Remaining UnitInfo work should focus on action list rendering and a
future ViewModel/selection binder, rather than re-expanding portrait, HP,
planning summary, or slide state inside the controller.

## UnitInfo Action List Refactor

Implemented on 2026-05-01 as the next UnitInfo facade pass:

- Extracted `UnitInfoActionListBinder` from `UnitInfoPanelController`.
- Moved generic action slot creation, required-slot repair, button visual
  repair, provider registration, click binding, and action visibility refresh
  out of the HUD facade.
- Preserved the old nested `ActionButtonSlot` serialized element type as a
  compatibility wrapper, because authored prefabs already contain
  `actionButtons` data.
- Added EditMode coverage for required-slot creation, duplicate prevention,
  serialized-compatible slot preservation, listener replacement, inactive
  provider registration, and button visual repair.
- Reduced `UnitInfoPanelController.cs` from 1516 lines after the runtime helper
  pass to 1287 lines.

Remaining UnitInfo work should focus on a ViewModel/selection binder and
possibly description/catalog text projection. Generic action-list mechanics
should stay in `UnitInfoActionListBinder`.

## UnitInfo Direct Order Panel Refactor

Implemented on 2026-05-01 as the direct-order panel cleanup pass:

- Extracted `UnitInfoDirectOrderPanelBinder` from
  `UnitInfoPanelController`.
- Moved direct-order button creation, default visuals, listener binding, and
  move/attack/hold/charge render-state application out of the HUD facade.
- Preserved existing serialized direct-order button fields on
  `UnitInfoPanelController` for prefab compatibility.
- Added EditMode coverage for default button creation, listener replacement,
  hidden-state rendering, civilian move-only rendering with `ActionLock`, and
  military action visibility/interactability.
- Reduced `UnitInfoPanelController.cs` from 1287 lines after the action-list
  pass to 1206 lines.

Remaining UnitInfo work should focus on selection/ViewModel boundaries and
catalog-backed description projection. Direct-order UI mechanics should stay in
`UnitInfoDirectOrderPanelBinder`.

## UnitInfo Default Layout Builder Refactor

Implemented on 2026-05-01 as the default-layout cleanup pass:

- Extracted `UnitInfoDefaultLayoutBuilder` from `UnitInfoPanelController`.
- Moved runtime default uGUI layout construction for canvas, background,
  roots, icon, portrait placeholder, text controls, HP controls, default action
  slots, and direct-order buttons out of the HUD facade.
- Kept `UnitInfoPanelController` responsible for assigning returned serialized
  references, binding portrait textures, planning summary presenter, and
  direct-order listeners.
- Added EditMode coverage for default canvas/control creation, action/direct
  order defaults, portrait layout mirroring, and prefab-reference early-return
  behavior.
- Reduced `UnitInfoPanelController.cs` from 1206 lines after the direct-order
  pass to 941 lines.

`UnitInfoPanelController` is now mostly orchestration. Remaining work should
focus on selection/ViewModel boundaries and catalog-backed display text
projection.

## MapPlanning Move Preview Ghost Refactor

Implemented on 2026-05-01 as the first MapPlanning final-structure slice:

- Extracted `MovePreviewGhostPresenter` from `MapPlanningInputController`.
- Moved runtime move ghost object creation, clone/proxy visual setup, material
  ownership, animation, removal, and cleanup out of the map input facade.
- Kept all move ghost serialized fields on `MapPlanningInputController` for
  scene/prefab compatibility; the facade now passes them through a presenter
  settings struct.
- Added EditMode coverage for the presenter namespace and deterministic cleanup
  API.
- Reduced `MapPlanningInputController.cs` from 3308 lines after C0p to 2931
  lines.

`MapPlanningInputController` is still the largest remaining Presentation
facade. The next MapPlanning pass should focus on either build placement mode
ownership or combat/move click routing, rather than re-expanding preview object
lifecycle inside the controller.
