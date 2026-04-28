/*************************************************
 * Project: Panoptes
 * File: TechTreePanelBootstrap.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Auto-binds TechTreePanelController at runtime.
 *************************************************/

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
            var rects = Resources.FindObjectsOfTypeAll<RectTransform>();
            for (var i = 0; i < rects.Length; i++)
            {
                var rect = rects[i];
                if (rect == null)
                {
                    continue;
                }

                if (!rect.gameObject.scene.IsValid())
                {
                    continue;
                }

                var name = rect.name ?? string.Empty;
                if (!string.Equals(name, TargetPanelName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Tech tree controller must live on panel root only.
                if (rect.parent != null)
                {
                    continue;
                }

                if (rect.GetComponent<TechTreePanelController>() != null)
                {
                    continue;
                }

                rect.gameObject.AddComponent<TechTreePanelController>();
            }
        }
    }
}
