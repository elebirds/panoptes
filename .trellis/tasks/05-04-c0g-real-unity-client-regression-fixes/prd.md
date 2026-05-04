# C0g Real Unity Client Regression Fixes

## Goal

Fix the first real-client regressions found after C0f: missing UI visual/text rendering, map bootstrap timing noise, disabled next-turn interaction, missing entrances to new management panels, and loss of "start from any scene routes to login" workflow.

## What I Already Know

- The screenshot shows the Game scene rendering semi-transparent management/UI frames with almost no readable text.
- The user suspects Git LFS assets may not be downloaded, and uGUI image/background materials are missing.
- The first map load logs `[MapRenderer] Waiting for server map nodes before rendering game map.`
- The next-turn button cannot be clicked.
- Several newly-created GUI panels exist but no player-facing entry points reach them.
- Previously, pressing Play from any scene routed through login; now the user must manually choose Login.
- C0f passed fixture replay, but it did not verify real scene entry, actual authored asset references, or runtime clickability.

## Requirements

- Verify whether Git LFS assets are missing and make the failure mode visible/actionable.
- Restore readable text and expected panel rendering in real Unity runtime.
- Make map rendering wait for backend game state without alarming one-time startup warnings.
- Restore next-turn button interaction through the final command-service path.
- Add clear in-game entrances to the management panels currently implemented.
- Restore best startup behavior so pressing Play from common scenes enters the intended auth/bootstrap flow without manual scene selection.

## Acceptance Criteria

- [x] Real Unity Play from `Game.unity` no longer auto-opens unreadable management panels.
- [x] UI Toolkit generated management rows carry USS classes so text styles resolve.
- [x] UI Toolkit documents no longer create blank per-panel `PanelSettings`; they share a configured runtime panel and Unity default runtime theme.
- [x] Management panel documents ignore full-screen picking outside actual panel bounds, so uGUI HUD buttons are not swallowed by transparent UI Toolkit roots.
- [x] Management panels have a reachable in-game entry point through the management host navigation without default content takeover.
- [x] First backend map state renders immediately even if it arrives before `AppManager.State` is observed as `Game`.
- [x] First map load waits gracefully for server data without emitting a warning-level log while still rendering on the first hydrated state.
- [x] Missing LFS/resources are detectable with an editor diagnostic; `git lfs install && git lfs pull` plus `git lfs fsck` was run successfully.
- [ ] Unity EditMode batchmode coverage could not be run while the project was already open in Unity; tests were updated and should be run after closing the editor instance.

## Implementation Notes

- Root cause 1: Git LFS was not installed for the repository, so `git lfs pull` initially skipped checkout. Running `git lfs install && git lfs pull` restored critical PNG/EXR assets, and `git lfs fsck` now reports OK.
- Root cause 2: UI Toolkit management rows were generated with stable names but no USS classes, leaving text unstyled and effectively invisible in runtime panels.
- Root cause 3: management UI auto-opened `NationalOverview`, and each `UIDocument` root could cover the full viewport for picking even when the visible panel was much smaller.
- Root cause 4: `MapRenderer.OnGameStateChanged` discarded the first node-bearing store update when `AppManager.State` had not yet switched to `Game`; the next turn pushed another state and finally rendered the map.
- Root cause 5: runtime-created blank `PanelSettings` did not provide a reliable UI Toolkit runtime theme/text setup, so geometry rendered but labels/buttons could fail to show text.

## Out of Scope

- Full visual polish of every management panel.
- Rebuilding all prefabs by hand into final art quality.
- New gameplay systems beyond restoring the current C0 flow.

## Technical Notes

- Client must remain pure presentation: UI does not call `NetworkManager` directly.
- Presentation must not reference generated Protocol.
- Commands should go through Core intent services and `IClientMessageSender`.
- UI Toolkit and uGUI can coexist but must share Store/ViewModel state.
