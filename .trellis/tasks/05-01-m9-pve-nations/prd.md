# M9 PVE Nations

## Goal

Make server-controlled nations a first-class backend flow: scenarios can declare
bot/AI participants, the headless harness can run them without clients, and PVE
actions are proven to reuse the same planning intent, validation, resolving, and
event projection path as human players.

## What I Already Know

- `participant.KindBot` and `participant.KindAI` already exist.
- `game.buildParticipantBindings` already attaches autonomous participants to
  `gamesession.NewAutonomousController(ai.RuleBotProvider{})`.
- `Coordinator.beginPlanning` already calls autonomous controllers with
  `runtime.BuildObservation(participantID)` and submits `planning.Intent`.
- Current `scenario.Definition` and debug `Harness` only model `PlayerIDs` as
  human participants, so scenario-authored PVE nations are not first-class.
- M9 preflight froze the contract that PVE may not mutate state directly or
  bypass planning/resolving.

## Requirements

- Extend scenario definitions so authors can declare participant kind
  (`human`, `bot`, `ai`) without breaking existing scenarios.
- Keep existing `PlayerIDs`/`Usernames` fallback behavior for old tests.
- Update debug harness to instantiate bot/AI participants and only perform
  client bootstrap sync for human participants.
- Add a PVE scenario that contains at least one human nation and one bot nation.
- Add headless regression proving:
  - bot/AI participant appears in authoritative state and room participants;
  - human client receives planning start normally;
  - bot receives observation-driven autonomous planning without a client;
  - after the human submits, the turn resolves because the bot submitted itself;
  - bot action produces normal projected domain events.
- Document M9 completion and remaining future work.

## Acceptance Criteria

- [x] PVE scenario authoring supports mixed human/bot participants.
- [x] A headless test proves a bot damages an enemy through normal planning and
      resolving, without direct state mutation or client commands.
- [x] M9 roadmap status is updated.
- [x] M9 regression gate document exists.
- [x] `cd server && go test -count=1 ./internal/game/scenario ./internal/debug ./internal/game/turn` passes.
- [x] `cd server && go test -count=1 ./...`, `make lint`, and `git diff --check` pass.

## Out Of Scope

- No smarter AI strategy beyond existing `RuleBotProvider`.
- No new protocol messages.
- No client UI.
- No PVE difficulty economy or scripting language.
- No strategic collapse or new victory condition.
