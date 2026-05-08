# brainstorm: non-stacking military units

## Goal

Evaluate whether Panoptes should disallow multiple military units occupying the same node, and outline the implementation path if the rule changes from stackable to non-stackable units.

## What I Already Know

- The current combat spec supports group node conflict and treats same-node contention as a first-class resolution case.
- Current production/spawn code already avoids occupied spawn tiles through `ResolveUnitSpawnPosition`.
- Current movement resolution allows multiple allied units to end on the same candidate node because `NodeConflictDetector` only creates conflict groups for hostile pairs.
- The client must remain presentation-only; occupancy legality and conflict outcomes belong on the server.

## Assumptions

- "No stacking" means at most one living unit per map node after formal resolution.
- This task is allowed to change proto, generated code, and in-memory structures if that is the cleanest way to remove old assumptions.
- Civilian units are included in the same occupancy rule; the implementation should not leave a "military-only" exception.
- Existing protocol did not need to change because each unit is already projected independently and node counts remain valid as 0/1 occupancy counts.

## Open Questions

- Should "no stacking" apply to all units, or only military units?
- When multiple friendly units try to enter the same legal node in the same turn, should losers stop before the node, stay at origin, or use deterministic priority?

## Requirements

- Maintain deterministic WEGO resolution.
- Enforce the occupancy rule on server-side movement resolution, spawning, bootstrap, scenario placement, production/research unit grants, and any future unit injection path.
- Preserve existing enemy conflict behavior: hostile node contention should still produce combat conflict, not silently become a friendly-style traffic jam.
- Add tests for same-faction movement collision, simultaneous destination collision, blocked destination, and production spawn fallback.
- Remove hidden "stacking is okay" assumptions from the model layer, query helpers, and any protocol-facing unit views.
- Add Chinese comments where the new rule is easy to misread during future maintenance.

## Acceptance Criteria

- [ ] No normal turn resolution leaves two living units on the same node.
- [ ] Friendly same-destination moves resolve deterministically.
- [ ] Enemy same-destination moves still use node conflict damage/fallback behavior.
- [ ] Spawn/production/research grants do not create occupied unit tiles.
- [ ] Client display remains driven by server state and does not perform legality checks.
- [ ] The codebase no longer depends on stack-friendly helpers for new behavior.
- [ ] The rule is documented in code where the final occupancy decision is made.

## Out of Scope

- Client-side path prediction.
- Full redesign of combat into multi-timestep movement unless needed to keep the no-stack rule consistent.

## Proposed Plan

### Phase 1: Model the rule explicitly

- Introduce a single authoritative occupancy helper in the domain layer.
- Encode "one living unit per node" once.
- Update unit queries/views so they expose occupancy in a way that cannot silently regress to stacking.

### Phase 2: Make resolution honor occupancy

- Update combat path planning and conflict handling so friendly collisions are blocked or deterministically resolved without technical debt.
- Keep hostile collisions as combat, not as a special-case occupancy bypass.
- Ensure the movement apply step cannot place two eligible units on the same final node.

### Phase 3: Make all entry points obey the same rule

- Update spawn, production, research grant, bootstrap, and debug/scenario placement paths.
- If a placement cannot satisfy the rule, choose one explicit fallback strategy and use it everywhere.

### Phase 4: Align protocol and generated artifacts

- Regenerate proto-derived code if the data model changes.
- Update any client-facing unit views only as needed to reflect server truth.
- Avoid introducing compatibility shims that preserve the old stacking model inside core code.

### Phase 5: Prove the rule

- Add regression tests for:
  - same-faction collision
  - hostile node conflict
  - spawn fallback when the ring is occupied
  - deterministic final occupancy after a mixed movement turn
  - no duplicate unit occupancy anywhere in the world after resolution

## Implementation Notes

- Chosen rule: all living units, including civilians, are non-stacking.
- Friendly-only node collisions produce no combat event or damage; all members use the existing node-conflict fallback movement.
- Unit starting positions are frozen blockers for the whole resolving pass regardless of faction.
- No proto regeneration was required.
- Backend code-spec updated in `.trellis/spec/backend/quality-guidelines.md`.

## Technical Notes

- Relevant code inspected:
  - `server/internal/domain/unit.go`
  - `server/internal/domain/spawn_resolver.go`
  - `server/internal/engine/combat/conflict_phase.go`
  - `server/internal/engine/combat/movement_phase.go`
  - `server/internal/engine/combat/path_phase.go`
  - `server/internal/game/orders/validation.go`
  - `server/internal/event/combat.go`
- Current resolver phases: Snapshot -> PathPlanning -> Conflict -> MovementApply -> Damage -> Cleanup.
- Current movement events mutate `PositionC` in `UnitMovedEvent.Apply`.
- This task started from a stackable movement model where friendly same-node overlap is currently possible; the change is intended to remove that assumption entirely.
