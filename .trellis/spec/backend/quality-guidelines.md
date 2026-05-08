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

### Scenario: Institutions as First-Class Category Loadouts

#### 1. Scope / Trigger
- Trigger: any backend/protocol/static-data change that touches long-term `制度`
  choices. Institutions are no longer policy-layer entries; they are separate
  content, protocol fields, and client catalog sections.

#### 2. Signatures
- Authored content: `data/content/institutions/institutions.json`
- UI metadata: `data/ui/catalogs/institutions.json`
- Command: `MsgSetInstitutionLoadout.institution_ids`
- Result: `MsgSetInstitutionLoadoutResult.institution_ids`
- Planning snapshot: `MsgPlanningSnapshot.planned_institution_ids`
- Player view: `InstitutionStateView.candidate_institution_ids` and
  `active_institution_ids`
- Static catalog: `StaticCatalogSnapshot.institution_categories` and
  `institutions`

#### 3. Contracts
- `PolicyDefinition` and policy JSON are for short-term national policies only.
- Every `InstitutionDefinition` must reference a known institution category.
- A submitted institution loadout is a full category-preserving loadout, not a
  single card toggle.
- Each category may have at most one selected institution.
- Technologies unlock institution candidates with `unlock_institution`; policy
  unlocks remain `unlock_policy`.
- Server validation is authoritative. Client UI may filter by server-provided
  candidate/active/planned ids, but must not reimplement legality rules.

#### 4. Validation & Error Matrix
- Unknown institution id -> `invalid_target`.
- Known policy id submitted as an institution -> `invalid_target`.
- Institution candidate not unlocked for the player -> `invalid_directive`.
- Duplicate selected choices in one category -> `invalid_directive`.
- Unmet institution prerequisites -> prerequisite validator error code.
- Valid loadout -> queue pending institution changes for next documented
  activation point.

#### 5. Good / Base / Bad Cases
- Good: selecting `free_market` preserves the existing `power` institution and
  replaces only the `economy` category.
- Base: an empty loadout is valid when the rules allow no pending choices.
- Bad: adding an institution to `policies.json` with `layer: institutional`, or
  sending `policy_ids` through any institution command/result path.

#### 6. Tests Required
- Static-data load tests cover institution categories and entries.
- Datagen validation rejects unknown institution categories.
- Planning tests cover unknown ids, national policy ids, locked candidates, and
  duplicate category choices.
- Activation tests cover next-turn pending institution application.
- Effect tests cover active institution modifier effects and logistics priority.
- Client tests cover separate policy/institution catalog hydration and full
  category-preserving loadout submission.

#### 7. Wrong vs Correct
#### Wrong
```go
Policies: []staticdata.PolicyDefinition{
    {ID: "academy_charter", Layer: "institutional"},
}
```
#### Correct
```go
InstitutionCategories: []staticdata.InstitutionCategoryDefinition{
    {ID: "administration"},
}
Institutions: []staticdata.InstitutionDefinition{
    {ID: "academy_charter", Category: "administration"},
}
```

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
- Trigger: any backend change that wires minister roles to LLM report generation.
- Minister LLM is a reported-expression and candidate-selection layer. It may select from rule-generated candidates through the report action contract, but it must not invent executable planning targets outside that bounded contract.

#### 2. Signatures
- Config field: `Config.MinisterLLMRoles string`
- Environment key: `MINISTER_LLM_ENABLED_ROLES`
- Default value: `domestic,military`
- Role gate: `MinisterEngine.SetEnabledRoles([]string{...})`
- Report path: `MinisterEngine.GenerateReports(ctx, room)`

#### 3. Contracts
- Enabled roles are comma-separated role ids and must be trimmed, lower-cased, and de-duplicated before passing to `SetEnabledRoles`.
- Blank configured roles fall back to `[]string{"domestic", "military"}`.
- Rule-generated draft/candidate targets come from the legal candidate pool, not from per-draft LLM calls.
- Draft cards remain `rule_only` until selected, accepted, rejected, or staled.
- Do not add per-candidate draft polishing; the single report prompt receives current observations plus Action Candidates, then returns narrative, metrics, and optional approval-gated actions.
- Minister reports may include narrative text, metrics, and approval-gated report actions. Report `actions` must follow the "Minister Report Actions Become Approval-Gated Proposals" contract below.

#### 4. Validation & Error Matrix
- Role omitted from enabled roles -> no report is generated for that role.
- LLM disabled or client missing -> deterministic fallback reports are generated where applicable, and rule-only candidates remain usable.
- LLM stream error -> use a Chinese fallback report; do not mutate draft candidates.
- Invalid report JSON -> parse the Chinese fallback report; do not apply actions from invalid payloads.
- English or empty player-visible report/metric text -> sanitize to the Chinese fallback strings.
- Unknown role with no profile -> no report is generated for that role.

#### 5. Good / Base / Bad Cases
- Good: `MINISTER_LLM_ENABLED_ROLES=domestic,military`; both roles produce subjective reports, and an LLM-selected legal candidate becomes an approval-gated `llm_action` proposal.
- Base: `MINISTER_LLM_ENABLED_ROLES=domestic`; domestic report generation runs, while military candidates remain rule-only and available.
- Bad: one LLM request per draft candidate just to rewrite card copy, or LLM output changes a military candidate from `move A2` to `attack enemy-1`.

#### 6. Tests Required
- Config/env test reads `MINISTER_LLM_ENABLED_ROLES`.
- Role parser test covers trim, lower-case, de-duplication, and blank fallback.
- Report generation test verifies a military role is included when enabled.
- Report generation test verifies parsed actions are forwarded to the room callback.
- Report parser tests cover invalid JSON/noisy JSON and English visible text fallback.

#### 7. Wrong vs Correct
#### Wrong
```go
// Do not start one LLM request per rule-generated candidate.
for _, draft := range drafts {
    go requestPerDraftLLMRewrite(draft)
}
```
#### Correct
```go
// Generate rule-only candidates once; report LLM selects from the bounded list.
drafts := buildMinisterDraftsFromLegalCandidates(turn, playerID, state, observation)
input.ActionCandidates = buildMinisterActionCandidateSummary(drafts)
```

### Scenario: Minister Subjective Report and Memory Pressure

#### 1. Scope / Trigger
- Trigger: backend changes that build minister report prompts, minister observation summaries, or minister memory/favor behavior.
- Minister reports are not neutral database dumps. They are subjective `reported` narratives generated from player-visible `observed` data, then shaped by minister profile, loyalty, ambition, favor, and recent memory.

#### 2. Signatures
- Report prompt builder: `BuildReportPrompt(profile MinisterProfile, input ReportPromptInput) llm.CompletionRequest`
- Runtime observation summary: `Runtime.BuildMinisterObservationSummary(playerID string, role string) string`
- Memory prompt: `MinisterMemory.ToPromptString() string`
- Feedback hook: `MinisterMemory.Add(MemoryEntry{PlayerResp: "accepted"|"rejected"|"stale"})`

#### 3. Contracts
- Report prompts must preserve the `truth -> observed -> reported` boundary and must forbid inventing hidden truth.
- Observation summaries must include information-report metadata when available: `report_mode`, `report_confidence`, `reported_omitted`, `reported_delayed`, and `reported_misread`.
- Role-specific observation focus belongs in the summary so domestic and military ministers can emphasize different facts without reading hidden state.
- The same observation may produce the same user prompt, but the system prompt must vary by minister profile and style pressure.
- Low loyalty combined with high ambition should push the report toward self-protective distortion pressure such as softening bad news or claiming credit.
- High cautiousness should push the report toward risk-boundary and uncertainty language.
- Report prompt output must be exactly `report`, `metrics`, `actions`, and `action_id`.
- `report` is the narrative track: a Chinese subjective minister report based only on observed information.
- `metrics` is the numeric track: an array of items with `label`, `value`, `trend`, `confidence`, and `is_delayed`; labels and values are Chinese player-visible text, while trend/confidence remain bounded enum strings.
- Minister memory favor starts at 50, is clamped to 0..100, and changes through feedback: accepted +5, rejected -8, stale -3.
- Low favor must add a prompt hint that the minister has been repeatedly rejected and should become more conservative and risk-focused.

#### 4. Validation & Error Matrix
- Missing observation summary -> use a Chinese no-observation fallback, not hidden truth.
- Nil memory -> use a no-memory fallback.
- Rejected or stale memory entries -> lower favor and alter future prompt hints, but do not directly mutate planning state.
- Profile fields present but extreme -> keep prompt generation deterministic and bounded; do not let prompt helpers panic.

#### 5. Good/Base/Bad Cases
- Good: the same high-distortion observation creates different report pressure for a cautious loyal domestic minister versus a low-loyalty ambitious military minister.
- Base: standard reporting mode includes delayed/omitted counts and asks the LLM to express uncertainty in Chinese narrative text.
- Bad: a prompt says the enemy is definitely in a hidden node that was not present in the observed snapshot.

#### 6. Tests Required
- Prompt test verifies report prompts include visibility boundaries, Chinese-only player text constraints, and no-fence JSON output requirements.
- Prompt test verifies the same observation gains different subjective pressure through different minister profiles.
- Session prompt test verifies observation summaries include information-report distortion metadata.
- Memory test verifies accept/reject/stale feedback changes favor and low favor changes the prompt hint.

### Scenario: Minister Report Actions Become Approval-Gated Proposals

#### 1. Scope / Trigger
- Trigger: minister report generation returns structured `actions` that should become player-visible proposals instead of mutating planning state immediately.
- Minister actions remain advisory output from the LLM, but the backend must convert them into pending minister drafts / proposals and let the player approve them through the existing minister draft flow.

#### 2. Signatures
- Engine callback: `RuntimeRoom.ApplyMinisterActions(playerID string, role string, actions []MinisterActionItem) error`
- Supported action types in the current contract:
  - `select_candidate`
  - `build`
  - `set_research`
  - `set_policy`
  - `set_institution_loadout`
  - `set_building_recipe`
- Action param contracts:
  - `select_candidate`: `draft_id` from the report prompt Action Candidates list
  - `build`: `node_id`, `building_type`, optional `city_id`
  - `set_research`: `technology_id`
  - `set_policy`: `policy_id`
  - `set_institution_loadout`: `institution_ids`
  - `set_building_recipe`: `node_id`, `recipe_id`
  - Map/unit decisions: choose a `kind=operation` candidate with `select_candidate`; do not expose `move_units`, `unit_order`, or `issue_unit_order` as minister report actions.
- Each action JSON item must have exactly `type` and `params`; `params` must be a JSON object.

#### 3. Contracts
- `MinisterEngine.generateOneReport` must forward non-empty `actions` to the room callback after parsing the report JSON.
- Report prompts should include current same-role pending minister drafts as Action Candidates when they exist.
- If Action Candidates exist, the prompt must instruct the LLM to prefer `select_candidate` over hand-written action params for the same decision surface.
- `select_candidate` must only be allowed to reference an existing current-turn pending draft for the same player and minister role.
- Rule-generated map/unit action space must be folded into bounded `operation` candidates. Each operation carries a small batch of normal planning commands and hides the raw per-unit/per-node search space from the LLM prompt.
- Accepting an operation proposal must pre-validate every contained command through the same normal planning validators, then apply the batch through normal planning state writes. If any command is invalid, no command in the operation may be applied.
- When one or more same-role rule candidates are selected, selected drafts become `llm_action` proposals and unselected same-role `rule_only` candidates become stale/unavailable.
- Session-level action application must route through existing planning validation and create pending minister drafts/proposals instead of writing planning orders directly.
- Build actions must be validated with the normal build-order rules before creating a pending minister draft.
- Research, policy, institution loadout, and building recipe actions must use the same validators as direct planning commands before creating a pending minister draft.
- Invalid minister actions are ignored after logging; they must not mutate authority directly or bypass the normal planning checks.
- Staged actions should be visible through `MsgGameSync.minister_proposals` and remain pending until accepted.

#### 4. Validation & Error Matrix
- Missing action type or required params -> ignore the action.
- `select_candidate` references an unknown, stale, cross-role, or unavailable candidate -> ignore the selection and do not stale other candidates.
- Build action fails `ValidateBuildOrder` -> log warning, do not create a proposal.
- Move action fails `ValidatePlanningUnitOrder` -> log warning, do not create a proposal.
- Research action fails `ValidateResearchTarget` -> log warning, do not create a proposal.
- Policy or institution action fails policy/institution validation -> log warning, do not create a proposal.
- Recipe action fails `ValidateRecipeSelection` -> log warning, do not create a proposal.
- Generic unit order fails `ValidatePlanningUnitOrder` -> log warning, do not create a proposal.
- Unsupported action type -> log warning, ignore.
- Valid action -> create a pending minister draft/proposal; the eventual accept path still uses the normal planning surfaces.

#### 5. Good/Base/Bad Cases
- Good: the legal candidate pool generates research/policy/operation candidates from the current observed snapshot, the report prompt lists them, and the LLM returns `select_candidate` for the candidate it wants to formally recommend.
- Good: LLM returns `set_research`, `set_policy`, `set_institution_loadout`, or `set_building_recipe` when no suitable candidate exists, and the session creates pending minister proposals that the player can approve.
- Base: LLM returns `build`, and the session creates a proposal that later flows through the same approve/reject path as other minister drafts.
- Bad: minister action writes to `state.TurnRuntime.Planning` by hand or skips validation because the LLM already emitted JSON.

#### 6. Tests Required
- Engine test confirms parsed actions are forwarded to the room callback.
- Session test confirms valid minister build actions stage pending minister drafts.
- Session test confirms deprecated map/unit action compatibility types are ignored rather than staged.
- Session test confirms expanded research, policy, institution, and recipe actions stage pending minister drafts without mutating planning state before approval.
- Session test confirms `select_candidate` marks the selected same-role candidate as `llm_action` while unselected rule candidates stay hidden in the runtime cache.
- Prompt test confirms Action Candidates are injected into report prompts and the selection contract is visible.
- Projection/query test confirms minister proposals carry typed commands, operation command batches, and raw JSON.
- Planning test confirms accepting an operation applies every step, and invalid operations reject without partial application.
- Regression tests confirm invalid minister actions are ignored, not applied.

#### 7. Wrong vs Correct
#### Wrong
```go
state.TurnRuntime.Planning.MinisterBuilds = append(state.TurnRuntime.Planning.MinisterBuilds, order)
```
#### Correct
```go
if errCode := economy.ValidateBuildOrder(state, playerID, nodeID, buildingType, cityID); errCode == "" {
    draft := domain.MinisterDraft{Kind: domain.MinisterDraftKindBuild, Status: domain.MinisterDraftStatusPending}
    state.TurnRuntime.Planning.SetMinisterDrafts(playerID, append(existingDrafts, draft))
}
```

### Scenario: Minister Legal Candidate Pool

#### 1. Scope / Trigger
- Trigger: backend changes that prepare default minister drafts, action candidates, or LLM-selectable gameplay options at planning start.
- Candidate generation is the rules-owned action-space layer. It enumerates legal options; it does not choose the final recommendation and does not mutate planning state.

#### 2. Signatures
- Candidate entrypoint: `buildMinisterDraftsFromLegalCandidates(turn int, playerID string, state *domain.GameState, observation *query.ObservationSnapshot) []domain.MinisterDraft`
- Intent entrypoint: `enumerateLegalMinisterCandidateIntents(playerID string, state *domain.GameState, observation *query.ObservationSnapshot) []planning.Intent`
- Runtime caller: `Runtime.PrepareMinisterDraftCacheForTurn(turn int)`

#### 3. Contracts
- Candidate generation must read from the player observation snapshot for map, building, and unit surfaces; hidden truth and memory-only nodes must not produce build, recipe, or unit-order candidates.
- Enumerated candidates must still pass the owning validators before draft creation:
  - research -> `economy.ValidateResearchTarget`
  - build -> `economy.ValidateBuildOrder`
  - recipe -> `economy.ValidateRecipeSelection`
  - national policy -> `planning.ValidatePolicySelection(..., "national")`
  - institutional loadout -> `planning.ValidateInstitutionLoadout`
  - unit orders/map actions -> `orders.ValidatePlanningUnitOrder`
- Candidate generation may enumerate many valid options for a surface. Ranking, selection, and narrative explanation belong to the minister report LLM via `select_candidate`.
- The hidden candidate cache is not projected into `state.TurnRuntime.Planning.MinisterDrafts` until the LLM selects a candidate or stages a new proposal.
- Generated candidates must be `MinisterDraftSourceRuleOnly`, `pending`, and `available` in the hidden runtime cache until selected, accepted, rejected, or staled.
- Candidate generation must not write `BuildOrders`, `RecipeSelections`, `UnitOrders`, pending research/policy/institution maps, or resolving caches.
- Draft IDs must include every command dimension that changes execution semantics, including `city_id`, `secondary_node_id`, and deterministic params when present.

#### 4. Validation & Error Matrix
- Missing state or player id -> return no candidates.
- Missing observation -> build a normal observation for the player, then enumerate from that observed view.
- Hidden or memory-only node/unit -> no candidate for that target.
- Validator rejects a candidate -> skip it; do not log as an LLM failure.
- Duplicate intent key -> keep the first deterministic candidate.
- City-core building definition -> skip as a normal build candidate.

#### 5. Good/Base/Bad Cases
- Good: two visible legal technologies, policies, buildings, recipes, or unit orders produce multiple rule-only candidate drafts for the LLM to choose from.
- Base: no legal candidates for a surface produces no draft for that surface, while other surfaces still enumerate.
- Bad: a hidden node exists in `state.NodeIndex` but not in `observation.VisibleNodes`, yet a build or move draft targets it.
- Bad: candidate preparation writes directly to `state.TurnRuntime.Planning.BuildOrders` before player approval.

#### 6. Tests Required
- Session test proves multiple legal candidates can be generated for research, policy, institution, build, recipe, and unit-order surfaces.
- Session test proves hidden nodes do not produce build or unit-order candidate drafts.
- Session test proves candidate generation does not mutate pending planning orders before approval.
- Draft test proves semantically different command dimensions produce distinct draft IDs.

#### 7. Wrong vs Correct
#### Wrong
```go
// Do not use the old RuleBot shortlist as the LLM candidate universe.
intents, _ := (ai.RuleBotProvider{}).BuildPlanningIntents(ctx, req)
```
#### Correct
```go
observation := r.BuildObservation(playerID)
drafts := buildMinisterDraftsFromLegalCandidates(turn, playerID, r.state, observation)
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

### Scenario: Unit Occupancy Is Non-Stacking

#### 1. Scope / Trigger
- Trigger: any backend change that creates units, moves units, projects node unit counts, builds debug scenarios, or changes combat movement/conflict resolution.
- Panoptes uses one-unit-per-node occupancy. Stacking is not a supported gameplay state.

#### 2. Signatures
- Domain occupancy helpers:
  - `domain.GetUnitAtNode(world donburi.World, pos domain.Position) (*donburi.Entry, bool)`
  - `domain.HasUnitAtNode(world donburi.World, pos domain.Position) bool`
  - `domain.UnitOccupancyViolations(world donburi.World) map[domain.Position][]*donburi.Entry`
- Spawn entrypoint: `domain.ResolveUnitSpawnPosition(state *domain.GameState, origin domain.Position) (domain.Position, bool)`
- Combat rule points:
  - `combat.StaticSnapshotBlockRule.SourceFor(...)`
  - `combat.NodeConflictDetector.Detect(...)`
  - `combat.MovementApplyPhase.Apply(...)`

#### 3. Contracts
- A stable authoritative world must not contain more than one living unit at the same `Position`.
- Unit starting positions are frozen movement blockers for the entire resolving pass, regardless of faction or whether the unit moves away this turn.
- Friendly units block movement but do not become `charge` targets and do not produce damage events.
- If multiple units candidate the same node in resolving, all members are treated as a node occupancy collision and move by the node-conflict fallback rule.
- Hostile pairs inside a node collision still produce normal conflict damage; friendly-only collisions only affect movement.
- Spawn, production, research grants, bootstrap, and debug/scenario placement must consult the shared occupancy rule before creating a unit.
- ECS `CreateUnit` is a low-level constructor. Gameplay entrypoints must validate or resolve occupancy before calling it.

#### 4. Validation & Error Matrix
- Spawn candidate occupied by any living unit -> skip candidate and keep searching.
- No legal spawn candidate -> do not create the unit; existing event behavior may skip without a client error.
- Friendly unit blocks path -> moving unit stops at the last legal non-conflicting tile.
- Friendly-only same-destination collision -> no damage/conflict event, all members fall back by movement rules.
- Hostile same-destination collision -> normal node conflict hostile-pair damage, all members fall back by movement rules.
- Debug/scenario placement attempts occupied node -> fail fast rather than silently creating stacked fixtures.

#### 5. Good/Base/Bad Cases
- Good: two allied infantry both target `N1_1`; neither takes damage, both fall back, and `UnitOccupancyViolations` is empty.
- Base: one infantry is produced near a barracks; `ResolveUnitSpawnPosition` skips buildings, reservations, impassable terrain, and occupied unit nodes.
- Bad: a bootstrap helper only checks for an existing infantry and creates a new infantry on top of a settler.
- Bad: `BlockRule` only blocks enemy units, allowing friendly units to move through or stop on each other.

#### 6. Tests Required
- Combat test for friendly same-destination collision with no damage and no final stack.
- Combat test for friendly starting position blocking movement.
- Combat test that hostile node conflict behavior still emits deterministic hostile pairs.
- Spawn/production/research/bootstrap tests that occupied candidates are skipped.
- Scenario/debug fixture tests or guards that fail on occupied placement.

#### 7. Wrong vs Correct
#### Wrong
```go
// Only enemy units block movement: this reintroduces friendly stacking.
if sources.Unit != nil && sources.Unit.Owner != unit.PlayerID {
    return *sources.Unit, true
}
```
#### Correct
```go
// Any living unit occupies the node; faction only affects whether damage/charge applies.
if sources.Unit != nil && sources.Unit.UnitID != unit.UnitID {
    return *sources.Unit, true
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
