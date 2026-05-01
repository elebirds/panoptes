using Panoptes.Core.Application.App;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Core.Infrastructure.Service;
using UnityEngine;

namespace Panoptes.Presentation.Composition
{
    public static class PanoptesCompositionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureProjectScope()
        {
            var managers = GameObject.Find("Managers");
            if (managers == null)
            {
                managers = new GameObject("Managers");
            }

            Object.DontDestroyOnLoad(managers);
            EnsureComponent<AppManager>(managers);
            EnsureComponent<NetworkManager>(managers);
            EnsureComponent<MessageDispatcher>(managers);
            EnsureComponent<SessionManager>(managers);
            EnsureComponent<ProjectLifetimeScope>(managers);
        }

        private static void EnsureComponent<T>(GameObject owner) where T : Component
        {
            if (owner.GetComponent<T>() == null)
            {
                owner.AddComponent<T>();
            }
        }
    }
}
