/*************************************************
 * Project: Panoptes
 * File: MovePathOverlayController.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Owns persistent move path markers for queued/server-approved moves.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class MovePathOverlayController
    {
        private readonly Dictionary<string, List<string>> _movePathNodeIdsByUnitId = new(StringComparer.Ordinal);

        public MovePathOverlayController(Transform hostTransform)
        {
        }

        public bool TryApplyAuthoritativeMovePathMarkers(
            string unitId,
            string targetNodeId,
            PathPreviewDto preview,
            IReadOnlyDictionary<string, QueuedCombatOrderDto> ordersByUnitId,
            Color arrowColor,
            Color destinationColor)
        {
            if (preview != null &&
                preview.Valid &&
                string.Equals(preview.UnitId, unitId, StringComparison.Ordinal) &&
                string.Equals(preview.TargetNodeId, targetNodeId, StringComparison.Ordinal) &&
                ApplyMovePathMarkers(unitId, preview.PathNodeIds, arrowColor, destinationColor))
            {
                return true;
            }

            if (ordersByUnitId != null &&
                ordersByUnitId.TryGetValue(unitId, out var order) &&
                order != null &&
                string.Equals(order.Action, "move", StringComparison.Ordinal) &&
                string.Equals(order.TargetNodeId, targetNodeId, StringComparison.Ordinal) &&
                ApplyMovePathMarkers(unitId, order.PathNodeIds, arrowColor, destinationColor))
            {
                return true;
            }

            return false;
        }

        public void RefreshQueuedMovePathMarkers(
            IReadOnlyDictionary<string, QueuedCombatOrderDto> ordersByUnitId,
            Color arrowColor,
            Color destinationColor)
        {
            ClearAllMovePathMarkers();
            if (ordersByUnitId == null)
            {
                return;
            }

            foreach (var pair in ordersByUnitId)
            {
                var order = pair.Value;
                if (order == null || !string.Equals(order.Action, "move", StringComparison.Ordinal))
                {
                    continue;
                }

                ApplyMovePathMarkers(order.UnitId, order.PathNodeIds, arrowColor, destinationColor);
            }
        }

        public void ClearMovePathMarkersForUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            var normalizedUnitId = unitId.Trim();
            if (!_movePathNodeIdsByUnitId.TryGetValue(normalizedUnitId, out var nodeIds) || nodeIds == null)
            {
                _movePathNodeIdsByUnitId.Remove(normalizedUnitId);
                return;
            }

            var map = MapRenderer.Instance;
            if (map != null)
            {
                for (var i = 0; i < nodeIds.Count; i++)
                {
                    var nodeId = nodeIds[i];
                    if (string.IsNullOrWhiteSpace(nodeId))
                    {
                        continue;
                    }

                    if (map.TryGetNodeView(nodeId, out var node) && node != null)
                    {
                        node.ClearMovePathMarker();
                    }
                }
            }

            _movePathNodeIdsByUnitId.Remove(normalizedUnitId);
        }

        public void ClearAllMovePathMarkers()
        {
            if (_movePathNodeIdsByUnitId.Count == 0)
            {
                return;
            }

            var keys = new List<string>(_movePathNodeIdsByUnitId.Keys);
            for (var i = 0; i < keys.Count; i++)
            {
                ClearMovePathMarkersForUnit(keys[i]);
            }
        }

        private bool ApplyMovePathMarkers(string unitId, IReadOnlyList<string> pathNodeIds, Color arrowColor, Color destinationColor)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            var normalizedUnitId = unitId.Trim();
            ClearMovePathMarkersForUnit(normalizedUnitId);

            if (pathNodeIds == null || pathNodeIds.Count < 2)
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return false;
            }

            var path = new List<NodeView>(pathNodeIds.Count);
            for (var i = 0; i < pathNodeIds.Count; i++)
            {
                var nodeId = pathNodeIds[i];
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    continue;
                }

                if (map.TryGetNodeView(nodeId, out var node) && node != null)
                {
                    path.Add(node);
                }
            }

            if (path.Count < 2)
            {
                return false;
            }

            var usedNodeIds = new List<string>(path.Count - 1);
            for (var i = 1; i < path.Count; i++)
            {
                var node = path[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    continue;
                }

                if (i == path.Count - 1)
                {
                    node.ShowMovePathDestination(destinationColor);
                }
                else
                {
                    var nextNode = path[i + 1];
                    if (nextNode == null)
                    {
                        continue;
                    }

                    var dir = nextNode.GridPos - node.GridPos;
                    node.ShowMovePathArrow(dir, arrowColor);
                }

                usedNodeIds.Add(node.NodeId);
            }

            if (usedNodeIds.Count == 0)
            {
                return false;
            }

            _movePathNodeIdsByUnitId[normalizedUnitId] = usedNodeIds;
            return true;
        }
    }
}
