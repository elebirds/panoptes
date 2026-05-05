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

---

## Testing Requirements

<!-- What level of testing is expected -->

(To be filled by the team)

---

## Code Review Checklist

<!-- What reviewers should check -->

(To be filled by the team)
