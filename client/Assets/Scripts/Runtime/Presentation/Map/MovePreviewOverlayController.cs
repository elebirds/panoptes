/*************************************************
 * Project: Panoptes
 * File: MovePreviewOverlayController.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Owns move preview highlights and turn markers.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class MovePreviewOverlayController
    {
        private readonly Dictionary<string, Color> _previewHighlightColorsByNodeId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GameObject> _moveTurnMarkers = new(StringComparer.Ordinal);
        private MapRenderer _mapRenderer;
        private Transform _hostTransform;

        public MovePreviewOverlayController(MapRenderer mapRenderer, Transform hostTransform)
        {
            _mapRenderer = mapRenderer;
            _hostTransform = hostTransform;
        }

        public void SetMapRenderer(MapRenderer mapRenderer)
        {
            _mapRenderer = mapRenderer;
        }

        public void SetHostTransform(Transform hostTransform)
        {
            _hostTransform = hostTransform;
        }

        public void ShowInvalidPreview(string nodeId, Color invalidColor)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            var map = _mapRenderer;
            if (map == null || !map.TryGetNodeView(nodeId, out var invalidNode) || invalidNode == null)
            {
                return;
            }

            invalidNode.SetHighlight(true, invalidColor);
            _previewHighlightColorsByNodeId[nodeId] = invalidColor;
        }

        public void ShowPreview(
            PathPreviewDto preview,
            Color highlightColor,
            Color firstTurnColor,
            Color futureTurnColor,
            float turnMarkerHeight)
        {
            if (preview == null || !preview.Valid)
            {
                return;
            }

            var map = _mapRenderer;
            if (map == null)
            {
                return;
            }

            if (preview.PathNodeIds != null)
            {
                for (var i = 0; i < preview.PathNodeIds.Count; i++)
                {
                    var nodeId = preview.PathNodeIds[i];
                    if (string.IsNullOrWhiteSpace(nodeId) || !map.TryGetNodeView(nodeId, out var node) || node == null)
                    {
                        continue;
                    }

                    var color = string.Equals(nodeId, preview.FirstTurnNodeId, StringComparison.Ordinal)
                        ? firstTurnColor
                        : highlightColor;
                    node.SetHighlight(true, color);
                    _previewHighlightColorsByNodeId[nodeId] = color;
                }
            }

            if (preview.TurnStops != null)
            {
                for (var i = 0; i < preview.TurnStops.Count; i++)
                {
                    var stop = preview.TurnStops[i];
                    if (stop == null || string.IsNullOrWhiteSpace(stop.NodeId) || !map.TryGetNodeView(stop.NodeId, out var node) || node == null)
                    {
                        continue;
                    }

                    var color = stop.TurnIndex <= 1 ? firstTurnColor : futureTurnColor;
                    node.SetHighlight(true, color);
                    _previewHighlightColorsByNodeId[stop.NodeId] = color;
                    if (stop.TurnIndex >= 2)
                    {
                        CreateMoveTurnMarker(stop.NodeId, node, stop.TurnIndex, turnMarkerHeight, futureTurnColor);
                    }
                }
            }
        }

        public void ClearPreview(Func<string, NodeView, bool> restoreNodeHighlight = null)
        {
            var map = _mapRenderer;
            if (map != null)
            {
                foreach (var pair in _previewHighlightColorsByNodeId)
                {
                    if (map.TryGetNodeView(pair.Key, out var node) && node != null)
                    {
                        if (restoreNodeHighlight != null && restoreNodeHighlight(pair.Key, node))
                        {
                            continue;
                        }

                        node.SetHighlightVisible(false);
                    }
                }
            }

            _previewHighlightColorsByNodeId.Clear();
            ClearMoveTurnMarkers();
        }

        public bool TryRestorePreviewHighlight(string nodeId, NodeView node)
        {
            if (node == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            if (!_previewHighlightColorsByNodeId.TryGetValue(nodeId, out var color))
            {
                return false;
            }

            node.SetHighlight(true, color);
            return true;
        }

        private void CreateMoveTurnMarker(string nodeId, NodeView node, int turnIndex, float turnMarkerHeight, Color turnMarkerColor)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || node == null)
            {
                return;
            }

            if (_moveTurnMarkers.TryGetValue(nodeId, out var existing) && existing != null)
            {
                var existingText = existing.GetComponent<TextMesh>();
                if (existingText != null)
                {
                    existingText.text = turnIndex.ToString();
                }
                return;
            }

            var marker = new GameObject($"MoveTurnMarker_{nodeId}");
            marker.transform.SetParent(_hostTransform, false);
            marker.transform.position = node.ResolveUnitAnchorWorldPosition() + Vector3.up * turnMarkerHeight;
            var text = marker.AddComponent<TextMesh>();
            text.text = turnIndex.ToString();
            text.characterSize = 0.18f;
            text.fontSize = 42;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = turnMarkerColor;
            _moveTurnMarkers[nodeId] = marker;
        }

        private void ClearMoveTurnMarkers()
        {
            if (_moveTurnMarkers.Count == 0)
            {
                return;
            }

            foreach (var pair in _moveTurnMarkers)
            {
                if (pair.Value != null)
                {
                    UnityEngine.Object.Destroy(pair.Value);
                }
            }

            _moveTurnMarkers.Clear();
        }
    }
}
