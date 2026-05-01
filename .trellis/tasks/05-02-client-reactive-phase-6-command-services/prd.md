# client reactive phase 6 command service migration

## Goal

Implement Phase 6 of the client reactive presentation architecture: move player
command submission behind injectable Core services so Presentation views and map
input do not call static intent/network paths directly.

## What I Already Know

* Phase 6 is defined in
  `docs/2026-05-02-client-reactive-presentation-architecture-implementation-plan.md`
  as Command Service Migration.
* Target flow is `Button / click -> ViewModel or UI facade command -> Service
  -> MessageSender -> Server`.
* Existing command construction lives in static `GameIntents`, which builds
  protocol messages and sends through static `MessageSender`.
* Several Presentation scripts still call `GameIntents` directly:
  `TurnHUD`, `GameChatPanelController`, `MinisterPanel`,
  `TechTreePanelController`, `RecipeSynthesisPanel`,
  `MapPlanningInputController`, and `GameSceneController`.
* `NetworkMessageSender` already implements `IClientMessageSender` and is
  registered in the Project scope.

## Assumptions

* This phase should keep legacy debug tooling on `GameIntents` for now; the
  migrated game UI path should use injectable services.
* Command services may construct protocol messages because they live in Core.
* Presentation may still read legacy caches where those panels have not yet
  migrated to Store/ViewModel, but command submission must route through
  services.

## Requirements

* Add Core command services:
  * `GameIntentService`
  * `PlanningIntentService`
  * `MinisterCommandService`
* Services must depend on `IClientMessageSender`, not `NetworkManager.Instance`
  or static `MessageSender`.
* Presentation command callers must use injected services instead of static
  `GameIntents`.
* Map planning input must send planning/build/preview commands through
  `PlanningIntentService`.
* Composition must register services and inject the relevant scene components
  through the final Game scope.

## Acceptance Criteria

* [x] UI/Presentation command callers no longer call `GameIntents`.
* [x] Command services build the same protocol messages as the legacy intent
  methods for migrated commands.
* [x] ViewModels still do not reference Protocol.
* [x] Services do not reference Unity UI controls.
* [x] No generated protocol files are modified.
* [x] Boundary and service tests cover the migration.

## Definition of Done

* EditMode tests added or updated for command services and boundaries.
* Unity compile/import check passes with no script compilation errors.
* `dotnet build client/Panoptes.Tests.EditMode.csproj` passes.
* `git diff --check` passes.
* Trellis task context validates.

## Out of Scope

* Full ViewModel migration for all command panels.
* Removing legacy `GameIntents` from Core debug tooling.
* Rewriting map selection legality checks or map UI state.

## Technical Notes

* Relevant specs:
  * `.trellis/spec/frontend/directory-structure.md`
  * `.trellis/spec/frontend/component-guidelines.md`
  * `.trellis/spec/frontend/state-management.md`
  * `.trellis/spec/frontend/quality-guidelines.md`
* Existing message send abstraction: `IClientMessageSender`.
* Existing final composition entry: `ClientCompositionInstaller.RegisterGame`.
