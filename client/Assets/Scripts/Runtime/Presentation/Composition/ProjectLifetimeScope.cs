using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using VContainer;
using VContainer.Unity;

namespace Panoptes.Presentation.Composition
{
    [UnityEngine.RequireComponent(typeof(NetworkManager))]
    [UnityEngine.RequireComponent(typeof(MessageDispatcher))]
    [UnityEngine.RequireComponent(typeof(SessionManager))]
    public sealed class ProjectLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            ClientCompositionInstaller.RegisterProject(
                builder,
                GetComponent<NetworkManager>(),
                GetComponent<MessageDispatcher>(),
                GetComponent<SessionManager>());
        }
    }
}
