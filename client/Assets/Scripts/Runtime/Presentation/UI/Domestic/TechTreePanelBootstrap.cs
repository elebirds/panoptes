/*************************************************
 * Project: Panoptes
 * File: TechTreePanelBootstrap.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Auto-binds TechTreePanelController at runtime.
 *************************************************/

using Panoptes.Presentation.Common;
using Panoptes.Presentation.Composition;
using UnityEngine;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class TechTreePanelBootstrap : MonoBehaviour
    {
        private const string TargetPanelName = "TechTreePanel";
        private float _nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindAnyObjectByType<TechTreePanelBootstrap>() != null)
            {
                return;
            }

            var go = new GameObject("TechTreePanelBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<TechTreePanelBootstrap>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScanTime)
            {
                return;
            }

            _nextScanTime = Time.unscaledTime + 0.5f;
            AttachControllers();
        }

        private static void AttachControllers()
        {
            while (true)
            {
                var rect = SceneObjectFinder.FindFirstSceneObject<RectTransform>(candidate =>
                {
                    var name = candidate.name ?? string.Empty;
                    return string.Equals(name, TargetPanelName, System.StringComparison.OrdinalIgnoreCase) &&
                        candidate.parent == null &&
                        candidate.GetComponent<TechTreePanelController>() == null;
                });

                if (rect == null)
                {
                    return;
                }

                var controller = rect.gameObject.AddComponent<TechTreePanelController>();
                SceneCommandServiceInjector.InjectIfAvailable(controller);
            }
        }
    }
}
