# M8 Asymmetric Information And Distorted Reporting

## Goal

Formalize the backend information chain as `truth -> observed -> reported` so
players and ministers no longer consume raw truth by default.

## Scope

- Add a reported layer derived from existing observation snapshots.
- Make reporting mode explicit and configurable for clear, standard, and high
  distortion modes.
- Add deterministic omission/delay/misread behavior that can be tested without
  LLM calls.
- Preserve debug/omniscient escape hatches as explicit modes.
- Expose enough report metadata for minister planning and client inspection.

## Non-Goals

- No client UI work.
- No new monetized or long-term token economy.
- No LLM-authored authority.
- No new victory condition.

## Acceptance

- Non-debug/non-omniscient views do not default to full truth.
- The same truth can produce different observed/reported outputs by player and
  reporting mode.
- A direct-inspection/clear mode can temporarily pierce or suppress distortion.
- `cd server && go test -count=1 ./...` and `make lint` pass.
