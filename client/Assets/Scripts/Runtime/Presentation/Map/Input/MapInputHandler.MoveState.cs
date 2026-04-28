/*************************************************
 * Project: Panoptes
 * File: MapInputHandler.MoveState.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Move preview cache, pending move state, and queued path overlay synchronization.
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
        private void SendMoveCommand(string unitId, string targetNodeId)
        {
            if (!string.IsNullOrWhiteSpace(unitId))
            {
                var normalizedUnitId = unitId.Trim();
                // Move command should cancel any pending deploy intent for the same unit.
                ClearPendingDeployCityCoreGhostForUnit(normalizedUnitId);
                _pendingMoveUnitIds.Add(normalizedUnitId);
                _pendingMoveTargetNodeByUnitId[normalizedUnitId] = targetNodeId ?? string.Empty;
                if (TryGetCurrentMovePreview(normalizedUnitId, targetNodeId, out var preview) &&
                    preview != null &&
                    preview.Valid)
                {
                    RememberPendingMovePath(normalizedUnitId, preview.PathNodeIds);
                }
            }

            RemoveMovePreview(unitId);
            if (!TryApplyAuthoritativeMovePathMarkers(unitId, targetNodeId))
            {
                _movePathOverlay?.ClearMovePathMarkersForUnit(unitId);
            }
            Debug.Log($"[MapInputHandler] 发送移动消息 unit={unitId} target={targetNodeId}");
            GameIntents.MoveUnit(unitId, targetNodeId);
            MoveCommandSent?.Invoke(unitId, targetNodeId);
            ClearNodeHighlights();
        }

        private bool TryResolvePlannedMoveTargetNodeId(string unitId, out string targetNodeId)
        {
            targetNodeId = string.Empty;
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            var normalizedUnitId = unitId.Trim();
            if (_pendingMoveTargetNodeByUnitId.TryGetValue(normalizedUnitId, out var pendingTargetNodeId) &&
                !string.IsNullOrWhiteSpace(pendingTargetNodeId))
            {
                targetNodeId = pendingTargetNodeId.Trim();
                return true;
            }

            var draftCache = _draftCache ?? PlanningDraftCache.Instance;
            if (draftCache == null ||
                !draftCache.OrdersByUnitId.TryGetValue(normalizedUnitId, out var order) ||
                order == null ||
                !string.Equals(order.Action, "move", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (order.PathNodeIds != null)
            {
                for (var i = order.PathNodeIds.Count - 1; i >= 0; i--)
                {
                    var pathNodeId = order.PathNodeIds[i];
                    if (string.IsNullOrWhiteSpace(pathNodeId))
                    {
                        continue;
                    }

                    targetNodeId = pathNodeId.Trim();
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(order.TargetNodeId))
            {
                targetNodeId = order.TargetNodeId.Trim();
                return true;
            }

            return false;
        }

        private void ClearPendingMoveStateForUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            var normalizedUnitId = unitId.Trim();
            _pendingMoveUnitIds.Remove(normalizedUnitId);
            _pendingMoveTargetNodeByUnitId.Remove(normalizedUnitId);
            _pendingMovePathNodeIdsByUnitId.Remove(normalizedUnitId);
            _movePathOverlay?.ClearMovePathMarkersForUnit(normalizedUnitId);
            RemoveMovePreview(normalizedUnitId);
        }

        private bool TryGetCurrentMovePreview(string unitId, string targetNodeId, out PathPreviewDto preview)
        {
            preview = null;
            if (string.IsNullOrWhiteSpace(unitId) || string.IsNullOrWhiteSpace(targetNodeId))
            {
                return false;
            }

            var draftCache = _draftCache ?? PlanningDraftCache.Instance;
            preview = draftCache != null ? draftCache.CurrentPreview : null;
            return preview != null &&
                   string.Equals(preview.UnitId, unitId, StringComparison.Ordinal) &&
                   string.Equals(preview.TargetNodeId, targetNodeId, StringComparison.Ordinal);
        }

        private IReadOnlyList<string> GetQueuedMovePathNodeIds(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            var draftCache = _draftCache ?? PlanningDraftCache.Instance;
            if (draftCache == null)
            {
                return null;
            }

            if (!draftCache.OrdersByUnitId.TryGetValue(unitId, out var order) ||
                order == null ||
                !string.Equals(order.Action, "move", StringComparison.Ordinal) ||
                order.PathNodeIds == null ||
                order.PathNodeIds.Count < 2)
            {
                return null;
            }

            return order.PathNodeIds;
        }

        private void RememberQueuedMovePaths()
        {
            var draftCache = _draftCache ?? PlanningDraftCache.Instance;
            if (draftCache == null)
            {
                return;
            }

            foreach (var pair in draftCache.OrdersByUnitId)
            {
                var order = pair.Value;
                if (order == null || !string.Equals(order.Action, "move", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                RememberPendingMovePath(order.UnitId, order.PathNodeIds);
            }
        }

        private void RememberPendingMovePath(string unitId, IReadOnlyList<string> pathNodeIds)
        {
            if (string.IsNullOrWhiteSpace(unitId) || pathNodeIds == null || pathNodeIds.Count < 2)
            {
                return;
            }

            var normalizedUnitId = unitId.Trim();
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
                _pendingMovePathNodeIdsByUnitId[normalizedUnitId] = copy;
            }
        }

        private IReadOnlyList<string> GetRememberedMovePathNodeIds(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            return _pendingMovePathNodeIdsByUnitId.TryGetValue(unitId.Trim(), out var pathNodeIds) && pathNodeIds != null && pathNodeIds.Count >= 2
                ? pathNodeIds
                : null;
        }

        private void RefreshPreviewVisuals()
        {
            ClearNodeHighlights();

            var preview = PlanningDraftCache.Instance != null ? PlanningDraftCache.Instance.CurrentPreview : null;
            if (preview == null || !preview.Valid)
            {
                if (!string.IsNullOrEmpty(_hoverPreviewNodeId) &&
                    !string.IsNullOrEmpty(_selectedUnit?.UnitId) &&
                    preview != null &&
                    string.Equals(preview.UnitId, _selectedUnit.UnitId, StringComparison.Ordinal) &&
                    string.Equals(preview.TargetNodeId, _hoverPreviewNodeId, StringComparison.Ordinal) &&
                    MapRenderer.Instance != null)
                {
                    _movePreviewOverlay?.ShowInvalidPreview(_hoverPreviewNodeId, moveInvalidColor);
                }
                return;
            }

            if (_selectedUnit == null ||
                !string.Equals(preview.UnitId, _selectedUnit.UnitId, StringComparison.Ordinal) ||
                !string.Equals(preview.TargetNodeId, _hoverPreviewNodeId, StringComparison.Ordinal))
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }
            _movePreviewOverlay?.ShowPreview(preview, moveHighlightColor, moveFirstTurnColor, moveFutureTurnColor, moveTurnMarkerHeight);
        }

        private void ClearMovePreviewState()
        {
            var hadHover = !string.IsNullOrEmpty(_hoverPreviewNodeId);
            _hoverPreviewNodeId = string.Empty;
            var previewCache = PlanningDraftCache.Instance;
            if (hadHover || previewCache?.CurrentPreview != null)
            {
                previewCache?.ClearPreview();
            }
            ClearNodeHighlights();
        }

        private bool TryApplyAuthoritativeMovePathMarkers(string unitId, string targetNodeId)
        {
            var draftCache = PlanningDraftCache.Instance;
            if (draftCache == null)
            {
                return false;
            }

            return _movePathOverlay != null &&
                   _movePathOverlay.TryApplyAuthoritativeMovePathMarkers(
                       unitId,
                       targetNodeId,
                       draftCache.CurrentPreview,
                       draftCache.OrdersByUnitId,
                       movePathArrowColor,
                       movePathDestinationColor);
        }

        private void RefreshQueuedMovePathMarkers()
        {
            var draftCache = _draftCache ?? PlanningDraftCache.Instance;
            if (draftCache == null)
            {
                _movePathOverlay?.ClearAllMovePathMarkers();
                return;
            }

            _movePathOverlay?.RefreshQueuedMovePathMarkers(
                draftCache.OrdersByUnitId,
                movePathArrowColor,
                movePathDestinationColor);
        }
    }
}
