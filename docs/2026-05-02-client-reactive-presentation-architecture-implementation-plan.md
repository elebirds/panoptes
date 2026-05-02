# Panoptes Client Reactive Presentation Architecture Implementation Plan

> Date: 2026-05-02
> Status: Implementation plan
> Strategy: Direct final architecture for migrated modules; no compatibility
> Composition Root.

## Decision

Panoptes client development should stop treating the reactive presentation
architecture as a distant target and begin implementing it as the default
structure for C0a and later work.

The final data and command flow is:

```text
Protocol / WebSocket
  -> Protocol Mapper
  -> Client Store / Read Model
  -> Reactive ViewModel
  -> Explicit Binder
  -> UI Toolkit UXML/USS or uGUI Prefab

Player Input
  -> ViewModel Command
  -> Application Service / Intent
  -> MessageSender
  -> Server
```

The client remains a pure presentation layer. The server is authoritative for
rules, legality, combat, economy, construction, and turn settlement.

## Migration Rule

Do not create a compatibility-style Composition Root that wraps singleton
lookups as a parallel architecture.

Instead:

- Migrated modules are owned by final VContainer scopes.
- New Stores, Services, ViewModels, and Binders must not actively call
  `*.Instance`.
- Existing singleton-backed modules may remain temporarily, but they are legacy
  islands, not dependencies for new architecture modules.
- When a module migrates, its dependency ownership moves to the final scope in
  the same slice.
- No new backend binding should be added directly to high-risk facade scripts.

This is intentionally sharper than a gradual bridge: it avoids teaching every
new module both the old and new dependency models.

## Approved Stack

| Concern | Decision | Locked Version / Source | Role |
|---|---|---|---|
| Lifecycle / DI | VContainer | `jp.hadashikick.vcontainer` `1.17.0` | Project/game scopes, constructor injection, scene lifetime ownership |
| State propagation | R3 | `com.cysharp.r3` `1.3.0` plus vendored NuGet core DLL | Store and ViewModel reactive state |
| Async | UniTask | `com.cysharp.unitask` `2.5.10` | Unity-native async initialization, requests, catalog load, scene flow |
| Dense UI | UI Toolkit | Unity built-in `com.unity.modules.uielements` | Minister, tech, policy, ledger, summaries, debug/admin |
| Map/world UI | uGUI | `com.unity.ugui` | Map HUD, world-space labels, overlays, previews, unit/building bars |

DOTween remains out of scope. Animation should continue using existing Unity
animation/coroutine code until a dedicated animation dependency decision is made.

## Target Directories

```text
client/Assets/Scripts/Runtime/Core/
  Foundation/
    Domain/
  Infrastructure/
    Network/
    Mapper/
  Application/
    Stores/
    Services/
    Intents/
    UseCases/

client/Assets/Scripts/Runtime/Presentation/
  Composition/
  ViewModels/
  Binders/
    Ugui/
    UiToolkit/
  UI/
  Map/
  Planning/

client/Assets/UI/
  Toolkit/
    Minister/
    Tech/
    Policy/
    Turn/
    Shared/
```

## Layer Rules

```text
Core/Foundation
  Pure DTOs, value objects, enums.
  No Unity UI, no NetworkManager, final target no Protocol references.

Core/Infrastructure
  NetworkManager, MessageDispatcher, protocol frames, protocol mappers.
  May reference Panoptes.Protocol.

Core/Application
  Store, Service, Intent, UseCase.
  Owns read models and command submission.
  Does not know Unity UI.

Presentation/ViewModels
  Compose stores into panel/screen state.
  Own commands that call application services.
  Must not hold TMP_Text, Button, VisualElement, GameObject, or prefab refs.

Presentation/Binders
  MonoBehaviour or UIDocument-facing glue.
  Receives ViewModels/Services through VContainer.
  Renders state explicitly to uGUI or UI Toolkit.

Presentation/Views
  Authored uGUI prefabs and UXML/USS.
```

## Phase 0: Policy And Dependency Foundation

Goal: make the final stack legal and remove documentation contradictions.

Deliverables:

- Update `AGENTS.md`.
- Update `docs/PANOPTES_AGENT_FRONTEND.md`.
- Update Trellis frontend specs.
- Update `docs/2026-05-01-client-reactive-ui-architecture-plan.md` to point to
  this implementation plan.
- Pin approved dependency versions before any production use.
- Import R3 core DLLs required by `R3.Unity` under `client/Assets/Plugins/`.

Acceptance:

- Docs consistently approve VContainer/R3/UniTask/UI Toolkit under the final
  architecture.
- Zenject, UniRx, DOTween, Photon, Mirror, and paid plugins remain forbidden.
- No generated protocol files change.
- Unity batchmode compilation succeeds after dependency resolution.

## Phase 1: Final Structure And Contracts

Goal: create the shape new work will depend on.

Deliverables:

- `Core/Application/Stores`
- `Core/Application/Services`
- `Core/Application/UseCases`
- `Presentation/Composition`
- `Presentation/ViewModels`
- `Presentation/Binders/Ugui`
- `Presentation/Binders/UiToolkit`
- Minimal contracts:
  - `IReadOnlyStore<TState>`
  - `IViewModel<TState>`
  - `IBinder<TViewModel>`
  - `ICommandService`

Acceptance:

- Contracts do not reference Unity UI controls.
- Presentation still does not reference generated Protocol.
- New code compiles after packages are installed.

## Phase 2: Final Composition Root

Goal: establish final ownership for migrated modules.

Target scopes:

```text
ProjectLifetimeScope
  Network client
  Message sender
  Auth service
  Config store
  Static catalog store
  App navigation services

GameLifetimeScope
  Game state store
  Planning draft store
  Selection store
  Turn store
  Game intent services
  Panel ViewModels
```

Rules:

- Do not wrap legacy singletons into a "compatibility container" for new
  modules.
- Migrated Binders receive dependencies through VContainer injection.
- Legacy singletons are deleted or isolated as old-module dependencies when a
  module migrates.

Acceptance:

- At least one scene owns a real `LifetimeScope`.
- New architecture code paths do not call `GameStateCache.Instance`,
  `PlanningDraftCache.Instance`, or `StaticCatalogCache.Instance`.

Status 2026-05-02: complete. `Assets/Scenes/Game.unity` owns the scene-level
`GameLifetimeScope` through a `Game Composition` root, while
`ProjectLifetimeScope` remains startup-owned by the existing persistent
`Managers` bootstrap to avoid duplicate project scopes.

## Phase 3: Store / Read Model Layer

Goal: replace cache-as-UI-source with explicit stores for migrated modules.

Initial stores:

- `StaticCatalogStore`
- `GameStateStore`
- `PlanningDraftStore`
- `SelectionStore`
- `TurnStore`

Store rules:

- Store state is server/cache/catalog mirror data only.
- Store exposes read-only reactive state.
- Store snapshots return immutable or cloned data.
- Store does not compute gameplay legality.
- Store does not know Unity UI.

Acceptance:

- Stores have EditMode tests for snapshot immutability and update flow.
- Store write APIs are internal/application-facing, not UI-facing.

Status 2026-05-02: complete. The initial Core Store layer now includes
`StaticCatalogStore`, `GameStateStore`, `PlanningDraftStore`, `SelectionStore`,
and `TurnStore`. Store state uses independent Core read-model DTOs, exposes
R3-backed read-only state, keeps write APIs internal, and is registered in the
final VContainer scopes without wrapping legacy cache singletons.

## Phase 4: First Complete uGUI Migration

Recommended first target: `UnitInfoPanel`.

Target flow:

```text
GameStateStore + SelectionStore + StaticCatalogStore
  -> UnitInfoViewModel
  -> UnitInfoUguiBinder
  -> existing UnitInfo authored prefab/uGUI controls
```

Deliverables:

- `UnitInfoState`
- `UnitInfoViewModel`
- `UnitInfoUguiBinder`
- Commands routed through application services.

Acceptance:

- UnitInfo projection lives in ViewModel.
- Binder only renders TMP/Button/Slider state and binds UI events to commands.
- Binder does not inspect Protocol and does not call cache singletons.
- Existing UI behavior remains.

Status 2026-05-02: complete. `UnitInfoViewModel` now projects selected unit
display state from `GameStateStore`, `SelectionStore`, `StaticCatalogStore`,
and `PlanningDraftStore`; `UnitInfoUguiBinder` renders the uGUI text, HP, and
direct-order state; and `UnitInfoPanelController` remains the prefab-facing
facade for scene selection, animation, portrait camera, and legacy action
registrars. The migrated ViewModel/Binder slice is registered in
`GameLifetimeScope` through VContainer and is covered by EditMode/static
boundary tests. Runtime falls back to the legacy facade path until server
message hydration into the Phase 3 stores is completed.

## Phase 5: First UI Toolkit Read-Only Migration

Recommended first target: `TurnSummary` or `NationalOverview`.

Target flow:

```text
TurnStore / GameStateStore
  -> TurnSummaryViewModel
  -> TurnSummaryUiToolkitBinder
  -> UIDocument + UXML + USS
```

Rules:

- Use explicit Binder rendering first.
- Do not enable Unity automatic data binding for gameplay state in this phase.
- UI Toolkit panel is read-only or local-form-only.

Acceptance:

- Runtime UI Toolkit panel renders from ViewModel state.
- UXML/USS names are stable and documented.
- uGUI and UI Toolkit share Store/ViewModel style.

Status 2026-05-02: complete. `TurnSummaryViewModel` projects read-only turn
status from `TurnStore` and `GameStateStore`; `TurnSummaryUiToolkitBinder`
renders a runtime `UIDocument` through explicit named element lookup; and
`TurnSummary.uxml` / `TurnSummary.uss` define the first stable UI Toolkit asset
pair under `Assets/UI/Toolkit/Turn`. The binder is registered in
`GameLifetimeScope` on a new runtime GameObject and remains read-only. No Unity
automatic data binding is used.

## Phase 6: Command Service Migration

Goal: standardize player command submission.

Services:

- `GameIntentService`
- `PlanningIntentService`
- `MinisterCommandService`

Command flow:

```text
Button / click
  -> ViewModel.Command()
  -> Service
  -> MessageSender
  -> Server
```

Acceptance:

- UI no longer builds protocol/request payloads directly.
- ViewModels do not reference Protocol.
- Services do not reference Unity UI controls.

Status (2026-05-02):

- Added injectable Core command services:
  `GameIntentService`, `PlanningIntentService`, and
  `MinisterCommandService`.
- Migrated Presentation command callers away from static `GameIntents` for
  turn submit, chat emotes, research selection, recipe commands, minister
  directives, and map planning commands.
- Command services send through `IClientMessageSender`; Protocol construction
  stays in Core.
- Legacy `GameIntents` remains for debug tooling and old non-migrated Core
  paths only.

## Phase 7: Map Input Re-Architecture

Goal: replace `MapPlanningInputController` as the planning input brain.

Target shape:

```text
MapInputBinder
PlanningToolViewModel
SelectionStore
PlanningDraftStore
MoveCommandService
BuildCommandService
CombatCommandService

World feedback:
  MovePreviewPresenter
  BuildPlacementPresenter
  CombatTargetingPresenter
```

Rules:

- Raycast/world object resolution stays in Map adapters or Binders.
- Selection state moves to `SelectionStore`.
- Tool/mode state moves to `PlanningToolViewModel`.
- Preview state comes from `PlanningDraftStore`.
- Commands go through services.

Acceptance:

- `MapPlanningInputController` is removed or reduced to a Binder/Adapter.
- Planning mode state no longer lives as scattered MonoBehaviour fields.

Status (2026-05-02):

- Added final Core map input state ownership with `PlanningToolStore`,
  `PlanningToolState`, `PlanningToolMode`, and `PlanningToolService`.
- Added `PlanningToolViewModel` / `PlanningToolViewState` so UI-facing prompt
  and mode projection are derived from `PlanningToolStore + SelectionStore`.
- Registered the planning tool stack in `ClientCompositionInstaller.RegisterGame`.
- Wired `MapPlanningInputController` as the current Unity map adapter: it still
  owns raycast/world feedback, but publishes selection IDs through
  `SelectionService` and tool/preview transitions through `PlanningToolService`.
- Extracted `MapPlanningInputStateAdapter` so the scene controller no longer
  directly stores planning tool / selection / ViewModel service references.
- Extracted `MapPlanningInputCoordinator` and `MapBuildPlacementCoordinator`
  for build/combat click routing; `MapPlanningInputController` now delegates
  routing order through narrow context interfaces.
- Extracted `MapNodeInfoProxyFactory` and `MapBuildingCatalogResolver`, moving
  node info proxy construction and building catalog alias resolution out of the
  scene input controller.
- Extracted move/combat presentation helpers: `MapMovePreviewPresentationController`,
  `MapMovePreviewRenderPlanner`, `MapAttackRangePresenter`, and
  `MapCombatTargetingResolver`, so move overlays, move ghost presentation,
  attack range projection, and combat target predicates no longer live inline
  in the scene input controller.
- Extracted `MapPendingDeployGhostController` and
  `MapTerritoryHighlightPresenter`, moving pending deploy city-core ghost
  bookkeeping/rendering and territory highlight restore/clear behavior out of
  the scene input controller.
- Completed Phase 7 adapter split by extracting `MapBuildPlacementSession`,
  `MapMoveCommandSession`, `MapPlanningCacheEventBridge`, and
  `MapUnitDamagePopupPresenter`. `MapPlanningInputController` now keeps the
  scene-facing Unity entry point, serialized map input settings, current
  `UnitView` adapter reference, and narrow delegation methods; build mode,
  move preview/pending state, cache subscription state, and damage popup
  bookkeeping no longer live as scattered MonoBehaviour fields.
- Existing map commands continue through `PlanningIntentService`; generated
  protocol files remain untouched.

Completion status (2026-05-02): Phase 7 complete. The remaining scene component
name is intentionally retained for prefab compatibility; functionally it is the
current `MapInputBinder` / adapter until prefab assets are renamed in a
separate verified asset migration.

## Phase 8: Management UI Migration

Recommended order:

1. `TurnSummary`
2. `BuildCommandPanel`
3. `TechTree`
4. `RecipeSynthesis`
5. `MinisterReport`
6. `Policy/NationalFocus`
7. `NationalLedger`

Every migrated panel follows:

```text
Store -> ViewModel -> Binder -> View
```

Status (2026-05-02):

- Phase 8 entered with the first `BuildCommandPanel` migration slice.
- Added `BuildCatalogViewModel` / `BuildCatalogState`, projecting
  `StaticCatalogStore + PlanningDraftStore` into grouped build catalog items.
- Added `BuildCatalogUiToolkitBinder` as the UI Toolkit build catalog surface
  with explicit fallback tree rendering and button command callbacks.
- Registered the build catalog ViewModel and binder in `RegisterGame` while
  keeping the existing uGUI `BuildCommandPanel` intact for prefab compatibility.
- Completed the remaining Phase 8 management slices:
  `TechTree`, `RecipeSynthesis`, `MinisterReport`, `Policy/NationalFocus`,
  and `NationalLedger`.
- Added shared `ManagementPanelState`, `ManagementPanelViewModelBase`, and
  `ManagementPanelUiToolkitRenderer` primitives so information-dense panels
  use one explicit UI Toolkit rendering shape without automatic data binding.
- Registered all Phase 8 ViewModels and UI Toolkit binders in the final game
  composition root. Existing uGUI management panels remain intact as authored
  prefab surfaces.
- Added EditMode coverage for representative management ViewModel projections
  and shared UI Toolkit rendering.

Completion status (2026-05-02): Phase 8 complete for migrated runtime slices.
Final authored UXML/USS polish and old uGUI removal remain separate asset and
cleanup work.

## Completion Standard

C0a architecture foundation is complete when:

- New UI/input features are owned by VContainer scopes.
- New state propagation uses R3.
- New async flows use UniTask.
- Management UI uses UI Toolkit where appropriate.
- Map/world-space UI remains uGUI where appropriate.
- At least one uGUI panel and one UI Toolkit panel run through the final
  Store/ViewModel/Binder chain.
- Legacy singletons are not dependencies of migrated modules.
- Boundary checks remain green.

## References

- VContainer release `1.17.0`: https://github.com/hadashiA/VContainer/releases/tag/1.17.0
- R3 release `1.3.0`: https://github.com/Cysharp/R3/releases/tag/1.3.0
- R3 NuGet `1.3.0`: https://www.nuget.org/packages/R3/1.3.0
- Microsoft.Bcl.TimeProvider `8.0.0`: https://www.nuget.org/packages/Microsoft.Bcl.TimeProvider/8.0.0
- Microsoft.Bcl.AsyncInterfaces `8.0.0`: https://www.nuget.org/packages/Microsoft.Bcl.AsyncInterfaces/8.0.0
- System.Threading.Channels `8.0.0`: https://www.nuget.org/packages/System.Threading.Channels/8.0.0
- System.Runtime.CompilerServices.Unsafe `6.0.0`: https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/6.0.0
- System.ComponentModel.Annotations `5.0.0`: https://www.nuget.org/packages/System.ComponentModel.Annotations/5.0.0
- UniTask release `2.5.10`: https://github.com/Cysharp/UniTask/releases/tag/2.5.10
- Unity Package Manager Git URL docs: https://docs.unity3d.com/Manual/upm-git.html
