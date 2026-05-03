using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Panoptes.Tests.EditMode.Composition
{
    public sealed class ClientCompositionBoundaryTests
    {
        [Test]
        public void CompositionRuntime_ShouldNotUseLegacySingletonLookup()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/Composition"),
                ResolveAssetPath("Scripts/Runtime/Core/Infrastructure/Network/NetworkMessageSender.cs")
            };

            var offenders = FindTokenOffenders(roots, "*.cs", ".Instance");

            Assert.That(offenders, Is.Empty, "Final composition code must receive dependencies from VContainer, not legacy singleton lookup.");
        }

        [Test]
        public void CompositionScopes_ShouldRegisterFinalStoresWithoutLegacyCacheFacades()
        {
            var root = ResolveAssetPath("Scripts/Runtime/Presentation/Composition");
            var offenders = FindTokenOffenders(
                new[] { root },
                "*.cs",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache");

            Assert.That(offenders, Is.Empty, "GameLifetimeScope must wait for final Stores instead of registering legacy cache facades.");

            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            Assert.That(installer, Does.Contain("StaticCatalogStore"));
            Assert.That(installer, Does.Contain("GameStateStore"));
            Assert.That(installer, Does.Contain("PlanningDraftStore"));
            Assert.That(installer, Does.Contain("SelectionStore"));
            Assert.That(installer, Does.Contain("PlanningToolStore"));
            Assert.That(installer, Does.Contain("ActionLockStore"));
            Assert.That(installer, Does.Contain("TurnStore"));
            Assert.That(installer, Does.Contain("SelectionService"));
            Assert.That(installer, Does.Contain("PlanningToolService"));
            Assert.That(installer, Does.Contain("GameIntentService"));
            Assert.That(installer, Does.Contain("PlanningIntentService"));
            Assert.That(installer, Does.Contain("MinisterCommandService"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<TokenHUD>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<ResourceHUD>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<TurnHUD>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<GameChatPanelController>"));
            Assert.That(installer, Does.Not.Contain("RegisterRuntimeSceneComponent<MinisterPanel>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<SettlementTimeline>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<TurnReportPanel>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<GameOverOverlay>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<UnitInfoPanelController>"));
            Assert.That(installer, Does.Not.Contain("RecipeSynthesisPanel"));
            Assert.That(installer, Does.Not.Contain("RegisterOptionalSceneComponent<TechTreePanelController>"));
            Assert.That(installer, Does.Contain("UnitInfoViewModel"));
            Assert.That(installer, Does.Contain("PlanningToolViewModel"));
            Assert.That(installer, Does.Contain("TokenHudViewModel"));
            Assert.That(installer, Does.Contain("ResourceHudViewModel"));
            Assert.That(installer, Does.Contain("ManagementPanelVisibilityStore"));
            Assert.That(installer, Does.Contain("TurnSummaryViewModel"));
            Assert.That(installer, Does.Contain("TurnSummaryUiToolkitBinder"));
            Assert.That(installer, Does.Contain("RecipeSynthesisContextStore"));
            Assert.That(installer, Does.Contain("RecipeSynthesisViewModel"));
            Assert.That(installer, Does.Contain("RecipeSynthesisUiToolkitBinder"));
            Assert.That(installer, Does.Contain("MinisterReportViewModel"));
            Assert.That(installer, Does.Contain("MinisterReportUiToolkitBinder"));
        }

        [Test]
        public void CompositionRuntime_ShouldNotReintroduceManualSceneInjectionShells()
        {
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/SceneCommandServiceInjector.cs")),
                Is.False,
                "Scene command injection must be owned by GameLifetimeScope registrations, not a manual scope lookup shell.");

            var root = ResolveAssetPath("Scripts/Runtime/Presentation/Composition");
            var offenders = FindTokenOffenders(
                new[] { root },
                "*.cs",
                "SceneCommandServiceInjector",
                "LifetimeScope.Find<GameLifetimeScope>()",
                "InjectGameObject");

            Assert.That(offenders, Is.Empty, "Presentation composition must not perform manual scene injection.");
        }

        [Test]
        public void PresentationCommandCallers_ShouldUseServicesInsteadOfStaticGameIntents()
        {
            var root = ResolveAssetPath("Scripts/Runtime/Presentation");
            var offenders = FindTokenOffenders(
                new[] { root },
                "*.cs",
                "GameIntents.");

            Assert.That(offenders, Is.Empty, "Migrated Presentation command callers must use injected command services.");
        }

        [Test]
        public void GameSceneController_ShouldOnlyEnsureHelpersNotInjectThemManually()
        {
            var controller = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/UI/Game/GameSceneController.cs"));

            Assert.That(controller, Does.Not.Contain("IObjectResolver"));
            Assert.That(controller, Does.Not.Contain("InjectIfPossible"));
            Assert.That(controller, Does.Not.Contain("InjectDynamicPresentationHelpers"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<GameChatPanelController>"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<MinisterPanel>"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<TurnHUD>"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<SettlementTimeline>"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<TurnReportPanel>"));
            Assert.That(controller, Does.Not.Contain("EnsurePrefabComponent<GameOverOverlay>"));
            Assert.That(controller, Does.Contain("EnsureComponent<ResourceHUD>"));
        }

        [Test]
        public void CommandServices_ShouldNotDependOnUnityUiOrStaticNetworkSender()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Core/Application/Services/GameIntentService.cs"),
                ResolveAssetPath("Scripts/Runtime/Core/Application/Services/PlanningIntentService.cs"),
                ResolveAssetPath("Scripts/Runtime/Core/Application/Services/MinisterCommandService.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "UnityEngine.UI",
                "TMPro",
                "NetworkManager",
                "MessageSender.");

            Assert.That(offenders, Is.Empty, "Command services may build messages, but must not know about UI controls or static network senders.");
        }

        [Test]
        public void UnitInfoMigratedSlice_ShouldNotDependOnProtocolOrLegacyCaches()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoPanelController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/UnitInfoViewModel.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Binders/Ugui/UnitInfoUguiBinder.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoHpState.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoDirectOrderState.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "Panoptes.Protocol",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                "NetworkManager.Instance",
                ".Instance");

            Assert.That(offenders, Is.Empty, "Migrated UnitInfo ViewModel/Binder must consume final stores and services only.");
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoReactiveBridge.cs")),
                Is.False,
                "UnitInfo must not reintroduce a compatibility bridge between controller, ViewModel, and Binder.");
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoPlanningSummaryPresenter.cs")),
                Is.False,
                "UnitInfo planning summary must be projected by UnitInfoViewModel and rendered by UnitInfoUguiBinder.");
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoHpStateResolver.cs")),
                Is.False,
                "UnitInfo HP state must come from UnitInfoViewModel, not a cache fallback resolver.");
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoDirectOrderStateResolver.cs")),
                Is.False,
                "UnitInfo direct-order state must come from UnitInfoViewModel, not a cache fallback resolver.");
        }

        [Test]
        public void TokenHudMigratedSlice_ShouldUseViewModelAndFinalStore()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/TokenHUD.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/TokenHudViewModel.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/TokenHudState.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "Panoptes.Protocol",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                "NetworkManager.Instance",
                "ActionLock.",
                ".Instance");

            Assert.That(offenders, Is.Empty, "TokenHUD must bind to TokenHudViewModel state instead of legacy cache singletons.");

            var hud = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/TokenHUD.cs"));
            var viewModel = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/TokenHudViewModel.cs"));
            Assert.That(hud, Does.Contain("TokenHudViewModel"));
            Assert.That(viewModel, Does.Contain("ActionLockStore"));
            Assert.That(viewModel, Does.Not.Contain("Panoptes.Core.Application.Intents"));
        }

        [Test]
        public void ResourceHudMigratedSlice_ShouldUseViewModelAndFinalStores()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/ResourceHUD.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Binders/Ugui/ResourceHudUguiBinder.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/ResourceHudViewModel.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/ResourceHudState.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "Panoptes.Protocol",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                "NetworkManager.Instance",
                "TechTreePanelController",
                ".Instance");

            Assert.That(offenders, Is.Empty, "ResourceHUD must bind to ResourceHudViewModel state instead of legacy cache singletons.");

            var hud = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/ResourceHUD.cs"));
            var viewModel = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/ResourceHudViewModel.cs"));
            Assert.That(hud, Does.Contain("ResourceHudViewModel"));
            Assert.That(hud, Does.Contain("ResourceHudUguiBinder"));
            Assert.That(viewModel, Does.Contain("GameStateStore"));
            Assert.That(viewModel, Does.Contain("StaticCatalogStore"));
        }

        [Test]
        public void HudOverlayControllers_ShouldBeFinalPathOwnedByComposition()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/CityCoreHpBarOverlayController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/BuildingConstructionOverlayController.cs")
            };

            var mapRendererSingletonToken = "MapRenderer" + ".Instance";
            var mapPlanningInputSingletonToken = "MapPlanningInputController" + ".Instance";
            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "Panoptes.Protocol",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                "NetworkManager.Instance",
                mapRendererSingletonToken,
                mapPlanningInputSingletonToken);

            Assert.That(offenders, Is.Empty, "HUD overlay controllers must consume final Stores and injected MapRenderer instead of legacy singleton/cache paths.");

            var cityOverlay = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/CityCoreHpBarOverlayController.cs"));
            var constructionOverlay = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/BuildingConstructionOverlayController.cs"));
            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            var mapRenderer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapRenderer.cs"));

            Assert.That(cityOverlay, Does.Contain("GameStateStore"));
            Assert.That(cityOverlay, Does.Contain("MapRenderer"));
            Assert.That(cityOverlay, Does.Contain("[Inject]"));
            Assert.That(constructionOverlay, Does.Contain("GameStateStore"));
            Assert.That(constructionOverlay, Does.Contain("PlanningDraftStore"));
            Assert.That(constructionOverlay, Does.Contain("MapRenderer"));
            Assert.That(constructionOverlay, Does.Contain("[Inject]"));
            Assert.That(installer, Does.Contain("RegisterComponentInHierarchy<MapRenderer>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<CityCoreHpBarOverlayController>"));
            Assert.That(installer, Does.Contain("RegisterRuntimeSceneComponent<BuildingConstructionOverlayController>"));
            Assert.That(mapRenderer, Does.Not.Contain("AddComponent<CityCoreHpBarOverlayController>"));
            Assert.That(mapRenderer, Does.Not.Contain("AddComponent<BuildingConstructionOverlayController>"));
            Assert.That(mapRenderer, Does.Not.Contain("FindAnyObjectByType<CityCoreHpBarOverlayController>"));
            Assert.That(mapRenderer, Does.Not.Contain("FindAnyObjectByType<BuildingConstructionOverlayController>"));
        }

        [Test]
        public void MapRenderer_ShouldReadAuthoritativeStateFromGameStateStore()
        {
            var rendererPath = ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapRenderer.cs");
            var cameraContextBuilderPath = ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapCameraContextBuilder.cs");
            var offenders = FindTokenOffenders(
                new[] { rendererPath, cameraContextBuilderPath },
                "*.cs",
                "Panoptes.Protocol",
                "GameStateCache",
                "NetworkManager.Instance");

            Assert.That(offenders, Is.Empty, "MapRenderer must render authoritative game state from GameStateStore, not Protocol or legacy cache paths.");

            var mapRenderer = File.ReadAllText(rendererPath);
            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            Assert.That(mapRenderer, Does.Contain("GameStateStore"));
            Assert.That(mapRenderer, Does.Contain("[Inject]"));
            Assert.That(mapRenderer, Does.Contain(".State.Subscribe"));
            Assert.That(installer, Does.Contain("RegisterComponentInHierarchy<MapRenderer>"));
        }

        [Test]
        public void TechTreeUiToolkitSlice_ShouldUseViewModelVisibilityStoreAndNoLegacyPanel()
        {
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/Domestic/TechTreePanelController.cs")),
                Is.False,
                "Tech tree must not reintroduce the legacy uGUI/cache controller.");
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/Domestic/TechTreePanelStateBuilder.cs")),
                Is.False,
                "Tech tree state must be projected by the final ViewModel path, not the legacy cache builder.");
            Assert.That(
                File.Exists(ResolveAssetPath("Prefabs/UI/Tech/TechTreePanel.prefab")),
                Is.False,
                "The deleted legacy TechTreePanelController must not remain as an authored prefab path.");
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/Domestic/TechTreeNodeView.cs")),
                Is.False,
                "The legacy uGUI tech node view must stay deleted; the final tech tree renders through UI Toolkit.");
            Assert.That(
                File.Exists(ResolveAssetPath("Prefabs/UI/Tech/TechNodeItem.prefab")),
                Is.False,
                "The legacy uGUI tech node prefab must stay deleted with TechTreeNodeView.");

            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/TechTreeViewModel.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/ManagementPanelVisibilityStore.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Binders/UiToolkit/TechTreeUiToolkitBinder.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Binders/UiToolkit/ManagementPanelUiToolkitBinderBase.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/ResourceHUD.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "Panoptes.Protocol",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                "ActionLock.",
                "NetworkManager.Instance",
                "TechTreePanelController",
                ".Instance");

            Assert.That(offenders, Is.Empty, "Tech tree final UI slice must use Store/ViewModel/Binder plus presentation visibility state only.");

            var resourceHud = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/ResourceHUD.cs"));
            var binder = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Binders/UiToolkit/TechTreeUiToolkitBinder.cs"));
            var viewModel = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/TechTreeViewModel.cs"));
            Assert.That(resourceHud, Does.Contain("ManagementPanelVisibilityStore"));
            Assert.That(resourceHud, Does.Contain("ManagementPanelId.TechTree"));
            Assert.That(binder, Does.Contain("BindVisibility"));
            Assert.That(viewModel, Does.Contain("StaticCatalogStore"));
            Assert.That(viewModel, Does.Contain("PlanningDraftStore"));

            var scene = File.ReadAllText(ResolveAssetPath("Scenes/Game.unity"));
            Assert.That(scene, Does.Not.Contain("TechTreePanel"));
        }

        [Test]
        public void TurnSummaryUiToolkitSlice_ShouldUseStableNamesAndFinalStores()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/TurnSummaryViewModel.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Binders/UiToolkit/TurnSummaryUiToolkitBinder.cs")
            };
            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "Panoptes.Protocol",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                "NetworkManager.Instance",
                ".Instance");

            Assert.That(offenders, Is.Empty, "Migrated TurnSummary ViewModel/Binder must consume final stores only.");

            var uxml = File.ReadAllText(ResolveAssetPath("UI/Toolkit/Turn/TurnSummary.uxml"));
            Assert.That(uxml, Does.Contain("turn-summary-root"));
            Assert.That(uxml, Does.Contain("turn-summary-title"));
            Assert.That(uxml, Does.Contain("turn-summary-turn-value"));
            Assert.That(uxml, Does.Contain("turn-summary-phase-value"));
            Assert.That(uxml, Does.Contain("turn-summary-events"));
        }

        [Test]
        public void MinisterReportUiToolkitSlice_ShouldRetireLegacyPanelAndUseFinalStores()
        {
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/Minister/MinisterPanel.cs")),
                Is.False,
                "Minister UI must not reintroduce the cache-backed MinisterPanel controller.");
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/UI/Minister/MinisterPanel.cs.meta")),
                Is.False,
                "Deleted MinisterPanel must not keep a Unity meta file or GUID alive.");

            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/MinisterReportViewModel.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Binders/UiToolkit/MinisterReportUiToolkitBinder.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "Panoptes.Protocol",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                "NetworkManager.Instance",
                ".Instance");

            Assert.That(offenders, Is.Empty, "Minister report final UI slice must render PlanningDraftStore through ViewModel/Binder only.");

            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            var viewModel = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/MinisterReportViewModel.cs"));
            var binder = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Binders/UiToolkit/MinisterReportUiToolkitBinder.cs"));

            Assert.That(installer, Does.Contain("MinisterCommandService"));
            Assert.That(installer, Does.Contain("MinisterReportViewModel"));
            Assert.That(installer, Does.Contain("MinisterReportUiToolkitBinder"));
            Assert.That(installer, Does.Not.Contain("RegisterRuntimeSceneComponent<MinisterPanel>"));
            Assert.That(viewModel, Does.Contain("PlanningDraftStore"));
            Assert.That(binder, Does.Contain("ManagementPanelUiToolkitBinderBase<MinisterReportViewModel>"));

            var assetOffenders = FindTokenOffenders(
                new[]
                {
                    ResolveAssetPath("Scenes"),
                    ResolveAssetPath("Prefabs")
                },
                "*.*",
                "3d8e60f192d6861409a7e45a51dc4617",
                "MinisterPanel");

            Assert.That(assetOffenders, Is.Empty, "Scenes and prefabs must not retain MinisterPanel script or GUID references.");
        }

        [Test]
        public void PlanningToolMigratedSlice_ShouldUseFinalStores()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Core/Application/Stores/PlanningToolStore.cs"),
                ResolveAssetPath("Scripts/Runtime/Core/Application/Services/PlanningToolService.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapPlanningInputStateAdapter.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/PlanningToolViewModel.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/PlanningToolViewState.cs")
            };
            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "Panoptes.Protocol",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                "NetworkManager.Instance",
                ".Instance",
                "UnityEngine.UI",
                "TMPro");

            Assert.That(offenders, Is.Empty, "PlanningTool state and ViewModel must consume final stores/services only.");

            var viewModel = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/ViewModels/PlanningToolViewModel.cs"));
            Assert.That(viewModel, Does.Not.Contain("PlanningToolService"));
            Assert.That(viewModel, Does.Not.Contain("SelectionService"));
            Assert.That(viewModel, Does.Not.Contain("public void Begin"));
            Assert.That(viewModel, Does.Not.Contain("public void Enter"));
            Assert.That(viewModel, Does.Not.Contain("public void Select"));
            Assert.That(viewModel, Does.Not.Contain("public void Clear"));
        }

        [Test]
        public void PlanningInputSlice_ShouldNotExposeLegacyCaches()
        {
            var root = ResolveAssetPath("Scripts/Runtime/Presentation/Planning/Input");
            Assert.That(
                File.Exists(Path.Combine(root, "PlanningInputContext.cs")),
                Is.False,
                "Planning input modes must not retain an empty compatibility context shell.");

            var offenders = FindTokenOffenders(
                new[] { root },
                "*.cs",
                "Panoptes.Core.Application.Cache",
                "GameStateCache",
                "PlanningDraftCache");

            Assert.That(offenders, Is.Empty, "Planning input context and modes must not expose legacy cache channels.");
        }

        [Test]
        public void MapInputAdapter_ShouldPublishToolAndSelectionState()
        {
            var controller = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapPlanningInputController.cs"));

            Assert.That(controller, Does.Contain("PlanningToolService"));
            Assert.That(controller, Does.Contain("PlanningToolViewModel"));
            Assert.That(controller, Does.Contain("SelectionService"));
            Assert.That(controller, Does.Contain("MapPlanningInputStateAdapter"));
            Assert.That(controller, Does.Contain("_inputState.PublishSelectedUnit(unit.UnitId)"));
            Assert.That(controller, Does.Not.Contain("private PlanningToolService"));
            Assert.That(controller, Does.Not.Contain("private SelectionService"));
            Assert.That(controller, Does.Not.Contain("private PlanningToolViewModel"));
            Assert.That(controller, Does.Not.Contain("private enum Mode"));
            Assert.That(controller, Does.Not.Contain("_mode"));
            Assert.That(
                CountOccurrences(controller, ".SetCombatActionMode("),
                Is.GreaterThan(0),
                "Controller should delegate combat mode mutations to the extracted adapter.");
        }

        [Test]
        public void MapPlanningInputPresenterSlice_ShouldUseInjectedMapRenderer()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapPlanningInputController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapBuildPlacementSession.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapMovePreviewPresentationController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MovePreviewOverlayController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MovePathOverlayController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapPendingDeployGhostController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapTerritoryHighlightPresenter.cs")
            };

            var mapRendererSingletonToken = "MapRenderer" + ".Instance";
            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                mapRendererSingletonToken,
                "SceneObjectFinder.FindFirstSceneObject<MapRenderer>",
                "FindAnyObjectByType<MapRenderer>",
                "FindFirstObjectByType<MapRenderer>",
                "FindObjectOfType<MapRenderer>",
                "NetworkManager.Instance",
                "Panoptes.Protocol");

            Assert.That(offenders, Is.Empty, "Map planning input presenters must receive MapRenderer explicitly instead of looking up singleton or scene fallbacks.");

            var controller = File.ReadAllText(roots[0]);
            Assert.That(controller, Does.Contain("MapRenderer mapRenderer"));
            Assert.That(controller, Does.Contain("ConfigureMapRenderer(mapRenderer)"));
            Assert.That(controller, Does.Contain("if (isActiveAndEnabled)"));
            Assert.That(controller, Does.Contain("SubscribeStoreEvents();"));
            Assert.That(controller, Does.Contain("_mapRenderer"));
            Assert.That(controller, Does.Contain("[Inject]"));

            var buildPlacementSession = File.ReadAllText(roots[1]);
            var previewPresentation = File.ReadAllText(roots[2]);
            var previewOverlay = File.ReadAllText(roots[3]);
            var pathOverlay = File.ReadAllText(roots[4]);
            var pendingDeploy = File.ReadAllText(roots[5]);
            var territoryHighlight = File.ReadAllText(roots[6]);
            Assert.That(buildPlacementSession, Does.Contain("MapRenderer _mapRenderer"));
            Assert.That(buildPlacementSession, Does.Contain("MapRenderer mapRenderer"));
            Assert.That(previewPresentation, Does.Contain("SetMapRenderer(MapRenderer mapRenderer)"));
            Assert.That(previewPresentation, Does.Contain("_pathOverlay?.SetMapRenderer(mapRenderer)"));
            Assert.That(previewPresentation, Does.Contain("_previewOverlay?.SetMapRenderer(mapRenderer)"));
            Assert.That(previewPresentation, Does.Contain("new MovePathOverlayController(_mapRenderer)"));
            Assert.That(previewPresentation, Does.Contain("new MovePreviewOverlayController(_mapRenderer, hostTransform)"));
            Assert.That(previewOverlay, Does.Contain("MovePreviewOverlayController(MapRenderer mapRenderer, Transform hostTransform)"));
            Assert.That(previewOverlay, Does.Contain("SetMapRenderer(MapRenderer mapRenderer)"));
            Assert.That(pathOverlay, Does.Contain("MovePathOverlayController(MapRenderer mapRenderer)"));
            Assert.That(pathOverlay, Does.Contain("SetMapRenderer(MapRenderer mapRenderer)"));
            Assert.That(pendingDeploy, Does.Contain("SetMapRenderer(MapRenderer mapRenderer)"));
            Assert.That(territoryHighlight, Does.Contain("SetMapRenderer(MapRenderer mapRenderer)"));
        }

        [Test]
        public void PresentationAssembly_ShouldReferenceVContainer()
        {
            var asmdef = ResolveAssetPath("Scripts/Runtime/Presentation/Panoptes.Presentation.asmdef");
            var content = File.ReadAllText(asmdef);

            Assert.That(content, Does.Contain("\"VContainer\""));
        }

        [Test]
        public void GameScene_ShouldOwnGameLifetimeScope()
        {
            var scenePath = ResolveAssetPath("Scenes/Game.unity");
            var scriptMetaPath = ResolveAssetPath("Scripts/Runtime/Presentation/Composition/GameLifetimeScope.cs.meta");
            var sceneContent = File.ReadAllText(scenePath);
            var scriptGuid = ReadGuid(scriptMetaPath);

            Assert.That(sceneContent, Does.Contain("m_Name: Game Composition"));
            Assert.That(sceneContent, Does.Contain($"guid: {scriptGuid}"));
            Assert.That(sceneContent, Does.Contain("Panoptes.Presentation.Composition.GameLifetimeScope"));
        }

        private static List<string> FindTokenOffenders(IEnumerable<string> roots, string searchPattern, params string[] forbiddenTokens)
        {
            var offenders = new List<string>();
            foreach (var root in roots)
            {
                foreach (var path in EnumerateFiles(root, searchPattern))
                {
                    var content = File.ReadAllText(path);
                    for (var i = 0; i < forbiddenTokens.Length; i++)
                    {
                        var token = forbiddenTokens[i];
                        if (content.IndexOf(token, StringComparison.Ordinal) >= 0)
                        {
                            offenders.Add($"{ToProjectRelativePath(path)} contains {token}");
                        }
                    }
                }
            }

            return offenders;
        }

        private static IEnumerable<string> EnumerateFiles(string path, string searchPattern)
        {
            if (File.Exists(path))
            {
                yield return path;
                yield break;
            }

            foreach (var file in Directory.EnumerateFiles(path, searchPattern, SearchOption.AllDirectories))
            {
                yield return file;
            }
        }

        private static string ResolveAssetPath(string assetRelativePath)
        {
            var localPath = Path.GetFullPath(Path.Combine("Assets", assetRelativePath));
            if (File.Exists(localPath) || Directory.Exists(localPath))
            {
                return localPath;
            }

            return Path.GetFullPath(Path.Combine("client", "Assets", assetRelativePath));
        }

        private static string ToProjectRelativePath(string path)
        {
            var fullPath = Path.GetFullPath(path).Replace('\\', '/');
            var assetsIndex = fullPath.LastIndexOf("/Assets/", StringComparison.Ordinal);
            return assetsIndex >= 0 ? fullPath.Substring(assetsIndex + 1) : fullPath;
        }

        private static string ReadGuid(string metaPath)
        {
            var guidLine = File.ReadLines(metaPath)
                .FirstOrDefault(line => line.StartsWith("guid:", StringComparison.Ordinal));
            Assert.That(guidLine, Is.Not.Null, $"{metaPath} does not contain a Unity guid.");
            return guidLine.Substring("guid:".Length).Trim();
        }

        private static int CountOccurrences(string content, string token)
        {
            var count = 0;
            var index = 0;
            while ((index = content.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }
    }
}
