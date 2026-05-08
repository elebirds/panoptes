# Technical Design: Tiered Settlement Playback

## Collaboration Boundary

This task should only modify client settlement playback and directly necessary tests unless the implementation discovers a hard blocker.

Primary allowed areas:

* `client/Assets/Scripts/Runtime/Presentation/Map/*SettlementPlayback*`
* `client/Assets/Scripts/Runtime/Presentation/Animation/UnitMoveAnim.cs`
* small supporting playback/camera policy files under `Presentation/Map` or `Presentation/Animation`
* targeted EditMode tests under `client/Assets/Scripts/Tests/EditMode`

Avoid unrelated edits to:

* generated protocol files
* server code
* static data
* broad `MapRenderer` cleanup
* broad `MapPlanningInputController` cleanup
* unrelated UI panels

If another agent has touched an overlapping file, read their changes and adapt. Do not revert unrelated work.

## Current Bottleneck

Current flow:

```text
MsgGameSync
  -> StoreMessageHydrator.HandleGameSync
  -> SettlementStore.Replace
  -> SettlementPlaybackController.OnSettlementChanged
  -> PlaySettlement
  -> foreach SettlementPlaybackStep: yield return PlayStep
```

The bottleneck is not just movement duration. It is the combination of:

* one coroutine chain for all settlement cues;
* per-step camera focus;
* per-frame camera focus during unit movement;
* global map/camera input lock;
* per-unit pauses.

## Target Flow

```text
SettlementStore
  -> SettlementPlaybackController
  -> SettlementPlaybackPlanBuilder
  -> SettlementPlaybackScheduler
  -> SettlementPlaybackRunner
  -> MapRenderer / UnitView / NodeView

Camera decisions:
  -> SettlementCameraPolicy / MapCameraDirector

Input decisions:
  -> explicit command/camera/selection lock policy
```

## Core Types

Suggested names, adjustable during implementation:

* `SettlementPlaybackTier`
  * `Ambient`
  * `Important`
  * `Critical`
* `SettlementPlaybackCue`
  * event type
  * actor unit id
  * target unit id
  * node/grid information
  * tier
  * visibility
  * cue kind: move, attack, damage, death, building damage, map pulse, snap, reconcile
* `SettlementPlaybackTrack`
  * per-actor ordered cue list
* `SettlementPlaybackWindow`
  * batch/focused/snap/reconcile execution window
* `SettlementPlaybackScheduler`
  * converts cues/tracks into windows

## Scheduling Rules

1. Same actor chain stays ordered.
2. Independent ambient moves can run in the same batch window.
3. Hidden or non-visible movement becomes snap/reconcile work.
4. Important/critical cues split focused windows.
5. A focused cue may request one camera focus, not per-frame follow.
6. A final reconcile step always runs after normal completion or skip.

## Camera Rules

* `UnitMoveAnim` should animate only unit visual state and transform position.
* Ambient movement must not call `CinemachineMapCameraController.TryFocus`.
* Focus should happen once per focused window if the policy allows it.
* User manual camera input during playback should suppress later automatic focus when possible.
* Camera pan/zoom should remain available during ambient playback.

## Input Rules

Keep separate concepts:

* command lock: blocks issuing new game commands during resolving;
* selection lock: optional and short-lived;
* camera lock: default false for ambient playback.

Avoid using one global "presentation input lock" as the default for all settlement playback.

## Test Plan

Add targeted EditMode tests for pure planning/scheduling:

* independent move events become a batch window;
* repeated cues for one unit remain ordered;
* critical city-core events create focused windows;
* hidden movement becomes snap work;
* scheduler output is deterministic.

Existing regression checks to keep green:

* composition smoke tests for scene/prefab registration;
* static client boundary tests;
* protocol replay/store hydration tests where applicable.

## Rollout Strategy

Prefer a sequence of small commits/slices:

1. Add pure tier/cue/scheduler tests and models.
2. Route current playback through runner with equivalent behavior.
3. Enable ambient move batching.
4. Decouple camera follow from `UnitMoveAnim`.
5. Split input lock behavior.
6. Add skip/fast-forward affordance if not already available.

Stop after each slice if tests fail or if another agent is actively modifying the same files.
