# brainstorm: recipe selection cancel/default empty

## Goal

Change building recipes from an implicit default-on-create behavior to an explicit player choice. Newly built buildings should start with no selected recipe, players must be able to cancel a recipe selection, and both the LLM minister flow and rule bot must explicitly choose a recipe when continuity requires it.

## What I already know

* Current building creation path in `server/internal/ecs/factory.go` auto-attaches `cfg.DefaultRecipeID` as a `BuildingOperationComp`.
* Recipe selection currently uses `MsgSetBuildingRecipe` / `SetBuildingRecipeIntent` / `ValidateRecipeSelection`, and empty recipe IDs are rejected.
* Planning snapshots already carry queued recipe selections, and the client mirrors them in `PlanningDraftCache`.
* Rule bot currently emits recipe-selection intents only when the visible building's current selected recipe differs from its chosen recipe.
* Minister candidate/draft plumbing already supports recipe-selection intents, so explicit recipe choice can reuse the existing planning command flow.
* The user explicitly اجازت changing proto definitions if needed.

## Assumptions (temporary)

* Cancelling a recipe selection should be a first-class command/result, not an overloaded empty-string `SetBuildingRecipe`.
* A building with no selected recipe should appear as idle/not producing until a recipe is manually chosen.
* Buildings that previously had an authored default recipe should still be able to produce automatically, but only because AI flow explicitly selects that recipe.

## Open Questions

* None blocking right now.

## Requirements (evolving)

* New buildings start with no selected recipe.
* Existing recipe selection command remains the positive-select path.
* Add a cancel/unset recipe command for owned buildings.
* Add server validation for canceling recipe selection.
* Update planning snapshot/state so recipe selection can be cleared and reflected in the client.
* Update LLM minister/default intent flow so recipe selection is emitted when needed for production continuity.
* Update rule bot so it emits recipe selection for buildings that need an explicit recipe, but skips buildings where no recipe should be chosen.
* Preserve existing authored default recipe metadata in static content; only runtime auto-selection behavior changes.

## Acceptance Criteria (evolving)

* [ ] A newly created building has no selected recipe unless a player explicitly chooses one.
* [ ] Players can cancel an existing recipe selection and the building returns to the empty recipe state.
* [ ] Canceling recipe selection is rejected for invalid or unauthorized targets.
* [ ] Client planning state shows the cleared selection after cancel.
* [ ] LLM minister / default-intent flow explicitly selects recipes for buildings that need production continuity.
* [ ] Rule bot emits recipe-selection intents when the building currently has no selected recipe but should be producing.
* [ ] Tests cover create, select, cancel, snapshot, and AI-planning behavior.

## Definition of Done

* Tests added/updated.
* Lint / typecheck / CI green.
* Protocol generation updated if proto changes are introduced.
* Client and server stay in sync on command/result names and planning snapshot fields.

## Out of Scope (explicit)

* No redesign of static recipe metadata.
* No new gameplay legality rules beyond explicit selection/cancel flow.
* No client-side recipe legality checks.

## Technical Notes

* Server creation path: `server/internal/ecs/factory.go`
* Server validation / planning handlers: `server/internal/engine/economy/validation.go`, `server/internal/game/planning/*`
* AI default selection flow: `server/internal/game/planning/default_intent.go`
* Rule bot recipe intent generation: `server/internal/game/ai/build_intents.go`, `server/internal/game/session/minister_candidate_pool.go`
* Client send path: `client/Assets/Scripts/Runtime/Core/Application/Services/PlanningIntentService.cs`
* Client planning cache: `client/Assets/Scripts/Runtime/Core/Application/Cache/PlanningDraftCache.cs`
* Client message hydration: `client/Assets/Scripts/Runtime/Core/Application/Stores/StoreMessageHydrator.cs`
* Relevant protocol: `protocol/panoptes/proto/v1/orders.proto`
