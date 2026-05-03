/*************************************************
 * Project: Panoptes
 * File: BuildPlacementGhostPresenter.cs
 * Author: Panoptes Team
 * Date: 2026-05-02
 * Description: Runtime build placement ghost presenter for map planning feedback.
 *************************************************/

using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Presentation.Planning.Feedback
{
    /// <summary>
    /// Owns the transient building ghost shown while the player previews build placement.
    /// </summary>
    public sealed class BuildPlacementGhostPresenter
    {
        private BuildingView _hoverGhost;

        public bool HasGhost => _hoverGhost != null;

        public void Recreate(NodeView node, string buildingType, string ownerId, Color defaultColor)
        {
            Clear();
            if (node == null || string.IsNullOrEmpty(buildingType))
            {
                return;
            }

            var prefab = node.ResolveBuildingPrefab(buildingType);
            if (prefab == null || node.BuildingAnchor == null)
            {
                return;
            }

            _hoverGhost = Object.Instantiate(prefab, node.BuildingAnchor, false);
            _hoverGhost.SetBuildingType(buildingType);
            _hoverGhost.SetOwner(ownerId);
            _hoverGhost.SetPlacementGhost(true, defaultColor);
        }

        public void Render(Color color)
        {
            if (_hoverGhost != null)
            {
                _hoverGhost.SetPlacementGhost(true, color);
            }
        }

        public void Clear()
        {
            if (_hoverGhost == null)
            {
                return;
            }

            DestroyObject(_hoverGhost.gameObject);
            _hoverGhost = null;
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
