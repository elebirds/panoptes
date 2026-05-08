# Research: Minister System Codebase Analysis

- **Query**: Deep analysis of the minister system codebase for "minister LLM deep integration" task
- **Scope**: internal
- **Date**: 2026-05-08

## Findings

### 1. Architecture Overview

The minister system spans four layers, from domain model to app composition:

```
server/cmd/server/app/minister_llm.go   -- App-level factory & config binding
server/internal/engine/minister/        -- Core engine (prompt, parser, memory, engine)
server/internal/game/session/           -- Session-level orchestration (drafts, observation, planning)
server/internal/game/query/             -- Observation & reporting (information distortion)
server/internal/domain/                 -- Domain types (MinisterDraft, state)
server/internal/event/                  -- Event model (MinisterActedEvent, TokenUsedEvent)
server/internal/llm/                    -- LLM abstraction layer
server/internal/staticdata/             -- Static data (Minister profile model)
protocol/panoptes/proto/v1/minister.proto -- Wire protocol
```

### 2. Files Found

| File Path | Description |
|---|---|
| `server/internal/engine/minister/doc.go` | Package declaration |
| `server/internal/engine/minister/engine.go` | Core engine: `MinisterEngine`, report generation, draft polishing, memory management |
| `server/internal/engine/minister/memory.go` | `MinisterMemory` struct with sliding window (max 5 entries), thread-safe |
| `server/internal/engine/minister/parser.go` | JSON parsing, markdown fence stripping, English-text sanitization, action execution |
| `server/internal/engine/minister/prompt.go` | Prompt templates: `BuildReportPrompt`, `BuildDraftPrompt`, persona injection |
| `server/internal/engine/minister/engine_test.go` | Tests for report generation, draft polish, role gating, English sanitization |
| `server/internal/engine/minister/parser_test.go` | Tests for JSON parsing, fenced JSON, noise extraction, English fallback |
| `server/internal/engine/minister/prompt_test.go` | Tests for prompt content injection and Chinese constraints |
| `server/internal/game/session/minister_prompt.go` | `BuildMinisterReportInput`, observation summary builder |
| `server/internal/game/session/minister_draft.go` | Draft cache: rule-based intent -> MinisterDraft, LLM polish job dispatch |
| `server/internal/game/session/minister_draft_test.go` | Tests for draft text, polish application |
| `server/internal/game/session/runtime.go` | `Runtime` struct: holds `ministerEngine`, draft cache, observation store |
| `server/internal/game/session/planning_start.go` | `BuildPlanningStartMessage`: builds proto message including drafts and info report |
| `server/internal/game/session/planning_start_runner.go` | `PlanningStartRunner`: technology activation, institution promotion stages |
| `server/internal/game/session/turn_start.go` | `PreparePlanningStartState`: turn start state preparation |
| `server/internal/domain/minister_draft.go` | `MinisterDraft` struct, kind/status/source enums, planning input helpers |
| `server/internal/event/minister.go` | `MinisterActedEvent` (audit-only, no-op Apply), `TokenUsedEvent`, `PolicyChangedEvent` |
| `server/internal/llm/interface.go` | `LLMClient` interface (`Stream`), `ChatClientAdapter` |
| `server/internal/llm/chatmodule/types.go` | `ChatRequest`, `ChatResponse`, `ChatHistory` types |
| `server/internal/llm/chatmodule/client.go` | `ChatClient` interface, OpenAI-compatible `Client`, streaming implementation |
| `server/internal/llm/chatmodule/provider.go` | Provider presets: Qwen, DeepSeek, OpenAI, Moonshot, Custom |
| `server/internal/llm/service/session.go` | Session ID generation, cancel function registry |
| `server/internal/config/config.go` | Config fields: `MinisterLLMEnabled`, `MinisterLLMProvider`, `MinisterLLMModel`, `MinisterLLMTimeoutMs`, `MinisterLLMRoles` |
| `server/cmd/server/app/minister_llm.go` | App factory: `buildMinisterEngineFactory`, provider routing, role parsing |
| `server/internal/game/query/reporting.go` | `BuildInformationReport`, reporting modes (clear/standard/high_distortion) |
| `server/internal/game/query/observation.go` | `ObservationSnapshot`, `ObservationStore`, node/unit memory |
| `server/internal/staticdata/units_model.go` | `Minister` struct: ID, Name, Role, IconKey, Ability, Personality, PersonalityDesc, Loyalty, Ambition |
| `protocol/panoptes/proto/v1/minister.proto` | `MsgMinisterReportChunk`, `MsgMinisterMetrics`, `MetricItem` |

### 3. Module-by-Module Analysis

#### 3.1 MinisterEngine (`server/internal/engine/minister/engine.go`)

**Fully implemented.** Core orchestration layer.

Key struct:
```go
type MinisterEngine struct {
    llmClient      llm.LLMClient
    memories       map[string]*MinisterMemory   // key: "playerID:role"
    enabledRoles   map[string]struct{}
    requestTimeout time.Duration
    model          string
    mu             sync.Mutex
}
```

Key methods:
- `GenerateReports(ctx, room)` -- iterates all human players x all profiles, spawns goroutines per report
- `generateOneReport(ctx, playerID, profile, room)` -- builds prompt, calls LLM, parses response, sends report chunk + metrics, records memory
- `PolishDraft(ctx, playerID, draft, input)` -- LLM polishes a rule-selected draft's display fields only
- `RecordMemory(playerID, role, entry)` -- adds memory entry (sliding window of 5)
- `SetEnabledRoles(roles)` / `SetTimeout(timeout)` / `SetModel(model)` -- configuration

**Key constraint**: `RuntimeRoom` interface requires `BuildMinisterReportInput(playerID, role)` -- the session layer provides this.

**Fallback behavior**: If LLM is nil or fails, returns hardcoded Chinese fallback JSON.

#### 3.2 Memory System (`server/internal/engine/minister/memory.go`)

**Fully implemented**, simple sliding window.

```go
type MinisterMemory struct {
    PlayerID string
    Role     string
    Entries  []MemoryEntry  // max 5, oldest evicted
    mu       sync.RWMutex
}

type MemoryEntry struct {
    Turn       int
    Type       string    // "report" or "draft"
    Content    string
    Outcome    string    // "generated", etc.
    PlayerResp string    // "ignored", "pending", "accepted", "rejected"
}
```

**Limitations**:
- Purely in-memory, no persistence across server restarts
- No semantic search or summarization -- raw entries injected into prompt as-is
- No cross-role memory sharing
- `PlayerResp` is always "ignored" for reports (MVP), "pending" for drafts

#### 3.3 Prompt System (`server/internal/engine/minister/prompt.go`)

**Fully implemented.** Two prompt types:

1. **Report prompt** (`BuildReportPrompt`): System prompt injects minister persona (name, role, personality, ability, loyalty, ambition) + Chinese-only constraint + JSON-only output contract. User prompt includes turn, phase, player, observation summary, current policy/research, memory.

2. **Draft prompt** (`BuildDraftPrompt`): System prompt adds "do not rewrite target" constraint. User prompt includes draft metadata (draft_id, kind, target_id, target_label).

**Key constraint in prompts**: All player-facing text must be Simplified Chinese. JSON keys and enum values may remain English. Output must be bare JSON (no markdown fences, no preamble).

#### 3.4 Parser System (`server/internal/engine/minister/parser.go`)

**Fully implemented.** Handles:

- `ParseMinisterResponse(response)` -> `MinisterOutput{Report, Metrics, Actions, ActionID}`
- `ParseDraftResponse(response)` -> `DraftOutput{Title, Summary, Rationale, RiskNote}`
- `normalizeJSONObjectPayload` -- strips markdown fences, extracts first valid JSON object from noisy text
- `sanitizePlayerVisibleChinese` -- if text is "obviously English" (3+ consecutive Latin letters, no Han characters), replaces with Chinese fallback
- `ExecuteActions` -- processes minister action items (build, move_units currently; repair_road and redirect_flow are stub/no-op)

**Action execution status**:
- `build`: Fully implemented -- appends to `state.TurnRuntime.Planning.MinisterBuilds`
- `move_units`: Fully implemented -- appends to `state.TurnRuntime.Planning.MinisterMoves`
- `repair_road`: Explicitly skipped (not yet in unified budget)
- `redirect_flow`: No-op (only logged)
- Unknown types: Logged as warning

#### 3.5 Draft System (`server/internal/game/session/minister_draft.go`)

**Fully implemented.** Two-phase pipeline:

1. **Rule phase**: `ai.RuleBotProvider{}.BuildPlanningIntents()` generates intents -> `buildMinisterDraftsFromIntents()` converts to `[]MinisterDraft` with Chinese text templates
2. **LLM polish phase**: `polishPreparedMinisterDraft()` calls `MinisterEngine.PolishDraft()` -> `applyPreparedMinisterDraftPolish()` updates only display fields (Title, Summary, Rationale, RiskNote), changes Source from `rule_only` to `rule+llm`

**Draft kinds supported**: research, policy, institution, build, recipe, unit_order

**Role assignment**: Most intents go to `domestic`, except `IssueUnitOrderIntent` which goes to `military` (except `settle_city` which stays `domestic`).

**Key constraint**: LLM cannot change executable fields (DraftID, TargetID, UnitID, Action, TargetNodeID, etc.).

#### 3.6 Observation & Reporting (`server/internal/game/query/reporting.go`)

**Fully implemented.** Three reporting modes:

| Mode | Confidence | Behavior |
|---|---|---|
| `clear` | "clear" | Direct inspection, no distortion |
| `standard` | "medium" | Memory-based omission/delay, no misread |
| `high_distortion` | "low" | Aggressive omission + delay + misread counts |

`BuildInformationReport(observation)` computes metadata: visible/memory/unknown node counts, visible/memory unit counts, delayed/omitted/misread counts, and notes array.

**Integration point**: `BuildMinisterObservationSummary()` in `session/minister_prompt.go` includes `report_mode`, `report_confidence`, `reported_omitted`, `reported_delayed`, `reported_misread` in the observation summary string passed to LLM prompts.

#### 3.7 LLM Abstraction Layer (`server/internal/llm/`)

**Fully implemented**, clean abstraction:

```
llm.LLMClient (interface)
  -> Stream(ctx, CompletionRequest) (<-chan string, error)
  
llm.CompletionRequest {
    Model, SystemPrompt, UserPrompt, SessionID
}

llm.ChatClientAdapter {
    Client chatmodule.ChatClient  // wraps OpenAI-compatible client
}
```

`chatmodule.Client` supports:
- `NormalChat` (blocking) and `StreamChat` (channel-based streaming)
- Provider presets: Qwen (DashScope), DeepSeek, OpenAI, Moonshot, Custom
- Options: model, temperature, topP, maxTokens
- Session management with cancel support

#### 3.8 Wire Protocol (`protocol/panoptes/proto/v1/minister.proto`)

**Minimal, already sufficient:**

```protobuf
message MsgMinisterReportChunk {
  string minister_role = 1;
  string chunk = 2;
  bool is_final = 3;
}

message MsgMinisterMetrics {
  string minister_role = 1;
  repeated MetricItem metrics = 2;
}

message MetricItem {
  string label = 1;
  string value = 2;
  string trend = 3;        // "up|down|stable"
  string confidence = 4;   // "high|medium|low"
  bool is_delayed = 5;
}
```

#### 3.9 App-Level Composition (`server/cmd/server/app/minister_llm.go`)

**Fully implemented.** Factory pattern:

1. `buildMinisterChatClient(cfg)` -- selects provider (qwen/deepseek), validates API key
2. `buildMinisterLLMClient(cfg)` -- wraps in `ChatClientAdapter`
3. `buildMinisterEngineFactory(cfg)` -- returns factory function that creates `MinisterEngine` with enabled roles, timeout, model
4. `parseMinisterEnabledRoles(raw)` -- parses comma-separated role string, defaults to `["domestic", "military"]`

Config fields:
```go
MinisterLLMEnabled   bool   // env:MINISTER_LLM_ENABLED, default:false
MinisterLLMProvider  string // env:MINISTER_LLM_PROVIDER, default:"qwen"
MinisterLLMModel     string // env:MINISTER_LLM_MODEL
MinisterLLMTimeoutMs int    // env:MINISTER_LLM_TIMEOUT_MS, default:50000
MinisterLLMRoles     string // env:MINISTER_LLM_ENABLED_ROLES, default:"domestic,military"
```

### 4. What Is Implemented vs Skeleton

| Feature | Status | Notes |
|---|---|---|
| MinisterEngine core | **Fully implemented** | Report gen, draft polish, memory, multi-role |
| Prompt system | **Fully implemented** | Persona injection, Chinese constraints, JSON contract |
| Parser system | **Fully implemented** | JSON extraction, English sanitization, action dispatch |
| Memory system | **Implemented (simple)** | In-memory sliding window, no persistence, no summarization |
| Draft pipeline | **Fully implemented** | Rule-based intents -> LLM polish -> display fields only |
| Observation/reporting | **Fully implemented** | Three distortion modes, metadata computation |
| LLM abstraction | **Fully implemented** | Multi-provider, streaming, session management |
| App composition | **Fully implemented** | Config-driven factory, role parsing |
| Protocol | **Minimal but sufficient** | Report chunks + metrics |
| Action execution | **Partial** | build + move_units work; repair_road/redirect_flow are stubs |
| MinisterActedEvent | **Audit-only** | Apply() is no-op, used for logging/tracking |

### 5. Key Interfaces and Data Structures

**RuntimeRoom interface** (engine -> session boundary):
```go
type RuntimeRoom interface {
    State() *domain.GameState
    HumanPlayerIDs() []string
    SendToPlayer(ctx context.Context, playerID string, msg proto.Message) error
    BuildMinisterReportInput(playerID string, role string) ReportPromptInput
}
```

**ActionRoom interface** (parser -> game state boundary):
```go
type ActionRoom interface {
    State() *domain.GameState
}
```

**LLMClient interface** (engine -> LLM boundary):
```go
type LLMClient interface {
    Stream(ctx context.Context, req CompletionRequest) (<-chan string, error)
}
```

**MinisterProfile** (static data -> prompt):
```go
type MinisterProfile struct {
    ID, Name, Role string
    Ability, Loyalty, Ambition int
    Personality, PersonalityDesc string
}
```

**MinisterDraft** (domain model, 25+ fields): Supports 6 kinds, 4 statuses, 2 sources, and executable fields (UnitID, Action, TargetNodeID, etc.).

### 6. Extension Points

1. **New minister roles**: Add to `MINISTER_LLM_ENABLED_ROLES` config, add static data entry, role is automatically picked up by `pickProfiles()`.

2. **New draft kinds**: Add enum to `domain.MinisterDraftKind`, add case in `ministerDraftFromIntent()`, add text template in `ministerDraftText()`.

3. **New action types**: Add case in `ExecuteActions()` in parser.go.

4. **New LLM providers**: Add preset in `chatmodule/provider.go`, add case in `buildMinisterChatClient()`.

5. **Memory enhancement**: `MinisterMemory` is a simple struct -- can be extended with persistence, summarization, or semantic search without changing the engine interface.

6. **Prompt customization**: `buildBaseSystemPrompt` + role-specific overlays in `buildReportSystemPrompt` / `buildDraftSystemPrompt`.

7. **Reporting modes**: `NormalizeReportingMode()` already supports clear/standard/high_distortion; new modes can be added.

8. **Protocol extension**: Proto messages can be extended with new fields (backward compatible).

### 7. Constraints and Invariants

1. **Rule authority**: LLM never decides executable targets. Drafts are rule-generated first; LLM only polishes display text.

2. **Chinese-only player text**: All player-facing strings must be Simplified Chinese. English text is sanitized to fallback strings.

3. **JSON-only output**: LLM output must be bare JSON. Markdown fences and preamble are stripped.

4. **Memory limit**: Max 5 entries per player:role pair, sliding window.

5. **Timeout**: Default 50s (`MINISTER_LLM_TIMEOUT_MS`).

6. **Thread safety**: `MinisterEngine.mu` protects memories, enabledRoles, model. `MinisterMemory.mu` protects entries.

7. **Fallback chain**: LLM nil -> hardcoded JSON -> parse error -> fallback JSON -> send failure -> logged warning.

8. **No persistence**: All minister state (memory, draft cache) is in-memory only.

### 8. Related Specs and Tasks

- `.trellis/tasks/05-06-military-minister-llm-integration/prd.md` -- Previous task that enabled military role LLM integration
- `.trellis/tasks/05-01-m8-asymmetric-information-reporting/prd.md` -- M8 task that built the reporting/distortion system
- `.trellis/tasks/05-01-m7-minister-default-execution/prd.md` -- M7 minister default execution (not read, but referenced)
- `.trellis/spec/backend/` -- Backend coding guidelines

### 9. Mandate Token (Mandate Token / 亲政令牌)

**Not found in codebase.** No references to "mandate", "mandate_token", "MandateToken", or "亲政" exist in any Go source files or proto files. This feature does not exist yet and would need to be designed and implemented from scratch.

The closest concept is `TokensLeft` in `domain.PlayerState` (line 95 of state.go), which represents action points per turn (configured via `rules.TokensPerTurn`). This is consumed by `TokenUsedEvent` and displayed in `MsgPlanningStart.Tokens`. However, this is a generic action-point system, not a "mandate token" system.

### 10. Information Distortion

**Fully implemented** in `server/internal/game/query/reporting.go`.

Three modes: `clear`, `standard`, `high_distortion`. The distortion is metadata-level (counts of omitted/delayed/misread nodes/units), not content-level manipulation. The actual observation filtering happens in `ObservationStore.BuildObservation()` (observation.go), which uses visibility and memory systems.

The minister prompt system already consumes distortion metadata via `BuildMinisterObservationSummary()`, which includes `report_mode`, `report_confidence`, `reported_omitted`, `reported_delayed`, `reported_misread` in the observation summary passed to LLM.

### Caveats / Not Found

1. **No mandate token system**: Completely absent from codebase. Would require new domain types, events, and potentially new proto messages.

2. **Memory not persisted**: Minister memory is purely in-memory. Server restart loses all memory. This is a known limitation for any "deep integration" that relies on memory continuity.

3. **No conversation history in LLM calls**: Each LLM call is stateless (single system+user prompt). The `ChatRequest.History` field exists in the chatmodule but is not used by the minister engine -- memory is injected as text in the user prompt instead.

4. **Action execution is partial**: Only build and move_units are functional. repair_road and redirect_flow are stubs.

5. **No minister personality effects on gameplay**: Personality, ability, loyalty, ambition are prompt-only attributes. They do not affect game mechanics (e.g., a "loyal" minister has the same behavior as a "disloyal" one in terms of game state).

6. **Single observation summary for all roles**: `BuildMinisterReportInput` builds one observation summary that is shared across all minister roles. There is no per-role information filtering or per-role distortion customization.
