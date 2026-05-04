# C0a Complete Management Panels and EditMode Fixes

## Goal

Finish the currently planned C0a management UI work after C0a-0/1/2 and make
the Unity EditMode suite pass, including pre-existing failures surfaced by the
full suite baseline.

## Scope

- C0a-3: Tech Tree authored UI Toolkit asset, prefab-backed composition, and
  row command wiring to `GameIntentService.SetResearchTarget`.
- C0a-4: Policy Focus authored UI Toolkit asset, prefab-backed composition,
  visibility binding, and row command wiring to `GameIntentService.SetPolicy`
  for national focus plus `SetInstitutionLoadout` for institution rows.
- C0a-5: National Ledger authored UI Toolkit asset, prefab-backed composition,
  visibility binding, and read-only rendering.
- Fix Unity EditMode failures discovered by
  `client/client/Temp/C0aFullEditModeBaseline.xml`.
- Keep Minister Report as later-scope unless needed to remove a current
  fallback/composition exception safely.

## Requirements

- No Presentation references to generated Protocol.
- No Presentation direct `NetworkManager.Instance` command paths.
- No new legacy singleton cache lookups in migrated Store/ViewModel/Binder code.
- No client gameplay legality checks; UI commands only forward selected ids to
  Core intent services.
- UI Toolkit assets must use stable element names compatible with
  `ManagementPanelUiToolkitRenderer`.
- Newly authored management binders must be loaded from prefabs, not
  `RegisterComponentOnNewGameObject`.
- Full Unity EditMode must pass or any remaining non-code environmental failure
  must be documented with XML evidence.

## Baseline Failures To Fix

- `StaticCatalogStoreHydratorTests.HydrateFromCache_ShouldPublishCacheCatalogSnapshot`
- `DebugWorkbenchTests.DebugTabRegistry_ShouldExposeDefaultSixTabs`
- `DebugWorkbenchTests.GameChatPanelController_ShouldExposeUiHooks_WithoutReferencingProtocol`
- `ClientRuntimeIntegrationTests.CityCoreRuntimeActions_ShouldUseCityCoreWithoutCastleAlias`
- `ClientRuntimeIntegrationTests.GameMessageHandler_ShouldForwardGameChatPostedIntoChatCache`
- `ClientRuntimeIntegrationTests.GameMessageHandler_ShouldPublishPlanningCommandResults_WithoutMutatingAuthoritativeState`
- `ClientRuntimeIntegrationTests.GameMessageHandler_ShouldPublishStructuredBuildAndRecipeFeedback`
- `ClientRuntimeIntegrationTests.GameSceneController_ShouldShowSuccessToast_WhenOwnTechnologyCompletesOnSettlement`
- `ClientRuntimeIntegrationTests.MapPlanningInputController_ShouldRequireExplicitBuildCityContext`
- `ClientRuntimeIntegrationTests.MessageDispatcher_ShouldDropCrossSessionGameEvents`
- `CityCoreBuildingActionResolverTests.RegisteredRecipeSynthesisActions_ShouldSwitchBuildAndRecipeContextsThroughVisibilityStore`
- `ResourceHudUguiBinderTests.TechButton_ShouldToggleFinalTechTreeVisibilityStore`
- `CommonOverlayTests.AppManager_Bootstrap_ShouldPlaceCommonOverlays_OutsideManagersCanvasGroup`
- `UnitInfoActionListBinderTests.Refresh_ShouldEnsureInactiveActionProvidersRegistered`

## Acceptance Criteria

- [x] TechTree UXML/USS and prefab exist and are registered through
      `RegisterComponentInNewPrefab`.
- [x] TechTree row action sends `MsgSetResearchTarget` through
      `GameIntentService`.
- [x] PolicyFocus UXML/USS and prefab exist and are registered through
      `RegisterComponentInNewPrefab`.
- [x] PolicyFocus visibility follows `ManagementPanelVisibilityStore`.
- [x] PolicyFocus row actions send `MsgSetPolicy` or
      `MsgSetInstitutionLoadout` through `GameIntentService`.
- [x] NationalLedger UXML/USS and prefab exist and are registered through
      `RegisterComponentInNewPrefab`.
- [x] NationalLedger visibility follows `ManagementPanelVisibilityStore`.
- [x] UI Toolkit generated GameObject exception list no longer includes
      TechTree, PolicyFocus, or NationalLedger.
- [x] `dotnet build client/Panoptes.Tests.EditMode.csproj` passes.
- [x] `dotnet test client/Panoptes.Tests.EditMode.csproj --no-build` passes.
- [x] Full Unity EditMode test run passes.
- [x] Task status and verification notes are persisted.

## Out Of Scope

- Final graphical node-link TechTree layout.
- Full Minister Report accept/reject UX.
- Rewriting map/HUD/world-space uGUI into UI Toolkit.
- UI Toolkit automatic data binding.
