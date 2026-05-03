# M5 Technology, Institutions, And National Modifiers

## Goal

Deepen the already-present technology and institution systems so they affect
the national machine, not only UI state.

M5 should preserve the existing next-turn activation model and add a concrete
institution-to-logistics consequence.

## Scope

- Add a logistics road capacity modifier trigger.
- Let active national policies, institutional policies, technologies, and
  buildings modify effective road capacity through the existing modifier stack.
- Add an institutional policy that changes logistics outcomes.
- Ensure technology can unlock the institutional policy candidate and slot in
  real content.
- Keep activation audited through existing technology/institution events.

## Non-Goals

- No institution switching cost enforcement yet.
- No new protocol fields.
- No minister decision logic.
- No full institution UI workflow beyond existing planning commands.

## Acceptance

- Technology unlocks an institutional policy candidate.
- Institution loadout still activates on the next planning start.
- An active institution can change logistics/capacity outcome.
- Modifier sources are auditable through technology and institution events.
- `cd server && go test -count=1 ./...` and `make lint` pass.
