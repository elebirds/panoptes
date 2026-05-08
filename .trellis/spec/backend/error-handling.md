# Error Handling

> How errors are handled in this project.

---

## Overview

<!--
Document your project's error handling conventions here.

Questions to answer:
- What error types do you define?
- How are errors propagated?
- How are errors logged?
- How are errors returned to clients?
-->

(To be filled by the team)

---

## Error Types

<!-- Custom error classes/types -->

(To be filled by the team)

---

## Error Handling Patterns

<!-- Try-catch patterns, error propagation -->

(To be filled by the team)

---

## API Error Responses

<!-- Standard error response format -->

Transport-facing command handlers return stable `error_code` strings. New
codes are allowed after review when an existing code would hide distinct player
feedback or make command debugging ambiguous.

### Reviewed Mandate Error Codes

| Code | Meaning | Client copy |
|---|---|---|
| `no_mandate_tokens` | The player tried to spend a mandate token with none remaining. | 亲政令牌不足 |
| `no_minister_actions` | A mandate override was requested, but there were no available minister drafts to reject. | 当前没有可否决的大臣行动 |
| `unknown_mandate_action` | The server received an unsupported mandate action after internal dispatch. | 未知的亲政操作 |

These are planning/transport result codes, not HTTP authentication errors.

---

## Common Mistakes

<!-- Error handling mistakes your team has made -->

(To be filled by the team)
