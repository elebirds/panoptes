using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Handler;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Presentation.UI.Common;
using VContainer;
using VContainer.Unity;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || PANOPTES_DEBUG_PANEL
using Panoptes.DebugTools;
#endif

namespace Panoptes.Presentation.Composition
{
    [UnityEngine.RequireComponent(typeof(NetworkManager))]
    [UnityEngine.RequireComponent(typeof(MessageDispatcher))]
    [UnityEngine.RequireComponent(typeof(SessionManager))]
    [UnityEngine.RequireComponent(typeof(AppManager))]
    [UnityEngine.RequireComponent(typeof(ConfigCache))]
    [UnityEngine.RequireComponent(typeof(ClientRuntimeConfigCache))]
    [UnityEngine.RequireComponent(typeof(StaticCatalogCache))]
    [UnityEngine.RequireComponent(typeof(RoomCache))]
    [UnityEngine.RequireComponent(typeof(GameStateCache))]
    [UnityEngine.RequireComponent(typeof(GameChatCache))]
    [UnityEngine.RequireComponent(typeof(PlanningDraftCache))]
    [UnityEngine.RequireComponent(typeof(LobbyMessageHandler))]
    [UnityEngine.RequireComponent(typeof(GameMessageHandler))]
    [UnityEngine.RequireComponent(typeof(LoadingOverlay))]
    [UnityEngine.RequireComponent(typeof(ProjectOverlayRegistry))]
#if UNITY_EDITOR || DEVELOPMENT_BUILD || PANOPTES_DEBUG_PANEL
    [UnityEngine.RequireComponent(typeof(DebugPanel))]
#endif
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var overlays = GetComponent<ProjectOverlayRegistry>();
            var messageDispatcher = GetComponent<MessageDispatcher>();
            var roomCache = GetComponent<RoomCache>();
            var gameStateCache = GetComponent<GameStateCache>();
            var gameChatCache = GetComponent<GameChatCache>();
            var planningDraftCache = GetComponent<PlanningDraftCache>();
            ClientCompositionInstaller.RegisterProject(
                builder,
                GetComponent<AppManager>(),
                GetComponent<NetworkManager>(),
                messageDispatcher,
                GetComponent<SessionManager>(),
                GetComponent<ConfigCache>(),
                GetComponent<ClientRuntimeConfigCache>(),
                GetComponent<StaticCatalogCache>(),
                roomCache,
                gameStateCache,
                gameChatCache,
                planningDraftCache,
                GetComponent<LoadingOverlay>(),
                overlays.ErrorToast,
                overlays.ConfirmDialog);
            GetComponent<LobbyMessageHandler>().UseProjectServices(messageDispatcher, roomCache);
            GetComponent<GameMessageHandler>().UseProjectServices(
                messageDispatcher,
                gameStateCache,
                planningDraftCache,
                gameChatCache,
                GetComponent<StaticCatalogCache>());
#if UNITY_EDITOR || DEVELOPMENT_BUILD || PANOPTES_DEBUG_PANEL
            builder.RegisterBuildCallback(container =>
            {
                var debugPanel = GetComponent<DebugPanel>();
                debugPanel.UseMessageSender(container.Resolve<IClientMessageSender>());
                debugPanel.UseRuntimeServices(
                    GetComponent<AppManager>(),
                    GetComponent<SessionManager>(),
                    roomCache,
                    gameStateCache,
                    GetComponent<ClientRuntimeConfigCache>(),
                    GetComponent<NetworkManager>());
            });
#endif
        }
    }
}
