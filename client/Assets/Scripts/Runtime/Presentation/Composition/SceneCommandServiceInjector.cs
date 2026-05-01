using UnityEngine;
using VContainer.Unity;

namespace Panoptes.Presentation.Composition
{
    public static class SceneCommandServiceInjector
    {
        public static void InjectIfAvailable(Component component)
        {
            if (component != null)
            {
                InjectIfAvailable(component.gameObject);
            }
        }

        public static void InjectIfAvailable(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            var scope = LifetimeScope.Find<GameLifetimeScope>();
            if (scope?.Container != null)
            {
                scope.Container.InjectGameObject(gameObject);
            }
        }
    }
}
