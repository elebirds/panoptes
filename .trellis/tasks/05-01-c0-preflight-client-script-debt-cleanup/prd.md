# C0 Preflight Client Script Debt Cleanup

## Goal

Before C0a backend-client connection work, reduce client script debt that would make real backend state binding fragile. The task should prepare the Unity client script architecture for manually-authored prefab UI, without trying to generate final UI or redesign scenes wholesale.

## What I already know

- User wants C0a, but agrees full C0 depends on hand-authored Unity prefab UI.
- User wants client script refactoring and debt cleanup before C0a.
- Project rule: client is a pure presentation layer; UI scripts must not directly call `NetworkManager`.
- Project rule: Presentation references Core only, not `Panoptes.Protocol`.
- `docs/PANOPTES_AGENT_FRONTEND.md` defines Core/Application/Infrastructure and Presentation package boundaries.
- `docs/CLIENT_CODE_QUALITY_AUDIT.md` already identifies the main debt cluster and says public MonoBehaviour entry points / serialized fields should be preserved unless prefabs/scenes are updated in the same commit.
- Current largest pre-C0 risks are still Presentation files:
  - `MapPlanningInputController.cs` around 3376 lines.
  - `BuildCommandPanel.cs` around 1633 lines.
  - `RecipeSynthesisPanel.cs` around 1392 lines.
  - `CityCoreBuildingActionRegistrar.cs` around 1063 lines.
- Current boundaries look healthy at a high level:
  - `Panoptes.Presentation` asmdef references `Panoptes.Core`, not `Panoptes.Protocol`.
  - UI already routes network work through services/intents.

## Assumptions (temporary)

- This task should be C0-preflight/C0p, not C0a itself.
- Do not generate new production UI prefabs.
- Refactor should be incremental and compatibility-preserving, because existing scenes/prefabs may rely on serialized field names and MonoBehaviour components.
- The best first batch should reduce risk for C0a data binding rather than chase perfect file sizes.

## Open Questions

- Resolved: start with prefab-ready binding/lifecycle, then complete a narrow pass across UI panel, planning input, map rendering, unit visual, action registrar, and cache boundaries.

## Requirements (evolving)

- Preserve client pure-presentation constraints.
- Preserve asmdef dependency direction.
- Preserve public MonoBehaviour entry points and serialized field compatibility unless updated and verified in the same change.
- Prefer reusable Core DTO/cache/view-model boundaries over Presentation scripts reaching into protocol or network.
- Prefer hand-authored prefab compatibility over generated UI.
- Add or update EditMode tests for extracted non-trivial logic.

## Refactoring Plan

### C0p0: Guardrails and Baseline

Purpose: make every later refactor measurable and reversible.

- Add or refresh static boundary tests:
  - no `Panoptes.Protocol` usage under `Runtime/Presentation`;
  - no `NetworkManager.Instance` usage under `Runtime/Presentation/UI`;
  - no production code edits under generated `Protocol/`.
- Capture current high-risk file line counts.
- Keep existing MonoBehaviour names, serialized fields, and prefab-facing entry points stable.
- Output: a small safety gate that can run after each refactor batch.

### C0p1: Prefab-Ready Binding and Lifecycle Pass

Purpose: prepare scripts for manually-authored prefabs before C0a.

- Replace touched fallback scene lookup with `SceneObjectFinder` or serialized references.
- Standardize event subscription/unsubscription through `EventSubscriptionBag` where files are touched.
- Introduce small binder/presenter helpers only where they reduce prefab coupling.
- Avoid generating production prefabs.
- Priority targets:
  - `ResourceHUD`;
  - `TurnHUD`;
  - `GameSceneController`;
  - `RecipeSynthesisPanel` subscription/lifecycle entry points;
  - common UI overlay usage (`ErrorToast`, `ConfirmDialog`, `LoadingOverlay`) only if needed.
- Output: UI scripts become safer to mount on hand-built prefabs.

### C0p2: UI Panel Presenter Split

Purpose: separate “render model building” from Unity view mutation.

- Split `BuildCommandPanel` remaining responsibilities:
  - catalog/layout read model construction;
  - item/group list rendering;
  - tooltip/render state coordination;
  - command dispatch adapter.
- Split `RecipeSynthesisPanel` responsibilities:
  - recipe row render model;
  - current building/city selection view model;
  - command submit adapter;
  - preview/error display.
- Keep panel MonoBehaviours as facade components so existing prefab bindings remain valid.
- Output: C0a can feed real backend DTO/cache data into panels without adding more logic to the MonoBehaviours.

### C0p3: Planning Input Decomposition

Purpose: make player intent submission compatible with C0a and later A0 minister flows.

- Move remaining build/move/deploy/combat mode logic out of `MapPlanningInputController` into `Presentation/Planning/Input/Modes`.
- Keep `MapPlanningInputController` as scene coordinator:
  - selection surface;
  - current mode;
  - overlay coordination;
  - intent sender port.
- Ensure map pointer/raycast code stays in `Presentation/Map/InputAdapter`.
- Output: player input modes become individually testable and can later share intent surfaces with minister/AI preview flows.

### C0p4: Map Rendering Split

Purpose: separate map state source, node/building/unit rendering, and overlays.

- Split `MapRenderer` into:
  - map source resolver;
  - node renderer;
  - building renderer;
  - unit renderer;
  - road/overlay coordination;
  - camera context publishing.
- Keep `MapRenderer` as the prefab-facing facade.
- Output: C0a game sync can update map visuals through narrow renderer APIs.

### C0p5: Unit Visual and HUD Action Cleanup

Purpose: reduce visual-state and action-button coupling after core C0a surfaces are ready.

- Split `SquadUnitVisualController` into visual state, formation layout, icon/health/status presenters, and animation adapters.
- Slim `CityCoreBuildingActionRegistrar` and unit action registrars into reusable action provider/binder pieces.
- Output: action buttons become hand-prefab-friendly and less tied to one panel.

### C0p6: GameStateCache Boundary Review

Purpose: only split Core cache after Presentation risk is reduced.

- Review `GameStateCache` for C0a needs:
  - runtime state mirror;
  - information report snapshot;
  - cities/storage/logistics/institutions;
  - units/nodes/buildings;
  - planning draft interaction.
- Extract read-only query/view-model helpers if repeated Presentation lookups remain.
- Do not put gameplay validation in cache or UI.
- Output: Core exposes stable read APIs for C0a without becoming a second rules engine.

### C0a Entry Gate

C0a can start when:

- prefab-facing facades are stable;
- boundary tests pass;
- UI scripts have a clear View/Presenter/Binder pattern for new hand-authored prefabs;
- map and core panels can consume `GameStateCache`/`StaticCatalogCache` without direct protocol/network access;
- known remaining giant files have a documented owner and next split, even if not fully eliminated.

### Final Refactor Completion Gate

The client script refactor is considered complete when:

- no single Presentation MonoBehaviour remains above an agreed size threshold unless documented as a facade;
- major panels keep business-free Unity views and testable render-model/presenter helpers;
- map rendering, map input, planning input, and UI panels live in separate modules with clear dependency direction;
- all C0a/C0b data surfaces can be bound to hand-authored prefabs through stable serialized fields or explicit binder scripts;
- Unity EditMode/static boundary tests cover layer constraints and key presenter logic.

## Acceptance Criteria (evolving)

- [x] A scoped C0-preflight implementation plan exists.
- [x] Refactor scope is small enough to land safely before C0a.
- [x] C0p0 static boundary tests cover direct `Panoptes.Protocol` references under `Runtime/Presentation`.
- [x] C0p0 static boundary tests cover direct `NetworkManager.Instance` usage under `Runtime/Presentation/UI`.
- [x] C0p0 line-count baseline test covers the high-risk files named in this PRD.
- [x] C0p1 started with a compatibility-preserving lifecycle/binding cleanup in `GameSceneController`.
- [x] Generated protocol production files were left untouched; verified by dirty-path review rather than a Unity test because task ownership is scoped to client EditMode tests and Presentation code.
- [x] C0p1 HUD lifecycle pass improves `ResourceHUD` and `TurnHUD` event/button binding through `EventSubscriptionBag`.
- [x] C0p2 splits Build panel list rendering into `BuildCommandListRenderer`.
- [x] C0p2 splits Recipe panel rendered-item bookkeeping into `RecipeSynthesisRenderedItemRegistry`.
- [x] C0p3 extracts move/deploy planning helpers into `Presentation/Planning/Input/Modes`.
- [x] C0p4 extracts map source, camera context, and render token helpers while keeping `MapRenderer` as facade.
- [x] C0p5 extracts squad render-budget logic and city-core action resolution helpers.
- [x] C0p6 extracts read-only `GameStateCache` query helpers without adding client-side rules.
- [x] Relevant static boundary checks pass locally.
- [x] Unity batchmode script import/compile reaches successful shutdown without `error CS`; TestRunner XML was not produced in this shell run and remains a licensed/interactive Unity follow-up.

## Definition of Done (team quality bar)

- Tests added/updated where appropriate.
- Lint / typecheck / Unity EditMode-equivalent gate green where locally available.
- Docs/notes updated if architecture boundary changes.
- Rollout/rollback considered if risky.

## Out of Scope (explicit)

- No final UI prefab generation.
- No full C0a server connection feature.
- No gameplay logic on client.
- No protocol message field/name changes.
- No broad rewrite of every large Presentation file in one batch.

## Technical Notes

- Inspected `docs/PANOPTES_AGENT_FRONTEND.md`.
- Inspected `docs/CLIENT_CODE_QUALITY_AUDIT.md`.
- Inspected client script tree under `client/Assets/Scripts`.
- Inspected asmdefs:
  - `client/Assets/Scripts/Runtime/Core/Panoptes.Core.asmdef`
  - `client/Assets/Scripts/Runtime/Presentation/Panoptes.Presentation.asmdef`
  - `client/Assets/Scripts/Tests/EditMode/Panoptes.Tests.EditMode.asmdef`
- Verification:
  - `git diff --check`
  - `rg "Panoptes\\.Protocol" client/Assets/Scripts/Runtime/Presentation`
  - `rg "NetworkManager\\.Instance" client/Assets/Scripts/Runtime/Presentation/UI`
  - `git status --porcelain -- server/internal/gen/proto client/Assets/Scripts/Protocol`
  - `/Applications/Unity/Hub/Editor/6000.4.1f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /Users/hhm/code/panoptes/client -runTests -testPlatform EditMode -testResults /tmp/panoptes-c0p-editmode-results.xml -logFile /tmp/panoptes-c0p-unity.log -quit`
