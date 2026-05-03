# M1.5 backend state responsibility

## Goal

Map durable state, turn runtime state, and future truth/observed/reported boundaries so later logistics and information systems do not blur state ownership.

## Requirements

* Document durable state versus `TurnRuntime.Planning` and `TurnRuntime.Resolving`.
* Identify where future local storage, logistics graphs, and priority profiles should live.
* Preserve the backend-only authority rule for game logic.
* Record truth/observed/reported as future boundaries without implementing them.

## Acceptance Criteria

* [x] State responsibility table exists.
* [x] Future M2/M3 state additions have recommended locations.
* [x] No client-side state authority is introduced.

## Out of Scope

* Implementing local storage or logistics state.
* Implementing information asymmetry.

## Technical Notes

* Parent roadmap: `docs/2026-04-30-backend-m1-rule-foundation-plan.md`.
* Current state references: `server/internal/domain/state.go`, `server/internal/domain/turn_runtime.go`.

## Verification

* Added `docs/2026-04-30-backend-state-responsibility.md`.
* Linked the responsibility note from `docs/SERVER_RUNTIME_ARCHITECTURE.md`.
* Added backend spec guidance in `.trellis/spec/backend/directory-structure.md`.
* `cd server && go test ./...` passed during implementation.
* M1.4/M1.5 review passed with `go build ./...`, focused backend tests, and `go test -count=1 ./...`.
