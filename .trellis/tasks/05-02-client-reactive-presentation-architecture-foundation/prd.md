# Client Reactive Presentation Architecture Foundation

## Goal

Move the Unity client from large singleton-driven Presentation facades toward
the final Panoptes Client Reactive Presentation Architecture:

```text
Protocol/WebSocket
  -> Mapper
  -> Store / Read Model
  -> Reactive ViewModel
  -> Explicit Binder
  -> uGUI or UI Toolkit View

Player Input
  -> ViewModel Command
  -> Service / Intent
  -> MessageSender
  -> Server
```

The user explicitly corrected the migration strategy: do not build a
compatibility-style Composition Root. The final Composition Root should be the
new owner for migrated modules. Legacy singletons may remain only as isolated
old-module dependencies until those modules are migrated; new modules must not
actively depend on singleton lookup.

## What I Already Know

- Current docs still forbid or defer VContainer/R3/UniTask/UI Toolkit usage.
- Current client has large Presentation facades and singleton caches/services.
- The agreed target stack is VContainer, R3, UniTask, UI Toolkit for dense
  management UI, and uGUI for map/world-space UI.
- Official/current package versions verified on 2026-05-02:
  - VContainer `1.17.0`
  - R3 `1.3.0`
  - UniTask `2.5.10`
- The first implementation step should persist the final plan and amend policy
  before introducing packages or code paths that depend on them.

## Requirements

- Persist the final direct-to-target implementation plan under `docs/`.
- Update `AGENTS.md`, frontend docs, and Trellis frontend specs so dependency
  policy is consistent.
- Approve VContainer/R3/UniTask/UI Toolkit with explicit boundaries and locked
  versions.
- Keep existing hard client boundaries:
  - Presentation must not reference generated Protocol.
  - UI must not call `NetworkManager.Instance` directly.
  - Client must not perform gameplay legality validation.
- Start implementation from the foundation, not more local facade helper splits.

## Acceptance Criteria

- [x] Final implementation plan is written to a docs file.
- [x] Dependency policy no longer contradicts the target architecture.
- [x] VContainer, R3.Unity, and UniTask are pinned in Unity package files.
- [x] R3 core DLLs required by R3.Unity are imported under
      `client/Assets/Plugins/`.
- [x] Docs state "no compatibility Composition Root" and define direct final
      ownership for migrated modules.
- [x] Minimal final contracts exist for Store, ViewModel, Binder, and command
      service boundaries.
- [x] Trellis context validates.
- [x] Unity batchmode compilation succeeds.
- [x] Static boundary checks remain clean.
- [x] No generated protocol files are modified.

## Verification

- `python3 ./.trellis/scripts/task.py validate .trellis/tasks/05-02-client-reactive-presentation-architecture-foundation`
- `git diff --check`
- Unity `6000.4.1f1` batchmode compile:
  `/Applications/Unity/Hub/Editor/6000.4.1f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath /Users/hhm/code/panoptes/client -logFile /tmp/panoptes-unity-architecture-foundation.log`
- Presentation boundary checks:
  - `rg -n "Panoptes\\.Protocol" client/Assets/Scripts/Runtime/Presentation -g '*.cs'`
  - `rg -n "NetworkManager\\.Instance" client/Assets/Scripts/Runtime/Presentation/UI -g '*.cs'`
- Generated protocol dirty check:
  `git status --porcelain -- server/internal/gen/proto client/Assets/Scripts/Protocol`

Unity TestRunner note: `-runTests -testPlatform EditMode` exited 0 but did not
emit the requested XML result file in this shell environment, matching the known
local TestRunner limitation documented in earlier client gates.

## Out of Scope For This First Slice

- Migrating a complete UI panel to ViewModel/Binder.
- Rewiring scenes/prefabs to VContainer scopes.
- Removing all existing singletons.
- Migrating production modules beyond the foundation contracts.

## Technical Notes

- `client/Packages/manifest.json` currently has no VContainer/R3/UniTask
  dependency.
- `docs/PANOPTES_AGENT_FRONTEND.md` still documents singleton management as the
  core principle and bans VContainer/R3.
- `docs/2026-05-01-client-reactive-ui-architecture-plan.md` still describes
  compatibility/pilot sequencing; it needs a direct-to-target amendment.
