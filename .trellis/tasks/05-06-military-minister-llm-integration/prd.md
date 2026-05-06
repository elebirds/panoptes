# Military Minister LLM Integration

## Goal

Connect the military minister to the existing minister LLM pipeline so military drafts and reports can use role-specific persona and prompt-driven wording, while keeping actual planning targets rule-selected and server-authoritative.

## What I Already Know

* Current draft targets still come from `ai.RuleBotProvider{}.BuildPlanningIntents(...)`.
* `IssueUnitOrderIntent` drafts are normally assigned to role `military`; `settle_city` is the special case routed to `domestic`.
* Draft polishing is currently gated to `domestic` only in `Runtime.PrepareMinisterDraftCacheForTurn`.
* The app factory enables only `domestic` via `engine.SetEnabledRoles([]string{"domestic"})`.
* `MinisterEngine` already supports multiple enabled roles, profile lookup by role, report generation by picked static-data profiles, per-role memory, and draft polishing by profile.
* Existing prompt/parser contracts already require JSON-only outputs and sanitize player-visible text to Chinese.

## Requirements

* Enable military minister LLM polishing for military unit-order drafts.
* Enable military minister LLM reports when minister LLM is enabled.
* Keep rule layer as the sole source of executable military targets and commands.
* Use a role-specific military persona through `MinisterProfile` and prompt generation.
* Preserve fallback behavior: if LLM is disabled, unavailable, times out, or returns invalid JSON, rule-only drafts remain usable.
* Do not add client-side validation or gameplay legality logic.
* Do not manually edit generated proto files.

## Acceptance Criteria

* [ ] When LLM is enabled and role `military` is enabled, a military unit-order draft can become `rule+llm`.
* [ ] Domestic draft polishing still works.
* [ ] Military report chunks and metrics can be sent when `GenerateMinisterReports` runs.
* [ ] LLM output cannot change `DraftID`, target IDs, unit IDs, actions, or proposed command payload.
* [ ] Tests cover domestic-only gating, domestic+military gating, and invalid/English LLM output fallback.

## Technical Approach

Recommended approach: extend the existing role-enabled minister engine rather than creating a separate military LLM path.

1. Replace the hard-coded draft polish gate `draft.MinisterRole != domestic` with an engine-level role check or simply enqueue all drafts when an engine exists, letting `PolishDraft` reject disabled roles.
2. Change app composition from `SetEnabledRoles([]string{"domestic"})` to include `military`, ideally through config such as `MINISTER_LLM_ENABLED_ROLES=domestic,military`.
3. Keep `BuildDraftPrompt` and `BuildReportPrompt` as shared prompt builders, but strengthen role-specific instructions in `buildBaseSystemPrompt` or add small role overlays keyed by `profile.Role`.
4. Ensure military profile data exists in authored static data and has clear Chinese name/personality/personality_desc.
5. Add focused tests around military draft polish and report generation.

## Decision (ADR-lite)

**Context**: Military minister drafts are already modeled as `MinisterDraft` with role `military`, but two explicit gates prevent them from using LLM.

**Decision**: Reuse the existing `MinisterEngine` multi-role design. The LLM may narrate, prioritize explanation, and express persona, but it must not decide or mutate executable military orders in this task.

**Consequences**: This is low-risk and protocol-compatible. True LLM decision-making for military strategy remains a later feature because it would require a different safety boundary and validation contract.

## Out of Scope

* LLM choosing new military targets.
* LLM executing actions from report `actions`.
* Protocol changes.
* Client UI redesign.
* New third-party dependencies.

## Technical Notes

* `server/internal/game/session/minister_draft.go`: current draft generation and domestic-only polish gate.
* `server/cmd/server/app/minister_llm.go`: current app-level enabled role list.
* `server/internal/engine/minister/engine.go`: existing multi-role engine, `SetEnabledRoles`, `PolishDraft`, `GenerateReports`.
* `server/internal/engine/minister/prompt.go`: existing shared persona and JSON prompt contracts.
* `server/internal/engine/minister/parser.go`: existing JSON extraction and Chinese-output sanitization.
* `server/internal/game/session/minister_prompt.go`: existing observation summary shared by all roles.
