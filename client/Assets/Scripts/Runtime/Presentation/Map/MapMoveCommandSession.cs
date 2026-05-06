using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Planning.Input.Modes;
using Panoptes.Presentation.Planning.Input.State;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class MapMoveCommandSession
    {
        private readonly PendingMoveState _pendingMoveState = new();

        private float _nextMovePreviewRequestAt;
        private int _movePreviewRequestSequence;
        private string _hoverPreviewNodeId = string.Empty;

        public string HoverPreviewNodeId => _hoverPreviewNodeId;

        public bool HasHoverPreview => !string.IsNullOrEmpty(_hoverPreviewNodeId);

        public bool IsUnitMovePending(string unitId)
        {
            return !string.IsNullOrWhiteSpace(unitId) && _pendingMoveState.Has(unitId);
        }

        public bool ShouldRequestPreview(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) &&
                   !string.Equals(_hoverPreviewNodeId, nodeId, StringComparison.Ordinal) &&
                   Time.unscaledTime >= _nextMovePreviewRequestAt;
        }

        public void RequestPreview(
            string unitId,
            string targetNodeId,
            float throttleSeconds,
            MapPlanningInputStateAdapter inputState,
            PlanningIntentService planningIntentService)
        {
            if (string.IsNullOrWhiteSpace(unitId) || string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            var normalizedUnitId = unitId.Trim();
            _hoverPreviewNodeId = targetNodeId;
            inputState?.SetMovePreviewTarget(_hoverPreviewNodeId);
            _nextMovePreviewRequestAt = Time.unscaledTime + Mathf.Max(0.02f, throttleSeconds);
            _movePreviewRequestSequence++;
            var requestId = $"move-preview-{normalizedUnitId}-{_movePreviewRequestSequence}";
            PanoptesLog.Log($"[MapPlanningInputController] 请求路径预览 unit={normalizedUnitId} hover_node={targetNodeId} request={requestId}");
            planningIntentService?.PreviewMove(requestId, normalizedUnitId, targetNodeId);
        }

        public void MarkPendingMove(string unitId, string targetNodeId, PathPreviewDto preview)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            var normalizedUnitId = unitId.Trim();
            _pendingMoveState.MarkPending(normalizedUnitId, targetNodeId);
            if (preview != null && preview.Valid)
            {
                _pendingMoveState.RememberPath(normalizedUnitId, preview.PathNodeIds);
            }
        }

        public bool TryResolvePlannedTargetNodeId(
            string unitId,
            IReadOnlyDictionary<string, QueuedUnitOrderDto> ordersByUnitId,
            out string targetNodeId)
        {
            return MoveSelectionInputMode.TryResolvePlannedTargetNodeId(
                unitId,
                _pendingMoveState,
                ordersByUnitId,
                out targetNodeId);
        }

        public void ClearUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            _pendingMoveState.ClearUnit(unitId.Trim());
        }

        public void ClearAll()
        {
            _pendingMoveState.ClearAll();
        }

        public bool TryGetCurrentPreview(
            string unitId,
            string targetNodeId,
            PathPreviewDto currentPreview,
            out PathPreviewDto preview)
        {
            preview = null;
            if (string.IsNullOrWhiteSpace(unitId) || string.IsNullOrWhiteSpace(targetNodeId))
            {
                return false;
            }

            preview = currentPreview;
            return MoveSelectionInputMode.MatchesPreview(preview, unitId, targetNodeId);
        }

        public IReadOnlyList<string> GetQueuedMovePathNodeIds(
            string unitId,
            IReadOnlyDictionary<string, QueuedUnitOrderDto> ordersByUnitId)
        {
            return string.IsNullOrWhiteSpace(unitId)
                ? null
                : MoveSelectionInputMode.GetQueuedMovePathNodeIds(unitId, ordersByUnitId);
        }

        public void RememberQueuedMovePaths(IReadOnlyDictionary<string, QueuedUnitOrderDto> ordersByUnitId)
        {
            if (ordersByUnitId == null)
            {
                return;
            }

            foreach (var pair in ordersByUnitId)
            {
                var order = pair.Value;
                if (order == null || !string.Equals(order.Action, "move", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                RememberPath(order.UnitId, order.PathNodeIds);
            }
        }

        public void RememberPath(string unitId, IReadOnlyList<string> pathNodeIds)
        {
            if (string.IsNullOrWhiteSpace(unitId) || pathNodeIds == null || pathNodeIds.Count < 2)
            {
                return;
            }

            _pendingMoveState.RememberPath(unitId, pathNodeIds);
        }

        public IReadOnlyList<string> GetRememberedPath(string unitId)
        {
            return string.IsNullOrWhiteSpace(unitId) ? null : _pendingMoveState.GetRememberedPath(unitId);
        }

        public void ClearRememberedPaths()
        {
            _pendingMoveState.ClearRememberedPaths();
        }

        public bool TryGetPendingTargetNodeId(string unitId, out string targetNodeId)
        {
            var normalizedUnitId = string.IsNullOrWhiteSpace(unitId) ? string.Empty : unitId.Trim();
            if (string.IsNullOrEmpty(normalizedUnitId))
            {
                targetNodeId = string.Empty;
                return false;
            }

            return _pendingMoveState.TryGetTargetNodeId(normalizedUnitId, out targetNodeId);
        }

        public void ClearPreviewState(MapPlanningInputStateAdapter inputState)
        {
            _hoverPreviewNodeId = string.Empty;
            inputState?.ClearMovePreviewTarget();
        }
    }
}
