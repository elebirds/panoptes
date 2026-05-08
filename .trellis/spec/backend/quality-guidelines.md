# Quality Guidelines

> Code quality standards for backend development.

---

## Overview

<!--
Document your project's quality standards here.

Questions to answer:
- What patterns are forbidden?
- What linting rules do you enforce?
- What are your testing requirements?
- What code review standards apply?
-->

(To be filled by the team)

---

## Forbidden Patterns

<!-- Patterns that should never be used and why -->

(To be filled by the team)

---

## Required Patterns

<!-- Patterns that must always be used -->

(To be filled by the team)

### Scenario: LLM Structured JSON Output

#### 1. Scope / Trigger
- Trigger: any backend path that asks an LLM provider for structured JSON and then unmarshals it into Go structs.

#### 2. Signatures
- Prompt contract: the model must be instructed to return a bare JSON object only.
- Parser contract: the backend must normalize the raw text payload before `json.Unmarshal`.

#### 3. Contracts
- Request-side requirements:
  - Explicitly say "bare JSON only".
  - Explicitly forbid Markdown fences such as ````` and ```json```.
  - Explicitly forbid explanatory prefix/suffix text.
- Response-side requirements:
  - Trim whitespace first.
  - Strip full fenced-code wrappers when present.
  - If noisy text surrounds the payload, extract the first syntactically valid JSON object before unmarshal.

#### 4. Validation & Error Matrix
- Bare JSON object returned -> parse directly.
- Fenced JSON returned -> strip fence, then parse.
- Prefix/suffix noise around JSON -> extract first valid JSON object, then parse.
- Truncated / invalid JSON -> treat as failed structured output and fall back; do not trust partial text as a valid object.

#### 5. Good / Base / Bad Cases
- Good: `{"title":"整备边防","summary":"..."}`.
- Base: ```json ... ``` wrapped output that becomes valid after fence stripping.
- Bad: partial JSON, unmatched braces, or free-form prose with no valid object.

#### 6. Tests Required
- Parser accepts bare JSON.
- Parser accepts fenced JSON.
- Parser accepts noisy prefix/suffix text when one valid JSON object exists.
- Parser rejects or falls back on truncated JSON.

#### 7. Wrong vs Correct
#### Wrong
```go
if err := json.Unmarshal([]byte(raw), &out); err != nil {
    return err
}
```
#### Correct
```go
normalized := normalizeJSONObjectPayload(raw)
if err := json.Unmarshal([]byte(normalized), &out); err != nil {
    return err
}
```

### Scenario: Minister LLM Role Enablement

#### 1. Scope / Trigger
- Trigger: any backend change that wires minister roles to LLM draft polishing or report generation.
- Minister LLM is an expression layer. It must never become the source of executable planning targets unless a separate decision contract is designed.

#### 2. Signatures
- Config field: `Config.MinisterLLMRoles string`
- Environment key: `MINISTER_LLM_ENABLED_ROLES`
- Default value: `domestic,military`
- Role gate: `MinisterEngine.SetEnabledRoles([]string{...})`
- Draft path: `MinisterEngine.PolishDraft(ctx, playerID, draft, input) (*DraftOutput, bool)`

#### 3. Contracts
- Enabled roles are comma-separated role ids and must be trimmed, lower-cased, and de-duplicated before passing to `SetEnabledRoles`.
- Blank configured roles fall back to `[]string{"domestic", "military"}`.
- Draft targets still come from `ai.RuleBotProvider.BuildPlanningIntents`.
- LLM draft polish may only replace `Title`, `Summary`, `Rationale`, `RiskNote`, and `Source`.
- LLM draft polish must not replace `DraftID`, `TargetID`, unit ids, action ids, node ids, recipe ids, policy ids, or command payload fields.
- Minister reports may include narrative text and metrics; report `actions` remain ignored in the current MVP.

#### 4. Validation & Error Matrix
- Role omitted from enabled roles -> `PolishDraft` returns `(nil, false)` and no report is generated for that role.
- LLM disabled or client missing -> rule-only drafts remain usable.
- LLM stream error -> keep existing rule-only draft text.
- Invalid draft JSON -> keep existing rule-only draft text.
- English or empty player-visible text -> sanitize to the Chinese fallback strings.
- Unknown role with no profile -> `PolishDraft` returns `(nil, false)`.

#### 5. Good / Base / Bad Cases
- Good: `MINISTER_LLM_ENABLED_ROLES=domestic,military`; rulebot selects a military unit order, and LLM only rewrites the visible military advice copy.
- Base: `MINISTER_LLM_ENABLED_ROLES=domestic`; domestic drafts are polished, military drafts remain rule-only.
- Bad: LLM output changes a military draft from `move A2` to `attack enemy-1`, or appends a new executable action from report JSON.

#### 6. Tests Required
- Config/env test reads `MINISTER_LLM_ENABLED_ROLES`.
- Role parser test covers trim, lower-case, de-duplication, and blank fallback.
- Draft polish test verifies a military role is rejected when only domestic is enabled.
- Draft polish test verifies military role succeeds when enabled.
- Draft polish test verifies only display fields change after polish.
- Draft/report tests cover invalid JSON and English visible text fallback.

#### 7. Wrong vs Correct
#### Wrong
```go
// Do not special-case one role in session code or let LLM alter command fields.
if draft.MinisterRole != "domestic" {
    continue
}
draft.TargetNodeID = llmOutput.TargetNodeID
```
#### Correct
```go
// Enqueue drafts through the shared engine; role gating stays in MinisterEngine.
output, ok := r.ministerEngine.PolishDraft(ctx, playerID, draft, input)
if ok {
    draft.Title = output.Title
    draft.Summary = output.Summary
    draft.Rationale = output.Rationale
    draft.RiskNote = output.RiskNote
    draft.Source = domain.MinisterDraftSourceRuleLLM
}
```

### Scenario: Minister Report Actions Routed Through Planning Rules

#### 1. Scope / Trigger
- Trigger: minister report generation returns structured `actions` that should be applied to the authoritative planning path instead of being ignored.
- Minister actions remain advisory output from the LLM, but the backend must convert them into normal planning writes and let existing validation decide whether they survive.

#### 2. Signatures
- Engine callback: `RuntimeRoom.ApplyMinisterActions(playerID string, actions []MinisterActionItem) error`
- Supported action types in the current contract:
  - `build`
  - `move_units`
- Build action params:
  - `node_id`
  - `building_type`
  - optional `city_id`
- Move action params:
  - `unit_id`
  - `target_node`

#### 3. Contracts
- `MinisterEngine.generateOneReport` must forward non-empty `actions` to the room callback after parsing the report JSON.
- Session-level action application must route through existing planning validation and planning writes.
- Build actions must be validated with the normal build-order rules before adding a planning build order.
- Move actions must be validated with the normal unit-order rules before calling the shared planning-unit-order writer.
- Invalid minister actions are ignored after logging; they must not mutate authority directly or bypass the normal planning checks.

#### 4. Validation & Error Matrix
- Missing action type or required params -> ignore the action.
- Build action fails `ValidateBuildOrder` -> log warning, do not queue a build order.
- Move action fails `ValidatePlanningUnitOrder` -> log warning, do not queue a unit order.
- Unsupported action type -> log warning, ignore.
- Valid build/move action -> queue through the normal planning surfaces.

#### 5. Good/Base/Bad Cases
- Good: LLM returns `build` with a valid node/building pair and the session queues a `domain.BuildOrder`.
- Base: LLM returns `move_units` and the session uses `game/orders.ApplyPlanningUnitOrder` to keep the active march cache in sync.
- Bad: minister action writes to `state.TurnRuntime.Planning` by hand or skips validation because the LLM already emitted JSON.

#### 6. Tests Required
- Engine test confirms parsed actions are forwarded to the room callback.
- Session test confirms valid minister build actions queue build orders.
- Session test confirms valid minister move actions queue unit orders and active marches.
- Regression tests confirm invalid minister actions are ignored, not applied.

#### 7. Wrong vs Correct
#### Wrong
```go
state.TurnRuntime.Planning.MinisterBuilds = append(state.TurnRuntime.Planning.MinisterBuilds, order)
```
#### Correct
```go
if errCode := economy.ValidateBuildOrder(state, playerID, nodeID, buildingType, cityID); errCode == "" {
    room.QueueBuildOrder(domain.BuildOrder{PlayerID: playerID, NodeID: nodeID, BuildingType: buildingType, CityID: cityID})
}
```

### Scenario: Minister Participation Mode Gates Direct Planning

#### 1. Scope / Trigger
- Trigger: backend changes that alter how players bypass minister suggestions or issue direct planning commands.
- The mandate-mode flag must be consumed by planning command routing, not just set by the `direct_command` minister directive.

#### 2. Signatures
- Config field: `Config.MinisterLLMParticipationMode string`
- Environment key: `MINISTER_LLM_PARTICIPATION_MODE`
- Default value: `weak`
- Runtime query: `Runtime.IsMinisterStrongMode() bool`
- Planning-session queries:
  - `IsMinisterStrongMode() bool`
  - `IsPlayerInMandateMode(playerID string) bool`

#### 3. Contracts
- `weak` mode preserves existing behavior: players may issue normal planning commands directly.
- `strong` mode requires human players to have mandate-mode authority before direct gameplay intents can mutate planning state.
- Autonomous participants remain governed by their controller/rulebot flow and are not blocked by the human minister participation gate.
- `SetMinisterDirectiveIntent`, `SubmitTurnIntent`, and `RevealNodeIntent` remain allowed without mandate mode.
- The `direct_command` minister directive spends a mandate token and enables mandate mode through `SetPlayerMandateMode`.
- Direct gameplay commands rejected by strong mode must not mutate planning state.

#### 4. Validation & Error Matrix
- Weak mode + direct command -> process through the existing handler.
- Strong mode + player already in mandate mode -> process through the existing handler.
- Strong mode + no mandate mode + direct gameplay intent -> reject before the command-specific handler runs.
- Strong mode + no mandate mode + no tokens left -> return `no_mandate_tokens`.
- Strong mode + no mandate mode + tokens available -> return `invalid_directive` with mandate guidance.

#### 5. Good/Base/Bad Cases
- Good: player sends `direct_command`, spends one mandate token, then queues a policy/build/unit order through normal validation.
- Base: weak mode behaves exactly like the pre-existing planning command flow.
- Bad: `direct_command` sets a runtime flag that no planning path reads, or strong mode bypasses normal command validation.

#### 6. Tests Required
- Config/env test reads `MINISTER_LLM_PARTICIPATION_MODE`.
- Runtime test verifies strong-mode detection is trimmed and case-insensitive.
- Planning test verifies strong mode rejects a direct gameplay intent before state mutation.
- Planning test verifies the same direct gameplay intent succeeds once mandate mode is enabled.

---

## Testing Requirements

<!-- What level of testing is expected -->

(To be filled by the team)

---

## Code Review Checklist

<!-- What reviewers should check -->

(To be filled by the team)
