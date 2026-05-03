/*************************************************
 * Project: Panoptes
 * File: MoveSelectionInputMode.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Planning input mode marker for move selection flow.
 *************************************************/

using Panoptes.Presentation.Planning.Input;
using Panoptes.Presentation.Planning.Input.State;
using Panoptes.Core.Domain;
using System;
using System.Collections.Generic;

namespace Panoptes.Presentation.Planning.Input.Modes
{
    /// <summary>
    /// Base lifecycle holder for move selection input; command dispatch remains server-authoritative.
    /// </summary>
    public sealed class MoveSelectionInputMode : IPlanningInputMode
    {
        public void Enter() { }
        public void Exit() { }
        public void Tick() { }
        public bool HandlePrimary() => false;
        public bool HandleCancel() => false;

        public static bool TryResolvePlannedTargetNodeId(
            string unitId,
            PendingMoveState pendingMoveState,
            IReadOnlyDictionary<string, QueuedUnitOrderDto> queuedOrders,
            out string targetNodeId)
        {
            targetNodeId = string.Empty;
            var normalizedUnitId = NormalizeId(unitId);
            if (string.IsNullOrEmpty(normalizedUnitId))
            {
                return false;
            }

            if (pendingMoveState != null &&
                pendingMoveState.TryGetTargetNodeId(normalizedUnitId, out var pendingTargetNodeId))
            {
                targetNodeId = pendingTargetNodeId;
                return true;
            }

            if (!TryGetQueuedMoveOrder(normalizedUnitId, queuedOrders, out var order))
            {
                return false;
            }

            if (TryGetLastPathNodeId(order.PathNodeIds, out targetNodeId))
            {
                return true;
            }

            targetNodeId = NormalizeId(order.TargetNodeId);
            return !string.IsNullOrEmpty(targetNodeId);
        }

        public static IReadOnlyList<string> GetQueuedMovePathNodeIds(
            string unitId,
            IReadOnlyDictionary<string, QueuedUnitOrderDto> queuedOrders)
        {
            return TryGetQueuedMoveOrder(unitId, queuedOrders, out var order) &&
                   order.PathNodeIds != null &&
                   order.PathNodeIds.Count >= 2
                ? order.PathNodeIds
                : null;
        }

        public static bool MatchesPreview(PathPreviewDto preview, string unitId, string targetNodeId)
        {
            return preview != null &&
                   string.Equals(preview.UnitId, unitId, StringComparison.Ordinal) &&
                   string.Equals(preview.TargetNodeId, targetNodeId, StringComparison.Ordinal);
        }

        private static bool TryGetQueuedMoveOrder(
            string unitId,
            IReadOnlyDictionary<string, QueuedUnitOrderDto> queuedOrders,
            out QueuedUnitOrderDto order)
        {
            order = null;
            var normalizedUnitId = NormalizeId(unitId);
            if (string.IsNullOrEmpty(normalizedUnitId) ||
                queuedOrders == null ||
                !queuedOrders.TryGetValue(normalizedUnitId, out order) ||
                order == null ||
                !string.Equals(order.Action, "move", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static bool TryGetLastPathNodeId(IReadOnlyList<string> pathNodeIds, out string nodeId)
        {
            nodeId = string.Empty;
            if (pathNodeIds == null)
            {
                return false;
            }

            for (var i = pathNodeIds.Count - 1; i >= 0; i--)
            {
                nodeId = NormalizeId(pathNodeIds[i]);
                if (!string.IsNullOrEmpty(nodeId))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
