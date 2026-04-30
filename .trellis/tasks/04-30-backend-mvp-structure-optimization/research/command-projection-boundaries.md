# Research: command projection boundaries

- Query: Research comprehensive backend structure optimization opportunities in planning command flow, game/planning, game/orders, game/query, game/projection, and transport dispatch boundaries. Identify duplication, cross-layer coupling, and concrete refactor batches that preserve protocol behavior.
- Scope: internal
- Date: 2026-04-30

## Findings

### Current ownership baseline

The backend structure spec says the current Turn V2 runtime is `planning -> resolving -> next planning`, with logic moved to the lowest layer that has enough information to perform it. It assigns:

- `game/planning` to planning command validation and draft writes.
- `game/query` to authoritative state -> player observations/views.
- `game/projection` to events/state -> client sync/report messages.
- `game/resolution` to resolving runner and state-only commit helpers.
- `transport` to protocol dispatch, codecs, HTTP/websocket transport boundaries.

Related spec: `.trellis/spec/backend/directory-structure.md`.

Protocol behavior to preserve:

- Transport V2 uses `ClientFrame.meta + oneof target` and `ServerFrame.meta + oneof target`; `transport.proto` defines `CommandMeta`, `EventMeta`, top-level command/event envelopes, and game command/event oneofs (`protocol/panoptes/proto/v1/transport.proto:19`, `protocol/panoptes/proto/v1/transport.proto:63`, `protocol/panoptes/proto/v1/transport.proto:83`, `protocol/panoptes/proto/v1/transport.proto:92`, `protocol/panoptes/proto/v1/transport.proto:120`, `protocol/panoptes/proto/v1/transport.proto:152`).
- Planning batch commands use `CommandEnvelope` plus `MsgGameCommandBatch` without changing existing field names/numbers (`protocol/panoptes/proto/v1/orders.proto:75`, `protocol/panoptes/proto/v1/orders.proto:97`).
- Planning snapshots and game sync messages are the client-visible projection surfaces (`protocol/panoptes/proto/v1/turn.proto:67`, `protocol/panoptes/proto/v1/turn.proto:209`).

### Files found

- `server/internal/game/command_handler.go` - registry-level game command adapter from transport registry to room.
- `server/internal/game/room.go` - room lifecycle, transport send bridge, planning draft wrappers, active march sync entry points, game sync broadcast.
- `server/internal/game/room_march.go` - active march state and route preview helper logic.
- `server/internal/game/map_actions.go` - map-action order translation into domain events.
- `server/internal/game/settlement.go` - room-level resolving orchestration hooks and unit-order freeze.
- `server/internal/game/planning/adapter.go` - manual `PlanningCommand` -> planning `IntentEnvelope` mapping.
- `server/internal/game/planning/service.go` - planning command orchestration, validation, mutation, response sending, preview generation, unit-order validation, minister draft transitions.
- `server/internal/game/planning/preview.go` - build/recipe preview evaluation helpers.
- `server/internal/game/orders/types.go` - unit-order types and conversion to domain directives/resolution orders.
- `server/internal/game/query/planning.go` - planning snapshot projection from state/drafts/active marches.
- `server/internal/game/query/observation.go` - per-viewer observation/memory store and visibility snapshots.
- `server/internal/game/query/views.go` - player/node/unit view projection.
- `server/internal/game/projection/sync.go` - `MsgGameSync` and typed domain-event envelope projection.
- `server/internal/game/projection/report.go` - event string/data payload mapping and planning-start event projection.
- `server/internal/game/session/planning_start.go` - `MsgPlanningStart` construction from observation plus planning snapshot.
- `server/internal/game/turn/coordinator.go` - phase gate, command routing, command-batch envelope conversion, autonomous intent submission.
- `server/internal/transport/dispatch/frame_dispatcher.go` - generated-style top-level `ClientFrame` dispatcher to specific handler interfaces.
- `server/internal/transport/dispatch/generated_commands.go` - generated command oneof dispatchers and handler interfaces.
- `server/internal/transport/inbound/dispatcher.go` - separate top-level inbound dispatcher to coarse `Handle*Command` handlers.
- `server/internal/transport/codec/codec.go` - outbound `proto.Message` -> `ServerFrame` wrapping.
- `server/internal/transport/websocket/client.go` - websocket read loop, inbound dispatch, problem responses, inbound message-name logging.
- `server/internal/transport/websocket/transport.go` - `GameTransport` adapter that encodes outbound messages with event meta.
- `server/cmd/server/app/server.go` - app wiring for websocket hub, inbound dispatcher, lobby handler, and game registry handler.

### Command flow observations

There are two frame dispatch paths:

- `transport/dispatch.Dispatcher` dispatches `ClientFrame` to generated `AuthHandler`, `LobbyHandler`, and `GameHandler` methods (`server/internal/transport/dispatch/frame_dispatcher.go:8`, `server/internal/transport/dispatch/frame_dispatcher.go:14`, `server/internal/transport/dispatch/frame_dispatcher.go:22`).
- `transport/inbound.Dispatcher` separately dispatches the same `ClientFrame` shape to coarse `HandleAuthCommand`, `HandleLobbyCommand`, and `HandleGameCommand` handlers (`server/internal/transport/inbound/dispatcher.go:9`, `server/internal/transport/inbound/dispatcher.go:21`, `server/internal/transport/inbound/dispatcher.go:27`, `server/internal/transport/inbound/dispatcher.go:34`).
- App wiring uses `transport/inbound.Dispatcher`, not `transport/dispatch.Dispatcher` (`server/cmd/server/app/server.go:38`).

The game path then re-enters generated dispatch inside the coordinator:

- `RegistryCommandHandler.HandleGameCommand` locates a room by participant ID and forwards the whole `GameCommand` (`server/internal/game/command_handler.go:18`, `server/internal/game/command_handler.go:23`, `server/internal/game/command_handler.go:27`).
- `GameRoom.HandleGameCommand` forwards to `turn.Coordinator` (`server/internal/game/room.go:82`, `server/internal/game/room.go:86`).
- `Coordinator.HandleGameCommand` phase-gates commands, then calls `cmddispatch.DispatchGameCommand` with an internal `gameCommandHandler` (`server/internal/game/turn/coordinator.go:147`, `server/internal/game/turn/coordinator.go:151`, `server/internal/game/turn/coordinator.go:155`, `server/internal/game/turn/coordinator.go:159`).
- `gameCommandHandler.Planning` delegates the whole `PlanningCommand` to `planning.Service.HandleCommand` (`server/internal/game/turn/coordinator.go:166`, `server/internal/game/turn/coordinator.go:170`).

Planning oneof handling is duplicated:

- Generated dispatch already has a full `PlanningHandler` interface and oneof switch for all planning commands (`server/internal/transport/dispatch/generated_commands.go:91`, `server/internal/transport/dispatch/generated_commands.go:109`, `server/internal/transport/dispatch/generated_commands.go:117`).
- `planning.EnvelopeFromPlanningCommand` manually switches over the same `PlanningCommand` oneof to produce intents or preview markers (`server/internal/game/planning/adapter.go:12`, `server/internal/game/planning/adapter.go:23`, `server/internal/game/planning/adapter.go:64`).
- `turn.planningCommandFromEnvelope` manually maps `CommandEnvelope` oneofs into `PlanningCommand` oneofs for batches (`server/internal/game/turn/coordinator.go:187`, `server/internal/game/turn/coordinator.go:191`, `server/internal/game/turn/coordinator.go:210`, `server/internal/game/turn/coordinator.go:214`).
- Websocket inbound logging has another manual planning-command name switch and currently does not cover every newer planning/game body, such as build/recipe preview, chat batch, and command batch in all branches (`server/internal/transport/websocket/client.go:193`, `server/internal/transport/websocket/client.go:197`, `server/internal/transport/websocket/client.go:202`, `server/internal/transport/websocket/client.go:230`).

Request/trace correlation is important and currently tested:

- `planning.Service.HandleCommand` derives event meta from inbound context for preview commands, and `HandleIntent` derives meta from `IntentEnvelope` for normal commands (`server/internal/game/planning/service.go:83`, `server/internal/game/planning/service.go:93`, `server/internal/game/planning/service.go:143`, `server/internal/game/planning/service.go:200`).
- `TestHandleGameCommandPropagatesRequestMetaToOutboundResponses` expects both result and planning snapshot messages to carry the request and trace IDs (`server/internal/game/turn_v2_test.go:167`, `server/internal/game/turn_v2_test.go:182`, `server/internal/game/turn_v2_test.go:217`, `server/internal/game/turn_v2_test.go:225`).

Optimization opportunity:

- Keep wire protocol unchanged, but converge command mapping to one owned path. The lowest-risk path is not to remove generated dispatch first; instead, make `planning` own a small adapter API that both direct `PlanningCommand` and `CommandEnvelope` batch flow use. Then remove duplicate batch conversion from `turn` after tests are green.
- Treat websocket message-name logging as observability, not dispatch policy. It should use protobuf reflection or a shared command-name helper so command additions do not require another manual switch.

### `game/planning` observations

`planning.Service` currently mixes five responsibilities:

- Transport-ish command context and problem construction through `cmddispatch.InboundContext`, `transport.ContextWithEventMeta`, and `transport/problem` imports (`server/internal/game/planning/service.go:26`, `server/internal/game/planning/service.go:27`, `server/internal/game/planning/service.go:28`, `server/internal/game/planning/service.go:83`, `server/internal/game/planning/service.go:200`).
- Domain validation and draft mutation for policies, research, institutions, build orders, recipes, unit orders, reveal tokens, and minister drafts (`server/internal/game/planning/service.go:146`, `server/internal/game/planning/service.go:393`, `server/internal/game/planning/service.go:412`, `server/internal/game/planning/service.go:433`, `server/internal/game/planning/service.go:564`, `server/internal/game/planning/service.go:624`, `server/internal/game/planning/service.go:652`).
- Direct outbound message sending for every command result plus optional planning snapshot (`server/internal/game/planning/service.go:155`, `server/internal/game/planning/service.go:166`, `server/internal/game/planning/service.go:233`, `server/internal/game/planning/service.go:245`, `server/internal/game/planning/service.go:406`, `server/internal/game/planning/service.go:427`, `server/internal/game/planning/service.go:586`, `server/internal/game/planning/service.go:643`, `server/internal/game/planning/service.go:674`, `server/internal/game/planning/service.go:684`).
- Preview response construction and route-planner usage (`server/internal/game/planning/service.go:93`, `server/internal/game/planning/service.go:731`, `server/internal/game/planning/service.go:757`).
- Unit-order validation and ECS/staticdata helpers that are not planning-response specific (`server/internal/game/planning/service.go:222`, `server/internal/game/planning/service.go:256`, `server/internal/game/planning/service.go:334`, `server/internal/game/planning/service.go:376`).

The `planning.Session` interface is broad:

- It includes state access, participant lookup, submit, transport sends, dev mode, every draft write, snapshot sending, node view building, and node lookup (`server/internal/game/planning/service.go:33`, `server/internal/game/planning/service.go:37`, `server/internal/game/planning/service.go:39`, `server/internal/game/planning/service.go:46`, `server/internal/game/planning/service.go:47`, `server/internal/game/planning/service.go:48`).
- `GameRoom` implements that interface, which keeps root `game` as the bridge but also invites state-only behavior into `GameRoom` (`server/internal/game/room.go:45`, `server/internal/game/room.go:46`, `server/internal/game/room.go:163`, `server/internal/game/room.go:175`, `server/internal/game/room.go:202`, `server/internal/game/room.go:255`).

Optimization opportunity:

- Split `planning.Service` into state-only command execution and a thin delivery adapter. Preserve response order exactly: command result first, token result if applicable, planning snapshot last. Tests already assert result/snapshot ordering (`server/internal/game/turn_v2_test.go:199`, `server/internal/game/turn_v2_test.go:203`, `server/internal/game/turn_v2_test.go:210`).
- Narrow planning interfaces by command family. For example:
  - `StateProvider` / `ParticipantProvider` for validation.
  - `DraftWriter` for queueing planning drafts.
  - `PlanningMessenger` for result/snapshot delivery.
  - `RoutePreviewer` for route previews.
  This can be staged without changing external behavior by keeping `GameRoom` as the initial adapter.
- Move transport-problem creation to an edge adapter where possible. State-only planning helpers can return project error codes or typed results; the command adapter can translate to `transport/problem`.

### `game/orders` observations

`game/orders` currently owns only unit-order type constants and conversions:

- Unit action constants and `UnitOrder` fields live in `orders/types.go` (`server/internal/game/orders/types.go:11`, `server/internal/game/orders/types.go:13`, `server/internal/game/orders/types.go:25`).
- It converts to/from `domain.UnitDirective` and `domain.UnitResolutionOrder` (`server/internal/game/orders/types.go:54`, `server/internal/game/orders/types.go:67`, `server/internal/game/orders/types.go:80`).

But unit-order behavior is spread elsewhere:

- Planning validates unit orders and attack/charge/range rules in `planning.Service` (`server/internal/game/planning/service.go:222`, `server/internal/game/planning/service.go:256`, `server/internal/game/planning/service.go:277`, `server/internal/game/planning/service.go:322`).
- Root `GameRoom.SetUnitOrder` writes planning directives, preserves paths, syncs active marches, and deletes active marches for non-move orders (`server/internal/game/room.go:202`, `server/internal/game/room.go:214`, `server/internal/game/room.go:231`, `server/internal/game/room.go:232`, `server/internal/game/room.go:236`).
- Active march preview creation and refresh lives in root `game` (`server/internal/game/room_march.go:16`, `server/internal/game/room_march.go:39`, `server/internal/game/room_march.go:81`).
- Resolving freezes planning directives into resolving unit orders in root `game/settlement.go` (`server/internal/game/settlement.go:47`, `server/internal/game/settlement.go:57`, `server/internal/game/settlement.go:67`, `server/internal/game/settlement.go:78`).
- Map actions are filtered from planning directives in root `game/map_actions.go` (`server/internal/game/map_actions.go:19`, `server/internal/game/map_actions.go:27`, `server/internal/game/map_actions.go:38`).

Optimization opportunity:

- Promote `game/orders` from type-only to the unit-order state boundary. Good candidates:
  - `ValidatePlanningUnitOrder(state, playerID, order) string`
  - `ApplyPlanningUnitOrder(state, order, routePreviewer) OrderApplyResult`
  - `CancelPlanningUnitOrder(state, playerID, unitID)`
  - `BuildResolvingUnitOrders(state, routePreviewer) map[string]domain.UnitResolutionOrder`
  - `BuildMapActionEvents(state) []event.Event` or a sibling `game/orders/mapactions` if event imports feel too broad.
- Keep route planning injected as an interface or function to avoid making `orders` own combat planner construction immediately.
- This reduces `GameRoom` to session/transport glue and moves state-only order behavior to the lowest layer with enough information, matching the backend structure spec.

### `game/query` and `game/projection` observations

`game/query` has a clear read-side role but mixes projection detail with some command-adjacent draft semantics:

- `BuildPlanningSnapshot` reads pending planning drafts and active marches, sorts them, and produces `MsgPlanningSnapshot` (`server/internal/game/query/planning.go:17`, `server/internal/game/query/planning.go:31`, `server/internal/game/query/planning.go:36`, `server/internal/game/query/planning.go:75`, `server/internal/game/query/planning.go:95`).
- It overlays active marches into queued unit orders so long movement remains visible during planning (`server/internal/game/query/planning.go:36`, `server/internal/game/query/planning.go:41`, `server/internal/game/query/planning.go:58`, `server/internal/game/query/planning.go:117`).
- It duplicates local map cloning via `cloneStringMap` even though `game/orders` has an equivalent helper (`server/internal/game/query/planning.go:140`, `server/internal/game/orders/types.go:94`).

`game/projection` composes game sync from observation plus query views:

- `ProjectGameSyncFromObservation` fills observation nodes, units, player view, planning snapshot, minister proposals, and domain events (`server/internal/game/projection/sync.go:24`, `server/internal/game/projection/sync.go:32`, `server/internal/game/projection/sync.go:42`, `server/internal/game/projection/sync.go:58`).
- `DomainEventEnvelopes` orders channels explicitly and calls `domainEventEnvelope` (`server/internal/game/projection/sync.go:65`, `server/internal/game/projection/sync.go:69`, `server/internal/game/projection/sync.go:76`, `server/internal/game/projection/sync.go:87`).
- `ProjectPlanningStartEvents` reuses the same domain event envelope mapper for planning-start events (`server/internal/game/projection/report.go:22`, `server/internal/game/projection/report.go:28`).
- `EventPayloadFromEvent` is documented as the single external string/data mapping entrypoint (`server/internal/game/projection/report.go:33`, `server/internal/game/projection/report.go:38`).

Projection duplication/coupling:

- `session.BuildPlanningStartMessageFromObservation` builds a planning-start message by combining observation fields plus a planning snapshot, overlapping with `ProjectGameSyncFromObservation` composition patterns (`server/internal/game/session/planning_start.go:16`, `server/internal/game/session/planning_start.go:30`, `server/internal/game/session/planning_start.go:37`, `server/internal/game/session/planning_start.go:44`).
- Root `GameRoom.broadcastGameSync` owns per-human observation building and calls projection directly (`server/internal/game/room.go:328`, `server/internal/game/room.go:338`, `server/internal/game/room.go:340`, `server/internal/game/room.go:348`).

Optimization opportunity:

- Introduce a shared projection composition helper for "state + observation + phase context" that both planning-start and game-sync can use. Keep `MsgPlanningStart` and `MsgGameSync` wire shapes unchanged.
- Keep `query` as read-side only. Avoid moving command validation there; if `BuildPlanningSnapshot` needs order-specific helper functions, prefer exported helpers from `game/orders` or unexported projection helpers rather than duplicating state transformations.
- Consider moving primitive clone helpers to `domain` or a tiny internal helper only if duplication grows. Do not add a new generic utility package for one or two map clone uses.

### Transport boundary observations

Outbound framing is centralized and should remain transport-owned:

- `codec.WrapServerMessage` maps concrete protobuf messages to `ServerFrame` oneofs (`server/internal/transport/codec/codec.go:53`, `server/internal/transport/codec/codec.go:61`, `server/internal/transport/codec/codec.go:63`, `server/internal/transport/codec/codec.go:134`).
- `WSTransport.Send` and `Broadcast` encode messages with `EventMetaFromContext` (`server/internal/transport/websocket/transport.go:29`, `server/internal/transport/websocket/transport.go:34`, `server/internal/transport/websocket/transport.go:42`, `server/internal/transport/websocket/transport.go:47`).
- `Client.readPump` decodes `ClientFrame`, dispatches it, and returns `Problem` frames on errors (`server/internal/transport/websocket/client.go:50`, `server/internal/transport/websocket/client.go:61`, `server/internal/transport/websocket/client.go:71`, `server/internal/transport/websocket/client.go:77`, `server/internal/transport/websocket/client.go:124`).

Boundary issue:

- `game/planning` imports transport packages for inbound context, event meta, and problems. This is not a hard violation of the documented `transport -> game` dependency direction because it imports an abstract/common transport package, not `transport/websocket`; however it does mean core planning command logic has transport error/meta concerns embedded (`server/internal/game/planning/service.go:26`, `server/internal/game/planning/service.go:27`, `server/internal/game/planning/service.go:28`).
- `transport/interface.go` defines `GameRoom.HandleGameCommand` in terms of `dispatch.InboundContext` and `pb.GameCommand`, so the transport package depends on generated proto and dispatch context while game implements it (`server/internal/transport/interface.go:25`, `server/internal/transport/interface.go:27`). This is an acceptable adapter seam today, but it makes "game command handling" protocol-shaped rather than application-shaped.

Optimization opportunity:

- Keep `transport/codec` and websocket problem framing unchanged.
- Define a game-owned inbound metadata type or command envelope only after planning internals are simplified. A premature boundary move would touch app wiring, websocket, debug HTTP, room registry, coordinator tests, and planning tests at once.
- First reduce duplication inside `transport` by making `inbound.Dispatcher` delegate to or replace `dispatch.Dispatcher`, then reduce game imports of `transport/problem` in smaller steps.

## Concrete Refactor Batches

### Batch 1: Centralize planning command adaptation

Goal: remove duplicated planning oneof mapping without changing protocol fields or message order.

Steps:

1. Add planning-owned helpers:
   - `EnvelopeFromPlanningCommand(inbound, cmd)` already exists; keep it.
   - Add `PlanningCommandFromCommandEnvelope(envelope)` or `EnvelopeFromCommandEnvelope(baseCtx, envelope)` in `game/planning`, not `game/turn`.
2. Make `turn.gameCommandHandler.CommandBatch` call the planning helper instead of local `planningCommandFromEnvelope`.
3. Preserve `participant_id -> PlayerID` and `command_id -> RequestID` behavior from current batch code (`server/internal/game/turn/coordinator.go:196`, `server/internal/game/turn/coordinator.go:197`, `server/internal/game/turn/coordinator.go:200`).
4. Delete or shrink `turn.planningCommandFromEnvelope`.
5. Add/adjust tests for each `CommandEnvelope` body that currently maps in `turn`.

Behavior constraints:

- No proto changes.
- Existing unsupported batch body behavior should remain `ErrPhaseMismatch` or be deliberately converted to the same public problem code currently observed by callers.
- Result/snapshot order and request meta must stay unchanged.

### Batch 2: Split preview commands from mutating intents

Goal: make preview commands read-only and easier to test independently.

Steps:

1. Add `planning.PreviewService` or unexported `handlePreviewCommand` with no draft mutation.
2. Move preview switch currently in `Service.HandleCommand` to that helper (`server/internal/game/planning/service.go:88`, `server/internal/game/planning/service.go:93`).
3. Keep response builders from `preview.go` and path preview from `service.go`, but consider moving path preview into `preview.go` for ownership consistency (`server/internal/game/planning/service.go:731`).
4. Preserve no-snapshot behavior for previews. Tests already check preview rejection does not mutate or send planning snapshots (`server/internal/game/planning/service_rules_test.go` references in rg output).

Behavior constraints:

- Preview response request IDs from request body must remain populated (`protocol/panoptes/proto/v1/turn.proto:81`, `protocol/panoptes/proto/v1/turn.proto:99`, `protocol/panoptes/proto/v1/turn.proto:117`).
- Transport-level request meta must still wrap outbound response through context.

### Batch 3: Move unit-order validation and freeze helpers into `game/orders`

Goal: put unit-order state behavior next to unit-order types, reducing `planning.Service` and root `GameRoom`.

Steps:

1. Move `validateUnitOrder`, unit capability helpers, `findAnyUnit`, and range helpers from `planning.Service` into `game/orders` or a new `game/orders/validation.go`.
2. Keep the public return value as an error-code string to avoid response behavior changes.
3. Move `lockUnitResolutionOrders` logic into `game/orders.BuildResolvingUnitOrders(state, previewer)` and leave `GameRoom` as a hook adapter.
4. Move active-march draft apply/cancel logic out of `GameRoom.SetUnitOrder` into a state-only orders helper that accepts route preview callbacks.
5. Keep `GameRoom.SetUnitOrder` as a compatibility wrapper during the batch, then narrow `planning.Session`.

Behavior constraints:

- Move orders must continue to sync active marches and preserve path previews.
- Attack-after-move must continue to preserve active march path or secondary-node route path (`server/internal/game/room.go:214`, `server/internal/game/room.go:220`, `server/internal/game/room.go:224`).
- `BuildPlanningSnapshot` must continue to show active marches as queued move orders (`server/internal/game/query/planning.go:36`, `server/internal/game/query/planning.go:117`).

### Batch 4: Narrow `planning.Session` and isolate delivery

Goal: keep `planning` focused on command execution while `GameRoom` remains transport/session glue.

Steps:

1. Introduce a planning result struct that records outbound messages and whether a snapshot should be sent.
2. Change internal handler functions to return that result instead of calling `room.SendToPlayer` directly.
3. Keep `Service.HandleCommand` as the public compatibility method that sends messages in the current order.
4. Narrow `planning.Session` after call sites no longer need direct `SendToPlayer`, `SendPlanningSnapshot`, `BuildNodeViewForPlayer`, and draft write wrappers in the same interface.

Behavior constraints:

- Preserve command result message types and order.
- Preserve ignored send errors if the current behavior intentionally discards them with `_ =`.
- Preserve request/trace meta on all messages.

### Batch 5: Consolidate transport dispatch and inbound logging

Goal: remove duplicated top-level dispatch and manual observability switches while preserving websocket behavior.

Steps:

1. Decide whether `transport/inbound.Dispatcher` should remain the production entrypoint. If yes, make `transport/dispatch.Dispatcher` purely generated command dispatch, not another top-level frame dispatcher; if no, wire app to `transport/dispatch.Dispatcher` plus handlers.
2. Remove or mark one top-level frame dispatcher obsolete after tests prove production wiring uses only one.
3. Replace websocket `inboundMessageName` planning switch with proto reflection on the deepest message body where possible.
4. Add coverage for build preview, recipe preview, chat, and command batch names if logs are test-covered later.

Behavior constraints:

- `Client.readPump` must still send `Problem` frames with original `CommandMeta` on decode/dispatch errors (`server/internal/transport/websocket/client.go:50`, `server/internal/transport/websocket/client.go:68`, `server/internal/transport/websocket/client.go:77`, `server/internal/transport/websocket/client.go:124`).
- `codec.WrapServerMessage` should remain the only outbound oneof wrapper.

### Batch 6: Share projection composition for planning start and game sync

Goal: reduce duplicated state+observation composition and keep read/projection boundaries clear.

Steps:

1. Add a small projection input struct, for example `projection.ObservedState{State, Observation, Turn, Phase, NextPhase}`.
2. Keep `query.BuildPlanningSnapshot` as the snapshot source.
3. Refactor `session.BuildPlanningStartMessageFromObservation` and `projection.ProjectGameSyncFromObservation` to use shared helper functions for resolving `MyPlayer`, `Nodes`, and `Units`.
4. Do not move `PlanningStart` into `projection` unless it reduces imports without creating a session/projection cycle. The current session package owns planning-start lifecycle state, so a helper-only extraction is safer.

Behavior constraints:

- `MsgPlanningStart` and `MsgGameSync` wire shapes remain unchanged.
- Planning-start events must stay separate from game-sync resolving events. Existing tests explicitly guard that separation in projection tests (rg found `TestProjectGameSyncDoesNotContainPlanningStartActivationEvents`).

## Duplication Summary

- Planning oneof mapping exists in generated dispatch, planning adapter, command-batch conversion, and websocket logging.
- Top-level frame dispatch exists in both `transport/dispatch` and `transport/inbound`.
- Unit-order behavior is split between `game/orders`, `game/planning`, root `game`, and `game/settlement`.
- Planning response sending repeats result + optional snapshot patterns across many handler methods.
- `cloneStringMap` exists in both query planning snapshot and orders conversion.
- State+observation projection patterns are repeated between planning-start and game-sync construction.

## Cross-Layer Coupling Summary

- `game/planning` imports transport dispatch/problem/meta packages, generated proto, engine packages, static data, ECS, and room-like session delivery in one service.
- Root `game.GameRoom` still owns state-only order/march helpers in addition to room lifecycle and transport sends.
- `transport/interface.GameRoom` is protocol-shaped (`pb.GameCommand` plus `dispatch.InboundContext`), which is pragmatic today but keeps transport context visible in game internals.
- `game/query.BuildPlanningSnapshot` knows active-march display semantics, which is acceptable as read-side projection but should not grow into command validation.

## Recommended Implementation Order

1. Batch 1: centralize planning command adaptation. Low risk, removes direct duplication, improves future command additions.
2. Batch 2: split preview commands. Low to medium risk, clarifies read-only command behavior.
3. Batch 3: move unit-order validation/freeze/apply helpers into `game/orders`. Medium risk, highest structural payoff.
4. Batch 4: narrow planning delivery/session interfaces. Medium risk, should follow Batch 3 to avoid moving unstable methods twice.
5. Batch 5: consolidate transport dispatch/logging. Medium risk because app wiring and tests may need coordination.
6. Batch 6: share projection composition. Low to medium risk, mostly read-side, but should be done after command refactors to avoid simultaneous broad churn.

## External References

- None. This research is repository-internal and protocol-preservation focused.

## Related Specs

- `AGENTS.md` - global backend constraints: no manual generated proto edits, transport/game dependency boundary, engine systems mutate through events, and proto field/name preservation.
- `.trellis/spec/backend/index.md` - backend guideline index.
- `.trellis/spec/backend/directory-structure.md` - active backend structure guidance and `GameRoom` thinness rule.
- `.trellis/workflow.md` - research artifacts must be persisted under task `research/`.

## Caveats / Not Found

- No active Trellis task was set by `task.py current --source`; the user supplied the exact target task path, so this artifact was written there.
- `quality-guidelines.md` and `error-handling.md` are still placeholders, so concrete backend quality/error conventions came from `AGENTS.md`, `docs/PANOPTES_AGENT_BACKEND.md`, code, and tests.
- This research did not run tests and did not edit code.
- The generated file header in `transport/dispatch/generated_commands.go` says not to edit it manually; any change to that file should go through the existing generator path or generated-source workflow.
