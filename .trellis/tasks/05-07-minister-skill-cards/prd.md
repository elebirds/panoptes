# brainstorm: minister skill cards

## Goal

Design a scalable minister skill card mechanism. Skill cards are combined with ministers at runtime rather than being fixed to a minister identity, so the same minister name may receive different skills in different games or turns. Skill effects are intentionally out of scope for the first design pass.

## What I Already Know

* Existing minister authored data lives in `data/content/ministers/ministers.json`.
* Existing minister schema lives in `data/schema/content/ministers.schema.json`.
* Existing server minister runtime code lives under `server/internal/engine/minister/`.
* Existing static catalog has a `ministers` section in `server/internal/staticdata`.
* Existing `MinisterView` in `protocol/panoptes/proto/v1/game_state.proto` exposes role, name, ability, and personality.
* Generated protocol files under `server/internal/gen/proto/` and `client/Assets/Scripts/Protocol/` must not be edited manually.

## Requirements

* Skill card definitions should be authored in one predictable place near minister content.
* Adding a new skill should normally mean adding one new definition in the same authored data area, then running the normal generation flow.
* Minister identity and skill assignment must be separate. A minister record should not permanently own specific skill cards.
* Runtime state should represent the actual assigned skill cards for a game/player/turn.
* The first implementation should support metadata, assignment plumbing, and one real effect card: `stargazing`.
* The design must remain compatible with future effect implementations without hardcoding game values in client code.
* `stargazing` / `观星` should be activatable through the existing minister directive command payload and grant full-map vision on the next turn only.

## Technical Approach

Recommended direction: split the feature into three concepts.

1. `Minister` remains the character/person profile.
2. `MinisterSkillCardDefinition` becomes static authored data.
3. `MinisterSkillLoadout` or equivalent runtime state records which card ids are assigned to which minister instance for a player/game/turn.

Authoring should live under `data/content/ministers/`, for example:

* `data/content/ministers/ministers.json`
* `data/content/ministers/skill_cards.json`

The static data pipeline should expose skill card definitions through the catalog, while runtime game state records assigned skill card ids and queued effects. Effects are implemented through an `effect_key` registry under the minister engine, so adding a new skill effect means adding a new authored skill card plus a handler for its effect key.

First skill:

* `id`: `stargazing`
* `name`: `观星`
* `effect_key`: `next_turn_full_map_vision`
* Trigger: minister directive payload `{"directive_type":"activate_skill","skill_card_id":"stargazing"}`
* Effect: queue `full_map_vision` for `state.Turn + 1`
* Duration: one turn only

## Acceptance Criteria

* [ ] Design identifies static skill definitions separately from runtime minister-card assignments.
* [ ] Design gives one canonical authoring location for new skill cards.
* [ ] Design avoids client-side game logic or validation.
* [ ] Design reserves extension points for future effect implementations.
* [ ] Design does not require editing generated protocol files manually.
* [ ] `观星` can be activated from an existing minister directive payload without a proto schema change.
* [ ] `观星` does not reveal the current turn, reveals the next turn, and expires after that turn.

## Out Of Scope

* Balancing concrete skill values.
* Building full UI interactions for skill activation.
* Binding specific skills permanently to specific ministers.
* Adding a new protocol command or a full skill activation UI.

## Technical Notes

* Existing static data path: `data/content/ministers/ministers.json`.
* Existing schema path: `data/schema/content/ministers.schema.json`.
* Existing server minister runtime path: `server/internal/engine/minister/`.
* Existing protocol view: `protocol/panoptes/proto/v1/game_state.proto`.
* Existing generation commands: `make data-gen` and `make gen`.
