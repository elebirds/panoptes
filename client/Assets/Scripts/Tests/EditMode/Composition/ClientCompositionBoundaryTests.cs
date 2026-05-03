using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Panoptes.Tests.EditMode.Composition
{
    public sealed class ClientCompositionBoundaryTests
    {
        private static readonly string ProtocolNamespaceToken = Token("Panoptes", ".Protocol");
        private static readonly string NetworkManagerSingletonToken = Token("NetworkManager", ".Instance");
        private static readonly string MapRendererSingletonToken = Token("MapRenderer", ".Instance");
        private static readonly string SceneObjectFinderMapRendererToken = Token("SceneObjectFinder.FindFirstSceneObject", "<MapRenderer>");
        private static readonly string FindAnyMapRendererToken = Token("FindAnyObjectByType", "<MapRenderer>");
        private static readonly string FindFirstMapRendererToken = Token("FindFirstObjectByType", "<MapRenderer>");
        private static readonly string FindObjectOfTypeMapRendererToken = Token("FindObjectOfType", "<MapRenderer>");

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
            Assert.That(installer, Does.Contain("RegisterComponentOnNewGameObject<TokenHUD>"));
            Assert.That(installer, Does.Contain("RegisterComponentInHierarchy<ResourceHUD>"));
            Assert.That(installer, Does.Contain("RegisterComponentInHierarchy<TurnHUD>"));
            Assert.That(installer, Does.Contain("RegisterComponentOnNewGameObject<GameChatPanelController>"));
            Assert.That(installer, Does.Contain("RegisterComponentInHierarchy<SettlementPlaybackController>"));
            Assert.That(installer, Does.Contain("RegisterComponentInHierarchy<CinemachineMapCameraController>"));
            Assert.That(installer, Does.Not.Contain("RegisterRuntimeSceneComponent<MinisterPanel>"));
            Assert.That(installer, Does.Contain("RegisterComponentOnNewGameObject<SettlementTimeline>"));
            Assert.That(installer, Does.Contain("RegisterComponentOnNewGameObject<TurnReportPanel>"));
            Assert.That(installer, Does.Contain("RegisterComponentOnNewGameObject<GameOverOverlay>"));
            Assert.That(installer, Does.Contain("RegisterComponentInHierarchy<UnitInfoPanelController>"));
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

            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            Assert.That(installer, Does.Not.Contain("RegisterRuntimeSceneComponent"));
            Assert.That(installer, Does.Not.Contain("SceneObjectFinder"));
            Assert.That(installer, Does.Not.Contain("FindFirstSceneObject"));
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
        public void GameSceneController_ShouldNotCreatePresentationHelpers()
        {
            var controller = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/UI/Game/GameSceneController.cs"));

            Assert.That(controller, Does.Not.Contain("IObjectResolver"));
            Assert.That(controller, Does.Not.Contain("InjectIfPossible"));
            Assert.That(controller, Does.Not.Contain("InjectDynamicPresentationHelpers"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<GameChatPanelController>"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<MinisterPanel>"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<TurnHUD>"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<SettlementTimeline>"));
            Assert.That(controller, Does.Not.Contain("EnsureComponent<TurnReportPanel>"));
            Assert.That(controller, Does.Not.Contain("EnsurePrefabComponent<GameOverOverlay>"));
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
                ProtocolNamespaceToken,
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                NetworkManagerSingletonToken,
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
                ProtocolNamespaceToken,
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                NetworkManagerSingletonToken,
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
                ProtocolNamespaceToken,
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                NetworkManagerSingletonToken,
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

            var mapPlanningInputSingletonToken = "MapPlanningInputController" + ".Instance";
            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                ProtocolNamespaceToken,
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                NetworkManagerSingletonToken,
                MapRendererSingletonToken,
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
            Assert.That(installer, Does.Contain("RegisterComponentOnNewGameObject<CityCoreHpBarOverlayController>"));
            Assert.That(installer, Does.Contain("RegisterComponentOnNewGameObject<BuildingConstructionOverlayController>"));
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
                ProtocolNamespaceToken,
                "GameStateCache",
                "AppManager.Instance",
                "ConfigCache.Instance",
                "ConfigCache.EnsureInstance",
                "UnitCache.Instance",
                NetworkManagerSingletonToken);

            Assert.That(offenders, Is.Empty, "MapRenderer must receive runtime dependencies from VContainer and render authoritative game state from GameStateStore.");

            var mapRenderer = File.ReadAllText(rendererPath);
            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            Assert.That(mapRenderer, Does.Contain("GameStateStore"));
            Assert.That(mapRenderer, Does.Contain("AppManager appManager"));
            Assert.That(mapRenderer, Does.Contain("ConfigCache configCache"));
            Assert.That(mapRenderer, Does.Contain("UnitCache unitCache"));
            Assert.That(mapRenderer, Does.Contain("ErrorToast errorToast"));
            Assert.That(mapRenderer, Does.Contain("[Inject]"));
            Assert.That(mapRenderer, Does.Contain(".State.Subscribe"));
            Assert.That(installer, Does.Contain("RegisterComponentInHierarchy<MapRenderer>"));
            Assert.That(installer, Does.Contain("RegisterComponentOnNewGameObject<UnitCache>"));
        }

        [Test]
        public void GameMapToastCallers_ShouldUseInjectedProjectOverlay()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/Game/GameSceneController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapRenderer.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapPlanningInputController.cs")
            };

            var offenders = FindTokenOffenders(roots, "*.cs", "ErrorToast.Instance");

            Assert.That(offenders, Is.Empty, "Game/Map presentation code must receive ErrorToast from Project scope instead of global singleton lookup.");

            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            var projectScope = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ProjectLifetimeScope.cs"));
            var bootstrap = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/PanoptesCompositionBootstrap.cs"));
            Assert.That(installer, Does.Contain("RegisterComponent(errorToast)"));
            Assert.That(projectScope, Does.Contain("ProjectOverlayRegistry"));
            Assert.That(bootstrap, Does.Contain("EnsureProjectOverlay<ErrorToast>"));
        }

        [Test]
        public void SettlementPlaybackController_ShouldUseFinalStoreAndInjectedMapRenderer()
        {
            var playbackPath = ResolveAssetPath("Scripts/Runtime/Presentation/Map/SettlementPlaybackController.cs");
            var dynamicCreationRoots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapRenderer.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/Game/GameSceneController.cs"),
                ResolveAssetPath("Scripts/Runtime/Core/Application/Handler/GameMessageHandler.cs")
            };
            var offenders = FindTokenOffenders(
                new[] { playbackPath },
                "*.cs",
                ProtocolNamespaceToken,
                NetworkManagerSingletonToken,
                "GameStateCache",
                "OnTurnSettled",
                MapRendererSingletonToken,
                "EnsureInstance");

            Assert.That(offenders, Is.Empty, "Settlement playback must consume SettlementStore and injected MapRenderer only.");

            var dynamicCreationOffenders = FindTokenOffenders(
                dynamicCreationRoots,
                "*.cs",
                "SettlementPlaybackController.EnsureInstance",
                "EnsureRuntimeComponent<SettlementPlaybackController>",
                "EnsureSettlementPlaybackController",
                "AddComponent<SettlementPlaybackController>");

            Assert.That(dynamicCreationOffenders, Is.Empty, "Settlement playback must be authored and VContainer-owned, not dynamically created.");

            var playback = File.ReadAllText(playbackPath);
            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            var gameScene = File.ReadAllText(ResolveAssetPath("Scenes/Game.unity"));
            Assert.That(playback, Does.Contain("SettlementStore"));
            Assert.That(playback, Does.Contain("MapRenderer _mapRenderer"));
            Assert.That(playback, Does.Contain("[Inject]"));
            Assert.That(playback, Does.Contain(".State.Subscribe"));
            Assert.That(playback, Does.Contain("state.Sequence"));
            Assert.That(installer, Does.Contain("RegisterComponentInHierarchy<SettlementPlaybackController>"));
            Assert.That(gameScene, Does.Contain("SettlementPlaybackController"));
            Assert.That(gameScene, Does.Contain("8d4f90e3c7b24a3ea9822cf7d9709b54"));
        }

        [Test]
        public void CinemachineMapCameraController_ShouldUseInjectedMapRenderer()
        {
            var cameraPath = ResolveAssetPath("Scripts/Runtime/Presentation/Map/CinemachineMapCameraController.cs");
            var rendererPath = ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapRenderer.cs");
            var offenders = FindTokenOffenders(
                new[] { cameraPath },
                "*.cs",
                ProtocolNamespaceToken,
                NetworkManagerSingletonToken,
                MapRendererSingletonToken,
                SceneObjectFinderMapRendererToken,
                FindAnyMapRendererToken,
                FindFirstMapRendererToken,
                FindObjectOfTypeMapRendererToken,
                "FindAnyObjectByType",
                "FindFirstObjectByType",
                "FindObjectOfType");

            Assert.That(offenders, Is.Empty, "Strategic camera must receive MapRenderer from VContainer instead of singleton or scene lookup.");

            var camera = File.ReadAllText(cameraPath);
            var renderer = File.ReadAllText(rendererPath);
            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            var gameScene = File.ReadAllText(ResolveAssetPath("Scenes/Game.unity"));

            Assert.That(camera, Does.Contain("[Inject]"));
            Assert.That(camera, Does.Contain("MapRenderer _mapRenderer"));
            Assert.That(camera, Does.Contain("ConfigureMapRenderer(mapRenderer)"));
            Assert.That(camera, Does.Contain("_mapRenderer.CameraContextReady +="));
            Assert.That(camera, Does.Not.Contain("FindAnyObjectByType<CinemachineCamera>"));
            Assert.That(renderer, Does.Not.Contain("AddComponent<CinemachineMapCameraController>"));
            Assert.That(renderer, Does.Not.Contain("GameObject.Find(\"CameraAnchor\")"));
            Assert.That(installer, Does.Contain("RegisterComponentInHierarchy<CinemachineMapCameraController>"));
            Assert.That(gameScene, Does.Contain("CameraAnchor"));
            Assert.That(gameScene, Does.Contain("CinemachineMapCameraController"));
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
                ProtocolNamespaceToken,
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                "ActionLock.",
                NetworkManagerSingletonToken,
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
                ProtocolNamespaceToken,
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                NetworkManagerSingletonToken,
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
                ProtocolNamespaceToken,
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                NetworkManagerSingletonToken,
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
                ProtocolNamespaceToken,
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                NetworkManagerSingletonToken,
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
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapTerritoryHighlightPresenter.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/InputAdapter/MapSelectionSurface.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapUnitDamagePopupPresenter.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                MapRendererSingletonToken,
                SceneObjectFinderMapRendererToken,
                FindAnyMapRendererToken,
                FindFirstMapRendererToken,
                FindObjectOfTypeMapRendererToken,
                NetworkManagerSingletonToken,
                ProtocolNamespaceToken);

            Assert.That(offenders, Is.Empty, "Map planning input presenters must receive MapRenderer explicitly instead of looking up singleton or scene fallbacks.");

            var controller = File.ReadAllText(roots[0]);
            Assert.That(controller, Does.Contain("MapRenderer mapRenderer"));
            Assert.That(controller, Does.Contain("ConfigureMapRenderer(mapRenderer)"));
            Assert.That(controller, Does.Contain("if (isActiveAndEnabled)"));
            Assert.That(controller, Does.Contain("SubscribeStoreEvents();"));
            Assert.That(controller, Does.Contain("_mapRenderer"));
            Assert.That(controller, Does.Contain("[Inject]"));
            Assert.That(controller, Does.Contain("new MapSelectionSurface(_pointerInput"));
            Assert.That(controller, Does.Contain("selectionSurface.SetMapRenderer(mapRenderer)"));
            Assert.That(controller, Does.Contain("_unitDamagePopups.SetMapRenderer(mapRenderer)"));

            var buildPlacementSession = File.ReadAllText(roots[1]);
            var previewPresentation = File.ReadAllText(roots[2]);
            var previewOverlay = File.ReadAllText(roots[3]);
            var pathOverlay = File.ReadAllText(roots[4]);
            var pendingDeploy = File.ReadAllText(roots[5]);
            var territoryHighlight = File.ReadAllText(roots[6]);
            var selectionSurface = File.ReadAllText(roots[7]);
            var damagePopups = File.ReadAllText(roots[8]);
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
            Assert.That(selectionSurface, Does.Contain("MapRenderer _mapRenderer"));
            Assert.That(selectionSurface, Does.Contain("SetMapRenderer(MapRenderer mapRenderer)"));
            Assert.That(damagePopups, Does.Contain("MapRenderer _mapRenderer"));
            Assert.That(damagePopups, Does.Contain("SetMapRenderer(MapRenderer mapRenderer)"));
        }

        [Test]
        public void MapDebugLocalSpawnHotkey_ShouldStayRemoved()
        {
            Assert.That(
                File.Exists(ResolveAssetPath("Scripts/Runtime/Presentation/Map/DebugUnitSpawnHotkey.cs")),
                Is.False,
                "Local debug unit spawning mutates client-only map state and must not return to final Presentation.");
        }

        [Test]
        public void MapRuntimeViews_ShouldReceiveLocalPlayerContextFromRenderer()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/NodeView.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/UnitView.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/BuildingView.cs")
            };
            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "GameStateCache",
                "PlanningDraftCache",
                "StaticCatalogCache",
                MapRendererSingletonToken,
                NetworkManagerSingletonToken,
                ProtocolNamespaceToken);

            Assert.That(offenders, Is.Empty, "Runtime map views must receive display context from MapRenderer, not legacy caches.");

            var renderer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapRenderer.cs"));
            var nodeView = File.ReadAllText(roots[0]);
            var unitView = File.ReadAllText(roots[1]);
            var buildingView = File.ReadAllText(roots[2]);
            Assert.That(renderer, Does.Contain("GetLocalPlayerId()"));
            Assert.That(renderer, Does.Contain("tile.SetLocalPlayerId(GetLocalPlayerId())"));
            Assert.That(renderer, Does.Contain("tile.SetBuildingCatalog(GetBuildingCatalog())"));
            Assert.That(renderer, Does.Contain("instance.SetLocalPlayerId(GetLocalPlayerId())"));
            Assert.That(nodeView, Does.Contain("SetLocalPlayerId(string localPlayerId)"));
            Assert.That(nodeView, Does.Contain("SetBuildingCatalog(IReadOnlyDictionary<string, CatalogBuildingDto> buildingCatalog)"));
            Assert.That(nodeView, Does.Contain("_buildingInstance.SetLocalPlayerId(_localPlayerId)"));
            Assert.That(unitView, Does.Contain("SetLocalPlayerId(string localPlayerId)"));
            Assert.That(buildingView, Does.Contain("SetLocalPlayerId(string localPlayerId)"));
        }

        [Test]
        public void MapStaticCatalogSources_ShouldFlowThroughStoreReadModel()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapRenderer.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapSourceResolver.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapRenderTokens.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapNodeInfoProxyFactory.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/Game/GameSceneController.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "StaticCatalogCache",
                "GameStateCache",
                "PlanningDraftCache",
                MapRendererSingletonToken,
                NetworkManagerSingletonToken,
                ProtocolNamespaceToken);

            Assert.That(offenders, Is.Empty, "Migrated map/UI catalog reads must flow through Store read models.");

            var state = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Core/Application/Stores/StaticCatalogState.cs"));
            var hydrator = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Core/Application/Stores/StaticCatalogStoreHydrator.cs"));
            var renderer = File.ReadAllText(roots[0]);
            Assert.That(state, Does.Contain("CatalogMapRuntimeBundleDto DefaultMap"));
            Assert.That(hydrator, Does.Contain("defaultMap: MapRuntimeBundle(defaultMap)"));
            Assert.That(renderer, Does.Contain("_staticCatalogStore?.Snapshot?.DefaultMap"));
        }

        [Test]
        public void MapAnimationAndPreview_ShouldReceiveRendererFromComposition()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/Animation/AnimationQueue.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Planning/Feedback/MovePreviewGhostPresenter.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapPlanningInputController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/SettlementPlaybackController.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "AnimationQueue.Instance",
                MapRendererSingletonToken,
                "new GameObject(\"AnimationQueue\")");

            Assert.That(offenders, Is.Empty, "Animation playback and move previews must receive scene dependencies from composition.");

            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            var queue = File.ReadAllText(roots[0]);
            var presenter = File.ReadAllText(roots[1]);
            Assert.That(installer, Does.Contain("RegisterComponentOnNewGameObject<AnimationQueue>"));
            Assert.That(queue, Does.Contain("Construct(MapRenderer mapRenderer)"));
            Assert.That(presenter, Does.Contain("SetMapRenderer(MapRenderer mapRenderer)"));
        }

        [Test]
        public void MapRenderer_ShouldNotCreateOrFindPresentationControllers()
        {
            var renderer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapRenderer.cs"));

            Assert.That(renderer, Does.Not.Contain("EnsureRuntimeControllers"));
            Assert.That(renderer, Does.Not.Contain("autoEnsureRuntimeControllers"));
            Assert.That(renderer, Does.Not.Contain("SceneObjectFinder"));
            Assert.That(renderer, Does.Not.Contain("FindAnyObjectByType"));
            Assert.That(renderer, Does.Not.Contain("MapPlanningInputController"));
            Assert.That(renderer, Does.Not.Contain("UnitInfoPanelController"));
            Assert.That(renderer, Does.Not.Contain("DisableGameplayInput"));
        }

        [Test]
        public void UnitInfoHudActions_ShouldUseInjectedReferences()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapPlanningInputController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoPanelController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/UnitInfoActionProviderBase.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/UI/HUD/CityCoreBuildingActionRegistrar.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "SceneObjectFinder",
                "FindAnyObjectByType",
                "FindFirstObjectByType",
                "FindObjectOfType");

            Assert.That(offenders, Is.Empty, "Unit info HUD/action providers must receive references from composition or local components.");

            var mapInput = File.ReadAllText(roots[0]);
            var panel = File.ReadAllText(roots[1]);
            var registrar = File.ReadAllText(roots[3]);
            Assert.That(mapInput, Does.Contain("UnitSelectionChanged?.Invoke(unit)"));
            Assert.That(mapInput, Does.Not.Contain("NotifyUnitInfoPanel"));
            Assert.That(panel, Does.Contain("MapPlanningInputController injectedMapPlanningInputController"));
            Assert.That(registrar, Does.Contain("MapPlanningInputController injectedMapPlanningInputController"));
            Assert.That(registrar, Does.Contain("UnitInfoPanelController injectedUnitInfoPanelController"));
        }

        [Test]
        public void DamagePopupPresentation_ShouldUseCompositionController()
        {
            var roots = new[]
            {
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapUnitDamagePopupPresenter.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/MapPlanningInputController.cs"),
                ResolveAssetPath("Scripts/Runtime/Presentation/Map/SettlementPlaybackController.cs")
            };

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "SceneObjectFinder",
                "FindAnyObjectByType",
                "new GameObject(\"DamageNumberPopupController");

            Assert.That(offenders, Is.Empty, "Damage popup presentation should use the composed DamageNumberPopupController.");

            var installer = File.ReadAllText(ResolveAssetPath("Scripts/Runtime/Presentation/Composition/ClientCompositionInstaller.cs"));
            var presenter = File.ReadAllText(roots[0]);
            Assert.That(installer, Does.Contain("RegisterComponentOnNewGameObject<DamageNumberPopupController>"));
            Assert.That(presenter, Does.Contain("SetDamagePopupController(DamageNumberPopupController popupController)"));
        }

        [Test]
        public void PresentationRuntimeOutsideComposition_ShouldNotUseSceneLookup()
        {
            var roots = Directory.GetFiles(
                    ResolveAssetPath("Scripts/Runtime/Presentation"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Composition{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .ToArray();

            var offenders = FindTokenOffenders(
                roots,
                "*.cs",
                "SceneObjectFinder.Find",
                "FindAnyObjectByType",
                "FindFirstObjectByType",
                "FindObjectOfType");

            Assert.That(offenders, Is.Empty, "Scene lookup belongs at the composition boundary, not in runtime Presentation scripts.");
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

        private static string Token(params string[] parts)
        {
            return string.Concat(parts);
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
