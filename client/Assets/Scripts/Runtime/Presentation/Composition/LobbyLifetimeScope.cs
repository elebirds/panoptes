using VContainer;
using VContainer.Unity;

namespace Panoptes.Presentation.Composition
{
    public sealed class LobbyLifetimeScope : LifetimeScope
    {
        protected override LifetimeScope FindParent()
        {
            return Find<ProjectLifetimeScope>();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            ClientCompositionInstaller.RegisterLobby(builder);
        }
    }
}
