/*************************************************
 * Project: Panoptes
 * File: PendingMoveState.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Presentation-only pending move bookkeeping for server-authoritative planning orders.
 *************************************************/

using System;
using System.Collections.Generic;

namespace Panoptes.Presentation.Planning.Input.State
{
    /// <summary>
    /// Tracks pending move presentation state without deciding whether a move is legal.
    /// </summary>
    public sealed class PendingMoveState
    {
        private readonly HashSet<string> _unitIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _targetNodeByUnitId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _pathNodeIdsByUnitId = new(StringComparer.Ordinal);

        public bool Has(string unitId)
        {
            return !string.IsNullOrWhiteSpace(unitId) && _unitIds.Contains(unitId.Trim());
        }

        public bool TryGetTargetNodeId(string unitId, out string targetNodeId)
        {
            targetNodeId = string.Empty;
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            if (!_targetNodeByUnitId.TryGetValue(unitId.Trim(), out var value) || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            targetNodeId = value.Trim();
            return true;
        }

        public void MarkPending(string unitId, string targetNodeId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            var normalizedUnitId = unitId.Trim();
            _unitIds.Add(normalizedUnitId);
            _targetNodeByUnitId[normalizedUnitId] = targetNodeId ?? string.Empty;
        }

        public void RememberPath(string unitId, IReadOnlyList<string> pathNodeIds)
        {
            if (string.IsNullOrWhiteSpace(unitId) || pathNodeIds == null || pathNodeIds.Count < 2)
            {
                return;
            }

            var copy = new List<string>(pathNodeIds.Count);
            for (var i = 0; i < pathNodeIds.Count; i++)
            {
                var nodeId = pathNodeIds[i];
                if (!string.IsNullOrWhiteSpace(nodeId))
                {
                    copy.Add(nodeId.Trim());
                }
            }

            if (copy.Count >= 2)
            {
                _pathNodeIdsByUnitId[unitId.Trim()] = copy;
            }
        }

        public IReadOnlyList<string> GetRememberedPath(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            return _pathNodeIdsByUnitId.TryGetValue(unitId.Trim(), out var pathNodeIds) && pathNodeIds != null && pathNodeIds.Count >= 2
                ? pathNodeIds
                : null;
        }

        public void ClearUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            var normalizedUnitId = unitId.Trim();
            _unitIds.Remove(normalizedUnitId);
            _targetNodeByUnitId.Remove(normalizedUnitId);
            _pathNodeIdsByUnitId.Remove(normalizedUnitId);
        }

        public void ClearAll()
        {
            _unitIds.Clear();
            _targetNodeByUnitId.Clear();
            _pathNodeIdsByUnitId.Clear();
        }

        public void ClearRememberedPaths()
        {
            _pathNodeIdsByUnitId.Clear();
        }
    }
}
