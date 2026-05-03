using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Planning.Feedback;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class MapTerritoryHighlightPresenter
    {
        private readonly HashSet<string> _highlightedNodeIds = new(StringComparer.Ordinal);
        private MapRenderer _mapRenderer;

        public int Count => _highlightedNodeIds.Count;

        public void SetMapRenderer(MapRenderer mapRenderer)
        {
            _mapRenderer = mapRenderer;
        }

        public bool Contains(string nodeId)
        {
            return !string.IsNullOrWhiteSpace(nodeId) && _highlightedNodeIds.Contains(nodeId);
        }

        public void Remember(string nodeId)
        {
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                _highlightedNodeIds.Add(nodeId);
            }
        }

        public void HighlightForNode(NodeDto centerNode, Color territoryHighlightColor)
        {
            Clear(null, default, null);

            var map = _mapRenderer;
            if (map == null || centerNode == null || map.TileViews == null || map.TileViews.Count == 0)
            {
                return;
            }

            var owner = Normalize(centerNode.TerritoryOwner);
            if (string.IsNullOrEmpty(owner))
            {
                return;
            }

            foreach (var pair in map.TileViews)
            {
                var nodeId = pair.Key;
                var nodeView = pair.Value;
                if (string.IsNullOrWhiteSpace(nodeId) || nodeView == null)
                {
                    continue;
                }

                if (!map.TryGetNodeState(nodeId, out var nodeState) || nodeState == null)
                {
                    continue;
                }

                var territoryOwner = Normalize(nodeState.TerritoryOwner);
                if (string.IsNullOrEmpty(territoryOwner) ||
                    !string.Equals(territoryOwner, owner, StringComparison.Ordinal))
                {
                    continue;
                }

                nodeView.SetHighlight(true, territoryHighlightColor);
                _highlightedNodeIds.Add(nodeId);
            }
        }

        public void Clear(
            ISet<string> attackHighlightedNodeIds,
            Color attackHighlightColor,
            Func<string, NodeView, bool> tryRestorePreviewHighlight)
        {
            var map = _mapRenderer;
            if (map == null)
            {
                _highlightedNodeIds.Clear();
                return;
            }

            foreach (var nodeId in _highlightedNodeIds)
            {
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    continue;
                }

                if (map.TryGetNodeView(nodeId, out var nodeView) && nodeView != null)
                {
                    if (attackHighlightedNodeIds != null && attackHighlightedNodeIds.Contains(nodeId))
                    {
                        nodeView.SetHighlight(true, attackHighlightColor);
                    }
                    else if (tryRestorePreviewHighlight != null && tryRestorePreviewHighlight(nodeId, nodeView))
                    {
                    }
                    else
                    {
                        nodeView.SetHighlightVisible(false);
                    }
                }
            }

            _highlightedNodeIds.Clear();
        }

        public bool TryRestore(string nodeId, NodeView node, Color territoryHighlightColor)
        {
            if (node == null || string.IsNullOrWhiteSpace(nodeId) || !_highlightedNodeIds.Contains(nodeId))
            {
                return false;
            }

            node.SetHighlight(true, territoryHighlightColor);
            return true;
        }

        private static string Normalize(string value)
        {
            return MapInputTokens.Normalize(value);
        }
    }
}
