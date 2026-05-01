# D2 Content Playability Gate

## Goal

Add a real-content playability gate after D0/D1 so the expanded authored catalog is proven reachable through backend headless execution, not only present in static data.

## What I already know

- D0/D1 expanded authored source to 18 buildings, 21 recipes, 18 technologies, 8 units, and 9 policies.
- H0 added reusable state invariants and mixed human/PVE soak coverage.
- Existing debug harness can run prepared rooms and wait for planning/game-sync messages.
- Real generated content is loaded in debug tests through `loadRealContentCatalog`.

## Assumptions

- D2 should mostly add tests and gate docs, not new gameplay systems.
- The playability gate can use scripted human planning commands to prove reachability; PVE soak separately proves autonomous mixed-run stability.
- Full balance and AI strategic use of every new content item remain later work.

## Requirements

- Add a deterministic real-content chain test that proves key D0/D1 content can be unlocked/used through normal backend systems.
- Add a mixed human/PVE real-content soak on `frontier_basin` or equivalent generated real map.
- Reuse `AssertStateInvariants` during soak.
- Check information report presence during long-run game sync/planning sync.
- Record a D2 gate and content chain coverage table.

## Acceptance Criteria

- [x] Real-content chain test covers storage/logistics, trade, research, defense/visibility, and professional military content.
- [x] Chain test proves at least one D1 unit can be produced or ECS-created from real catalog ability paths.
- [x] Mixed human/PVE soak uses generated real content and runs at least 50 turns unless game over.
- [x] Soak asserts state invariants every turn.
- [x] D2 gate doc exists.
- [x] `cd server && go test -count=1 ./internal/debug` passes.
- [x] `cd server && go test -count=1 ./...`, `make lint`, and `git diff --check` pass.

## Definition of Done

- Tests added/updated.
- Gate note committed.
- Trellis task marked completed.

## Out of Scope

- No new authored content.
- No new gameplay systems.
- No client UI work.
- No final balance pass.
- No smarter PVE strategy.

## Technical Notes

- Read `.trellis/spec/backend/index.md`, `.trellis/spec/backend/directory-structure.md`, and `.trellis/spec/guides/cross-layer-thinking-guide.md`.
- Likely files: `server/internal/debug/*_test.go`, `docs/2026-05-01-backend-d2-content-playability-gate.md`, and Trellis task files.
