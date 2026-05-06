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

---

## Testing Requirements

<!-- What level of testing is expected -->

(To be filled by the team)

---

## Code Review Checklist

<!-- What reviewers should check -->

(To be filled by the team)
