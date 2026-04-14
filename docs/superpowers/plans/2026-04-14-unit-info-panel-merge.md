# Unit Info Panel Merge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Merge unit order planning UI into the unit info panel so only one selection-driven unit panel remains on screen.

**Architecture:** Keep `UnitInfoPanelController` as the only unit panel facade and absorb the per-unit planning summary and direct action buttons from `UnitOrdersPanel`. Move the global submit action to a non-selection-dependent panel so the game remains operable when no unit is selected.

**Tech Stack:** Unity UI (uGUI + TextMeshPro), client Core caches/events, NUnit EditMode source-guard tests

---

### Task 1: Lock the intended UI boundary with tests

**Files:**
- Modify: `client/Assets/Scripts/Tests/EditMode/Lobby/ClientRuntimeIntegrationTests.cs`

- [ ] Add a source-level test asserting `GameSceneController` no longer auto-mounts `UnitOrdersPanel`.
- [ ] Add a source-level test asserting `UnitInfoPanelController` consumes `PlanningDraftCache` and renders planning-order summary behavior.
- [ ] Run client compile verification to ensure the new tests compile.

### Task 2: Move global submit out of the unit-only panel

**Files:**
- Modify: `client/Assets/Scripts/Runtime/Presentation/UI/Turn/StrategicPanel.cs`

- [ ] Add a `SubmitTurn` button to `StrategicPanel`.
- [ ] Bind it to `GameIntents.SubmitTurn()` and `ActionLock`.
- [ ] Keep it visible whenever planning is active, independent of unit selection.

### Task 3: Merge `UnitOrdersPanel` behavior into `UnitInfoPanelController`

**Files:**
- Modify: `client/Assets/Scripts/Runtime/Presentation/UI/HUD/UnitInfoPanelController.cs`
- Read: `client/Assets/Scripts/Runtime/Presentation/UI/Turn/UnitOrdersPanel.cs`

- [ ] Add `PlanningDraftCache` consumption and selected-unit order summary rendering.
- [ ] Add move / attack / hold / charge direct action buttons into the existing unit info layout.
- [ ] Keep the panel hidden when no unit is selected.
- [ ] Preserve existing registry-driven contextual actions such as settle-city.

### Task 4: Remove the duplicate scene panel and dead code

**Files:**
- Modify: `client/Assets/Scripts/Runtime/Presentation/UI/Game/GameSceneController.cs`
- Delete: `client/Assets/Scripts/Runtime/Presentation/UI/Turn/UnitOrdersPanel.cs`

- [ ] Remove `EnsureComponent<UnitOrdersPanel>()` from scene bootstrap.
- [ ] Delete the standalone `UnitOrdersPanel` runtime script once all behavior is absorbed.
- [ ] Update tests that still expect the old panel to exist.

### Task 5: Verify

**Files:**
- Modify if needed: `client/Assets/Scripts/Tests/EditMode/Lobby/ClientRuntimeIntegrationTests.cs`

- [ ] Run Windows client compile verification.
- [ ] Run source scans to confirm there are no remaining runtime references to `UnitOrdersPanel`.
- [ ] Summarize any remaining non-project warnings separately from this feature.
