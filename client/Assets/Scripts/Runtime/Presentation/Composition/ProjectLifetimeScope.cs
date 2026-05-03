using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Presentation.UI.Common;
using VContainer;
using VContainer.Unity;

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
    [UnityEngine.RequireComponent(typeof(ProjectOverlayRegistry))]
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var overlays = GetComponent<ProjectOverlayRegistry>();
            ClientCompositionInstaller.RegisterProject(
                builder,
                GetComponent<AppManager>(),
                GetComponent<NetworkManager>(),
                GetComponent<MessageDispatcher>(),
                GetComponent<SessionManager>(),
                GetComponent<ConfigCache>(),
                GetComponent<ClientRuntimeConfigCache>(),
                GetComponent<StaticCatalogCache>(),
                GetComponent<RoomCache>(),
                GetComponent<GameStateCache>(),
                GetComponent<GameChatCache>(),
                overlays.ErrorToast,
                overlays.ConfirmDialog);
        }
    }
}
