# Research: domain-engine-boundaries

- Query: Comprehensive backend structure optimization opportunities for domain, event, ecs, engine, building, staticdata, and datagen boundaries; identify state model bloat, event ownership risks, static data coupling, large files worth splitting, and behavior-preserving refactor batches.
- Scope: internal
- Date: 2026-04-30

## Findings

### Files Found

- `AGENTS.md` - project-wide architecture constraints: generated code is off-limits, engine systems read state and emit events, settlement writes flow through `Event.Apply`, and static gameplay data comes from root `data/`.
- `.trellis/spec/backend/directory-structure.md` - active backend module ownership guide; current target shape keeps `domain` state-local, `event` as mutation events, `ecs` as factories/components/queries, `engine` as rule systems, and `building` as building rules/lifecycle orchestration.
- `server/internal/domain/state.go` - central state model, turn runtime, research/institution/player structs, modifier resolution, unlock checks, building HP refresh, and constructor all in one 587-line file.
- `server/internal/domain/state_model.go` - compatibility structured facade (`Meta`, `Clock`, `Outcome`, `WorldState`, `PlayerStore`, `Runtime`) backed by the legacy flat fields.
- `server/internal/domain/components.go` - ECS component definitions currently live in `domain`, then are re-exported by `ecs`.
- `server/internal/domain/types.go` - terrain/unit/building/resource/point value types plus bag math in one 324-line file.
- `server/internal/domain/lifecycle.go` - building lifecycle status normalization, city online checks, city lookup, and ECS lifecycle state writes.
- `server/internal/building/rules.go` - placement, city context, city founding footprint, control checks, and territory constraints.
- `server/internal/building/runtime.go` - read-side building runtime status adapter.
- `server/internal/building/capture.go` - capture aftermath rule for whether captured buildings become ruined or delayed.
- `server/internal/building/orchestration/lifecycle.go` - resolving-stage building lifecycle system for city capture and facility takeover.
- `server/internal/ecs/components.go` - type aliases and component aliases back to `domain`.
- `server/internal/ecs/factory.go` - node/unit/building factories, staticdata lookup, fallback definitions, component attachment, and scope/takeover wiring.
- `server/internal/ecs/query.go` - ECS queries plus building wrappers and a legacy placement check.
- `server/internal/event/interface.go` - `Event` interface and the single write-entry contract.
- `server/internal/event/production.go` - production/build/upkeep events and several ECS helper mutations in one 327-line file.
- `server/internal/event/lifecycle.go` - facility takeover, building ruin, city capture, city ownership transfer, city footprint mutation, and nearest-city helper.
- `server/internal/event/combat.go` - unit/building/city-core combat mutation events and shared find helpers.
- `server/internal/event/recipe.go` - recipe and building status mutation events.
- `server/internal/engine/economy/orchestrator.go` - economy stage pipeline and immediate event application between stages.
- `server/internal/engine/economy/build.go` - build-order settlement and simulated per-turn budgets.
- `server/internal/engine/economy/recipe.go` - recipe selection/progress, affordability ratios, partial consumption math, and status event generation in one 432-line file.
- `server/internal/engine/unit_resolution_runner.go` - root engine wrapper that applies combat/upkeep events directly when used outside `game/resolution`.
- `server/internal/game/resolution/runner.go` - Turn V2 resolving pipeline: planning commit, freeze, unit, map, building, economy.
- `server/internal/game/resolution/collector.go` - channel collector that both records and applies events.
- `server/internal/game/settlement.go` - room-level bridge that still owns unit order freezing, route preview hooks, broadcasts, game-over checks, and scratch cleanup.
- `server/internal/staticdata/model.go` - static runtime model, map authoring/runtime structs, UI structs, and bundle structs in one 549-line file.
- `server/internal/staticdata/catalog.go` - default catalog singleton, loading, indexing, manifest/hash helpers, and read APIs in one 385-line file.
- `server/internal/staticdata/sections.go` - catalog section constants, section serialization, and section hashes.
- `server/internal/datagen/generator.go` - load/compile/emit, map compilation, UI merge, layout generation, and proto/code render entry points in one 996-line file.
- `server/internal/datagen/schema.go` - JSON schema construction in one 973-line file.
- `server/internal/datagen/validate.go` - authored source loading and schema validation in one 658-line file.
- `Makefile` - `data-gen` runs `server/cmd/datagen`; `gen` runs data generation before proto/db generation.
- `server/go.mod` - relevant versions: Go 1.26, donburi v1.15.7, protobuf v1.36.11, gojsonschema v1.2.0.

### Code Patterns

- `GameState` is currently a transitional dual model: structured fields (`Meta`, `Clock`, `Outcome`, `WorldState`, `PlayerStore`, `Runtime`) coexist with flat fields (`GameID`, `Turn`, `Phase`, `World`, `Players`, `TurnRuntime`) in `server/internal/domain/state.go:16`, and `RefreshStructuredModel` copies/pointers them in `server/internal/domain/state_model.go:38`.
- `RuntimeState` is a pointer facade over `TurnRuntime` (`server/internal/domain/state_model.go:33`), which preserves behavior but adds state-model surface area that future code can accidentally treat as independently authoritative.
- `PlanningInputs` mixes player commands, minister drafts, policies, research, institutions, war directives, and unit orders in one runtime struct (`server/internal/domain/state.go:43`). `ClearPostResolutionScratch` must know every scratch field by name (`server/internal/domain/turn_runtime.go:6`), so new planning state is easy to forget during cleanup.
- `domain` imports `staticdata` directly (`server/internal/domain/state.go:12`) and uses `staticdata.Default()` for modifiers, unlock checks, rules, and building HP (`server/internal/domain/state.go:234`, `server/internal/domain/state.go:350`, `server/internal/domain/state.go:389`, `server/internal/domain/state.go:447`, `server/internal/domain/state.go:521`). This is the largest static-data coupling: pure state methods now depend on global catalog presence.
- `domain` also owns ECS component definitions (`server/internal/domain/components.go:11`) while `ecs` re-exports aliases (`server/internal/ecs/components.go:11`). That avoids import cycles, but makes `domain` depend on donburi and keeps ECS-specific storage concerns inside the state-model package.
- `building` is closer to the desired ownership surface, but rules still read `staticdata.Default()` for city radius, terrain, and minimum city distance (`server/internal/building/rules.go:42`, `server/internal/building/rules.go:76`, `server/internal/building/rules.go:99`, `server/internal/building/rules.go:180`).
- Placement logic exists in two forms: canonical `building.ValidatePlacement` uses city context, safe zones, control, resource node contesting, and city radius (`server/internal/building/rules.go:51`), while `ecs.CanPlaceBuildingAt` repeats a narrower terrain/owner/resource check (`server/internal/ecs/query.go:100`). This is a concrete drift risk.
- `ecs.CreateBuilding` does more than factory work: it reads staticdata, falls back to city-core definitions, attaches operation state, mutates node owner, attaches binding/takeover components, and reads rules (`server/internal/ecs/factory.go:122`, `server/internal/ecs/factory.go:170`). This makes ECS factory a hidden lifecycle/binding owner.
- Events are still the official write entry (`server/internal/event/interface.go:14`), and the Turn V2 collector records plus applies events immediately (`server/internal/game/resolution/collector.go:33`). This matches spec, but the event package has grown into a cross-aggregate mutation layer.
- `event.CityCapturedEvent.Apply` transfers city ownership, rebinds core, rewrites footprint node owners, iterates all in-city buildings, reads staticdata, and applies capture lifecycle decisions (`server/internal/event/lifecycle.go:138`). This is the highest event ownership risk because a single event owns city, node, building binding, lifecycle, and catalog semantics.
- Events call other events' `Apply` directly in a few places (`server/internal/event/lifecycle.go:148`, `server/internal/event/production.go:272`). This preserves current behavior but bypasses collector recording of nested events, so reports can miss semantically distinct sub-events.
- Economy runner intentionally applies after each stage (`server/internal/engine/economy/orchestrator.go:59`, `server/internal/engine/economy/orchestrator.go:77`) and `game/resolution` mirrors that via collector (`server/internal/game/resolution/runner.go:139`). Any refactor must preserve this inter-stage visibility.
- Root `engine.UnitResolutionRunner.Run` applies events directly (`server/internal/engine/unit_resolution_runner.go:30`), while `game/resolution` uses `ResolveCombat`/`ResolveUpkeep` plus collector apply (`server/internal/game/resolution/runner.go:93`). This duplicate application path is a small ownership hazard; behavior is currently safe because Turn V2 avoids `Run`.
- Building lifecycle is split across `domain.SetBuildingLifecycleState` (`server/internal/domain/lifecycle.go:42`), `building.RuntimeState` (`server/internal/building/runtime.go:10`), building orchestration event production (`server/internal/building/orchestration/lifecycle.go:19`), and lifecycle event application (`server/internal/event/lifecycle.go:24`). The split is workable but should be made explicit in file/package names.
- Staticdata runtime model mixes server gameplay structs, map authoring/runtime structs, UI catalog structs, and bundle structs in `server/internal/staticdata/model.go:189`, `server/internal/staticdata/model.go:263`, `server/internal/staticdata/model.go:349`, and `server/internal/staticdata/model.go:440`.
- Staticdata catalog mixes global default state, filesystem loading, hash generation, indexes, sorted list APIs, and section payload access (`server/internal/staticdata/catalog.go:33`, `server/internal/staticdata/catalog.go:50`, `server/internal/staticdata/catalog.go:79`, `server/internal/staticdata/catalog.go:147`, `server/internal/staticdata/catalog.go:190`). This is a split candidate but not a behavioral bug.
- Datagen is the clearest file-size issue: generation flow and output rendering are in `server/internal/datagen/generator.go:31`, authored schema generation in `server/internal/datagen/schema.go:53`, and validation source loading in `server/internal/datagen/validate.go:77`. The package boundary is fine; file boundaries are not.
- The data generation path is broad by design: `make data-gen` calls datagen (`Makefile:19`), and `make gen` runs data generation before proto generation (`Makefile:25`). Any split must preserve generated output paths in `server/internal/datagen/generator.go:111`.

### State Model Bloat

- Highest-value bloat reduction is to separate `domain/state.go` by ownership without changing exported names:
  - `state_core.go`: `GameState`, `TurnRuntime`, `PlanningInputs`, `ResolvingState`, `PlayerState`, `MapData`, `BuildOrder`, `RecipeSelectionOrder`, `MoveOrder`, and constructor.
  - `modifiers.go`: `ActiveModifierEffects`, `ApplyFloatModifier`, resource/point/scalar modifier helpers, research/industry output helpers.
  - `unlocks.go`: technology/building/recipe/policy active checks and `technologyExplicitlyUnlocks`.
  - `building_hp.go`: `RefreshBuildingMaxHPForPlayer` and `RefreshBuildingMaxHPAtEntry`.
- The structured compatibility fields should either stay as generated/read-side mirrors or become the only public model in a later task. Mixing both should not expand further; new code should prefer one authoritative access path.
- `PlanningInputs` needs grouped scratch substructs before adding more fields. A behavior-preserving batch can introduce embedded groups or helper reset methods first, then switch `ClearPostResolutionScratch` to delegate to those helpers.

### Event Ownership Risks

- Keep event types as mutation commands, but extract reusable mutation helpers into packages with narrower ownership:
  - city ownership transfer helpers near `domain`/`building` state-local code.
  - footprint ownership mutation helpers near `building`.
  - nearest-city lookup outside `event` if producers need it.
- Avoid adding new nested `OtherEvent.Apply` calls. If a producer wants multiple reportable facts, emit multiple events from the engine/orchestrator; if it is only an implementation helper, extract a private mutation helper rather than invoking another event.
- `CityCapturedEvent.Apply` should be the first event split candidate because it crosses city store, node ownership, building binding, lifecycle state, and staticdata lookup in one method.
- `production.go` should split by event family (`build_events.go`, `resource_events.go`, `road_events.go`, `unit_production_events.go`, `point_events.go`, `upkeep_events.go`) while keeping event names and `Kind()` strings unchanged.

### Static Data Coupling

- `staticdata.Default()` is pervasive and currently acts as a global service locator. It is acceptable for MVP, but domain-level calls are the riskiest because they make `domain.NewGameState` and simple state helpers fail or produce zeros when no catalog is set.
- Behavior-preserving decoupling can start with optional catalog parameters at the engine/service boundary, not a full dependency-injection rewrite:
  - Add local helper functions in `domain` that guard nil catalog and centralize fallback rules before changing call sites.
  - Move modifier resolution to a new low-level package only after all callers use helper APIs instead of hand-reading `staticdata.Default()`.
  - Keep `staticdata.Default()` as the compatibility path until tests are migrated.
- Staticdata structs should be split by subject (`economy_model.go`, `building_model.go`, `unit_model.go`, `map_model.go`, `ui_model.go`, `bundle_model.go`) before attempting semantic changes.
- Catalog APIs can be split by concern (`default.go`, `load.go`, `index.go`, `hash.go`, `queries.go`) without changing package imports.

### Large Files Worth Splitting

- `server/internal/datagen/generator.go` (996 lines): split into `generate.go`, `emit.go`, `map_compile.go`, `ui_merge.go`, `layout.go`, `proto_render.go`, `resource_keys_render.go`.
- `server/internal/datagen/schema.go` (973 lines): split by schema family (`schema_core.go`, `schema_registry.go`, `schema_content.go`, `schema_ui.go`, `schema_maps.go`, `schema_helpers.go`).
- `server/internal/datagen/validate.go` (658 lines): split authored document loading, validation target assembly, schema execution, and cross-reference checks.
- `server/internal/domain/state.go` (587 lines): split as described above; this is also a boundary cleanup, not just size cleanup.
- `server/internal/staticdata/model.go` (549 lines): split by data subject; lowest risk because it mostly contains structs.
- `server/internal/engine/maploader/procedural.go` (547 lines): large but outside the requested core boundary except static map runtime coupling.
- `server/internal/engine/economy/recipe.go` (432 lines): split into selection, progress loop, affordability math, bag math, and status-event helpers.
- `server/internal/staticdata/catalog.go` (385 lines): split default/load/index/query/hash concerns.
- `server/internal/event/production.go` (327 lines), `server/internal/event/lifecycle.go` (264 lines), `server/internal/event/combat.go` (221 lines), `server/internal/event/research.go` (207 lines), and `server/internal/event/recipe.go` (189 lines): split by event family and mutation helper ownership.
- `server/internal/building/rules.go` (249 lines): split placement, city context, territory footprint, and controller helpers.
- `server/internal/ecs/factory.go` (223 lines): split node, unit, building factories and consider moving building-specific attachment helpers into `building`.

### Concrete Refactor Batches That Preserve Behavior

1. File-only splits with no package moves:
   - Split `staticdata/model.go`, `staticdata/catalog.go`, `datagen/generator.go`, `datagen/schema.go`, `datagen/validate.go`, `domain/types.go`, and `domain/state.go`.
   - Preserve package names, exported identifiers, JSON tags, generated output paths, event kind strings, and tests.

2. Canonicalize building placement:
   - Replace `ecs.CanPlaceBuildingAt` with either a wrapper around `building.ValidatePlacement` where city context is available or mark it as a narrow static node check with a different name.
   - Keep current `building.ValidatePlacement` as canonical because it already includes city online, safe-zone, resource node contesting, and city radius semantics.

3. Move building ECS attachment ownership out of `ecs`:
   - Introduce `building.AttachComponents(entry, cfg, cityID)` or similar, called by `ecs.CreateBuilding`.
   - Keep `ecs.CreateBuilding` exported and behavior-compatible, but make binding/takeover lifecycle attachment visibly owned by `building`.

4. Extract event mutation helpers:
   - Start with private helpers inside `event/lifecycle.go` for city transfer, footprint transfer, and in-city building capture.
   - Then move helpers to `building` only if no import cycle is created.
   - Preserve `CityCapturedEvent.Apply` externally, but make it orchestrate small helpers rather than owning all mutation detail.

5. Remove nested event apply paths:
   - Replace `CityCapturedEvent.Apply -> CityCoreDestroyedEvent.Apply` and `UnitStarvingEvent.Apply -> UnitDiedEvent.Apply` with shared private mutation helpers first.
   - If report fidelity matters, later change producers to emit both events explicitly through the collector.

6. Narrow domain/staticdata coupling:
   - Centralize catalog reads behind helper functions inside `domain` first.
   - Move modifier resolution out of `state.go` into `domain/modifiers.go`.
   - In a later batch, introduce a `CatalogView` interface consumed by engine/building validation while keeping `staticdata.Default()` as the default adapter.

7. Clarify event application ownership:
   - Deprecate or remove direct-use `UnitResolutionRunner.Run` after confirming only Turn V2 paths use `ResolveCombat`/`ResolveUpkeep`.
   - Keep `game/resolution.Collector.ApplyNow` as the single Turn V2 application/record path.

8. Group turn runtime scratch:
   - Add helper reset methods on grouped planning substructures before changing storage shape.
   - Maintain current JSON/proto behavior by keeping public field names until all planning/session code is updated.

## External References

- `server/go.mod:3` declares Go 1.26.
- `server/go.mod:16` uses `github.com/yohamta/donburi v1.15.7` for ECS.
- `server/go.mod:18` uses `google.golang.org/protobuf v1.36.11`.
- `server/go.mod:33` uses `github.com/xeipuuv/gojsonschema v1.2.0` for authored data validation.
- No web research was needed; all findings are from repository source and project specs.

## Related Specs

- `AGENTS.md`: generated-code restrictions, server dependency direction, engine-system event discipline, static data source of truth, and system file size guidance.
- `.trellis/workflow.md`: research must be persisted under task `research/`.
- `.trellis/spec/backend/index.md`: backend spec entry point.
- `.trellis/spec/backend/directory-structure.md`: current backend ownership boundaries and examples for keeping `GameRoom` thin.
- `.trellis/spec/guides/index.md`: cross-layer and code-reuse thinking triggers; this task touches cross-layer data flow and repeated helper patterns.

## Caveats / Not Found

- Current session had no active task according to `python3 ./.trellis/scripts/task.py current --source`; the user supplied the exact task research path, so this file was written there.
- This is structural research only. No code was edited and no tests were run.
- No import-cycle experiment was performed. Proposed package moves should be validated with `go test ./...` after each batch.
- Several large files are tests or generated JSON; this report prioritizes non-test Go source and behavior-preserving package/file splits.
- Error-code consistency was not fully audited. Some internal reasons such as `outside_territory`, `terrain_not_buildable`, and `insufficient_points` appear near building/economy code but were not compared against transport-facing error mappings.
