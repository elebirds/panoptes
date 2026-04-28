/*************************************************
 * Project: Panoptes
 * File: MapInputHandler.Deploy.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Territory expansion ghost state and deploy target resolution for settler-style units.
 *************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Presentation.Animation;
using Panoptes.Presentation.UI.Common;
using Panoptes.Presentation.UI.HUD;
using UnityEngine;
using UnityEngine.Rendering;

namespace Panoptes.Presentation.Map
{
    public sealed partial class MapInputHandler
    {
        private void ShowPendingDeployCityCoreGhost(string unitId, string centerNodeId)
        {
            var normalizedUnitId = NormalizeToken(unitId);
            var normalizedNodeId = string.IsNullOrWhiteSpace(centerNodeId) ? string.Empty : centerNodeId.Trim();
            if (string.IsNullOrEmpty(normalizedUnitId) || string.IsNullOrEmpty(normalizedNodeId))
            {
                return;
            }

            if (_pendingDeployGhostNodeByUnitId.TryGetValue(normalizedUnitId, out var oldNodeId)
                && !string.IsNullOrWhiteSpace(oldNodeId)
                && !string.Equals(oldNodeId, normalizedNodeId, StringComparison.Ordinal))
            {
                TryClearPendingDeployGhostNode(oldNodeId);
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            if (!ShouldRenderPendingBuildGhost(normalizedNodeId))
            {
                _pendingDeployGhostNodeByUnitId.Remove(normalizedUnitId);
                return;
            }

            map.ApplyBuildingPlacement(normalizedNodeId, "city_core", GetLocalOwnerId(), true, 100, buildPlacedGhostColor);
            _pendingDeployGhostNodeByUnitId[normalizedUnitId] = normalizedNodeId;
        }

        private void ClearPendingDeployCityCoreGhostForUnit(string unitId)
        {
            var normalizedUnitId = NormalizeToken(unitId);
            if (string.IsNullOrEmpty(normalizedUnitId))
            {
                return;
            }

            if (!_pendingDeployGhostNodeByUnitId.TryGetValue(normalizedUnitId, out var nodeId))
            {
                return;
            }

            _pendingDeployGhostNodeByUnitId.Remove(normalizedUnitId);
            TryClearPendingDeployGhostNode(nodeId);
        }

        private void ClearAllPendingDeployGhosts()
        {
            if (_pendingDeployGhostNodeByUnitId.Count == 0)
            {
                return;
            }

            var nodeIDs = new HashSet<string>(_pendingDeployGhostNodeByUnitId.Values);
            _pendingDeployGhostNodeByUnitId.Clear();

            foreach (var nodeId in nodeIDs)
            {
                TryClearPendingDeployGhostNode(nodeId);
            }
        }

        private void TryClearPendingDeployGhostNode(string nodeId)
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

            // Keep real buildings intact; only clear stale ghost markers.
            if (map.TryGetNodeState(nodeId, out var state) && state != null &&
                !string.IsNullOrWhiteSpace(state.BuildingType))
            {
                return;
            }

            map.ApplyBuildingPlacement(nodeId.Trim(), string.Empty, string.Empty, false, 0);
        }

        private void TryResolvePendingDeployGhostByNode(string nodeId, NodeDto node)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || node == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(node.BuildingType))
            {
                return;
            }

            if (_pendingDeployGhostNodeByUnitId.Count == 0)
            {
                return;
            }

            var normalizedNodeId = nodeId.Trim();
            string unitIdToRemove = null;
            foreach (var pair in _pendingDeployGhostNodeByUnitId)
            {
                if (string.Equals(pair.Value, normalizedNodeId, StringComparison.Ordinal))
                {
                    unitIdToRemove = pair.Key;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(unitIdToRemove))
            {
                _pendingDeployGhostNodeByUnitId.Remove(unitIdToRemove);
            }
        }

        private string ResolveExpandCenterNodeId(string unitId, Vector2Int fallbackGrid)
        {
            var normalizedUnitId = string.IsNullOrWhiteSpace(unitId) ? string.Empty : unitId.Trim();
            if (!string.IsNullOrEmpty(normalizedUnitId)
                && _pendingMoveTargetNodeByUnitId.TryGetValue(normalizedUnitId, out var pendingNodeId)
                && !string.IsNullOrWhiteSpace(pendingNodeId))
            {
                return pendingNodeId;
            }

            if (MapRenderer.Instance != null
                && !string.IsNullOrEmpty(normalizedUnitId)
                && MapRenderer.Instance.TryGetUnitView(normalizedUnitId, out var unitView)
                && unitView != null)
            {
                var fromView = ResolveNodeIdByGrid(unitView.GridPos);
                if (!string.IsNullOrWhiteSpace(fromView))
                {
                    return fromView;
                }
            }

            if (fallbackGrid != default)
            {
                var fromFallback = ResolveNodeIdByGrid(fallbackGrid);
                if (!string.IsNullOrWhiteSpace(fromFallback))
                {
                    return fromFallback;
                }
            }

            return string.Empty;
        }
    }
}
