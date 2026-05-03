/*************************************************
 * Project: Panoptes
 * File: PendingDeployState.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Presentation-only pending deploy ghost bookkeeping for territory expansion intents.
 *************************************************/

using System;
using System.Collections.Generic;

namespace Panoptes.Presentation.Planning.Input.State
{
    /// <summary>
    /// Tracks which unit owns each pending deploy ghost without validating the deploy action.
    /// </summary>
    public sealed class PendingDeployState
    {
        private readonly Dictionary<string, string> _ghostNodeByUnitId = new(StringComparer.Ordinal);

        public int Count => _ghostNodeByUnitId.Count;

        public void SetGhostNode(string unitId, string nodeId)
        {
            if (string.IsNullOrWhiteSpace(unitId) || string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            _ghostNodeByUnitId[unitId.Trim()] = nodeId.Trim();
        }

        public bool TryGetGhostNode(string unitId, out string nodeId)
        {
            nodeId = string.Empty;
            return !string.IsNullOrWhiteSpace(unitId) &&
                   _ghostNodeByUnitId.TryGetValue(unitId.Trim(), out nodeId) &&
                   !string.IsNullOrWhiteSpace(nodeId);
        }

        public bool TryRemoveUnit(string unitId, out string nodeId)
        {
            nodeId = string.Empty;
            if (!TryGetGhostNode(unitId, out nodeId))
            {
                return false;
            }

            _ghostNodeByUnitId.Remove(unitId.Trim());
            return true;
        }

        public bool TryFindUnitByNode(string nodeId, out string unitId)
        {
            unitId = string.Empty;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            var normalizedNodeId = nodeId.Trim();
            foreach (var pair in _ghostNodeByUnitId)
            {
                if (string.Equals(pair.Value, normalizedNodeId, StringComparison.Ordinal))
                {
                    unitId = pair.Key;
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyCollection<string> SnapshotGhostNodes()
        {
            return new HashSet<string>(_ghostNodeByUnitId.Values, StringComparer.Ordinal);
        }

        public void RemoveUnit(string unitId)
        {
            if (!string.IsNullOrWhiteSpace(unitId))
            {
                _ghostNodeByUnitId.Remove(unitId.Trim());
            }
        }

        public void Clear()
        {
            _ghostNodeByUnitId.Clear();
        }
    }
}
