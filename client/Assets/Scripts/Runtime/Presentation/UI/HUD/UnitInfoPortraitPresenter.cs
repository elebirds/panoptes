using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    public static class UnitInfoPortraitPresenter
    {
        public static bool TryComputeUnitBounds(UnitView unit, out Bounds bounds)
        {
            bounds = default;
            if (unit == null)
            {
                return false;
            }

            var renderers = unit.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            var hasBounds = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }
    }
}
