using System;
using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Feedback;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Presentation.Binders.UiToolkit;
using Panoptes.Presentation.Animation;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.Common;
using Panoptes.Presentation.UI.Auth;
using Panoptes.Presentation.UI.Game;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.UI.Lobby;
using Panoptes.Presentation.UI.Turn;
using Panoptes.Presentation.ViewModels;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Panoptes.Presentation.Composition
{
    public static class ClientCompositionInstaller
    {
        public static void RegisterProject(
            IContainerBuilder builder,
            AppManager appManager,
            NetworkManager networkManager,
            MessageDispatcher messageDispatcher,
            SessionManager sessionManager,
            ConfigCache configCache,
            ClientRuntimeConfigCache clientRuntimeConfigCache,
            StaticCatalogCache staticCatalogCache,
            RoomCache roomCache,
            GameStateCache gameStateCache,
            GameChatCache gameChatCache,
            PlanningDraftCache planningDraftCache,
            LoadingOverlay loadingOverlay,
            ErrorToast errorToast,
            ConfirmDialog confirmDialog)
        {
            if (appManager == null)
            {
                throw new ArgumentNullException(nameof(appManager));
            }

            if (networkManager == null)
            {
                throw new ArgumentNullException(nameof(networkManager));
            }

            if (messageDispatcher == null)
            {
                throw new ArgumentNullException(nameof(messageDispatcher));
            }

            if (sessionManager == null)
            {
                throw new ArgumentNullException(nameof(sessionManager));
            }

            if (configCache == null)
            {
                throw new ArgumentNullException(nameof(configCache));
            }

            if (clientRuntimeConfigCache == null)
            {
                throw new ArgumentNullException(nameof(clientRuntimeConfigCache));
            }

            if (staticCatalogCache == null)
            {
                throw new ArgumentNullException(nameof(staticCatalogCache));
            }

            if (roomCache == null)
            {
                throw new ArgumentNullException(nameof(roomCache));
            }

            if (gameStateCache == null)
            {
                throw new ArgumentNullException(nameof(gameStateCache));
            }

            if (gameChatCache == null)
            {
                throw new ArgumentNullException(nameof(gameChatCache));
            }

            if (planningDraftCache == null)
            {
                throw new ArgumentNullException(nameof(planningDraftCache));
            }

            if (loadingOverlay == null)
            {
                throw new ArgumentNullException(nameof(loadingOverlay));
            }

            if (errorToast == null)
            {
                throw new ArgumentNullException(nameof(errorToast));
            }

            if (confirmDialog == null)
            {
                throw new ArgumentNullException(nameof(confirmDialog));
            }

            var messageSender = new NetworkMessageSender(networkManager);
            roomCache.UseSessionManager(sessionManager);
            gameStateCache.UseProjectCaches(staticCatalogCache, planningDraftCache, gameChatCache);
            messageDispatcher.UseGameEventSessionGate(new GameEventSessionGate(gameStateCache));
            appManager.UseProjectServices(
                networkManager,
                messageDispatcher,
                sessionManager,
                clientRuntimeConfigCache,
                configCache,
                staticCatalogCache,
                roomCache,
                gameStateCache,
                gameChatCache,
                messageSender);
            networkManager.UseProjectServices(
                sessionManager,
                messageDispatcher,
                roomCache,
                clientRuntimeConfigCache,
                configCache,
                gameStateCache,
                appManager,
                loadingOverlay);
            builder.RegisterComponent(appManager).AsSelf();
            builder.RegisterComponent(networkManager).AsSelf();
            builder.RegisterComponent(messageDispatcher).AsSelf();
            builder.RegisterComponent(sessionManager).AsSelf();
            builder.RegisterComponent(configCache).AsSelf();
            builder.RegisterComponent(clientRuntimeConfigCache).AsSelf();
            builder.RegisterComponent(staticCatalogCache).AsSelf();
            builder.RegisterComponent(roomCache).AsSelf();
            builder.RegisterComponent(gameStateCache).AsSelf();
            builder.RegisterComponent(gameChatCache).AsSelf();
            builder.RegisterComponent(loadingOverlay).AsSelf().As<ILoadingOverlayPresenter>();
            builder.RegisterComponent(errorToast).AsSelf();
            builder.RegisterComponent(confirmDialog).AsSelf();
            builder.Register(_ => new AuthService(networkManager.ServerUrl), Lifetime.Singleton).AsSelf();
            builder.RegisterInstance(messageSender).As<IClientMessageSender>();
            builder.Register<StaticCatalogStore>(Lifetime.Singleton).AsSelf();
            builder.Register<StaticCatalogStoreHydrator>(Lifetime.Singleton).AsSelf();
            builder.RegisterBuildCallback(container => container.Resolve<AppManager>().UseStaticCatalogStoreHydrator(container.Resolve<StaticCatalogStoreHydrator>()));
        }

        public static void RegisterAuth(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<LoginPanel>();
        }

        public static void RegisterLobby(IContainerBuilder builder)
        {
            builder.Register<LobbyService>(Lifetime.Scoped).AsSelf();
            builder.RegisterComponentInHierarchy<LobbySceneController>();
            builder.RegisterComponentInHierarchy<LobbyPanelController>();
            builder.RegisterComponentInHierarchy<RoomPanelController>();
        }

        public static void RegisterGame(IContainerBuilder builder)
        {
            builder.Register<GameStateStore>(Lifetime.Singleton).AsSelf();
            builder.Register<PlanningDraftStore>(Lifetime.Singleton).AsSelf();
            builder.Register<SelectionStore>(Lifetime.Singleton).AsSelf();
            builder.Register<PlanningToolStore>(Lifetime.Singleton).AsSelf();
            builder.Register<ActionLockStore>(Lifetime.Singleton).AsSelf();
            builder.Register<TurnStore>(Lifetime.Singleton).AsSelf();
            builder.Register<GameChatStore>(Lifetime.Singleton).AsSelf();
            builder.Register<GameOverStore>(Lifetime.Singleton).AsSelf();
            builder.Register<SettlementStore>(Lifetime.Singleton).AsSelf();
            builder.Register<GameplayFeedbackStore>(Lifetime.Singleton).AsSelf();
            builder.Register<SelectionService>(Lifetime.Singleton).AsSelf();
            builder.Register<PlanningToolService>(Lifetime.Singleton).AsSelf();
            builder.Register<GameIntentService>(Lifetime.Singleton).AsSelf();
            builder.Register<PlanningIntentService>(Lifetime.Singleton).AsSelf();
            builder.Register<MinisterCommandService>(Lifetime.Singleton).AsSelf();
            builder.Register<LocalGameSessionResetService>(Lifetime.Singleton).AsSelf();
            builder.Register<StoreHydrationHelper>(Lifetime.Singleton).AsSelf();
            builder.Register<StoreMessageHydrator>(Lifetime.Singleton).AsSelf();
            builder.Register<UnitCache>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentInHierarchy<GameSceneController>();
            builder.RegisterComponentInHierarchy<MapRenderer>();
            builder.RegisterComponentInNewPrefab(
                LoadRequiredComponent<AnimationQueue>("Prefabs/Runtime/AnimationQueue"),
                Lifetime.Singleton);
            builder.RegisterComponentInNewPrefab(
                LoadRequiredComponent<DamageNumberPopupController>("Prefabs/UI/DamageNumberPopupController"),
                Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<SettlementPlaybackController>();
            builder.RegisterComponentInHierarchy<CinemachineMapCameraController>();
            builder.RegisterComponentInHierarchy<MapPlanningInputController>();
            builder.Register<TokenHudViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<ResourceHudViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<ManagementPanelVisibilityStore>(Lifetime.Singleton).AsSelf();
            builder.Register<NationalOverviewViewModel>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentInNewPrefab(
                LoadRequiredComponent<ManagementHostUiToolkitBinder>("Prefabs/UI/ManagementHost"),
                Lifetime.Singleton);
            builder.RegisterComponentInNewPrefab(
                LoadRequiredComponent<TokenHUD>("Prefabs/UI/TokenHUD"),
                Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<ResourceHUD>();
            builder.RegisterComponentInHierarchy<TurnHUD>();
            builder.RegisterComponentInNewPrefab(
                LoadRequiredComponent<CityCoreHpBarOverlayController>("Prefabs/UI/CityCoreHpBarOverlay"),
                Lifetime.Singleton);
            builder.RegisterComponentInNewPrefab(
                LoadRequiredComponent<BuildingConstructionOverlayController>("Prefabs/UI/BuildingConstructionOverlay"),
                Lifetime.Singleton);
            builder.RegisterComponentInNewPrefab(
                LoadRequiredComponent<GameChatPanelController>("Prefabs/UI/GameChatPanel"),
                Lifetime.Singleton);
            builder.RegisterComponentInNewPrefab(
                LoadRequiredComponent<SettlementTimeline>("Prefabs/UI/SettlementTimeline"),
                Lifetime.Singleton);
            builder.RegisterComponentInNewPrefab(
                LoadRequiredComponent<TurnReportPanel>("Prefabs/UI/TurnReportPanel"),
                Lifetime.Singleton);
            builder.RegisterComponentInNewPrefab(
                LoadRequiredComponent<GameOverOverlay>("Prefabs/UI/GameOverOverlay"),
                Lifetime.Singleton);
            builder.Register<UnitInfoViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<PlanningToolViewModel>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentInHierarchy<UnitInfoPanelController>();
            builder.RegisterComponentInHierarchy<CityCoreBuildingActionRegistrar>();
            builder.RegisterComponentInHierarchy<SettlerUnitActionRegistrar>();
            builder.Register<TurnSummaryViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<BuildCatalogViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<BuildCatalogContextStore>(Lifetime.Singleton).AsSelf();
            builder.Register<TechTreeViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<RecipeSynthesisContextStore>(Lifetime.Singleton).AsSelf();
            builder.Register<RecipeSynthesisViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<MinisterReportViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<PolicyFocusViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<NationalLedgerViewModel>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentOnNewGameObject<TurnSummaryUiToolkitBinder>(
                Lifetime.Singleton,
                "Turn Summary UI Toolkit");
            builder.RegisterComponentInNewPrefab(
                LoadRequiredComponent<BuildCatalogUiToolkitBinder>("Prefabs/UI/BuildCatalog"),
                Lifetime.Singleton);
            builder.RegisterComponentOnNewGameObject<TechTreeUiToolkitBinder>(
                Lifetime.Singleton,
                "Tech Tree UI Toolkit");
            builder.RegisterComponentInNewPrefab(
                LoadRequiredComponent<RecipeSynthesisUiToolkitBinder>("Prefabs/UI/RecipeSynthesis"),
                Lifetime.Singleton);
            builder.RegisterComponentOnNewGameObject<MinisterReportUiToolkitBinder>(
                Lifetime.Singleton,
                "Minister Report UI Toolkit");
            builder.RegisterComponentOnNewGameObject<PolicyFocusUiToolkitBinder>(
                Lifetime.Singleton,
                "Policy Focus UI Toolkit");
            builder.RegisterComponentOnNewGameObject<NationalLedgerUiToolkitBinder>(
                Lifetime.Singleton,
                "National Ledger UI Toolkit");
            builder.RegisterBuildCallback(container => container.Resolve<StoreMessageHydrator>().Attach());
            builder.RegisterBuildCallback(container => container.Resolve<TurnSummaryUiToolkitBinder>());
            builder.RegisterBuildCallback(container => container.Resolve<BuildCatalogUiToolkitBinder>());
            builder.RegisterBuildCallback(container => container.Resolve<TechTreeUiToolkitBinder>());
            builder.RegisterBuildCallback(container => container.Resolve<RecipeSynthesisUiToolkitBinder>());
            builder.RegisterBuildCallback(container => container.Resolve<MinisterReportUiToolkitBinder>());
            builder.RegisterBuildCallback(container => container.Resolve<PolicyFocusUiToolkitBinder>());
            builder.RegisterBuildCallback(container => container.Resolve<NationalLedgerUiToolkitBinder>());
        }

        private static T LoadRequiredComponent<T>(string resourcePath) where T : Component
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Missing prefab at Resources/{resourcePath}.prefab");
            }

            var component = prefab.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException($"Prefab Resources/{resourcePath}.prefab is missing {typeof(T).Name}.");
            }

            return component;
        }
    }
}
