/*************************************************
 * Project: Panoptes
 * File: PendingBuildState.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Presentation-only pending build bookkeeping for optimistic build ghosts.
 *************************************************/

using System;
using System.Collections.Generic;

namespace Panoptes.Presentation.Planning.Input.State
{
    /// <summary>
    /// Tracks pending build ghost records without determining whether placement is legal.
    /// </summary>
    public sealed class PendingBuildState<TRecord>
    {
        private readonly List<TRecord> _records = new();
        private readonly Func<TRecord, string> _nodeIdSelector;

        public PendingBuildState(Func<TRecord, string> nodeIdSelector)
        {
            _nodeIdSelector = nodeIdSelector ?? throw new ArgumentNullException(nameof(nodeIdSelector));
        }

        public IReadOnlyList<TRecord> Records => _records;

        public void Add(TRecord record)
        {
            _records.Add(record);
        }

        public bool HasNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            for (var i = 0; i < _records.Count; i++)
            {
                if (string.Equals(_nodeIdSelector(_records[i]), nodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public void RemoveNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            for (var i = _records.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_nodeIdSelector(_records[i]), nodeId, StringComparison.Ordinal))
                {
                    _records.RemoveAt(i);
                }
            }
        }

        public string GetLastNodeId()
        {
            for (var i = _records.Count - 1; i >= 0; i--)
            {
                var nodeId = _nodeIdSelector(_records[i]);
                if (!string.IsNullOrWhiteSpace(nodeId))
                {
                    return nodeId;
                }
            }

            return string.Empty;
        }
    }
}
