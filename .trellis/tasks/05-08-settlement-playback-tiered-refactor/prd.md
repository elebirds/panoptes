# brainstorm: settlement playback tiered refactor

## Goal

Refactor the client settlement playback system so turn-start resolving events no longer force every visible unit movement into a long, camera-driven, non-interactive linear sequence. The new presentation model should preserve server-authoritative event order where it matters, but classify events by importance and dependency so ordinary movement can be batched or summarized while important combat and city events remain legible.

## What I Already Know

* The user wants a structural refactor, not a small parameter tweak.
* Multiple agents may work in the same project worktree, so this task must keep edits scoped and avoid unrelated files.
* The client is a pure presentation layer. It must not compute game legality or authoritative results.
* `MsgGameSync` is already hydrated into stores before settlement playback begins.
* `StoreMessageHydrator.HandleGameSync()` updates `GameStateStore`, `PlanningDraftStore`, `TurnStore`, then writes `SettlementStore`.
* `SettlementPlaybackController` subscribes to `SettlementStore` and owns most current playback orchestration.
* Current playback is step-linear: `PlaySettlement()` iterates every `SettlementPlaybackStep` and `yield return`s each step.
* Current playback locks map input and camera input for the whole settlement playback.
* Current movement playback calls `UnitMoveAnim.Play(... followCameraEnabled: true)`.
* `UnitMoveAnim` directly calls `CinemachineMapCameraController.TryFocus()` while a unit moves.
* `AnimationQueue` is still used for backend move command bridging, but resolving playback is primarily driven by `SettlementPlaybackController`.
* Existing docs approve VContainer, R3, UniTask, UI Toolkit/uGUI split, and prohibit DOTween.

## Assumptions

* We should keep protocol and generated files unchanged for this task.
* We should keep server behavior unchanged; the server remains authoritative and event order remains source data.
* The first implementation slice should not require scene or prefab rewiring unless the code change cannot be made safely otherwise.
* The default user experience should be: ordinary movement is quick and mostly parallel; only high-importance events pull focus.
* During resolving playback, command submission can remain blocked, but camera navigation and information panels should remain usable unless a short critical cue explicitly requires focus.

## Requirements

### Functional Requirements

* Convert raw settlement DTO events into a presentation-oriented playback plan before execution.
* Classify playback events into at least three tiers:
  * `Critical`: victory/loss, city core destroyed/damaged, owned high-value unit death, major visible combat result.
  * `Important`: visible combat, unit death, building damage, contested building/city effects.
  * `Ambient`: ordinary visible unit movement, hidden movement, low-value map/production cues.
* Preserve per-unit causality: a unit's own move, attack, damage, and death cues must not be reordered in a way that creates visual contradictions.
* Allow independent ambient movement to play concurrently or in bounded-size batches.
* Snap or summarize hidden/non-visible movement instead of animating it.
* Decouple camera control from unit movement animation.
* Camera should only auto-focus for critical/important cues, not every ambient move.
* Player camera input should remain available during ordinary playback.
* Command-producing input should remain blocked while the game is in resolving or while the client is intentionally suppressing command input.
* Provide a skip or fast-forward path that stops playback and reconciles map presentation to the current authoritative store state.
* Keep turn report and settlement timeline useful as the durable record; animation should not be the only way to understand what happened.

### Non-Functional Requirements

* No manual edits under generated protocol directories.
* No new third-party dependencies.
* Preserve Presentation-to-Protocol boundary: new Presentation code must not reference generated protocol classes directly.
* Prefer Core Domain DTOs and Store data already available to Presentation.
* Keep existing scene/prefab serialized compatibility unless a same-slice verification updates the asset.
* Keep implementation files focused. Avoid making `SettlementPlaybackController`, `MapRenderer`, or `MapPlanningInputController` larger as the primary strategy.
* Avoid unrelated cleanup while other agents may be editing the same worktree.

## Proposed Architecture

### 1. Playback Plan Layer

Add a pure planning layer under `Presentation/Map` or a nearby playback package:

* `SettlementPlaybackPlan`
* `SettlementPlaybackTrack`
* `SettlementPlaybackCue`
* `SettlementPlaybackTier`
* `SettlementPlaybackPlanBuilder` or replacement for the current builder

This layer receives `TurnSettlementDto` and emits presentation cues. It should be deterministic and easy to test in EditMode without a Unity scene.

### 2. Scheduler Layer

Add a scheduler that groups cues into execution windows:

* `ParallelMoveBatch`: multiple independent ambient moves.
* `FocusedCue`: a single important/critical cue that may request camera focus.
* `SnapCue`: non-visible or already-final-state change.
* `ReconcileCue`: final map reconciliation after playback.

Scheduling rule of thumb:

* Same unit chain: sequential.
* Different units with movement only: parallel.
* Movement followed by combat for one actor: movement completes before that actor's combat cue.
* Critical cue starts a new focused window.
* Hidden movement never blocks visible important events.

### 3. Runner Layer

Keep Unity-object mutation in the runner:

* Resolve `UnitView` and `NodeView` from `MapRenderer`.
* Play batched movement coroutines.
* Show highlights, attack animation, damage pulse, death removal, damage popups.
* Call `MapRenderer.ReconcileUnitsToCurrentState()` at the end or on skip.

`SettlementPlaybackController` should become the subscription and lifecycle facade:

* Subscribe to `SettlementStore`.
* Build plan.
* Schedule plan.
* Start/stop runner.
* Expose skip/fast-forward API.

### 4. Camera Policy

Remove camera-follow responsibility from `UnitMoveAnim`.

Introduce a camera-facing policy/collaborator:

* `SettlementCameraPolicy`
* or `MapCameraDirector`

Responsibilities:

* Decide whether a cue may request focus.
* Focus once before an important/critical cue.
* Avoid per-frame unit following for ambient movement.
* Suppress auto-focus after player manual camera input during the current playback.

### 5. Input Lock Policy

Split current global locking behavior into explicit concepts:

* Command input lock: blocks planning/command actions during resolving.
* Selection lock: optional, short-lived, for the currently highlighted focused cue.
* Camera input lock: default off; only critical short-lived cues may opt in if needed.

Current all-or-nothing calls in `SettlementPlaybackController.BeginPlaybackInputLock()` should be replaced by this policy.

## Event Tiering Proposal

### Critical

* `city_core_destroyed`
* lethal damage to city core or victory/loss-adjacent cue
* owned hero/unique/high-value unit death, when such metadata is available
* game over message/overlay

Playback behavior:

* Focus once.
* Use clear highlight/damage popup.
* Short pause allowed.
* May temporarily suppress camera input only while the cue fires.

### Important

* `unit_died`
* visible `unit_damaged`
* `building_damaged`
* `city_core_damaged`
* visible attack/combat exchange

Playback behavior:

* Focus only if on screen importance warrants it.
* Batch nearby impacts when possible.
* Keep pauses short and bounded.

### Ambient

* `unit_moved`
* production/building progress messages
* low-risk map pulses
* hidden or out-of-view movement

Playback behavior:

* Visible movement plays in parallel batches.
* Hidden movement snaps.
* No camera hijack.
* No global camera input lock.

## Implementation Plan

### Phase A: Planning and Context

* Finalize this PRD.
* Add Trellis implement/check context entries for frontend specs and affected files.
* Confirm no blocking product question remains before implementation.

### Phase B: Extract Plan and Tier Model

* Add pure `SettlementPlaybackTier` and cue/plan model.
* Refactor current `SettlementPlaybackPlanBuilder` logic into the new model without changing behavior yet.
* Add EditMode tests for classification and per-unit sequence preservation.

### Phase C: Introduce Scheduler

* Add scheduler that converts cues into execution windows.
* Start with movement batching:
  * independent ambient moves can share one window;
  * focused important cues remain single windows.
* Add EditMode tests proving N independent move events produce one or a bounded number of batch windows rather than N focused sequential windows.

### Phase D: Refactor Runner

* Move Unity playback methods out of `SettlementPlaybackController` into a runner/collaborator.
* Make `SettlementPlaybackController` a thin store subscription/lifecycle owner.
* Ensure skip/fast-forward cancels active coroutines and reconciles map state.

### Phase E: Camera and Input Policy

* Remove per-frame camera focus from ordinary movement playback.
* Introduce camera policy/director.
* Split command input lock from camera input lock.
* Keep command input disabled during resolving, but allow camera pan/zoom for ordinary playback.

### Phase F: UX Controls and Polish

* Add or wire a skip/fast-forward affordance through an existing turn report/timeline surface or a small HUD control.
* Provide user-visible playback status only if it fits the existing HUD style.
* Keep durable information in `SettlementTimeline` and `TurnReportPanel`.

### Phase G: Verification

* Run targeted EditMode tests for playback planning/scheduling.
* Run existing composition and boundary tests.
* Run Unity batchmode compile/test gate if available and practical.
* Manually inspect Game scene/prefab impact only if assets changed.

## Acceptance Criteria

* [ ] A settlement containing multiple independent visible unit movements no longer plays as one focused serial sequence per unit.
* [ ] Ordinary visible movement can play in parallel batches or bounded windows.
* [ ] Hidden movement does not consume animation time.
* [ ] Important combat, damage, death, and city-core events remain visible and understandable.
* [ ] Unit movement animation no longer directly controls the camera.
* [ ] Ordinary playback does not globally lock camera pan/zoom.
* [ ] Command input remains blocked where resolving state requires it.
* [ ] Skip/fast-forward reconciles the map to authoritative current state.
* [ ] EditMode tests cover tier classification, scheduler batching, and per-unit causality.
* [ ] Existing client boundary tests remain green: no Presentation protocol references, no UI direct NetworkManager use.

## Definition of Done

* Code changes are scoped to settlement playback, map playback support, and directly necessary tests.
* No generated protocol files are edited.
* No new third-party dependencies are introduced.
* New planner/scheduler logic has EditMode test coverage.
* Existing relevant EditMode tests pass.
* Any prefab/scene changes, if required, are explicitly verified.
* Final notes mention any remaining behavior that is intentionally deferred.

## Out of Scope

* Server resolving logic changes.
* Protocol changes.
* Generated client/server protocol edits.
* New animation middleware or tweening dependency.
* Full redesign of `MapRenderer`.
* Full redesign of `MapPlanningInputController`.
* Reworking turn report content beyond what is necessary for skip/playback status.

## Technical Notes

* Existing playback entry: `client/Assets/Scripts/Runtime/Presentation/Map/SettlementPlaybackController.cs`.
* Existing move helper: `client/Assets/Scripts/Runtime/Presentation/Animation/UnitMoveAnim.cs`.
* Existing command move queue: `client/Assets/Scripts/Runtime/Presentation/Animation/AnimationQueue.cs`.
* Existing camera controller: `client/Assets/Scripts/Runtime/Presentation/Map/CinemachineMapCameraController.cs`.
* Existing map reconciliation methods: `MapRenderer.PrepareSettlementPlaybackUnits()`, `PlaceSettlementPlaybackUnitsAtMoveStarts()`, `ReconcileUnitsToCurrentState()`.
* Store hydration source: `client/Assets/Scripts/Runtime/Core/Application/Stores/StoreMessageHydrator.cs`.
* Existing turn summary surfaces: `SettlementTimeline`, `TurnReportPanel`, `TurnSummaryViewModel`, `NationalOverviewViewModel`.
* Relevant docs:
  * `AGENTS.md`
  * `docs/PANOPTES_AGENT_FRONTEND.md`
  * `docs/TURN_V2_REFACTOR_PLAN.md`
  * `docs/2026-05-02-client-reactive-presentation-architecture-implementation-plan.md`
  * `.trellis/spec/frontend/index.md`

## Open Questions

* None blocking for the first implementation slice. Default assumption: command input stays blocked during resolving, camera/navigation stays available for ordinary playback.
