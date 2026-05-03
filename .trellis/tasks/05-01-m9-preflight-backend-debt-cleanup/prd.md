# M9 Preflight Backend Debt Cleanup

## Goal

Close the minimum backend debt that should not be carried into M9 PVE 국가 work:
make the PVE intent reuse contract explicit, add one long-running headless
regression that crosses M2-M8 systems, and document the reported-information
boundary for M9.

## What I Already Know

- M0-M8 are completed and committed on `codex/axial-coordinates`.
- M9 will add server-controlled nations that must reuse player rules instead
  of bypassing planning/resolving.
- The highest-risk pre-M9 debts are:
  - PVE intent reuse contract.
  - A long headless scenario covering multiple final-backend systems together.
  - Clear boundary for whether M9 PVE consumes `observed` or `reported` data.
- Existing debug harness tests already cover real-content happy path, local
  logistics shortage, facility takeover, minister defaults, and information
  reports in separate slices.

## Requirements

- Add a concise backend debt table / M9 preflight record in `docs/`.
- Define the PVE contract: AI/PVE must output existing planning intents and
  must not mutate state or bypass validation.
- Define the M9 information contract: PVE decisions use `observed` snapshots;
  `reported` metadata is advisory until a later minister/personality layer
  consumes distorted narrative content.
- Add one headless regression that spans expansion, logistics shortage,
  networked warfare disruption, minister planning/draft visibility, and
  information reporting.
- Keep changes scoped; do not implement M9 PVE itself.

## Acceptance Criteria

- [x] Documentation identifies P0/P1 debts and M9 entry gates.
- [x] Documentation states the PVE intent reuse contract and reported-data
      boundary.
- [x] A backend test exercises a multi-turn real-content flow that observes
      minister draft/default planning surface and `InformationReportView`, then
      creates a logistics shortage and a warfare disruption event.
- [x] `cd server && go test -count=1 ./...` passes.
- [x] `make lint` and `git diff --check` pass.

## Out Of Scope

- No PVE player implementation.
- No new protocol messages.
- No LLM behavior changes.
- No client UI changes.
