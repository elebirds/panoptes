# Fix Archer Adjacent Attack Mismatch

## Goal

Make archer attack targeting consistent between backend legality and client presentation so adjacent enemy unit attacks can be issued without the client accidentally submitting an invalid node attack.

## Requirements

- Confirm backend archer rules from authored/generated static data before changing behavior.
- Preserve the server-authoritative model: the client may mirror catalog-based targeting affordances, but must not invent gameplay legality beyond display/submission routing.
- If archer cannot attack a target type, the frontend must not show or submit that target as attackable.
- If archer can attack an adjacent enemy unit, the frontend must send a unit-target attack even when the click raycast lands on the node instead of the unit model.
- Archer must not submit structure/node attack orders when the catalog says `can_attack_structures` is false.

## Acceptance Criteria

- [x] Backend data inspection confirms archer `attack_range` and structure-attack capability.
- [x] Attack range highlights do not imply structure/node attacks for units that cannot attack structures.
- [x] Clicking an enemy unit's node in attack mode resolves to `AttackUnit` when the selected unit can attack and the target unit is in range.
- [x] Clicking an enemy structure/node with an archer does not send an invalid `AttackNode` order.
- [x] Existing Unity edit-mode tests are updated or new focused tests are added where practical.

## Definition of Done

- Tests added/updated for touched targeting helpers.
- Available frontend/backend test commands are run or limitations are recorded.
- Generated protobuf files remain untouched.

## Out of Scope

- Changing server combat rules or authored static balance.
- Adding client-side pathfinding or combat damage legality.
- Changing protobuf schema.

## Technical Notes

- `data/content/units/units.json` and `data/generated/server/sections/units.json` define archer as `attack_range: 2` and `can_attack_structures: false`.
- `server/internal/game/orders/validation.go` rejects archer structure/node attacks via `unitCanAttackStructures`, but unit-target attacks are accepted for hostile units.
- `client/Assets/Scripts/Runtime/Presentation/Map/MapPlanningInputController.cs` can fall through from attack mode node clicks to `TryIssueStructureTargetOrder`, which submits `AttackNode`.
- `client/Assets/Scripts/Runtime/Presentation/Map/MapAttackRangePresenter.cs` currently highlights all nodes in range and includes the origin.
- Verification note: local `go` is unavailable in this session, and no `client/Panoptes.Tests.EditMode.csproj` exists in this checkout, so targeted Go/Unity edit-mode tests could not be launched here. `git diff --check` passed.
