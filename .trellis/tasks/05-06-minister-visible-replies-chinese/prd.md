# minister visible replies should be chinese

## Goal

Ensure all minister natural-language text shown to players is generated in Simplified Chinese instead of English, so the player-facing LLM minister experience stays consistent with the game's language.

## Requirements

* Constrain minister report generation so player-facing free-text output must be Simplified Chinese.
* Constrain minister draft polishing so player-facing free-text output must be Simplified Chinese.
* Keep the existing JSON output contract unchanged.
* Add a lightweight backend fallback so obviously English player-facing text does not leak through unchanged.

## Acceptance Criteria

* [ ] `BuildReportPrompt` explicitly instructs the model that player-facing natural-language fields must be Simplified Chinese.
* [ ] `BuildDraftPrompt` explicitly instructs the model that player-facing natural-language fields must be Simplified Chinese.
* [ ] Parsing or post-processing applies a Chinese fallback for obviously English minister text without changing the JSON schema.
* [ ] Backend tests cover the new language constraint and fallback behavior.

## Definition of Done

* Tests added or updated for prompt and fallback behavior.
* Relevant backend tests pass.

## Technical Approach

Update minister prompt builders in `server/internal/engine/minister/prompt.go` to state that all player-visible natural-language strings must be written in Simplified Chinese and not English sentences. Add a small post-parse sanitizer for minister outputs that replaces obviously English free-text fields with Chinese fallback copy while preserving structured metrics/actions JSON.

## Decision (ADR-lite)

**Context**: The current minister prompts require JSON output and visibility boundaries, but do not constrain the language of player-facing text.
**Decision**: Enforce the requirement primarily in the prompt layer, and add a small backend fallback as a guardrail for model drift or provider variation.
**Consequences**: This keeps the contract local to the minister backend path and avoids changing protobufs or client rendering, at the cost of a narrow heuristic fallback rather than full language detection.

## Out of Scope

* Translating structured identifiers such as `action_id`.
* Translating numeric metric trends or enum-like values.
* Changing client-side UI code or protobuf schema.

## Technical Notes

* Prompt builders live in `server/internal/engine/minister/prompt.go`.
* Report generation flows through `server/internal/engine/minister/engine.go` and `server/internal/engine/minister/parser.go`.
* Draft polishing also uses `server/internal/engine/minister/parser.go`.
