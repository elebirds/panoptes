# Backend Headless Acceptance Conventions

Status: M1.4 contract, April 30, 2026.

This document standardizes backend-only validation for M1 and gives M2-M6 a
stable scenario shape. It does not introduce gameplay rules or client
automation.

## Current Harness Inventory

The backend validation stack has three layers:

| Layer | Entry point | Current role |
| --- | --- | --- |
| Deterministic setup | `server/internal/game/scenario` | Builds small in-memory catalogs, maps, players, units, buildings, and initial planning state. |
| Headless room harness | `server/internal/debug/harness.go` | Starts a prepared `GameRoom`, injects planning commands, submits turns, waits for `MsgPlanningStart` and `MsgGameSync`, and records `TurnRecord` state summaries. |
| Debug inspection | `server/internal/debug` and `server/internal/transport/http` | Captures outbound messages, summarizes state, records command results, records latest sync/game-over payloads, and exposes DEV_MODE HTTP helpers. |

Current M1 synthetic scenarios:

| Scenario | Primary coverage | Harness test coverage |
| --- | --- | --- |
| `research_unlock_build` | Research completes on one turn and activates for building on the next planning start. | `TestHarnessResearchUnlockBuild_NextTurnOnly` |
| `industry_budget_exhaustion` | Shared industry budget accepts the first build and reports a later skipped build. | `TestHarnessIndustryBudgetExhaustion_RecordsBudgetAndSkip`, `TestHarnessGameSyncBuildRevalidation_ReportsSkippedBuild` |
| `building_modifier_point_preview` | Building point modifiers remain visible in sync output. | `TestHarnessPointPreviewRemainsEffectiveOutputAfterGameSync` |
| `settler_found_city` | Unit order settles a city and removes the settler. | `TestHarnessSettlerFoundCity_RecordsTurnArtifacts` |
| `recipe_blocked_by_input` | Recipe input blocking is visible in events and state summary. | `TestHarnessRecipeBlockedByInput_RecordsGameSyncAndState` |
| `disabled_recipe_skipped` | Disabled production buildings report recipe skip events. | `TestHarnessDisabledRecipeReportsRecipeSkipped` |
| `outer_facility_capture` | Contested out-of-city facility becomes disabled during takeover. | `TestHarnessOuterFacilityCapture_DeactivatesContestedFacility` |
| `capital_destroy_gameover` | Capital core destruction emits game-over output and summary winner. | `TestHarnessCapitalDestroyGameOver_StopsAtGameOver` |

Current real-content harness scenarios:

| Scenario | Primary coverage | Harness test coverage |
| --- | --- | --- |
| `real_content_mvp_happy_path` | Real generated data can run research, build, settle, train, move, attack, and end the game without Unity. | `TestHarnessRealContentHappyPath_CompletesFullMVPGame` |
| `real_content_facility_takeover` | Real generated data can transfer a facility and reactivate it after takeover. | `TestHarnessRealContentFacilityTakeover_TransfersOwnershipAndReactivates` |

`TestHarnessRoundTripSmoke` is a manual debug integration smoke gated by
`PANOPTES_RUN_DEBUG_INTEGRATION=1`. It should remain optional; regular
acceptance must stay in normal `go test` paths.

## Scenario Naming

Existing M1 synthetic scenario names are historical and remain valid. New M2-M6
scenarios must use this slug shape:

```text
m<stage>_<domain>_<rule>_<expected_outcome>
```

Rules:

- Use lowercase ASCII snake_case only.
- Keep the slug stable once assertions depend on it.
- Use the same slug for `Definition.Name`, in-memory catalog manifest
  `ContentVersion`, `BundleHash`, `DefaultMapID`, map bundle `ID`, and
  `GameState.GameID`.
- Keep synthetic scenarios in `server/internal/game/scenario`; keep harness
  assertions in `server/internal/debug`.
- Prefix real generated-data fixtures with `real_content_`; they may stay
  test-local when they need large maps or generated catalog details.

Recommended future examples:

| Milestone | Example slug | Acceptance focus |
| --- | --- | --- |
| M2 | `m2_road_connectivity_building_requires_network` | Road/network connectivity gates city or facility behavior. |
| M3 | `m3_local_storage_recipe_consumes_reachable_inputs` | Recipes draw from local or reachable stock, not global stock. |
| M4 | `m4_priority_logistics_high_priority_receives_capacity_first` | Priority allocation decides constrained flow before low-priority demand. |
| M5 | `m5_policy_modifier_next_turn_activation_visible` | Policy/institution modifiers are auditable and activate on the documented turn. |
| M6 | `m6_network_war_supply_cut_reduces_combat_effect` | Combat resolution observes road/supply network disruption. |

## Acceptance Contract

Each backend-only acceptance scenario should provide:

- Deterministic setup: explicit static catalog, map, players, resources, units,
  buildings, research/policy state, and any initial planning orders.
- Command script: planning commands injected through `debug.Harness`, not direct
  mutation, except when the scenario deliberately seeds pre-existing state.
- Turn synchronization: wait for the expected `MsgPlanningStart` before issuing
  commands, then wait for the expected `MsgGameSync`.
- Command result recording when validating command boundaries, rejects, previews,
  or DEV_MODE HTTP behavior.
- Event assertions: check event channel and kind for each authoritative rule
  effect.
- State summary assertions: check `debug.StateSummary` for durable outcomes such
  as ownership, city binding, building lifecycle, unit position, resources,
  winner, and game-over reason.
- Sync/projection assertions when a rule must be visible to the player, including
  memory/current visibility semantics.

## Known Gaps

- There is no central runtime registry for all harness scenarios. M1 keeps the
  inventory in docs plus tests to avoid adding a new abstraction solely for
  bookkeeping.
- Command result recording is strongest on DEV_MODE HTTP paths; add direct
  harness assertions when a future scenario needs typed command result payloads.
- State summaries are structured but not golden-file based. Prefer explicit
  field assertions until scenario output becomes too large to read.
- The harness validates backend behavior only. Client rendering and interaction
  remain out of scope for M1.4.
