# brainstorm: independent institution data model

## Goal

Move institutions out of the current policy catalog into a first-class institution data model. Policies should remain short-term national directions selected turn by turn; institutions should become long-term state structures grouped by domains such as power, citizenship, economy, trade, military, and administration. Each institution domain should have mutually exclusive choices, and active institutions should affect how the national machine behaves across turns.

## What I Already Know

* Current authored policy data lives in `data/content/policies/policies.json`.
* Current institutional options are represented as `PolicyDefinition` entries with `layer: "institutional"`.
* The user wants institutions migrated to a new JSON instead of overlapping with policy data.
* Current institution gameplay is closer to a card-slot loadout: `InstitutionState` stores `SlotCount`, candidate policy ids, active policy ids, pending policy ids, and pending activation turn.
* Current backend command is `MsgSetInstitutionLoadout` with `policy_ids`; the user explicitly authorized changing the original proto and regenerating outputs for this migration.
* Existing effects that already influence national behavior:
  * `modifier_effects` through `GameState.ActiveModifierEffects()` and `ApplyFloatModifier()`.
  * `logistics_priority` through economy logistics priority calculation.
* Current frontend displays institutions inside the Policy Focus panel by splitting catalog policy rows by `layer`.
* Current frontend sends `SetInstitutionLoadout(policyId)` for an institutional row, which is a single-option interaction and does not represent domain-by-domain institutions.

## Assumptions

* Policy and institution should become separate authored content sections:
  * `data/content/policies/policies.json` for short-term national policies.
  * `data/content/institutions/institutions.json` for long-term institutions.
* UI catalog metadata should also become separate:
  * `data/ui/catalogs/policies.json`
  * `data/ui/catalogs/institutions.json`
* The first implementation should perform a clean breaking protocol migration so institutions are named as institutions everywhere instead of flowing through policy-named fields.
* The initial migration should prioritize correctness and clarity over a full Victoria 3-style law enactment simulation.

## Decisions

* Protocol compatibility is not required for this migration. The existing proto may be changed directly and regenerated with `make gen`.
* Institution commands and state fields should use institution-oriented names, not policy-oriented names.

## Requirements

### Product Semantics

* Policies represent short-term national direction:
  * Selected as one active national policy.
  * Same-turn lock-in at resolving start.
  * Good for temporary priorities such as war preparedness, recovery, expansion, or reorganization.
* Institutions represent long-term state structure:
  * Grouped into domains/categories.
  * Each category can have at most one active institution.
  * Changes are planned during planning, locked in during resolving, and activate on a future planning start.
  * Good for structural defaults: who has power, who counts as citizens, how the economy is organized, how trade works, how the army is maintained, and how administration/reporting works.

### Initial Institution Categories

* `power`: who holds effective authority.
  * Examples: royal_prerogative, bureaucratic_cabinet, ministerial_devolution.
* `citizenship`: who the state prioritizes and protects.
  * Examples: stratified_subjects, civic_subjects, universal_subjects.
* `economy`: how production and shortage allocation are organized.
  * Examples: estate_economy, guild_regulation, state_workshops, free_market.
* `trade`: how external exchange and market access work.
  * Examples: inward_rationing, mercantile_charter, protective_tariffs, free_trade.
* `military`: how the state maintains and mobilizes forces.
  * Examples: feudal_levies, professional_army, conscription, war_supply_board.
* `administration`: how information, reporting, and local execution work.
  * Examples: local_autonomy, central_bureaucracy, inspectorate, secretariat_cabinet.

### Authored Data Shape

MVP institution file:

```json
{
  "$schema": "../../schema/content/institutions.schema.json",
  "categories": [
    {
      "id": "administration",
      "name": "行政制度",
      "description": "决定中央如何收集信息、分配命令并约束地方执行。",
      "sort_order": 60
    }
  ],
  "institutions": [
    {
      "id": "central_bureaucracy",
      "category": "administration",
      "activation_timing": "next_turn",
      "prerequisites": [
        { "type": "technology_unlocked", "target_id": "civic_institutions" }
      ],
      "explicit_effects": [],
      "modifier_effects": [
        {
          "trigger": "logistics.road_capacity",
          "modifier_type": "flat",
          "value": 1
        }
      ],
      "logistics_priority": [
        { "tag": "research", "priority": 60 },
        { "tag": "city_core", "priority": 80 }
      ],
      "governance_effects": [
        { "type": "report_accuracy", "target": "logistics", "value": 15 },
        { "type": "minister_autonomy", "target": "all", "value": -10 }
      ],
      "tags": ["administration", "information"]
    }
  ]
}
```

MVP can parse and preserve `governance_effects` but does not need to consume every effect type immediately. The first consumed effect classes should be:

* `modifier_effects`
* `logistics_priority`

The second implementation wave should consume `governance_effects` for minister/reporting behavior.

### Server Model

* Add `InstitutionDefinition`, `InstitutionCategoryDefinition`, and `GovernanceEffectDefinition` in static data.
* Add catalog indexing APIs:
  * `GetInstitution(id)`
  * `Institutions()`
  * `GetInstitutionCategory(id)`
  * `InstitutionCategories()`
* Keep `PolicyDefinition` for national policy only.
* Generated server bundle should include separate `institutions` and `institution_categories` sections or a combined `institutions` section with categories and entries.
* Existing technologies that unlock institutional policies should migrate from `unlock_policy` to a new explicit effect such as `unlock_institution`, or `unlock_policy` should remain only for national policy unlocks.
* Institution candidate state should store institution ids, not policy ids.
* Replace slot-count semantics with category semantics:
  * candidate institutions are unlocked by id.
  * active institutions are stored by category.
  * pending institution changes are stored by category.
  * validation rejects unknown institution ids, unmet prerequisites, non-candidates, and more than one selected institution per category.

### Server Effects

Institutions should influence the state through explicit unified query points:

* `GameState.ActiveModifierEffects(playerID)` appends active institution `modifier_effects`.
* Economy logistics priority reads both active national policy priorities and active institution priorities.
* Future `ActiveGovernanceEffects(playerID)` returns active institution governance effects for:
  * report accuracy or delay,
  * minister autonomy,
  * mandate token count,
  * veto cost,
  * default draft/proposal tendency,
  * institution reform delay/cost.

Policies should continue to influence same-turn modifiers and logistics priority but should not define governance effects unless a future design explicitly adds temporary emergency powers.

### Protocol

Breaking migration path:

* Update proto sources directly, then run `make gen`.
* Rename institution loadout payloads from policy terminology to institution terminology.
  * `MsgSetInstitutionLoadout.policy_ids` should become `institution_ids`.
  * `MsgSetInstitutionLoadoutResult.policy_ids` should become `institution_ids`.
  * `MsgPlanningSnapshot.planned_institution_policy_ids` should become `planned_institution_ids`.
  * `InstitutionStateView.candidate_policy_ids` should become `candidate_institution_ids`.
  * `InstitutionStateView.active_policy_ids` should become `active_institution_ids`.
* Add static catalog proto entries for institutions instead of overloading `PolicyCatalogEntry`.
* Add new fields to `StaticCatalogSnapshot` using new field numbers:
  * `repeated InstitutionCategoryCatalogEntry institution_categories = <new number>;`
  * `repeated InstitutionCatalogEntry institutions = <new number>;`
* Update all generated Go and C# usages to the new names.

### Client

* Static catalog cache/store should expose institutions separately from policies.
* Policy Focus UI should stop showing institutional choices inside the policy list.
* Add or repurpose a management panel for institutions:
  * Title: `制度`
  * Group by institution category.
  * Each category shows current active institution, planned institution, and available candidates.
  * Selecting an institution in a category builds a full category loadout and sends `MsgSetInstitutionLoadout`.
* UI should filter institution choices by candidate ids from `GameStateCache.GetInstitutionState()`, while still showing active/planned state.
* Frontend must remain a presentation layer:
  * It may filter visible/available candidates based on server state.
  * It must not perform authoritative legality checks beyond using server-provided candidate/active/planned data.

### Data Migration

* Move current institutional policy entries out of `data/content/policies/policies.json`:
  * `academy_charter`
  * `logistics_corps`
  * `foundry_directives`
  * `mercantile_charter`
  * `research_mandate`
* Add categories to the migrated entries.
* Move UI metadata for those ids from `data/ui/catalogs/policies.json` to `data/ui/catalogs/institutions.json`.
* Update technology effects in `data/content/technologies/technologies.json` to unlock institutions.
* Regenerate data and proto outputs through approved commands:
  * `make data-gen`
  * `make gen`

### Testing Requirements

Backend tests should cover:

* Static data loads institutions and categories from the new JSON.
* Data validation rejects institution entries with unknown category ids.
* Data validation rejects duplicate active loadout choices for a category.
* Technology activation unlocks institution candidates via `unlock_institution`.
* Planning rejects national policies in institution loadout.
* Planning rejects unknown institutions and non-candidate institutions.
* Planning accepts one institution per category and queues pending changes.
* Planning start activates pending institution changes on the documented turn.
* `ActiveModifierEffects()` includes active institution modifier effects.
* Logistics priority includes active institution priority definitions.

Frontend tests or focused verification should cover:

* Static catalog hydration maps institution catalog entries separately from policies.
* Institution UI groups choices by category.
* Institution UI marks active/planned choices.
* Institution UI sends a full loadout preserving other active/planned categories when changing one category.
* Policy UI only shows national policies.

## Acceptance Criteria

* [x] Authored institution data lives in a new institution JSON and no institutional entries remain in the policy JSON.
* [x] Policies and institutions have separate staticdata models, generated schema, UI metadata, and catalog sections.
* [x] Institution choices are category-based and mutually exclusive per category.
* [x] Existing institution effects still work after migration: research output, road capacity, recipe progress, and logistics priority.
* [x] Technology unlocks institution candidates from the new institution catalog.
* [x] Server planning and activation rules preserve next-turn institution activation.
* [x] Client displays institutions separately from national policies.
* [x] Client can send institution loadout changes without losing selections in other categories.
* [x] `make data-validate`, `make data-gen`, `make gen`, and relevant Go/C# checks pass or blockers are documented.

## Definition Of Done

* Tests added or updated for static data, validation, planning, activation, and effect application.
* Protocol sources are updated directly for clean institution terminology, with generated Go/C# code produced by `make gen`.
* Generated static data is produced by `make data-gen`.
* Client remains presentation-only and does not duplicate server legality rules.
* Documentation or Trellis spec is updated if new conventions are introduced.
* Rollback path is clear: revert institution JSON/proto/staticdata migration together rather than partially.

## Out Of Scope

* Full Victoria 3-style law enactment, political movements, legitimacy, or interest groups.
* Preserving backward compatibility with old policy-named institution proto fields.
* Making every governance effect operational in the first implementation.
* Adding new third-party dependencies.
* Client-side authoritative legality validation.

## Implementation Plan

### Phase A: Static Data Split

1. Add institution staticdata structs and catalog indexing.
2. Add `data/content/institutions/institutions.json`.
3. Add `data/ui/catalogs/institutions.json`.
4. Update schema generation and semantic validation.
5. Update generator bundle and section output.
6. Migrate existing institutional policy entries and UI metadata.

### Phase B: Unlocks And Domain State

1. Add `unlock_institution` explicit effect.
2. Update technology activation to unlock institution candidates.
3. Refactor `InstitutionState` naming internally toward institution ids.
4. Replace slot-count validation with category mutual exclusion.
5. Preserve pending activation turn behavior.

### Phase C: Effect Consumption

1. Update `ActiveModifierEffects()` to read active institutions from the institution catalog.
2. Update logistics priority calculation to read active institution priorities.
3. Add `ActiveGovernanceEffects()` skeleton and tests, even if only a subset is consumed initially.

### Phase D: Protocol And Catalog Sync

1. Rename institution command/state fields from policy ids to institution ids in proto.
2. Add institution catalog proto messages.
3. Add institution entries to `StaticCatalogSnapshot`.
4. Run `make gen`.
5. Update server projection and client protocol mappers.

### Phase E: Client UI

1. Add institution DTOs/store hydration.
2. Split Policy Focus to show only national policies.
3. Add Institution panel grouped by category.
4. Make category selection send a full institution loadout.
5. Keep server response/error feedback path.

### Phase F: Verification

1. Run data validation and generation.
2. Run relevant Go tests for staticdata, datagen, planning, resolution, economy.
3. Run Unity/client compile or available edit-mode tests.
4. Update docs/spec if a new staticdata convention should be preserved.

## Technical Notes

* Existing policy/institution data source: `data/content/policies/policies.json`.
* Existing staticdata model: `server/internal/staticdata/effects_model.go`.
* Existing schema generation: `server/internal/datagen/schema.go`.
* Existing semantic validation: `server/internal/datagen/validate.go`.
* Existing bundle generation: `server/internal/datagen/generator.go`.
* Existing institution validation: `server/internal/game/planning/institution.go`.
* Existing technology activation: `server/internal/event/research.go`.
* Existing modifier consumption: `server/internal/domain/modifiers.go`.
* Existing logistics priority consumption: `server/internal/engine/economy/logistics_priority.go`.
* Existing protocol catalog surface: `protocol/panoptes/proto/v1/data_catalog.proto`.
* Existing game state institution view: `protocol/panoptes/proto/v1/game_state.proto`.
* Existing institution command surface: `protocol/panoptes/proto/v1/orders.proto`.
* Existing frontend policy/institution combined UI: `client/Assets/Scripts/Runtime/Presentation/ViewModels/PolicyFocusViewModel.cs` and `client/Assets/Scripts/Runtime/Presentation/Binders/UiToolkit/PolicyFocusUiToolkitBinder.cs`.
* Relevant Trellis spec context: backend index, frontend index, cross-layer thinking guide, code-reuse thinking guide.

## Research References

No external research was needed for this planning pass. The decision is driven by product semantics, current repo architecture, and project constraints in AGENTS.md.
