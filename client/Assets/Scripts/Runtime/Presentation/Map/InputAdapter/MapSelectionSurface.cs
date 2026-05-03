/*************************************************
 * Project: Panoptes
 * File: MapSelectionSurface.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Map raycast adapter that resolves nodes and units from Unity physics hits.
 *************************************************/

using Panoptes.Core.Domain;
using UnityEngine;

namespace Panoptes.Presentation.Map.InputAdapter
{
    /// <summary>
    /// Converts pointer raycasts into map presentation objects without making command decisions.
    /// </summary>
    public sealed class MapSelectionSurface : IMapSelectionSurface
    {
        private readonly MapPointerInput _pointerInput;
        private readonly System.Func<Camera> _cameraProvider;
        private readonly System.Func<LayerMask> _maskProvider;
        private readonly System.Func<float> _distanceProvider;
        private MapRenderer _mapRenderer;

        public MapSelectionSurface(
            MapPointerInput pointerInput,
            System.Func<Camera> cameraProvider,
            System.Func<LayerMask> maskProvider,
            System.Func<float> distanceProvider,
            MapRenderer mapRenderer)
        {
            _pointerInput = pointerInput;
            _cameraProvider = cameraProvider;
            _maskProvider = maskProvider;
            _distanceProvider = distanceProvider;
            _mapRenderer = mapRenderer;
        }

        public void SetMapRenderer(MapRenderer mapRenderer)
        {
            _mapRenderer = mapRenderer;
        }

        public bool TryRaycastNode(out NodeView nodeView)
        {
            nodeView = null;
            if (!TryRaycast(out var hit))
            {
                return false;
            }

            var map = _mapRenderer;
            return map != null && map.TryGetNodeViewByWorld(hit.point, out nodeView) && nodeView != null;
        }

        public bool TryRaycastUnit(out UnitView unitView)
        {
            unitView = null;
            if (!TryRaycast(out var hit))
            {
                return false;
            }

            if (hit.collider.GetComponentInParent<MoveGhostTag>() != null)
            {
                return false;
            }

            unitView = hit.collider.GetComponentInParent<UnitView>();
            return unitView != null;
        }

        public bool TryGetClickedNodeContext(out NodeView nodeView, out NodeDto nodeState)
        {
            nodeView = null;
            nodeState = null;

            var map = _mapRenderer;
            if (map == null)
            {
                return false;
            }

            if (TryRaycastNode(out nodeView)
                && nodeView != null
                && !string.IsNullOrWhiteSpace(nodeView.NodeId)
                && map.TryGetNodeState(nodeView.NodeId, out nodeState)
                && nodeState != null)
            {
                return true;
            }

            if (!TryRaycastUnit(out var unit) || unit == null)
            {
                return false;
            }

            if (!map.TryGetNodeIdByGrid(unit.GridPos, out var nodeId) || string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            if (!map.TryGetNodeView(nodeId, out nodeView) || nodeView == null)
            {
                return false;
            }

            return map.TryGetNodeState(nodeId, out nodeState) && nodeState != null;
        }

        private bool TryRaycast(out RaycastHit hit)
        {
            hit = default;

            var camera = _cameraProvider?.Invoke();
            if (camera == null)
            {
                return false;
            }

            var ray = camera.ScreenPointToRay(_pointerInput.GetPointerPosition());
            return Physics.Raycast(ray, out hit, _distanceProvider(), _maskProvider(), QueryTriggerInteraction.Ignore);
        }
    }
}
