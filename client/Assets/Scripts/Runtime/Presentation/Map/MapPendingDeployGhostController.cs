using Panoptes.Core.Domain;
using Panoptes.Presentation.Planning.Feedback;
using Panoptes.Presentation.Planning.Input.State;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class MapPendingDeployGhostController
    {
        private readonly PendingDeployState _state = new();

        public int Count => _state.Count;

        public void ShowCityCoreGhost(
            string unitId,
            string centerNodeId,
            string ownerId,
            Color ghostColor)
        {
            var normalizedUnitId = Normalize(unitId);
            var normalizedNodeId = string.IsNullOrWhiteSpace(centerNodeId) ? string.Empty : centerNodeId.Trim();
            if (string.IsNullOrEmpty(normalizedUnitId) || string.IsNullOrEmpty(normalizedNodeId))
            {
                return;
            }

            if (_state.TryGetGhostNode(normalizedUnitId, out var oldNodeId) &&
                !string.IsNullOrWhiteSpace(oldNodeId) &&
                !string.Equals(oldNodeId, normalizedNodeId, System.StringComparison.Ordinal))
            {
                ClearGhostNode(oldNodeId);
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            if (!ShouldRenderPendingBuildGhost(normalizedNodeId, map))
            {
                _state.RemoveUnit(normalizedUnitId);
                return;
            }

            map.ApplyBuildingPlacement(normalizedNodeId, "city_core", ownerId, true, 100, ghostColor);
            _state.SetGhostNode(normalizedUnitId, normalizedNodeId);
        }

        public void ClearForUnit(string unitId)
        {
            var normalizedUnitId = Normalize(unitId);
            if (string.IsNullOrEmpty(normalizedUnitId))
            {
                return;
            }

            if (!_state.TryRemoveUnit(normalizedUnitId, out var nodeId))
            {
                return;
            }

            ClearGhostNode(nodeId);
        }

        public void ClearAll()
        {
            if (_state.Count == 0)
            {
                return;
            }

            var nodeIds = _state.SnapshotGhostNodes();
            _state.Clear();
            foreach (var nodeId in nodeIds)
            {
                ClearGhostNode(nodeId);
            }
        }

        public void ResolveCommittedNode(string nodeId, NodeDto node)
        {
            if (string.IsNullOrWhiteSpace(nodeId) ||
                node == null ||
                string.IsNullOrWhiteSpace(node.BuildingType) ||
                _state.Count == 0)
            {
                return;
            }

            var normalizedNodeId = nodeId.Trim();
            if (_state.TryFindUnitByNode(normalizedNodeId, out var unitIdToRemove))
            {
                _state.RemoveUnit(unitIdToRemove);
            }
        }

        public void RememberGhost(string unitId, string nodeId)
        {
            _state.SetGhostNode(unitId, nodeId);
        }

        public bool HasGhostForUnit(string unitId)
        {
            return _state.TryGetGhostNode(unitId, out _);
        }

        private static void ClearGhostNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            if (map.TryGetNodeState(nodeId, out var state) &&
                state != null &&
                !string.IsNullOrWhiteSpace(state.BuildingType))
            {
                return;
            }

            map.ApplyBuildingPlacement(nodeId.Trim(), string.Empty, string.Empty, false, 0);
        }

        private static bool ShouldRenderPendingBuildGhost(string nodeId, MapRenderer map)
        {
            return map != null &&
                   !string.IsNullOrWhiteSpace(nodeId) &&
                   map.TryGetNodeState(nodeId, out var nodeState) &&
                   nodeState != null &&
                   string.IsNullOrWhiteSpace(nodeState.BuildingType);
        }

        private static string Normalize(string value)
        {
            return MapInputTokens.Normalize(value);
        }
    }
}
