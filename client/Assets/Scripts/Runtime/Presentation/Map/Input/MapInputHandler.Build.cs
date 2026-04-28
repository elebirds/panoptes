/*************************************************
 * Project: Panoptes
 * File: MapInputHandler.Build.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Build placement mode state, preview requests, hover ghost, and pending build rollback.
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
        private void EnterBuildPlacement(string buildingType, string cityId, BuildPlacementRule rule)
        {
            _buildType = ResolveBackendBuildingType(NormalizeToken(buildingType));
            if (IsManualPlacementBlocked(_buildType))
            {
                Debug.Log($"[MapInputHandler] {_buildType} is pre-placed by map config and cannot be manually built.");
                ExitBuildMode();
                return;
            }
            _activeBuildCityId = string.IsNullOrWhiteSpace(cityId) ? string.Empty : cityId.Trim();
            if (string.IsNullOrEmpty(_activeBuildCityId))
            {
                Debug.LogWarning($"[MapInputHandler] Missing build city context before entering build mode. building={_buildType}");
                ShowUserError("缺少建造城市上下文，无法进入建造模式");
                ExitBuildMode();
                return;
            }

            _mode = Mode.Build;
            _buildRule = rule;
            BlockInputAfterModeSwitch();

            ClearCombatSelection();
            DestroyHoverGhost();
        }

        private bool IsManualPlacementBlocked(string buildingType)
        {
            return BuildPlacementInputMode.IsManualPlacementBlocked(
                buildingType,
                disallowManualCityCorePlacement,
                manualPlacementBlockedBuildingTypes);
        }

        private void ExitBuildMode()
        {
            RestoreNodeHighlightAfterHover(_hoverNode);

            _mode = Mode.None;
            _buildType = string.Empty;
            _activeBuildCityId = string.Empty;
            _hoverNode = null;
            DestroyHoverGhost();
            ClearBuildPreviewState();
        }

        private void UpdateBuildMode()
        {
            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            if (GetRightMouseButtonDown())
            {
                ExitBuildMode();
                return;
            }
            
            if (IsPointerOverUI())
            {
                RestoreNodeHighlightAfterHover(_hoverNode);
                _hoverNode = null;
                DestroyHoverGhost();
                ClearBuildPreviewState();
                return;
            }

            var hasNode = TryRaycastNode(out var node);
            if (!hasNode)
            {
                RestoreNodeHighlightAfterHover(_hoverNode);
                _hoverNode = null;
                DestroyHoverGhost();
                ClearBuildPreviewState();

                if (GetLeftMouseButtonDown() && !IsPointerOverUI() && logInvalidBuildClick)
                {
                    Debug.LogWarning("[MapInputHandler] Invalid build target: cursor is outside map tile.");
                }
                return;
            }

            if (_hoverNode != node)
            {
                RestoreNodeHighlightAfterHover(_hoverNode);

                _hoverNode = node;
                RecreateHoverGhost(node);
                RequestBuildPreview(node.NodeId);
            }
            else if (!TryGetCurrentBuildPreview(node.NodeId, out _) &&
                     Time.unscaledTime >= _nextBuildPreviewRequestAt)
            {
                RequestBuildPreview(node.NodeId);
            }

            var highlightColor = ResolveBuildPreviewColor(node.NodeId);

            if (_hoverNode != null)
            {
                _hoverNode.SetHighlight(true, highlightColor);
            }

            if (_hoverGhost != null)
            {
                _hoverGhost.SetPlacementGhost(true, highlightColor);
            }

            if (GetLeftMouseButtonDown())
            {
                if (IsPointerOverUI())
                {
                    return;
                }

                var backendBuildingType = ResolveBackendBuildingType(_buildType);
                if (TryGetCurrentBuildPreview(node.NodeId, out var preview) &&
                    preview != null &&
                    !preview.Valid)
                {
                    ShowUserError(BuildPreviewPresenter.ResolveMessage(preview));
                }
                if (!SendBuildCommand(backendBuildingType, node.NodeId))
                {
                    return;
                }

                var ownerId = GetLocalOwnerId();
                if (ShouldRenderPendingBuildGhost(node.NodeId))
                {
                    map.ApplyBuildingPlacement(node.NodeId, backendBuildingType, ownerId, true, 100, buildPlacedGhostColor);
                }
                _pendingBuilds.Add(new PendingBuildRecord
                {
                    buildingType = backendBuildingType,
                    nodeId = node.NodeId,
                    ownerId = ownerId,
                    isGhost = true
                });
                ExitBuildMode();
            }
        }

        private Color ResolveBuildPreviewColor(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return buildInvalidColor;
            }

            if (HasPendingBuild(nodeId))
            {
                return buildPlacedGhostColor;
            }

            if (TryGetCurrentBuildPreview(nodeId, out var preview) && preview != null)
            {
                return preview.Valid ? buildValidColor : buildInvalidColor;
            }

            return string.Equals(_hoverBuildPreviewNodeId, nodeId, StringComparison.Ordinal)
                ? buildPendingColor
                : buildInvalidColor;
        }

        private bool ShouldRenderPendingBuildGhost(string nodeId)
        {
            var map = MapRenderer.Instance;
            if (map == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            return map.TryGetNodeState(nodeId, out var nodeState) &&
                   nodeState != null &&
                   string.IsNullOrWhiteSpace(nodeState.BuildingType);
        }

        private void RecreateHoverGhost(NodeView node)
        {
            DestroyHoverGhost();
            if (node == null || string.IsNullOrEmpty(_buildType))
            {
                return;
            }

            var prefab = node.ResolveBuildingPrefab(_buildType);
            if (prefab == null || node.BuildingAnchor == null)
            {
                return;
            }

            _hoverGhost = Instantiate(prefab, node.BuildingAnchor, false);
            _hoverGhost.SetBuildingType(_buildType);
            _hoverGhost.SetOwner(GetLocalOwnerId());
            _hoverGhost.SetPlacementGhost(true, buildValidColor);
        }

        private void DestroyHoverGhost()
        {
            if (_hoverGhost != null)
            {
                Destroy(_hoverGhost.gameObject);
                _hoverGhost = null;
            }
        }

        private bool SendBuildCommand(string buildingType, string nodeId)
        {
            buildingType = ResolveBackendBuildingType(buildingType);
            if (string.IsNullOrWhiteSpace(_activeBuildCityId))
            {
                Debug.LogWarning($"[MapInputHandler] Missing build city context. node={nodeId} building={buildingType}");
                ShowUserError("Current node is missing city context.");
                return false;
            }

            GameIntents.BuildToken(nodeId, buildingType, _activeBuildCityId);
            BuildCommandSent?.Invoke(buildingType, nodeId);
            return true;
        }

        private void RemovePendingBuild(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            for (int i = _pendingBuilds.Count - 1; i >= 0; i--)
            {
                if (_pendingBuilds[i].nodeId == nodeId)
                {
                    _pendingBuilds.RemoveAt(i);
                }
            }
        }

        private string GetLastPendingBuildNodeId()
        {
            if (_pendingBuilds == null || _pendingBuilds.Count == 0)
            {
                return string.Empty;
            }

            for (var i = _pendingBuilds.Count - 1; i >= 0; i--)
            {
                var nodeId = _pendingBuilds[i].nodeId;
                if (!string.IsNullOrWhiteSpace(nodeId))
                {
                    return nodeId;
                }
            }

            return string.Empty;
        }

        private void RollbackPendingBuild(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            nodeId = nodeId.Trim();
            RemovePendingBuild(nodeId);

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            map.ApplyBuildingPlacement(nodeId, string.Empty, string.Empty, false, 0);
        }

        private bool HasPendingBuild(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            for (int i = 0; i < _pendingBuilds.Count; i++)
            {
                if (string.Equals(_pendingBuilds[i].nodeId, nodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshBuildPreviewVisuals()
        {
            if (_mode != Mode.Build || _hoverNode == null)
            {
                return;
            }

            var color = ResolveBuildPreviewColor(_hoverNode.NodeId);
            _hoverNode.SetHighlight(true, color);
            if (_hoverGhost != null)
            {
                _hoverGhost.SetPlacementGhost(true, color);
            }
        }

        private void RequestBuildPreview(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) ||
                string.IsNullOrWhiteSpace(_buildType) ||
                string.IsNullOrWhiteSpace(_activeBuildCityId))
            {
                return;
            }

            _hoverBuildPreviewNodeId = nodeId.Trim();
            _nextBuildPreviewRequestAt = Time.unscaledTime + Mathf.Max(0.02f, buildPreviewRequestThrottleSeconds);
            _buildPreviewRequestSequence++;
            var requestId = $"build-preview-{_buildType}-{_buildPreviewRequestSequence}";
            var draftCache = PlanningDraftCache.EnsureInstance();
            draftCache?.TrackBuildPreviewRequest(requestId, _hoverBuildPreviewNodeId, _buildType, _activeBuildCityId);
            GameIntents.PreviewBuild(requestId, _hoverBuildPreviewNodeId, _buildType, _activeBuildCityId);
        }

        private bool TryGetCurrentBuildPreview(string nodeId, out BuildPreviewDto preview)
        {
            preview = null;
            if (string.IsNullOrWhiteSpace(nodeId) ||
                string.IsNullOrWhiteSpace(_buildType) ||
                string.IsNullOrWhiteSpace(_activeBuildCityId))
            {
                return false;
            }

            var draftCache = _draftCache ?? PlanningDraftCache.Instance;
            preview = draftCache != null ? draftCache.CurrentBuildPreview : null;
            return preview != null &&
                   string.Equals(preview.NodeId, nodeId.Trim(), StringComparison.Ordinal) &&
                   string.Equals(preview.BuildingTypeId, _buildType, StringComparison.Ordinal) &&
                   string.Equals(preview.CityId, _activeBuildCityId, StringComparison.Ordinal);
        }

        private void ClearBuildPreviewState()
        {
            _hoverBuildPreviewNodeId = string.Empty;
            _nextBuildPreviewRequestAt = 0f;
            (_draftCache ?? PlanningDraftCache.Instance)?.ClearBuildPreview();
        }
    }
}
