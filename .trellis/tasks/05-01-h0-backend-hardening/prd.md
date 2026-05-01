# H0 Backend Hardening

## Goal

Add the first backend hardening layer after M0-M9: deterministic long-run
headless soak and reusable state invariants for mixed human/PVE games.

## Requirements

- Add reusable debug/test invariants that catch impossible state after each turn.
- Add a 30+ turn headless soak that includes both a human participant and an
  autonomous PVE participant.
- Keep PVE on the normal planning/resolving path.
- Check resources, building HP, unit HP/positions, duplicate unit IDs, and
  per-turn `GameSync` information report presence.
- Record H0 gate results in docs.

## Acceptance Criteria

- [x] `AssertStateInvariants` or equivalent reusable helper exists.
- [x] Mixed human/PVE soak runs for at least 30 turns unless game over occurs.
- [x] Soak asserts state invariants on every turn.
- [x] H0 gate doc exists.
- [x] `cd server && go test -count=1 ./internal/debug` passes.
- [x] `cd server && go test -count=1 ./...`, `make lint`, and `git diff --check` pass.

## Out Of Scope

- No new gameplay mechanics.
- No smarter PVE strategy.
- No replay serialization format.
- No client work.
