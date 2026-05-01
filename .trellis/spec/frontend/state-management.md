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
Player input -> ViewModel command -> Service/Intent -> MessageSender -> Server
```

UI Toolkit runtime data binding may be evaluated later for stable detail panels
and forms, but the first implementation path is explicit Binder rendering. Do
not bind UI Toolkit directly to mutable gameplay cache objects.

Migrated modules should be owned by final VContainer scopes. Do not add a
compatibility Composition Root that wraps old singleton caches as the new module
API. Existing singleton caches may remain for legacy modules until those modules
migrate.

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
