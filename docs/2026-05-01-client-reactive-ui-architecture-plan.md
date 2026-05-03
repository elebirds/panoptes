# Panoptes Client Reactive UI Architecture Plan

> Date: 2026-05-01
> Status: Superseded by the direct implementation plan
> Scope: C0a and later client UI/script architecture

This target plan has been superseded for implementation by
`docs/2026-05-02-client-reactive-presentation-architecture-implementation-plan.md`.
The main correction is that migrated modules should not use a compatibility
Composition Root. They should move directly to the final VContainer-owned
Store/ViewModel/Binder architecture.

## 1. Decision

Panoptes should evolve toward a hybrid Unity client architecture:

```text
Protocol / WebSocket
  -> Protocol Mapper
  -> Client Store / Read Model
  -> Reactive ViewModel
  -> Explicit Binder
  -> UI Toolkit UXML/USS or uGUI Prefab

Player Input
  -> ViewModel Command
  -> Application Command Service
  -> IClientMessageSender
  -> Server
```

The client remains a pure presentation layer. The server is authoritative for
rules and legality. The client may project, filter, format, and animate server
state, but it must not become a second rules engine.

## 2. Proposed Stack

| Concern | Target | Role |
|---|---|---|
| Lifecycle / DI | VContainer | Own application and scene scopes; remove ad-hoc singleton lookups over time |
| State propagation | R3 | Replace scattered `OnChanged + Refresh()` chains with composable observable state |
| Async | UniTask | Unify login, connect, catalog loading, request/response waits, and scene initialization |
| Information UI | UI Toolkit | Use UXML/USS for dense panels that AI and new Unity users can edit safely |
| Map / world UI | uGUI | Keep scene-bound HUD, overlays, unit bars, and map feedback on uGUI where it fits Unity objects |
| Animation | DOTween candidate | Optional later; only for presentation animation, never rules or timing authority |

Repository policy now approves VContainer/R3/UniTask for the final client
architecture with locked versions. DOTween remains out of scope.

## 3. Layer Responsibilities

```text
Core/Foundation
  Pure DTOs, enums, value objects.
  Final target: no generated protocol references.

Core/Infrastructure
  NetworkManager, MessageDispatcher, protocol frames, protocol mappers.
  May reference Panoptes.Protocol.

Core/Application
  Stores, caches, services, intents, use cases.
  Owns read model state and exposes read-only streams/snapshots.

Presentation/ViewModels
  Combine stores into panel/screen state.
  Own commands that call services/intents.
  Must not hold Button, Label, TMP_Text, VisualElement, GameObject, or prefab references.

Presentation/Binders
  MonoBehaviour or UIDocument-facing glue.
  Query or serialize UI references, subscribe to ViewModel state, render state.

Presentation/Views
  uGUI prefabs or UI Toolkit UXML/USS.
```

## 4. UI Toolkit Boundary

Use UI Toolkit first for information-heavy, panel-like experiences:

- Minister reports and draft review.
- Tech tree and technology detail panels.
- Policy / national focus / institution panels.
- Building catalog and recipe synthesis panels.
- Turn summary, national ledger, and debug/admin panels.

Keep uGUI for scene-bound and world-space experiences:

- Map HUD and selection chrome.
- Unit health bars, flags, and labels.
- Building overlays and construction bars.
- Movement/combat/build previews.
- Camera- and raycast-driven feedback.

The two systems share the same Core stores and ViewModels. They must not invent
separate state models for the same gameplay concept.

## 5. Data Binding Policy

UI Toolkit runtime data binding is worth evaluating, but it should not be the
first architectural dependency.

Default first-stage rule:

```text
ViewModel.State.Subscribe(Render)
Render(state) explicitly writes Label/Button/ListView/uGUI fields
```

Use data binding later only for stable, mostly field-shaped UI:

- Read-only detail panels.
- Settings and filters.
- Login or non-gameplay forms.
- Static metadata display.

Avoid initial data binding for:

- Map HUD and high-frequency visual state.
- Drag/hover/click previews.
- Command availability rules.
- Complex list item lifecycle.

Default binding direction is one-way:

```text
Store / ViewModel -> UI
```

Two-way binding is allowed only for non-authoritative local input such as search
text, filters, settings, or login fields. It must not directly mutate game
runtime state.

## 6. Dependency Scopes

Target VContainer scopes:

```text
ProjectLifetimeScope
  AuthStore
  ConfigStore
  StaticCatalogStore
  Network services
  App-level navigation

GameLifetimeScope
  GameStateStore
  PlanningDraftStore
  SelectionStore
  TurnStore
  Game screen ViewModels
```

Binders should receive ViewModels or services through injection. Migrated
modules must not use a compatibility Composition Root or active singleton
lookup. Existing singletons may remain only as isolated legacy dependencies for
modules that have not migrated yet.

## 7. Rollout Plan

| Phase | Goal | Output |
|---|---|---|
| C0a-P0 | Policy and dependency foundation | Update AGENTS/docs/specs and lock approved packages |
| C0a-P1 | Final contracts and directories | Add Store/Service/ViewModel/Binder/Composition structure |
| C0a-P2 | Final Composition Root | Add Project/Game LifetimeScopes without compatibility singleton bridge |
| C0a-P3 | Store/read-model layer | Add reactive Store APIs and immutable snapshots |
| C0a-P4 | uGUI migration | Move one existing panel through Store -> ViewModel -> Binder |
| C0a-P5 | UI Toolkit migration | Add one read-only UIDocument panel through the same state chain |
| C0a-P6 | Command services and map input | Move commands and map planning state into services/stores/viewmodels |

Each phase must keep these gates green:

- No generated protocol edits.
- No `Panoptes.Protocol` references under `Runtime/Presentation`.
- No direct NetworkManager singleton calls under `Runtime/Presentation/UI`.
- No static command compatibility shell in migrated Presentation paths.
- Unity script import/compile succeeds.

## 8. Current Script Refactor Plan

The current C0p state is acceptable for starting C0a, but these files are not in
their final shape.

| Area | Current Shape | Target Shape | Priority |
|---|---|---|---|
| `MapPlanningInputController` | 3308-line input facade with extracted modes | `PlanningToolController` + small `IPlanningInputMode` implementations + preview presenter | High |
| `UnitInfoPanelController` | 2009-line panel controller | `UnitInfoViewModel`, `UnitStatsBinder`, `UnitActionListRenderer`, `UnitSelectionBinder` | High |
| `MapRenderer` | 1514-line render coordinator with source helpers | `MapRenderCoordinator` + node/unit/building/fog/overlay renderers | High |
| `SquadUnitVisualController` | 1675-line visual controller | state presenter + health/flag/model/path/animation binders | Medium |
| `BuildCommandPanel` | 1497-line panel with list renderer extracted | UI Toolkit pilot candidate or ViewModel + list binder | High |
| `RecipeSynthesisPanel` | 1377-line panel with registry extracted | ViewModel + item lifecycle binder; UI Toolkit candidate after pilot | Medium |
| `GameStateCache` | 1352-line cache facade with read queries extracted | split stores/read models by domain once R3 is introduced | Medium |
| `StaticCatalogCache` | 1369-line catalog facade | split catalog loaders, query indexes, and UI metadata stores | Medium |
| `MinisterDto` | Foundation DTO still references generated protocol view | move protocol conversion to mapper/cache before minister UI work | High before minister C0a |

## 9. C0a Entry Rule

C0a may begin on top of the C0p baseline if new work follows this rule:

```text
New server state -> Core cache/store/DTO -> ViewModel/helper -> Binder -> UI
```

Do not push new backend binding logic into the large facade scripts. If a C0a
feature touches one of the high-priority large files, shrink the touched area in
the same change or add a local helper that prevents further growth.
