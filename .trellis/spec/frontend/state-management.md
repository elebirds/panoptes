# State Management

> How state is managed in this project.

---

## Overview

<!--
Document your project's state management conventions here.

Questions to answer:
- What state management solution do you use?
- How is local vs global state decided?
- How do you handle server state?
- What are the patterns for derived state?
-->

Client state mirrors server state and static catalog data for presentation only.
The client never becomes a second rules engine.

Target direction for C0a+ is reactive read models: Core stores expose read-only
state, ViewModels compose that state into panel/screen models, and Binders render
those models to uGUI or UI Toolkit. R3 is the approved state propagation library
for migrated modules.

The Unity R3 package depends on the vendored R3 core DLLs in
`client/Assets/Plugins/`; Store/ViewModel code may use R3 APIs only through this
approved installation.

---

## State Categories

<!-- Local state, global state, server state, URL state -->

- Server runtime state: `GameStateCache`.
- Static content and UI metadata: `StaticCatalogCache`.
- Planning draft/preview state: `PlanningDraftCache`.
- Local presentation state: MonoBehaviour-private fields for selection,
  expanded panels, hover state, and animation bookkeeping.

Target additions after dependency policy changes:

- Store state streams: server/cache snapshots exposed as read-only observables.
- ViewModel state: panel-ready state records derived from one or more stores.
- Form state: local, non-authoritative input such as search, filters, settings,
  and login fields.

Current Phase 3 store classes live under `Core/Application/Stores`:

- `StaticCatalogStore`
- `GameStateStore`
- `PlanningDraftStore`
- `SelectionStore`
- `TurnStore`

Store state DTOs must be independent Core read models, not generated Protocol
messages and not nested legacy cache JSON classes. Write/update methods stay
`internal` so UI and Binders consume only `Snapshot` and `State`.

Phase 4 establishes the first migrated uGUI slice:

- `UnitInfoViewModel` composes `GameStateStore`, `SelectionStore`,
  `StaticCatalogStore`, and `PlanningDraftStore` into `UnitInfoState`.
- `UnitInfoUguiBinder` renders `UnitInfoState` into TMP, Slider, and Button
  controls.
- `UnitInfoPanelController` stays as the prefab-facing facade for existing
  scene selection, slide animation, portrait camera, and legacy action
  registrars. Do not move serialized fields out of that facade unless the
  affected prefab/scene assets are updated and verified.
- The migrated ViewModel/Binder files must not reference generated Protocol,
  legacy cache singletons, or direct NetworkManager singleton calls.

Phase 5 establishes the first migrated UI Toolkit read-only slice:

- `TurnSummaryViewModel` composes `TurnStore` and `GameStateStore` into
  `TurnSummaryState`.
- `TurnSummaryUiToolkitBinder` renders a `UIDocument` through explicit
  `Q<T>("name")` lookups and `Render(state)`.
- Stable UXML names live in `client/Assets/UI/Toolkit/Turn/TurnSummary.uxml`;
  binder constants and tests should reference the same names.
- Do not use Unity automatic data binding for gameplay state in this pilot.
- UI Toolkit binders must not reference generated Protocol, legacy cache
  singletons, or direct NetworkManager singleton calls.

Phase 6 establishes injectable command services for migrated player commands:

- `GameIntentService` owns general game commands such as submit turn, chat,
  research target, policy, reveal, and war zone submission.
- `PlanningIntentService` owns planning commands such as move, attack, charge,
  build, build preview, recipe preview, and recipe selection.
- `MinisterCommandService` owns minister draft accept/reject directives.
- Presentation command callers should receive these services through VContainer
  injection and must not call static command compatibility shells.
- Command services may construct Protocol messages because they live in Core,
  but they must send only through `IClientMessageSender`.

---

## When to Use Global State

<!-- Criteria for promoting state to global -->

Use global/cache state only for data pushed by the server or loaded from the
generated catalog. Keep transient UI state local to the panel/controller unless
multiple views need to observe it.

---

## Server State

<!-- How server data is cached and synchronized -->

`GameStateCache` is the public client mirror facade. Extract read-only query
helpers, such as `GameStateCacheReadQueries`, when repeated snapshot logic grows,
but keep gameplay validation out of these helpers.

Presentation reads Core DTO/cache APIs and should not inspect generated
protocol messages directly.

Default data flow:

```text
Server -> Core cache/store -> ViewModel -> Binder -> UI
Player input -> ViewModel command -> Core command service -> IClientMessageSender -> Server
```

UI Toolkit runtime data binding may be evaluated later for stable detail panels
and forms, but the first implementation path is explicit Binder rendering. Do
not bind UI Toolkit directly to mutable gameplay cache objects.

Migrated modules should be owned by final VContainer scopes. Do not add a
compatibility Composition Root that wraps old singleton caches as the new module
API. Existing singleton caches may remain for legacy modules until those modules
migrate.

### Presentation-Owned Transient Map Visuals

- Pending map ghosts, hover ghosts, and similar transient visuals remain
  presentation-owned state even when the underlying map mesh is rebound from
  authoritative `GameStateStore` snapshots.
- `MapRenderer` refresh/rebuild paths may clear node visuals back to the latest
  authoritative node snapshot. Do not mutate authoritative store snapshots to
  keep temporary visuals alive.
- When a transient visual must survive renderer refreshes, restore it from the
  owning presentation/session state after renderer presentation refresh
  completes. Current example: pending build ghosts are replayed from
  `MapBuildPlacementSession` after `MapRenderer.StatePresentationRefreshed`.
- Do not normalize map node ids with catalog/command token helpers such as
  `MapInputTokens.Normalize`, because protocol node ids are case-sensitive
  display keys like `V22`. Trim node ids only; reserve lowercase normalization
  for building/action/catalog tokens.
- Pending build visuals are represented by the green translucent building ghost
  only. Restore paths that replay a pending build must recreate the building
  ghost but must not turn on the node highlight layer, because that layer paints
  over the terrain and can make the whole tile look white. Regression tests
  should assert both direct backend build ghost application and queued build
  restoration leave the node highlight inactive.

### Map Attack Targeting Affordances

- Attack highlights are target affordances, not raw radius previews. Highlight
  enemy unit nodes for units that can attack, and highlight enemy structure
  nodes only when the selected unit's static catalog flags allow structure
  attacks.
- Presentation may use static catalog fields such as `attack`,
  `attack_range`, and `can_attack_structures` to decide which attack buttons,
  highlights, and click routes to show. It must still submit commands through
  `PlanningIntentService`; the backend remains authoritative for final
  validation and resolution.
- When a click in attack mode raycasts the node under an enemy unit instead of
  the unit model, route the command as a unit-target attack if the map renderer
  can resolve a hostile `UnitView` on that node. Do not fall back to
  `AttackNode` unless the node is an enemy structure and the selected unit can
  attack structures.
- Regression tests should cover both the generic range presenter filtering and
  the catalog targeting helper so units with `attack_range: 0` do not get a
  fake one-tile attack affordance.

### Composition Scope Ownership

- `ProjectLifetimeScope` is owned by the startup/bootstrap path that creates the
  persistent `Managers` object. Do not also serialize another `Managers`
  hierarchy into `Boot.unity`; Unity runtime initialization runs before the
  first scene loads, so duplicating it risks two project scopes.
- Gameplay scenes that host migrated presentation/application modules should
  own a real scene `GameLifetimeScope`. `Assets/Scenes/Game.unity` contains the
  current `Game Composition` root for this purpose.
- `GameLifetimeScope` must stay free of legacy `GameStateCache`,
  `PlanningDraftCache`, and `StaticCatalogCache` registrations. Phase 3+ stores
  should be registered directly as migrated read-model dependencies.
- Project scope registers `StaticCatalogStore`; game scope registers
  `GameStateStore`, `PlanningDraftStore`, `SelectionStore`, and `TurnStore`.
- Formal runtime services that still maintain legacy Core mirrors receive
  companion caches from the project composition root; they must not rediscover
  those collaborators through static singleton entrypoints.
- Debug-only diagnostics may read singleton runtime objects when compiled under
  editor/development/debug-panel gates. Those reads are not the standard
  Presentation dependency path.

---

## Common Mistakes

<!-- State management mistakes your team has made -->

- Returning mutable internal collections from cache queries. Return cloned
  snapshots instead.
- Recomputing server legality rules in UI state builders.
- Letting Presentation reach into generated protocol types instead of Core DTOs.
- Using two-way binding to mutate authoritative game state.
- Maintaining separate uGUI and UI Toolkit state models for the same gameplay
  concept.
- Adding a serialized project-level `Managers` object to `Boot.unity` while the
  runtime bootstrap already creates one.
- Exposing legacy cache nested JSON types from Store state; map catalog data
  into standalone Core DTOs before Store publication.
- Assuming authoritative map rebinds will preserve transient presentation
  visuals such as pending build ghosts. Reapply those visuals from
  presentation-owned pending state after renderer refresh instead.
