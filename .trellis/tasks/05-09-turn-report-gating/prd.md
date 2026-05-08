# brainstorm: turn report gating for next turn planning

## Goal

Prevent the next planning window from opening while the previous turn's settlement playback and minister reaction window are still in progress. Keep `planning` and `resolving` as the only authoritative gameplay phases, but introduce a display/intermission gate so the player never loses the first part of the new turn to unresolved settlement playback.

## What I already know

* The repo already treats `planning / resolving` as the authoritative turn model.
* `docs/TURN_V2_REFACTOR_PLAN.md` already mentions `turn_report` as an optional display state.
* `protocol/panoptes/proto/v1/turn.proto` already defines `MsgTurnReport`.
* The server currently emits `MsgGameSync` after resolving and then advances toward the next `MsgPlanningStart`.
* The client already has `TurnReportPanel`, but it is only a passive summary view over `SettlementStore`.
* `GameStateCache.ResolveGameSyncPhase()` currently maps `MsgGameSync` with events to `resolving`, which makes the report window invisible as a distinct presentation state.
* The existing client state flow already separates `MsgGameSync`, `MsgPlanningStart`, and settlement summary handling, so this can be extended without rewriting the whole UI stack.

## Assumptions (temporary)

* Use `turn_report` as a presentation gate only, not as a third rules phase.
* Keep the authoritative server loop as `planning -> resolving -> planning`.
* Add a client/server completion handshake for the report window, with a timeout fallback so the game cannot deadlock.
* Treat 10 seconds as the minimum reaction window for minister output and report playback.

## Open Questions

None blocking for the plan. The implementation will decide whether the completion signal is a new proto message or a small reuse of an existing game command pattern.

## Requirements (evolving)

* After resolving, the server must be able to hold the room in a report gate before sending the next `MsgPlanningStart`.
* The report gate must allow settlement playback and minister generation to run in parallel.
* The client must be able to show the report state explicitly instead of pretending it is already in planning.
* Planning input must stay locked until the report gate completes.
* The gate must have a timeout fallback.
* Existing settlement data and phase data must remain source-of-truth on the server.
* The client must not infer gameplay rules from the report gate.

## Proposed Implementation Shape

1. Add a report gate in the turn coordinator between resolving and the next planning start.
2. Reuse or extend `MsgTurnReport` so the server can describe the report window explicitly.
3. Add a completion path from the client report UI back to the server, with timeout fallback.
4. Make `MsgGameSync` and client phase mapping understand `turn_report` as a presentation state.
5. Let minister generation start during the report gate so the 10-second reaction window does not block the next turn.

## Acceptance Criteria (evolving)

* [ ] After a turn resolves, the client shows a report/intermission state before planning becomes interactive.
* [ ] The next `MsgPlanningStart` is not delivered until the report gate closes.
* [ ] The client can finish report playback and signal readiness without using gameplay logic locally.
* [ ] The gate times out safely if a client never acknowledges readiness.
* [ ] Minister warm-up runs during the report gate.
* [ ] Existing planning and settlement tests still pass after the flow change.

## Definition of Done (team quality bar)

* Tests added/updated (unit/integration where appropriate)
* Lint / typecheck / CI green
* Docs/notes updated if behavior changes
* Rollout/rollback considered if risky

## Out of Scope (explicit)

* Changing the authoritative turn model away from `planning / resolving`
* Moving gameplay rule calculation into the client
* Reworking settlement ordering or combat resolution rules
* Adding new gameplay content

## Technical Notes

* Server flow entry: `server/internal/game/turn/coordinator.go`
* Runtime state: `server/internal/game/session/runtime.go`
* Phase projection: `server/internal/game/projection/sync.go`
* Settlement/report projection: `server/internal/game/projection/report.go`
* Client phase hydration: `client/Assets/Scripts/Runtime/Core/Application/Stores/StoreHydrationProtocolMapper.cs`
* Client game state cache: `client/Assets/Scripts/Runtime/Core/Application/Cache/GameStateCache.cs`
* Report UI: `client/Assets/Scripts/Runtime/Presentation/UI/Turn/TurnReportPanel.cs`
* Protocol already contains `MsgTurnReport` in `protocol/panoptes/proto/v1/turn.proto`
* Relevant docs: `docs/TURN_V2_REFACTOR_PLAN.md`, `docs/SERVER_RUNTIME_ARCHITECTURE.md`, `docs/PANOPTES_AGENT_FRONTEND.md`

