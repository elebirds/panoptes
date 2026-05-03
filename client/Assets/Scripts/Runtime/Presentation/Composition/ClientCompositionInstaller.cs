using System;
using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Presentation.Binders.UiToolkit;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.Domestic;
using Panoptes.Presentation.UI.Game;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.UI.Minister;
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
            SessionManager sessionManager)
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

            builder.RegisterComponent(appManager).AsSelf();
            builder.RegisterComponent(networkManager).AsSelf();
            builder.RegisterComponent(messageDispatcher).AsSelf();
            builder.RegisterComponent(sessionManager).AsSelf();
            builder.Register(_ => new AuthService(), Lifetime.Singleton).AsSelf();
            builder.Register<IClientMessageSender, NetworkMessageSender>(Lifetime.Singleton);
            builder.Register<StaticCatalogStore>(Lifetime.Singleton).AsSelf();
            builder.Register<StaticCatalogStoreHydrator>(Lifetime.Singleton).AsSelf();
            builder.RegisterBuildCallback(container => container.Resolve<AppManager>().UseStaticCatalogStoreHydrator(container.Resolve<StaticCatalogStoreHydrator>()));
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
            builder.RegisterComponentInHierarchy<GameSceneController>();
            builder.RegisterComponentInHierarchy<MapPlanningInputController>();
            builder.Register<TokenHudViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<ResourceHudViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<ManagementPanelVisibilityStore>(Lifetime.Singleton).AsSelf();
            RegisterRuntimeSceneComponent<TokenHUD>(builder, "TokenHUD");
            RegisterRuntimeSceneComponent<ResourceHUD>(builder, "ResourcePanel");
            RegisterRuntimeSceneComponent<TurnHUD>(builder, "TurnHUD");
            RegisterRuntimeSceneComponent<GameChatPanelController>(builder, "GameChatPanel");
            RegisterRuntimeSceneComponent<MinisterPanel>(builder, "MinisterPanel");
            RegisterRuntimeSceneComponent<SettlementTimeline>(builder, "SettlementTimeline");
            RegisterRuntimeSceneComponent<TurnReportPanel>(builder, "TurnReportPanel");
            RegisterRuntimeSceneComponent<GameOverOverlay>(builder, "GameOverOverlay");
            builder.Register<UnitInfoViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<PlanningToolViewModel>(Lifetime.Singleton).AsSelf();
            RegisterRuntimeSceneComponent<UnitInfoPanelController>(builder, "UnitInfoPanel");
            RegisterOptionalSceneComponent<RecipeSynthesisPanel>(builder);
            builder.Register<TurnSummaryViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<BuildCatalogViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<TechTreeViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<RecipeSynthesisViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<MinisterReportViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<PolicyFocusViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<NationalLedgerViewModel>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentOnNewGameObject<TurnSummaryUiToolkitBinder>(
                Lifetime.Singleton,
                "Turn Summary UI Toolkit");
            builder.RegisterComponentOnNewGameObject<BuildCatalogUiToolkitBinder>(
                Lifetime.Singleton,
                "Build Catalog UI Toolkit");
            builder.RegisterComponentOnNewGameObject<TechTreeUiToolkitBinder>(
                Lifetime.Singleton,
                "Tech Tree UI Toolkit");
            builder.RegisterComponentOnNewGameObject<RecipeSynthesisUiToolkitBinder>(
                Lifetime.Singleton,
                "Recipe Synthesis UI Toolkit");
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

        private static void RegisterRuntimeSceneComponent<T>(
            IContainerBuilder builder,
            string objectName)
            where T : Component
        {
            var component = SceneObjectFinder.FindFirstSceneObject<T>();
            var createdByComposition = component == null;
            if (component == null)
            {
                var go = new GameObject(objectName, typeof(RectTransform));
                go.SetActive(false);

                var canvasTransform = FindGameCanvasTransform();
                if (canvasTransform != null)
                {
                    go.transform.SetParent(canvasTransform, false);
                }

                component = go.AddComponent<T>();
            }

            builder.RegisterComponent(component).AsSelf();
            if (createdByComposition)
            {
                builder.RegisterBuildCallback(_ => component.gameObject.SetActive(true));
            }
        }

        private static void RegisterOptionalSceneComponent<T>(IContainerBuilder builder)
            where T : Component
        {
            var component = SceneObjectFinder.FindFirstSceneObject<T>();
            if (component != null)
            {
                builder.RegisterComponent(component).AsSelf();
            }
        }

        private static Transform FindGameCanvasTransform()
        {
            var canvas = SceneObjectFinder.FindFirstSceneObject<Canvas>();
            return canvas != null ? canvas.transform : null;
        }
    }
}
