# Research: runtime orchestration

- Query: Comprehensive backend structure optimization opportunities for `server/internal/game` root, `game/session`, `game/turn`, and room/runtime orchestration in the current backend MVP structure optimization task.
- Scope: internal
- Date: 2026-04-30

## Findings

### Files Found

- `server/internal/game/room.go` - root room facade that wires session runtime, turn coordinator, planning/chat interfaces, sync broadcast, game-over handling, debug visibility, and planning draft writes.
- `server/internal/game/settlement.go` - room-level entrypoint for the resolving pipeline and remaining room-owned order freeze hook.
- `server/internal/game/room_march.go` - room-owned active march synchronization, route preview, and post-settlement march refresh helpers.
- `server/internal/game/map_actions.go` - room-owned map action event construction for `settle_city`.
- `server/internal/game/session/runtime.go` - large runtime object that owns state lifecycle, participant lookup, transport sending, bootstrap/catalog sync, observations, chat sequence, planning-start cache, minister engine integration, and initial world setup.
- `server/internal/game/session/planning_start_runner.go` - state-only planning-start runner with explicit stage order.
- `server/internal/game/session/controller.go` - human/autonomous controller abstraction and bot planning intent submission.
- `server/internal/game/turn/coordinator.go` - turn loop, phase gates, planning begin, command routing, submission wait, and max-turn draw handling.
- `server/internal/game/planning/service.go` - planning command/intent service; its `Session` interface is currently implemented by `GameRoom`.
- `server/internal/game/chat/service.go` - chat command service with a smaller session interface.
- `server/internal/game/resolution/runner.go` - resolving runner and stage order, using hooks for room-dependent behavior.
- `server/internal/game/resolution/planning_commit.go` - current state-only planning commit event builder; this shows the current task's first extraction already exists.
- `server/internal/domain/turn_runtime.go` - current domain helper for post-resolution scratch cleanup; this shows the current task's second extraction already exists.
- `server/internal/game/resolution/runner_architecture_test.go` - architecture test keeping resolution from importing root `game`.
- `server/internal/transport/websocket/architecture_test.go` - architecture test keeping websocket transport from importing business packages.
- `server/internal/game/turn_v2_test.go` - broad integration coverage for Turn V2 resolving behavior and sync projection.
- `server/internal/game/session/runtime_test.go` - broad runtime/bootstrap/planning-start/minster draft coverage.
- `server/internal/domain/state_runtime_test.go` - focused domain tests for runtime container initialization and scratch cleanup.

### Current Shape

`GameRoom` is now mostly a facade rather than the resolving implementation itself, but it still implements many unrelated host surfaces. It owns only four fields (`ID`, `runtime`, `coordinator`, `prepared`) at `server/internal/game/room.go:36`, wires `gamesession.Runtime` and `gameturn.Coordinator` at `server/internal/game/room.go:48`, starts initialization plus registration plus coordinator goroutine at `server/internal/game/room.go:63`, and forwards command handling to the coordinator at `server/internal/game/room.go:82`.

The current task's intended minimal extraction already appears implemented: `RunTurnResolution` delegates planning commit event construction to `gameresolution.BuildPlanningCommitEvents` at `server/internal/game/settlement.go:24`, and clears scratch through `TurnRuntime.ClearPostResolutionScratch()` at `server/internal/game/settlement.go:44`. The helper itself is state-only in `server/internal/game/resolution/planning_commit.go:10`; the domain cleanup helper is in `server/internal/domain/turn_runtime.go:6`.

The remaining root `game` responsibilities are centered on adapters and room-dependent hooks: order freezing still calls `room.lockUnitResolutionOrders()` at `server/internal/game/settlement.go:26`, route preview is still built from room helpers at `server/internal/game/settlement.go:74`, active marches are refreshed through a hook at `server/internal/game/settlement.go:29`, and map action events are still room methods at `server/internal/game/settlement.go:32`.

`game/resolution` is directionally healthy. Its runner takes `*domain.GameState` plus hooks at `server/internal/game/resolution/runner.go:11`, stages are explicit at `server/internal/game/resolution/runner.go:48`, and the package has an architecture guard that fails if non-test resolution files import root `game` at `server/internal/game/resolution/runner_architecture_test.go:11`.

`game/turn.Coordinator` is a mixed coordinator/dispatcher. It owns the loop from bootstrap wait through planning, resolving, max-turn draw, and turn increment at `server/internal/game/turn/coordinator.go:51`; it begins planning by notifying humans, generating minister reports, and invoking autonomous controllers at `server/internal/game/turn/coordinator.go:96`; it also owns proto command routing and batch envelope expansion at `server/internal/game/turn/coordinator.go:147` and `server/internal/game/turn/coordinator.go:187`.

`game/session.Runtime` is the largest God-object candidate. It is 1,096 lines and carries state, participant bindings, config, transport, observations, submit channel, cancellation, bootstrap readiness, chat sequence/history, planning-start cache, minister draft cache, and minister engine fields in one struct at `server/internal/game/session/runtime.go:42`. Its methods span initialization at `server/internal/game/session/runtime.go:80`, transport sending at `server/internal/game/session/runtime.go:262`, planning-start state preparation at `server/internal/game/session/runtime.go:301`, report generation at `server/internal/game/session/runtime.go:352`, chat sequence/history at `server/internal/game/session/runtime.go:372`, initial capital/unit bootstrap at `server/internal/game/session/runtime.go:442`, observation/debug visibility at `server/internal/game/session/runtime.go:740`, and static catalog sync/bootstrap handoff at `server/internal/game/session/runtime.go:788`.

The current tests document several behavior contracts that any refactor must preserve: planning commit extraction coverage is focused in `server/internal/game/resolution/planning_commit_test.go:10`; scratch cleanup preserves active marches and point budgets while clearing planning/resolving transients in `server/internal/domain/state_runtime_test.go:36`; fatal resolution must keep planning lock-in events but skip post-combat systems in `server/internal/game/turn_v2_test.go:662`; bootstrap catalog sync sends manifest first and only sends the remainder after catalog sync in `server/internal/game/session/runtime_test.go:147`; planning-start preparation is guarded to once per turn in `server/internal/game/session/runtime_test.go:428`.

### God-Object Responsibilities

`GameRoom` currently acts as both boundary facade and behavior owner. It implements `planning.Session` at `server/internal/game/room.go:46`, exposes draft write methods from `QueueBuildOrder` through `CancelUnitOrder` at `server/internal/game/room.go:163`, owns broadcast/sync/game-over behavior at `server/internal/game/room.go:328`, owns resolution host methods at `server/internal/game/room.go:369`, and still has ECS helper queries such as `nodeIDAt` and `playerIDForUnit` at `server/internal/game/room.go:460`. This is not currently breaking dependency direction, but it invites future features to add more logic to root `game`.

The `planning.Session` interface is broad enough to keep `GameRoom` central. It requires state access, participant lookup, submit, send, dev mode, draft writes, snapshot sends, node view projection, and node lookup at `server/internal/game/planning/service.go:33`. Many methods are command-specific rather than required by every planning path, so future refactors can split it into smaller command ports only after tests are in place.

`session.Runtime` is a clearer God-object risk than `GameRoom` after the current task. It mixes lifecycle/state initialization, transport protocol sequencing, catalog chunk compression, observation store, debug visibility, chat, minister generation, and bootstrap map/player setup. `runtime.go` also contains pure helpers (`resolveRequestedSections` at `server/internal/game/session/runtime.go:993`, `compressCatalogSection` at `server/internal/game/session/runtime.go:1068`, `splitCatalogSection` at `server/internal/game/session/runtime.go:1081`) beside transport side effects, which makes targeted tests heavier than necessary.

`turn.Coordinator` is smaller but owns two axes: phase progression and command dispatch. The type's `Host` interface combines `chat.Session`, `planning.Session`, turn resolution, draw, and game-over checks at `server/internal/game/turn/coordinator.go:26`. That makes coordinator tests require a large stub host surface at `server/internal/game/turn/coordinator_test.go:222`.

### Dependency Direction Risks

The strongest hard dependency guard is already present: websocket transport cannot import auth/game/lobby business packages, enforced at `server/internal/transport/websocket/architecture_test.go:11`. `game/resolution` also has a local guard against importing root `game` at `server/internal/game/resolution/runner_architecture_test.go:11`.

The current risk is not an existing forbidden import cycle; it is adapter gravity. Root `game` imports many runtime subpackages in `server/internal/game/room.go:20`, including `planning`, `projection`, `query`, `resolution`, `session`, and `turn`. That is expected for a facade, but any new rule logic added there makes the facade harder to extract later.

`game/session` imports `game/query` and `game/participant` but not root `game`, which is acceptable today. However, `session.Runtime` also imports `transport`, `transport/problem`, `staticdata`, `engine/maploader`, `engine/minister`, `event`, and protobuf in `server/internal/game/session/runtime.go:20`. This broad import set makes `session` a hub package; adding more rule logic there would increase cross-layer coupling.

`game/turn` imports `game/chat`, `game/planning`, and `game/session` at `server/internal/game/turn/coordinator.go:16`, but not root `game`. The direction is still acceptable. The risk is conceptual rather than import-level: command dispatch and phase progression share one type, so new command classes may keep growing the coordinator.

### Concrete Refactor Batches

1. **Current task batch: keep as-is after verification.** The low-risk extraction requested by the PRD is already present: planning commit event construction lives in `game/resolution`, scratch cleanup lives in `domain`. Preserve this shape and use `go test ./...` under `server/` as the acceptance gate. Avoid expanding this batch into `Runtime` splitting.

2. **Map action extraction batch.** Move `plannedMapActionEvents`, `cityFoundingEvent`, `findUnitEntryByID`, and `isTerritoryExpansionUnit` from root `game` into a state-only package such as `game/resolution/mapaction` or `game/mapaction` only if it can accept `*domain.GameState` plus node lookup without `GameRoom`. The strongest candidate is `server/internal/game/map_actions.go:19`; it currently only needs state except for `r.NodeByID(target)` at `server/internal/game/map_actions.go:62`, which can become `state.GetNode(target)`. Add tests for success, invalid unit, invalid target, and `CanFoundCityAt` failure.

3. **March/order freeze extraction batch.** Extract `lockUnitResolutionOrders`, active march sync, route preview, and post-settlement march refresh behind a small state-level service that receives a route planner. Relevant code is `server/internal/game/settlement.go:47` and `server/internal/game/room_march.go:16`. This is higher risk than map actions because order freeze depends on previewing paths and preserving active marches across planning/replacement cases. Start with tests for move order path capture, attack preserving prior march path, active march deletion after arrival, and preview rebuild fallback.

4. **Bootstrap/catalog split batch.** Split `session.Runtime` by file and helper type, not by package boundary first. Candidate surfaces: `bootstrap_sync.go` for manifest/catalog sync and bootstrap readiness (`server/internal/game/session/runtime.go:788` through `server/internal/game/session/runtime.go:973`), `catalog_payload.go` for `resolveRequestedSections`, `compressCatalogSection`, and `splitCatalogSection` (`server/internal/game/session/runtime.go:993`), and `state_initializer.go` for map load, prepared state normalization, capital bootstrap, city-state initialization, and initial infantry (`server/internal/game/session/runtime.go:80` through `server/internal/game/session/runtime.go:150`, plus `server/internal/game/session/runtime.go:427`). This reduces file-level God-object pressure without changing public runtime APIs.

5. **Coordinator dispatch split batch.** Move `gameCommandHandler` and `planningCommandFromEnvelope` out of `coordinator.go` into `command_handler.go` inside `game/turn` or a narrow `turn/dispatch` file. This keeps `Coordinator.Start` focused on the loop while preserving the same `Host` interface initially. The extraction point is `server/internal/game/turn/coordinator.go:147`. After that, consider reducing `Host` only if tests show the stub burden remains high.

6. **Planning port narrowing batch.** Split `planning.Session` after the above lower-risk extractions. For example, preview commands need send/state/node view; build/research/policy commands need draft writes; submit needs only submit. The current interface at `server/internal/game/planning/service.go:33` is the binding that keeps `GameRoom` large. This should be deferred until command handler tests are easier to adjust, because it can touch many planning service tests.

7. **Game-over/session lifecycle batch.** Normalize `checkGameOver`, `handleDraw`, and `forfeitDisconnectedPlayer` into a room lifecycle helper after resolution/map/march are moved. They currently duplicate message construction, debug hook recording, broadcast, registry unregister, and runtime cancel at `server/internal/game/room.go:394`, `server/internal/game/room.go:410`, and `server/internal/game/room.go:427`. A small helper could reduce duplication without changing event semantics.

### Recommended Order

The conservative order is: finish and verify the current PRD extraction; then extract map actions; then split `session/runtime.go` by file/helper type; then split coordinator command dispatch; then attempt march/order freeze; then narrow `planning.Session`; finally deduplicate game-over lifecycle. This order keeps behavior-preserving, state-only changes first and leaves pathing/planning ports for later when tests can pin down edge cases.

## Code Patterns

- Facade wiring: `GameRoom` constructs runtime and coordinator together in `NewRoom` at `server/internal/game/room.go:48`.
- Runtime ownership: `Runtime` stores transport, observations, submit channel, state, bootstrap flags, chat state, planning-start cache, minister draft cache, and minister engine in one struct at `server/internal/game/session/runtime.go:42`.
- Hooked resolution: root `game` supplies only room-dependent hooks to `TurnResolutionRunner` at `server/internal/game/settlement.go:24`.
- State-only resolving helper: `BuildPlanningCommitEvents(state *domain.GameState)` avoids `GameRoom` entirely at `server/internal/game/resolution/planning_commit.go:10`.
- State-local scratch helper: `(*TurnRuntime).ClearPostResolutionScratch()` preserves durable active marches/point budgets while clearing transient inputs at `server/internal/domain/turn_runtime.go:6`.
- Broad planning port: `planning.Session` includes command writes, transport sends, node reveal/query, and submit in one interface at `server/internal/game/planning/service.go:33`.
- Coordinator command dispatch: `HandleGameCommand` gates by phase then dispatches through generated-command handler methods at `server/internal/game/turn/coordinator.go:147`.
- Bootstrap two-step protocol: `sendBootstrapMessages` sends manifests first at `server/internal/game/session/runtime.go:895`, while `HandleStaticCatalogSyncRequest` sends chunks/completion and then `sendBootstrapRemainder` at `server/internal/game/session/runtime.go:811`.
- Planning-start guard: `PreparePlanningStartStateIfNeeded` caches by turn and also prepares/applies minister drafts at `server/internal/game/session/runtime.go:301`.
- Architecture tests: resolution import guard at `server/internal/game/resolution/runner_architecture_test.go:11`; websocket transport business import guard at `server/internal/transport/websocket/architecture_test.go:11`.

## External References

- No external references were needed. This research is based on current repository code, tests, Trellis task PRD, and project/backend specs.

## Related Specs

- `AGENTS.md` - global project rules: no generated proto edits, engine systems mutate through events, strict transport/game dependency boundary, no new dependencies.
- `.trellis/workflow.md` - research artifacts must be persisted under task `research/`.
- `.trellis/spec/backend/index.md` - backend guidelines index.
- `.trellis/spec/backend/directory-structure.md` - current backend package layout and explicit convention to keep `GameRoom` thin.
- `.trellis/spec/backend/quality-guidelines.md` - currently mostly placeholder; no extra backend quality constraints found there.
- `.trellis/tasks/04-30-backend-mvp-structure-optimization/prd.md` - active task scope and acceptance criteria for conservative, behavior-preserving extraction.
- `.trellis/tasks/04-30-backend-mvp-structure-optimization/research/backend-current-state.md` - prior current-state notes; this file extends them with concrete follow-up batches.

## Caveats / Not Found

- The Trellis session had no active task according to `python3 ./.trellis/scripts/task.py current --source`; the user supplied the exact target path, so this artifact was written there without changing active task state.
- No tests directly named `plannedMapActionEvents`, `cityFoundingEvent`, `lockUnitResolutionOrders`, `syncActiveMarchWithOrder`, or `refreshActiveMarchesAfterSettlement` were found via `rg`; coverage appears mostly through broad Turn V2 integration tests.
- `server/internal/game/session/runtime.go` is large, but a package split is not recommended as an immediate MVP move; a file/helper split is lower risk because many tests construct `Runtime` directly.
- The current code already contains the PRD's minimal extractions, so remaining opportunities should be treated as later batches rather than changes to sneak into the current small acceptance scope.
