using System;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Presentation.Binders.UiToolkit;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.Game;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.ViewModels;
using VContainer;
using VContainer.Unity;

namespace Panoptes.Presentation.Composition
{
    public static class ClientCompositionInstaller
    {
        public static void RegisterProject(
            IContainerBuilder builder,
            NetworkManager networkManager,
            MessageDispatcher messageDispatcher,
            SessionManager sessionManager)
        {
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

            builder.RegisterComponent(networkManager).AsSelf();
            builder.RegisterComponent(messageDispatcher).AsSelf();
            builder.RegisterComponent(sessionManager).AsSelf();
            builder.Register(_ => new AuthService(), Lifetime.Singleton).AsSelf();
            builder.Register<IClientMessageSender, NetworkMessageSender>(Lifetime.Singleton);
            builder.Register<StaticCatalogStore>(Lifetime.Singleton).AsSelf();
            builder.Register<StaticCatalogMessageHydrator>(Lifetime.Singleton).AsSelf();
            builder.RegisterBuildCallback(container => container.Resolve<StaticCatalogMessageHydrator>().Attach());
        }

        public static void RegisterGame(IContainerBuilder builder)
        {
            builder.Register<GameStateStore>(Lifetime.Singleton).AsSelf();
            builder.Register<PlanningDraftStore>(Lifetime.Singleton).AsSelf();
            builder.Register<SelectionStore>(Lifetime.Singleton).AsSelf();
            builder.Register<PlanningToolStore>(Lifetime.Singleton).AsSelf();
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
            builder.Register<StoreHydrationBootstrapSeeder>(Lifetime.Singleton).AsSelf();
            builder.Register<StaticCatalogLegacyHydrationBridge>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentInHierarchy<GameSceneController>();
            builder.RegisterComponentInHierarchy<MapPlanningInputController>();
            builder.Register<UnitInfoViewModel>(Lifetime.Singleton).AsSelf();
            builder.Register<PlanningToolViewModel>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentInHierarchy<UnitInfoPanelController>();
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
            builder.RegisterBuildCallback(container => container.Resolve<StoreHydrationBootstrapSeeder>().SeedFromDefaultCaches());
            builder.RegisterBuildCallback(container => container.Resolve<StaticCatalogLegacyHydrationBridge>().AttachToDefaultCache());
            builder.RegisterBuildCallback(container => container.Resolve<TurnSummaryUiToolkitBinder>());
            builder.RegisterBuildCallback(container => container.Resolve<BuildCatalogUiToolkitBinder>());
            builder.RegisterBuildCallback(container => container.Resolve<TechTreeUiToolkitBinder>());
            builder.RegisterBuildCallback(container => container.Resolve<RecipeSynthesisUiToolkitBinder>());
            builder.RegisterBuildCallback(container => container.Resolve<MinisterReportUiToolkitBinder>());
            builder.RegisterBuildCallback(container => container.Resolve<PolicyFocusUiToolkitBinder>());
            builder.RegisterBuildCallback(container => container.Resolve<NationalLedgerUiToolkitBinder>());
        }
    }
}
