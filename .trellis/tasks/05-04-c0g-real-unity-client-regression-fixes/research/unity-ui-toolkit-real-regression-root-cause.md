# Research: Unity UI Toolkit real-client regression root cause

- Query: Research Panoptes Unity UI Toolkit real-client regressions: unstyled UI Toolkit, naked top-left text, second-turn Review panel, UI Toolkit blocking uGUI tech tree button, and first-turn map not rendering.
- Scope: mixed
- Date: 2026-05-04

## Findings

### Executive root cause

The current real-client failures are not a single USS typo. They come from a split runtime pattern:

- Authored management prefabs (`ManagementHost`, `BuildCatalog`, `TechTree`, `RecipeSynthesis`, `PolicyFocus`, `NationalLedger`) carry serialized UXML/USS references on their binder component, then rely on the binder to add/configure `UIDocument` at runtime.
- `TurnSummaryUiToolkitBinder` and `MinisterReportUiToolkitBinder` are created with `RegisterComponentOnNewGameObject`, so their serialized `visualTreeAsset` and `styleSheet` fields are always null in real runtime. They fall back to code-generated visual trees with no USS attached.
- `MinisterReportUiToolkitBinder` inherits the management panel base but never calls `BindVisibility`, so it never runs the base visibility gate and remains visible by default when its generated GameObject is resolved.
- `TurnSummaryUiToolkitBinder` is not connected to `ManagementPanelVisibilityStore` at all, even though the management host has a "Turn" nav button that sets `ManagementPanelId.TurnSummary`.
- `UiToolkitRuntimeDocument` tries to resolve `Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme")`, but no such asset exists under `client/Assets/Resources`. When this returns null, the shared runtime `PanelSettings` has no default runtime theme.

This explains the reported symptoms:

- "UI Toolkit no style" and "裸文字": dynamic `TurnSummary` / `MinisterReport` have no serialized USS and use fallback trees; fallback roots have no authored positioning/style sheet, so generated labels/buttons can appear as raw UI Toolkit controls.
- "第二回合弹出 Review 界面": second-turn minister drafts make `MinisterReportViewModel` project rows with action label `Review`; the binder is already visible because it has no visibility binding.
- "挡住 uGUI 科技树按钮": visible UI Toolkit documents are rendered/input-processed above uGUI due high shared panel sort order, and documents without root `PickingMode.Ignore` or without `display: none` can participate in picking even when visually sparse.
- "地图第一回合仍不渲染": current `MapRenderer` now listens to `GameStateStore`, but it still lacks an explicit "render current snapshot now after subscription/injection" path and a rendered-backend-state guard. The one-time startup path can return while waiting for nodes, and correctness depends on receiving a later store emission.

### Files found

- `client/Assets/Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs`: game-scope VContainer registration for UI Toolkit binders and map dependencies.
- `client/Assets/Scripts/Runtime/Presentation/Binders/UiToolkit/ManagementPanelUiToolkitBinderBase.cs`: shared management panel binder, runtime `UIDocument`/`PanelSettings` configuration, base visibility gate.
- `client/Assets/Scripts/Runtime/Presentation/Binders/UiToolkit/ManagementPanelUiToolkitRenderer.cs`: generated management row fallback tree and row labels.
- `client/Assets/Scripts/Runtime/Presentation/Binders/UiToolkit/ManagementHostUiToolkitBinder.cs`: host navigation and visibility application.
- `client/Assets/Scripts/Runtime/Presentation/Binders/UiToolkit/BuildCatalogUiToolkitBinder.cs`: custom build catalog binder with separate visibility handling and partially unclassed generated labels.
- `client/Assets/Scripts/Runtime/Presentation/Binders/UiToolkit/TurnSummaryUiToolkitBinder.cs`: standalone runtime-created binder with no visibility store and no authored prefab registration.
- `client/Assets/Scripts/Runtime/Presentation/Binders/UiToolkit/MinisterReportUiToolkitBinder.cs`: management-panel subclass missing `BindVisibility`.
- `client/Assets/Scripts/Runtime/Presentation/ViewModels/ManagementPanelVisibilityStore.cs`: single-active-panel visibility model.
- `client/Assets/Scripts/Runtime/Presentation/ViewModels/MinisterReportViewModel.cs`: creates `Review` action rows from interactive minister drafts.
- `client/Assets/Scripts/Runtime/Presentation/Map/MapRenderer.cs`: backend map rendering subscription and first render flow.
- `client/Assets/Resources/Prefabs/UI/*.prefab`: authored UI Toolkit prefab assets; current YAML shows only binder components, not serialized `UIDocument` components.
- `client/Assets/UI/Toolkit/Management/*.uxml` / `*.uss`, `client/Assets/UI/Toolkit/Turn/TurnSummary.*`: authored UI Toolkit assets.
- `client/Assets/Scripts/Tests/EditMode/Presentation/C0cUiToolkitAssetSmokeTests.cs`: asset smoke gate, but it invokes `Awake`/`OnEnable`, so runtime-added `UIDocument` can make a prefab look valid even if the authored prefab lacks one.

### Code patterns

- `ClientCompositionInstaller.RegisterGameplayBindings` registers `ManagementPanelVisibilityStore` at `client/Assets/Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs:206`.
- The same method loads authored prefab binders for host/build/tech/recipe/policy/ledger at `ClientCompositionInstaller.cs:208`, `ClientCompositionInstaller.cs:251`, `ClientCompositionInstaller.cs:254`, `ClientCompositionInstaller.cs:257`, `ClientCompositionInstaller.cs:263`, and `ClientCompositionInstaller.cs:266`.
- `TurnSummaryUiToolkitBinder` and `MinisterReportUiToolkitBinder` are exceptions created with `RegisterComponentOnNewGameObject` at `ClientCompositionInstaller.cs:248` and `ClientCompositionInstaller.cs:260`. Their fields at `TurnSummaryUiToolkitBinder.cs:23` and `ManagementPanelUiToolkitBinderBase.cs:14` / `:15` have no prefab-provided values in this path.
- `ClientCompositionInstaller` resolves those dynamic binders during build at `ClientCompositionInstaller.cs:270` and `ClientCompositionInstaller.cs:274`, making them live immediately on scene composition.
- `ManagementPanelUiToolkitBinderBase.BindVisibility` is the required hide/show entry point at `ManagementPanelUiToolkitBinderBase.cs:104`.
- Base `ApplyVisibility` sets root picking ignore, panel root picking position, and root display none/flex at `ManagementPanelUiToolkitBinderBase.cs:193`.
- `TechTreeUiToolkitBinder` correctly calls `BindVisibility(visibilityStore, ManagementPanelId.TechTree)` at `TechTreeUiToolkitBinder.cs:14`.
- `RecipeSynthesisUiToolkitBinder`, `PolicyFocusUiToolkitBinder`, and `NationalLedgerUiToolkitBinder` correctly bind visibility at `RecipeSynthesisUiToolkitBinder.cs:19`, `PolicyFocusUiToolkitBinder.cs:16`, and `NationalLedgerUiToolkitBinder.cs:10`.
- `MinisterReportUiToolkitBinder` only declares `DefaultTitle` and never binds visibility at `MinisterReportUiToolkitBinder.cs:5`.
- `TurnSummaryUiToolkitBinder` has no `ManagementPanelVisibilityStore` field/injection and never sets `root.style.display` or `root.pickingMode` at `TurnSummaryUiToolkitBinder.cs:38` and `TurnSummaryUiToolkitBinder.cs:99`.
- `ManagementHostUiToolkitBinder.ShowTurnSummary` sets `ManagementPanelId.TurnSummary` at `ManagementHostUiToolkitBinder.cs:403`, but no `TurnSummaryUiToolkitBinder` observes that state.
- `ManagementHostUiToolkitBinder.ApplyVisibility` always keeps the host root document visible (`root.style.display = DisplayStyle.Flex`) at `ManagementHostUiToolkitBinder.cs:478`, even when `activePanel == None`; this means the nav host remains an input participant above uGUI.
- `UiToolkitRuntimeDocument.EnsureConfigured` creates/configures shared runtime panel settings at `ManagementPanelUiToolkitBinderBase.cs:226`.
- `UiToolkitRuntimeDocument.ResolveRuntimeTheme` loads `Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme")` at `ManagementPanelUiToolkitBinderBase.cs:272`; repository inspection found no `client/Assets/Resources/UnityDefaultRuntimeTheme*.asset`.
- Authored UI Toolkit prefab YAML currently shows binder-only prefabs, for example `ManagementHost.prefab:10` component list only has transform and binder; its UXML/USS are serialized on the binder at `ManagementHost.prefab:47` and `:48`. The same pattern appears in `BuildCatalog.prefab:47`/`:48` and `TechTree.prefab:47`/`:48`.
- `BuildCatalogUiToolkitBinder.CreateGroup` creates `build-catalog-group-title` label with a name but no class at `BuildCatalogUiToolkitBinder.cs:152`; `CreateItem` creates item title/description labels with names but no classes at `BuildCatalogUiToolkitBinder.cs:173` and `:176`, while the USS styles are class selectors (`BuildCatalog.uss:52`, `:79`, `:85`). This can leave generated rows unstyled.
- `MinisterReportViewModel.Project` emits `Review` as the action label for interactive drafts at `MinisterReportViewModel.cs:44`.
- `MapRenderer.Start` returns when `BuildBackendGameMap` has no nodes at `MapRenderer.cs:177` and `MapRenderer.cs:249`.
- `MapRenderer.SubscribeGameState` only subscribes at `MapRenderer.cs:270`; it does not explicitly call `OnGameStateChanged(_gameStateStore.Snapshot)` after subscription.
- `MapRenderer.OnGameStateChanged` updates latest state and calls `RefreshRenderedGameState` when nodes exist at `MapRenderer.cs:282`.
- `MapRenderer.RefreshRenderedGameState` rebuilds only when tile count differs or a node is missing at `MapRenderer.cs:356`; there is no explicit `_hasRenderedBackendMap` flag to make first successful backend render observable and idempotent.
- `MapRenderer.BuildFromNodes` logs missing tile prefab at `MapRenderer.cs:943`, clears and instantiates nodes at `MapRenderer.cs:949` and `MapRenderer.cs:966`, then publishes camera context at `MapRenderer.cs:988`.

### Must-modify points

1. Replace dynamic `RegisterComponentOnNewGameObject` UI Toolkit panels with authored prefabs or inject their assets explicitly.

   - Modify `ClientCompositionInstaller.RegisterGameplayBindings`.
   - Create/load authored `Resources/Prefabs/UI/TurnSummary.prefab` and `Resources/Prefabs/UI/MinisterReport.prefab`, or introduce a small factory/config object that assigns `VisualTreeAsset` and `StyleSheet` before first render.
   - Preferred project-consistent path: authored prefabs, matching existing `BuildCatalog`, `TechTree`, `RecipeSynthesis`, `PolicyFocus`, and `NationalLedger`.
   - Update `ClientCompositionBoundaryTests.UiToolkitBinders_ShouldBeTheOnlyNewGameObjectCompositionException`; the current test blesses exactly the two runtime-created panels causing the real regression.

2. Make all management-like UI Toolkit panels observe `ManagementPanelVisibilityStore`.

   - `MinisterReportUiToolkitBinder` must inject `ManagementPanelVisibilityStore` and call `BindVisibility(visibilityStore, ManagementPanelId.MinisterReport)`.
   - `TurnSummaryUiToolkitBinder` must either become a management-panel subclass/binder that observes `ManagementPanelId.TurnSummary`, or be deliberately reclassified as always-on HUD and moved out of management host navigation. Given `ManagementHostUiToolkitBinder.ShowTurnSummary`, the current code clearly expects visibility-store ownership.
   - Add visibility tests that assert both panels start `DisplayStyle.None`, show only for their panel id, and return to none when another panel is active.

3. Fix runtime `PanelSettings` / theme handling.

   - Do not rely on `Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme")` unless an actual runtime theme asset is committed under `client/Assets/Resources` at that path.
   - Prefer a serialized/shared `PanelSettings` asset with `Theme Style Sheet` assigned, then point every management `UIDocument` at it.
   - If runtime creation remains, `UiToolkitRuntimeDocument.ResolveRuntimeTheme` must load a known project asset path or fail visibly in editor/development builds; silent null theme is not acceptable for this regression class.
   - Asset smoke tests should assert `document.panelSettings.themeStyleSheet != null`, not just `panelSettings != null`.

4. Stop invisible or empty UI Toolkit roots from swallowing uGUI input.

   - For `ManagementHostUiToolkitBinder.ApplyVisibility`, decide whether host navigation should be visible when no active panel exists. If not, set `root.style.display = DisplayStyle.None` when `activePanel == None`.
   - If the host nav must stay visible, constrain picking to the actual nav/panel rectangle and ensure the full document root remains `PickingMode.Ignore`.
   - Apply the same root-picking rule to `TurnSummaryUiToolkitBinder`; currently it never sets root picking ignore.
   - Check `BuildCatalogUiToolkitBinder.ApplyVisibility`; it hides the document but does not set root `PickingMode.Ignore` / panel root `Position`, unlike the shared base.
   - Coordinate UI Toolkit `PanelSettings.sortingOrder` with uGUI canvas orders. The current helper sets all runtime documents to sorting order `420`, so any visible UI Toolkit panel is intended to be above uGUI.

5. Fix generated label class assignment where USS uses classes.

   - In `BuildCatalogUiToolkitBinder.CreateGroup` / `CreateItem`, add classes matching `BuildCatalog.uss`: `build-catalog-group-title`, `build-catalog-item-title`, and `build-catalog-item-description`.
   - Extend tests to assert class presence on generated build catalog labels. Current tests only assert this for shared management panel rows, not build catalog rows.

6. Make first backend map render deterministic.

   - In `MapRenderer.SubscribeGameState`, after subscribing, explicitly call a local "try render current snapshot" path with `_gameStateStore.Snapshot` so rendering does not depend on a future store emission.
   - Introduce an explicit `_hasRenderedBackendMap` / `_lastRenderedNodeCount` guard, so the first node-bearing snapshot always calls `BuildFromBackendNodes` once and later same-size updates can use `RefreshRenderedGameState`.
   - Keep the existing low-severity waiting log, but ensure it is not the only first-turn observable.
   - Add a focused EditMode test around `MapRenderer`: construct with a `GameStateStore`, publish a node-bearing state before or immediately after injection/subscription, and assert tile views are created without needing a second turn update.

### External references

- Unity Manual, "Render UI in the Game view": every UI Document references a UXML source asset and a Panel Settings asset; multiple UI Documents can share one Panel Settings; same-level UI Documents are rendered by Sort Order; Panel Settings controls default styles and panel sort order. https://docs.unity.cn/2022.1/Documentation/Manual/UIE-render-runtime-ui.html
- Unity Manual, "Panel Settings properties reference": `Theme Style Sheet` applies a default TSS file to every UIDocument rendered by the panel; `Sort Order` draws higher panels above lower panels; `Scale with Screen Size` uses reference resolution/screen-match parameters. https://docs.unity.cn/Manual/UIE-Runtime-Panel-Settings.html
- Unity Scripting API, `PanelSettings` (Unity 6.2 Alpha docs): multiple UIDocuments can point to the same `PanelSettings`; `themeStyleSheet` applies to every UI Document attached to the panel; `sortingOrder` controls ordering when multiple panels are used. https://docs.unity.cn/6000.2/Documentation/ScriptReference/UIElements.PanelSettings.html
- Unity Scripting API, `UIDocument`: `panelSettings`, `rootVisualElement`, `sortingOrder`, and `visualTreeAsset` are the UIDocument properties connecting UXML UI to runtime GameObject rendering. https://docs.unity.cn/ScriptReference/UIElements.UIDocument.html
- Unity Manual, "Runtime UI event system": UI Toolkit and uGUI can coexist; when both are used, UI Toolkit uses panel sort order compared with uGUI canvas/raycast targets to decide whether pointer events go to UI Toolkit, uGUI, or the scene. https://docs.unity3d.com/ja/2023.2/Manual/UIE-Runtime-Event-System.html
- Unity Scripting API, `PickingMode`: `Position` is default and enables picking by position rectangle; `Ignore` prevents picking as the result of mouse events. https://docs.unity.cn/ScriptReference/UIElements.PickingMode.html

### Related specs

- `.trellis/spec/frontend/directory-structure.md`: Presentation owns Unity views/presenters/binders; UI Toolkit work should route state through Core stores and ViewModels.
- `.trellis/spec/frontend/component-guidelines.md`: UI Toolkit binders should use explicit `root.Q<T>("name")` lookup and a single `Render(state)` method; uGUI remains for map/HUD/world-space UI.
- `.trellis/spec/frontend/state-management.md`: default flow is `Server -> Core cache/store -> ViewModel -> Binder -> UI`; UI Toolkit and uGUI must share Core stores/ViewModels.
- `.trellis/spec/frontend/quality-guidelines.md`: `RegisterComponentOnNewGameObject` is reserved for current UIDocument management binders until authored UI Toolkit prefabs exist; this research finds that exception is now a real-client regression source and should be retired or narrowed.

## Caveats / Not Found

- No code was changed.
- I did not run Unity Editor or PlayMode here; conclusions are from repository source/assets plus Unity official documentation.
- The official Unity runtime docs opened were 2022.1/2022.3/2023.2 and Unity 6.2 API pages. The project targets Unity 6000.4.1f1, but the referenced UIDocument/PanelSettings/PickingMode contracts are stable enough for this diagnosis.
- I did not find an authored `UnityDefaultRuntimeTheme` asset under `client/Assets/Resources`, so `Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme")` should be treated as returning null unless Unity injects an undocumented built-in resource by that exact name. The code should not rely on that for production.
- I did not find a `MinisterReport` UXML/USS or prefab asset. The current minister report UI is therefore necessarily fallback-generated.
- The map issue needs one real-client repro pass after implementing deterministic first-snapshot rendering. The code now has a better subscription path than the original PRD described, but it still lacks an explicit immediate current-snapshot render/guard and a test for "store hydrated before second turn".
