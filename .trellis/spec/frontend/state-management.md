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

---

## State Categories

<!-- Local state, global state, server state, URL state -->

- Server runtime state: `GameStateCache`.
- Static content and UI metadata: `StaticCatalogCache`.
- Planning draft/preview state: `PlanningDraftCache`.
- Local presentation state: MonoBehaviour-private fields for selection,
  expanded panels, hover state, and animation bookkeeping.

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

---

## Common Mistakes

<!-- State management mistakes your team has made -->

- Returning mutable internal collections from cache queries. Return cloned
  snapshots instead.
- Recomputing server legality rules in UI state builders.
- Letting Presentation reach into generated protocol types instead of Core DTOs.
