/*************************************************
 * Project: Panoptes
 * File: MapInputHandler.Highlights.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Map node highlight restoration for attack ranges, territory overlays, and previews.
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
        private void ClearNodeHighlights()
        {
            var map = MapRenderer.Instance;
            if (map == null)
            {
                _highlightNodeIds.Clear();
                _movePreviewOverlay?.ClearPreview(RestoreTerritoryHighlightAfterPreviewOverlayClear);
                return;
            }

            foreach (var nodeId in _highlightNodeIds)
            {
                if (map.TryGetNodeView(nodeId, out var node))
                {
                    if (_territoryHighlightNodeIds.Contains(nodeId))
                    {
                        node.SetHighlight(true, territoryHighlightColor);
                    }
                    else
                    {
                        node.SetHighlightVisible(false);
                    }
                }
            }

            _highlightNodeIds.Clear();
            _movePreviewOverlay?.ClearPreview(RestoreTerritoryHighlightAfterPreviewOverlayClear);
        }

        private bool RestoreTerritoryHighlightAfterPreviewOverlayClear(string nodeId, NodeView node)
        {
            if (node == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            if (_territoryHighlightNodeIds.Contains(nodeId))
            {
                node.SetHighlight(true, territoryHighlightColor);
                return true;
            }

            return false;
        }

        private void RestoreNodeHighlightAfterHover(NodeView node)
        {
            if (node == null)
            {
                return;
            }

            var nodeId = node.NodeId;
            if (!string.IsNullOrWhiteSpace(nodeId) && _territoryHighlightNodeIds.Contains(nodeId))
            {
                node.SetHighlight(true, territoryHighlightColor);
                return;
            }

            if (!string.IsNullOrWhiteSpace(nodeId) && _highlightNodeIds.Contains(nodeId))
            {
                node.SetHighlight(true, attackRangeHighlightColor);
                return;
            }

            if (_movePreviewOverlay != null && _movePreviewOverlay.TryRestorePreviewHighlight(nodeId, node))
            {
                return;
            }

            node.SetHighlightVisible(false);
        }

        private void HighlightTerritoryForNode(NodeDto centerNode)
        {
            ClearTerritoryHighlights();

            var map = MapRenderer.Instance;
            if (map == null || centerNode == null || map.TileViews == null || map.TileViews.Count == 0)
            {
                return;
            }

            var owner = NormalizeToken(centerNode.TerritoryOwner);
            if (string.IsNullOrEmpty(owner))
            {
                // Fallback for compatibility: if territory_owner is absent on center node, do not highlight.
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

                var territoryOwner = NormalizeToken(nodeState.TerritoryOwner);
                if (string.IsNullOrEmpty(territoryOwner))
                {
                    continue;
                }

                if (!string.Equals(territoryOwner, owner, StringComparison.Ordinal))
                {
                    continue;
                }

                nodeView.SetHighlight(true, territoryHighlightColor);
                _territoryHighlightNodeIds.Add(nodeId);
            }
        }

        private void ClearTerritoryHighlights()
        {
            var map = MapRenderer.Instance;
            if (map == null)
            {
                _territoryHighlightNodeIds.Clear();
                return;
            }

            foreach (var nodeId in _territoryHighlightNodeIds)
            {
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    continue;
                }

                if (map.TryGetNodeView(nodeId, out var nodeView) && nodeView != null)
                {
                    if (_highlightNodeIds.Contains(nodeId))
                    {
                        nodeView.SetHighlight(true, attackRangeHighlightColor);
                    }
                    else if (_movePreviewOverlay != null && _movePreviewOverlay.TryRestorePreviewHighlight(nodeId, nodeView))
                    {
                    }
                    else
                    {
                        nodeView.SetHighlightVisible(false);
                    }
                }
            }

            _territoryHighlightNodeIds.Clear();
        }
    }
}
