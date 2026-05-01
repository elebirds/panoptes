using System;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.Binders.UiToolkit;
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
        }

        public static void RegisterGame(IContainerBuilder builder)
        {
            builder.Register<GameStateStore>(Lifetime.Singleton).AsSelf();
            builder.Register<PlanningDraftStore>(Lifetime.Singleton).AsSelf();
            builder.Register<SelectionStore>(Lifetime.Singleton).AsSelf();
            builder.Register<TurnStore>(Lifetime.Singleton).AsSelf();
            builder.Register<SelectionService>(Lifetime.Singleton).AsSelf();
            builder.Register<GameIntentService>(Lifetime.Singleton).AsSelf();
            builder.Register<PlanningIntentService>(Lifetime.Singleton).AsSelf();
            builder.Register<MinisterCommandService>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentInHierarchy<GameSceneController>();
            builder.RegisterComponentInHierarchy<MapPlanningInputController>();
            builder.Register<UnitInfoViewModel>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentInHierarchy<UnitInfoPanelController>();
            builder.Register<TurnSummaryViewModel>(Lifetime.Singleton).AsSelf();
            builder.RegisterComponentOnNewGameObject<TurnSummaryUiToolkitBinder>(
                Lifetime.Singleton,
                "Turn Summary UI Toolkit");
            builder.RegisterBuildCallback(container => container.Resolve<TurnSummaryUiToolkitBinder>());
        }
    }
}
