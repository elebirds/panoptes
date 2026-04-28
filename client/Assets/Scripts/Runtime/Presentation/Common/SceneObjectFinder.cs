using System;
using UnityEngine;

namespace Panoptes.Presentation.Common
{
    public static class SceneObjectFinder
    {
        public static T FindFirstSceneObject<T>(Predicate<T> predicate = null) where T : Component
        {
            var candidates = Resources.FindObjectsOfTypeAll<T>();
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (!IsSceneObject(candidate) || predicate?.Invoke(candidate) == false)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        public static RectTransform FindSceneRectByName(params string[] names)
        {
            return FindFirstSceneObject<RectTransform>(rect =>
            {
                if (string.IsNullOrWhiteSpace(rect.name))
                {
                    return false;
                }

                for (var i = 0; i < names.Length; i++)
                {
                    if (string.Equals(rect.name, names[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            });
        }

        public static bool IsSceneObject(Component component)
        {
            return component != null && component.gameObject.scene.IsValid();
        }
    }
}
