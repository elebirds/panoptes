# M1.6 backend regression gate

## Goal

Run and record the backend quality gate after M1 child tasks complete, ensuring the project is ready to enter M2 road/connectivity work.

## Requirements

* Run backend tests.
* Run backend build if relevant.
* Confirm M1 did not introduce unplanned proto/data/schema changes.
* Summarize residual risks before M2.

## Acceptance Criteria

* [x] `cd server && go test ./...` passes.
* [x] `cd server && go build ./...` passes or a blocker is documented.
* [x] Dirty diff is within M1 scope.
* [x] M2 entry risk notes are written.

## Out of Scope

* Fixing unrelated pre-existing failures unless they block M1 validation.
* Starting M2 implementation.

## Technical Notes

* Parent roadmap: `docs/2026-04-30-backend-m1-rule-foundation-plan.md`.

## Verification

* Archived regression record: `docs/2026-04-30-backend-m1-regression-gate.md`.
* `cd server && go test -count=1 ./...` passed.
* `cd server && go build ./...` passed.
* `cd server && go vet ./...` passed.
* `cd protocol && go run github.com/bufbuild/buf/cmd/buf@latest lint` passed.
* `git diff --check` passed.
* `make lint` remains blocked in this local environment because the `buf` executable is not installed; protocol lint was verified through `go run`.
