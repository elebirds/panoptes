# client phase7 territory highlight deploy extraction

## Goal

Continue reducing `MapPlanningInputController` toward the final map input
Binder/Adapter by moving territory highlight and pending deploy ghost
presentation responsibilities out of the scene controller.

## What I Already Know

* Top-level input routing, build placement routing, node info proxy creation,
  move preview presentation, attack range projection, and combat target
  predicates have already been extracted.
* `MapPlanningInputController` still owns territory highlight bookkeeping and
  pending deploy ghost node bookkeeping.
* The controller still needs to keep prefab-facing serialized fields and public
  APIs stable for now.

## Assumptions

* Extracted helpers stay under `Presentation/Map`.
* Helpers may use Core DTOs and Unity views, but must not reference generated
  protocol files.
* This slice does not change server-authoritative rules or command payloads.

## Requirements

* Extract territory highlight rendering/bookkeeping from the controller.
* Extract pending deploy ghost rendering/bookkeeping from the controller.
* Preserve existing behavior for restore/clear interactions with attack and
  move preview highlights.
* Add focused EditMode coverage for extracted pure/helper behavior.

## Acceptance Criteria

* [x] `MapPlanningInputController` no longer owns raw territory highlight ID
      bookkeeping directly.
* [x] Pending deploy ghost state/rendering is delegated to a helper.
* [x] Tests cover the extracted helper behavior.
* [x] Static Presentation boundary checks pass.
* [x] dotnet build/test and Unity compile pass.

## Definition Of Done

* Tests added/updated.
* Static boundary checks pass.
* Unity script import/compile succeeds.
* Trellis task context validates.
* Work committed.

## Out Of Scope

* Full deletion/rename of `MapPlanningInputController`.
* Protocol/backend changes.
* Replacing existing map renderer APIs.
