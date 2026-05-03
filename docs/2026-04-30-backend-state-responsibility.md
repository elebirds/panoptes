# Panoptes backend state responsibility

> Status: M1.5 boundary note
> Date: 2026-04-30
> Scope: durable state, turn runtime state, and future truth/observed/reported
> boundaries. This document does not implement local storage, logistics, or
> information asymmetry.

## Goal

Backend game logic remains server-authoritative. The client may cache messages
for display, but it must not own durable game state, validate game legality, or
derive hidden rule state.

This note records where current and future backend state belongs so M2/M3
systems do not mix durable truth with per-turn scratch data.

## Responsibility Table

| State area | Current location | Lifetime | Owner | Write path | Notes |
|---|---|---:|---|---|---|
| Match identity and clock | `domain.GameState.GameID`, `Turn`, `Phase`; mirrored by `GameMeta`, `TurnClock` | Match | `domain`, `game/turn` | Turn coordinator and lifecycle events/helpers | Durable truth. `Phase` gates commands; clients only display projected phase. |
| Outcome | `GameState.IsOver`, `WinnerID`, `OverReason`; mirrored by `GameOutcome` | Match | `domain`, `engine/combat`, `game/turn` | Events such as `CityCoreDestroyedEvent`, then room/game-over broadcast | Durable truth. Fatal resolving may stop later stages, but already applied events remain authoritative. |
| World map and ECS entities | `GameState.World`, `Map`, `NodeIndex`; mirrored by `WorldState` | Match | `domain`, `ecs`, `building`, `engine` | ECS factories plus event `Apply()` during resolving | Durable truth. Building/unit/node facts live here, not in client projections. |
| Player economy and progression | `GameState.Players[*]` (`Resources`, `Research`, `Policy`, `Institutions`, city records) | Match | `domain`, `engine/economy`, `game/resolution` | Planning lock-in events and economy/building/research events | Durable truth. Pending choices belong in planning runtime until lock-in. |
| Planning command drafts | `TurnRuntime.Planning` (`BuildOrders`, `RecipeSelections`, `Pending*`, `UnitOrders`) | Current planning turn | `game/planning`, `game/orders` | Planning handlers write drafts directly after validation | Server-authoritative runtime input. It is not durable world state and is cleared after resolving. |
| Minister proposal scratch | `TurnRuntime.Planning.MinisterDrafts`, `MinisterDirectives`, `MinisterBuilds`, `MinisterMoves` | Current planning turn | `game/planning`, future minister layer | Current MVP rejects directive execution; drafts are proposal/display state only | Reserved for M7. It must not silently execute or mutate durable truth in M1. |
| Frozen unit orders | `TurnRuntime.Resolving.UnitOrders` | Current resolving turn | `game/orders`, `game/resolution`, `engine/combat` | `OrderFreezeStage` builds it from planning drafts and active marches | Resolving scratch. Combat reads this snapshot; it is cleared after resolving. |
| Active marches | `TurnRuntime.Resolving.ActiveMarches` | Cross-turn command cache | `game/orders` | Planning move/cancel and post-settlement march refresh | Runtime-owned, but not just one-turn scratch. It records ongoing player intent and derived route preview, not physical truth. Physical unit position remains in ECS. |
| Point budgets | `TurnRuntime.Resolving.PointBudgets` | Resolving economy pass | `domain`, `engine/economy` | Economy refresh/spend/clear helpers | Runtime budget, not inventory. Player resources remain durable truth. |
| Client sync/report messages | `game/query`, `game/projection`, proto `*View`/`Msg*` | Message/display cache only | Backend projection layer | Derived from authoritative state and event collector | Reported to clients. The client may render it but cannot treat it as rule authority. |

## Future State Placement

| Future state | Recommended location | Why |
|---|---|---|
| Local storage / stockpiles | Durable truth under `domain` state, likely attached to city/node/building ownership records or dedicated storage structs referenced by ECS bindings | Stockpiles survive turns, can be captured/blocked, and affect economy legality. They must be serialized as backend truth and mutated by events. |
| In-transit logistics quantities | Durable truth only if shipment identity persists across turns; otherwise resolving scratch under a future logistics stage | A shipment that can be intercepted, delayed, or observed is truth. A one-pass allocation table used only to settle the current turn is runtime scratch. |
| Logistics graph topology | Durable road/edge/facility facts in `domain`/ECS; graph object rebuilt in `engine/logistics` or `game/resolution` stage scratch | Roads and facilities are truth. The computed graph is a derived view of truth plus current blockers and should not be the storage source of record. |
| Logistics allocation results | Event-applied durable deltas for delivered/consumed resources; per-run allocation tables in resolving scratch | Only final resource/storage changes need to survive. Solver internals should be auditable through events or debug traces, not stored as durable state. |
| Priority profiles | Durable player/nation configuration in `PlayerState`/policy/institution state after lock-in; pending edits in `TurnRuntime.Planning` | Priority choices are strategic state once committed. Policy-derived priority should be computed from active policy/institution truth, not duplicated in logistics scratch. |
| M2 road/network commands | Planning drafts in `TurnRuntime.Planning.UnitOrders` or future planning map-action drafts; durable road/facility results in ECS/domain through map/building events | Commands are turn inputs. Built roads/facilities are world truth. |
| M3 economy/logistics caches | Future `TurnRuntime.Resolving` logistics scratch or local variables inside logistics runner | Caches should be recomputable from truth and cleared after the resolving pass unless they represent persistent shipments. |

## Truth / Observed / Reported Boundary

M1 keeps the current MVP projection behavior: most client views are close to
truth. The architecture must still leave room for the final information model:

| Layer | Meaning | Current owner | Future rule |
|---|---|---|---|
| `truth` | Full server-authoritative state used for legality and settlement | `domain.GameState`, ECS components, durable player/world structs | Only backend rules read this directly. Durable local storage, road state, policy profile, and persistent logistics facts live here. |
| `observed` | Player/nation-specific state after fog, sensors, scouting, and ownership filters | `game/query` and `game/projection.ObservedState` | Query/projection should derive this from truth. Do not store observed state as an alternate truth source unless an explicit memory system is introduced. |
| `reported` | Minister/institution/UI report generated from observed state, possibly incomplete or biased | `game/projection` today; future minister/report layer | Reports can omit or transform information, but they must not drive rule legality. Minister defaults may propose planning drafts, not mutate truth directly. |

New APIs should avoid returning raw `*domain.GameState` to transport/client
code when a player-specific view is enough. Rule engines may read truth; client
messages should flow through query/projection so M8 information asymmetry can
replace the projection internals without changing command authority.

## Non-Goals

- No local storage structs are added in M1.
- No logistics graph or flow solver is added in M1.
- No priority profile mechanics are added in M1.
- No fog, misinformation, minister bias, or reported-state memory is added in M1.
