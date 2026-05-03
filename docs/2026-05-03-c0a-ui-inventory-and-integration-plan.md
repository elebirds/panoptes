# C0a UI Inventory and Integration Plan

Date: 2026-05-03

## Goal

Before C0a starts, define the real UI surface area that the Unity client needs
to author and wire. This inventory uses the final client architecture as the
standard:

```text
Store / Service -> ViewModel -> Binder -> authored uGUI prefab or UI Toolkit UIDocument
```

The purpose is to avoid starting C0a with unclear UI ownership. C0a should
connect real backend state to real client surfaces, not continue architecture
cleanup or rely on generated fallback UI.

## Current State Summary

- The runtime script architecture has final Store/ViewModel/Binder slices for
  the major HUD and management panels.
- uGUI prefab assets already exist for most map/HUD/world-space surfaces.
- UI Toolkit authored assets currently exist only for `TurnSummary`.
- `BuildCatalog`, `TechTree`, `RecipeSynthesis`, `MinisterReport`,
  `PolicyFocus`, and `NationalLedger` have ViewModel/Binder slices, but still
  rely on fallback `UIDocument` trees registered by composition until authored
  UI Toolkit prefabs exist.
- `RecipeSynthesis` already has a command path to `PlanningIntentService`.
- `BuildCatalog` enters build placement through `PlanningToolService`.
- `TechTree`, `PolicyFocus`, and `MinisterReport` expose action labels in row
  state, but do not yet have complete command wiring in their UI Toolkit
  binders.
- `PolicyFocus`, `MinisterReport`, and `NationalLedger` do not yet participate
  in `ManagementPanelVisibilityStore`; the visibility enum currently covers
  only `TechTree`, `BuildCatalog`, and `RecipeSynthesis`.

## Existing UI Assets

### uGUI / Prefab Assets

Existing runtime UI prefabs include:

| Asset | Current role |
|---|---|
| `client/Assets/Resources/Prefabs/UI/TokenHUD.prefab` | Planning token and phase status HUD |
| `client/Assets/Prefabs/UI/ResourcePanel.prefab` | Resource HUD surface |
| `client/Assets/Prefabs/UI/TrunPanel.prefab` | Turn/countdown panel surface |
| `client/Assets/Prefabs/UI/UnitInfoPanel.prefab` | Selected unit/building info and direct actions |
| `client/Assets/Resources/Prefabs/UI/GameChatPanel.prefab` | In-game chat/emote panel |
| `client/Assets/Resources/Prefabs/UI/SettlementTimeline.prefab` | Settlement event timeline |
| `client/Assets/Resources/Prefabs/UI/TurnReportPanel.prefab` | Settlement report summary |
| `client/Assets/Resources/Prefabs/UI/GameOverOverlay.prefab` | Game-over result overlay |
| `client/Assets/Resources/Prefabs/UI/BuildingConstructionOverlay.prefab` | World/HUD construction progress overlay |
| `client/Assets/Resources/Prefabs/UI/CityCoreHpBarOverlay.prefab` | City core HP overlay |
| `client/Assets/Resources/Prefabs/UI/CastleHPBar.prefab` | Castle HP overlay |
| `client/Assets/Prefabs/UI/ConfirmDialog.prefab` | Modal confirmation dialog |
| `client/Assets/Prefabs/UI/ErrorToast.prefab` | Error/toast feedback |
| `client/Assets/Prefabs/UI/PlayerSlot.prefab` | Lobby player slot |

These should stay uGUI for C0a unless the surface is explicitly part of the
information-dense management UI.

### UI Toolkit Assets

Existing authored UI Toolkit assets:

| Asset | Current role |
|---|---|
| `client/Assets/UI/Toolkit/Turn/TurnSummary.uxml` | Turn summary pilot |
| `client/Assets/UI/Toolkit/Turn/TurnSummary.uss` | Turn summary styling |

Missing authored UI Toolkit assets:

| Needed asset group | Current script support |
|---|---|
| Build catalog | `BuildCatalogViewModel`, `BuildCatalogUiToolkitBinder` |
| Tech tree | `TechTreeViewModel`, `TechTreeUiToolkitBinder` |
| Recipe synthesis | `RecipeSynthesisViewModel`, `RecipeSynthesisUiToolkitBinder` |
| Minister report | `MinisterReportViewModel`, `MinisterReportUiToolkitBinder` |
| Policy / National focus | `PolicyFocusViewModel`, `PolicyFocusUiToolkitBinder` |
| National ledger | `NationalLedgerViewModel`, `NationalLedgerUiToolkitBinder` |
| Management panel host / navigation | Partial: `ManagementPanelVisibilityStore`; no authored host yet |

## UI Surface Catalog

### Game Shell and Session Surfaces

| Surface | Technology | Existing asset | Information displayed | Player operations | C0a status |
|---|---|---|---|---|---|
| Login | uGUI prefab / scene | Existing scripts and scene | username/password, auth errors, loading | login/register | Keep existing; not C0a focus |
| Lobby / room | uGUI prefab / scene | Existing scripts and `PlayerSlot` prefab | rooms, players, ready state, bots | create/join/leave/ready/add bot/start | Keep existing; only fix if C0a flow blocks |
| Loading / error | uGUI prefab | `ErrorToast`, `ConfirmDialog`, loading overlay script | errors, progress, confirmation text | confirm/cancel | Keep uGUI |

### Map, HUD, and World-Space UI

These are scene-anchored, spatial, or high-frequency surfaces. They should stay
uGUI / prefab based.

| Surface | Technology | Existing asset/script | Information displayed | Player operations | C0a status |
|---|---|---|---|---|---|
| Map renderer | scene GameObjects + prefabs | `MapRenderer`, map prefabs | terrain, visible nodes, roads, buildings, units, fog | select node/unit, preview movement/building | Existing final-architecture script path; use in C0a |
| Planning input overlays | scene/prefab uGUI + world objects | `MapPlanningInputController`, `Planning/Input`, feedback presenters | move path, build ghost, territory highlight, attack range | choose target node/unit, confirm implicit map intent | Existing path; verify with real backend sync |
| Resource HUD | uGUI prefab | `ResourceHUD`, `ResourceHudViewModel` | resources, points, icon rows, deltas | open tech tree | Existing; keep uGUI |
| Token HUD | uGUI prefab | `TokenHUD`, `TokenHudViewModel` | tokens left, phase, submitted/resolving status | none | Existing; keep uGUI |
| Turn HUD | uGUI prefab / scene panel | `TurnHUD` | turn number, countdown, current interactive state | submit turn | Existing; keep uGUI |
| Unit info | uGUI prefab | `UnitInfoPanelController`, `UnitInfoViewModel`, `UnitInfoUguiBinder` | selected unit/building name, description, HP, planning summary, portrait, available actions | move, attack, hold, charge, contextual action buttons | Existing; keep uGUI |
| Building construction overlay | uGUI/world overlay prefab | `BuildingConstructionOverlayController` | construction/operation progress, building status | none | Existing; keep uGUI |
| City/castle HP bars | world-space uGUI prefabs | `CityCoreHPBar`, `CastleHPBar`, overlays | HP and damage state | none | Existing; keep uGUI |
| Chat panel | uGUI prefab | `GameChatPanelController` | recent chat/emote transcript | send emotes | Existing; keep uGUI |
| Game over overlay | uGUI prefab | `GameOverOverlay` | winner/loser/reason/narrative | post-game navigation if prefab provides it | Existing; keep uGUI |

### Management UI

These are information-dense, list/table/tree oriented surfaces. They should be
UI Toolkit, with authored UXML/USS and explicit binders.

| Surface | Technology | Current script support | Information displayed | Player operations | C0a status |
|---|---|---|---|---|---|
| Management host / navigation | UI Toolkit | Partial visibility store only | active panel, tabs/sidebar, close state | open/close/switch panels | Must build before real C0a panel work |
| Turn summary | UI Toolkit | `TurnSummaryViewModel`, `TurnSummaryUiToolkitBinder`, authored UXML/USS | turn, phase, tokens, visible nodes, known units, recent planning-start events | read-only | Pilot exists; good first validation surface |
| National overview | UI Toolkit | can be derived from `NationalLedgerViewModel` or a new focused ViewModel | compact state: turn, phase, resources, units, cities, active focus/research | read-only initially | Recommended C0a first product slice |
| National ledger | UI Toolkit | `NationalLedgerViewModel`, `NationalLedgerUiToolkitBinder` | overview counters, resources, catalog counts | read-only | Script exists; needs authored asset and visibility |
| Build catalog | UI Toolkit | `BuildCatalogViewModel`, `BuildCatalogUiToolkitBinder` | building groups, names, descriptions, placement kind, pending state | choose building, enter map placement mode | Script exists; needs authored asset |
| Tech tree | UI Toolkit | `TechTreeViewModel`, `TechTreeUiToolkitBinder` | branch, tier, cost, description, planned research state | set research target | Needs authored asset and command wiring |
| Recipe synthesis | UI Toolkit | `RecipeSynthesisViewModel`, `RecipeSynthesisUiToolkitBinder` | recipes for selected building, work/base progress, preview/selected state | set building recipe | Script and command path exist; needs authored asset |
| Policy / national focus | UI Toolkit | `PolicyFocusViewModel`, `PolicyFocusUiToolkitBinder` | national focus options, institution policies, activation timing, planned state | adopt national focus, set institution loadout | Needs visibility and command wiring |
| Minister report | UI Toolkit | `MinisterReportViewModel`, `MinisterReportUiToolkitBinder` | minister drafts grouped by role, summary/rationale/status | review; later accept/reject/direct | Keep read-only or hidden for C0a unless needed |
| Turn report / settlement review | UI Toolkit long-term, uGUI existing now | uGUI `TurnReportPanel`, `SettlementTimeline`; UI Toolkit `TurnSummary` | settlement counts, events, warnings, production/build results | read-only | C0a can keep existing uGUI; later consolidate |

## C0a Required UI Work

### Required before C0a can feel real

1. Create an authored **Management UI host**.
   - Recommended technology: UI Toolkit.
   - Role: panel chrome, tabs/sidebar, close behavior, shared panel area.
   - Must own the first real replacement for `RegisterComponentOnNewGameObject`
     fallback usage.

2. Author at least one production UI Toolkit management panel.
   - Recommended first panel: `NationalOverview` or `TurnSummary`.
   - Reason: low command risk, validates Store/ViewModel/Binder against real
     server state.

3. Decide how management panels are opened from existing uGUI HUD.
   - `ResourceHUD` already opens `TechTree`.
   - City/building contextual actions already open `BuildCatalog` and
     `RecipeSynthesis`.
   - C0a still needs a stable global entry for `NationalOverview`,
     `NationalLedger`, `PolicyFocus`, and later `MinisterReport`.

4. Extend `ManagementPanelVisibilityStore`.
   - Current enum: `TechTree`, `BuildCatalog`, `RecipeSynthesis`.
   - Needed: `TurnSummary`, `NationalOverview`, `NationalLedger`,
     `PolicyFocus`, and optionally `MinisterReport`.

5. Wire missing row commands.
   - `TechTree`: row action should call `GameIntentService.SetResearchTarget`.
   - `PolicyFocus`: national rows should call `GameIntentService.SetPolicy`;
     institution rows need a clear loadout interaction before calling
     `SetInstitutionLoadout`.
   - `MinisterReport`: for now likely read-only; accept/reject should wait
     until minister product requirements are reopened.

### Not required for first C0a slice

- Removing all old uGUI HUD prefabs.
- Rewriting map/world-space UI in UI Toolkit.
- Implementing full minister interaction.
- Building final tech-tree graph layout. A grouped list is acceptable first.
- Automatic UI Toolkit data binding. Keep explicit binder rendering.

## Recommended C0a Sequence

### C0a-0: Author the Management Host

Deliverables:

- `client/Assets/UI/Toolkit/Management/ManagementHost.uxml`
- `client/Assets/UI/Toolkit/Management/ManagementHost.uss`
- `client/Assets/Resources/Prefabs/UI/ManagementHost.prefab`
- A `ManagementHostUiToolkitBinder` or equivalent scene component that toggles
  `ManagementPanelVisibilityStore`.

Information:

- active panel title
- tabs/sidebar entries
- close button

Operations:

- open/switch/close management panels

### C0a-1: National Overview / Turn Summary

Deliverables:

- Use existing `TurnSummary` as the smallest validation surface, or create a
  new `NationalOverviewViewModel` if the desired first screen should combine
  resources, turn, units, cities, research, and policy.

Information:

- turn and phase
- tokens left
- resource/point snapshot
- visible nodes / known units / city count
- current or planned research target
- current or planned national focus
- recent settlement/planning events

Operations:

- read-only initially
- optional buttons to open `TechTree`, `PolicyFocus`, `NationalLedger`

### C0a-2: Build Catalog and Recipe Synthesis

Deliverables:

- Authored UI Toolkit assets for build catalog and recipe synthesis.
- Replace fallback tree reliance with named UXML elements.

Information:

- building catalog grouped by placement kind
- building description and pending state
- selected building recipe list
- recipe work/base progress and selected/preview state

Operations:

- enter build placement mode
- select building recipe

### C0a-3: Tech Tree

Deliverables:

- Authored UI Toolkit tech list/tree.
- Command wiring from row action to `GameIntentService.SetResearchTarget`.

Information:

- branch
- tier
- research cost
- description
- current planned research target
- completed/active/pending activation state when the Store exposes it

Operations:

- set research target

### C0a-4: Policy / National Focus

Deliverables:

- Authored UI Toolkit policy panel.
- Visibility support.
- Command wiring.

Information:

- national focus candidates
- institution policy candidates
- active/planned state
- activation timing
- priority/logistics effect summary where catalog data exposes it

Operations:

- adopt national focus
- set institution loadout

### C0a-5: National Ledger

Deliverables:

- Authored UI Toolkit ledger panel.
- Visibility support.

Information:

- resources and points
- map/node/unit counts
- building/recipe/tech/policy/unit catalog counts
- eventually per-city/per-network summaries

Operations:

- read-only

### C0a-Later: Minister Report

Deliverables:

- Authored UI Toolkit minister report.
- Product decision for review/accept/reject behavior.

Information:

- minister role
- draft title
- summary
- rationale
- status

Operations:

- C0a: read-only or hidden
- Later: accept/reject/direct through `MinisterCommandService`

## Immediate Recommendation

Start C0a with:

1. `ManagementHost`
2. `NationalOverview` built from existing Stores
3. authored `TurnSummary` integration inside the host

Then move to interactive management surfaces:

1. `BuildCatalog`
2. `RecipeSynthesis`
3. `TechTree`
4. `PolicyFocus`

This gives the team a stable UI Toolkit authoring workflow before introducing
more command-heavy panels.

## Acceptance Criteria for This Inventory

- [x] Existing UI assets are listed.
- [x] Needed UI surfaces are listed.
- [x] Each surface has technology ownership: uGUI prefab or UI Toolkit.
- [x] Each surface lists information displayed and player operations.
- [x] C0a order is explicit.
- [x] Known current gaps are captured.

