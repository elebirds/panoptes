using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Planning.Feedback;
using Panoptes.Presentation.Planning.Input.Modes;
using Panoptes.Presentation.Planning.Input.State;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public readonly struct MapBuildPlacementVisualSettings
    {
        public MapBuildPlacementVisualSettings(
            Color validColor,
            Color invalidColor,
            Color pendingColor,
            Color placedGhostColor,
            float previewRequestThrottleSeconds,
            bool disallowManualCityCorePlacement,
            string[] manualPlacementBlockedBuildingTypes)
        {
            ValidColor = validColor;
            InvalidColor = invalidColor;
            PendingColor = pendingColor;
            PlacedGhostColor = placedGhostColor;
            PreviewRequestThrottleSeconds = previewRequestThrottleSeconds;
            DisallowManualCityCorePlacement = disallowManualCityCorePlacement;
            ManualPlacementBlockedBuildingTypes = manualPlacementBlockedBuildingTypes;
        }

        public Color ValidColor { get; }
        public Color InvalidColor { get; }
        public Color PendingColor { get; }
        public Color PlacedGhostColor { get; }
        public float PreviewRequestThrottleSeconds { get; }
        public bool DisallowManualCityCorePlacement { get; }
        public string[] ManualPlacementBlockedBuildingTypes { get; }
    }

    public sealed class MapBuildPlacementSession
    {
        private readonly PendingBuildState<MapPlanningInputController.PendingBuildRecord> _pendingBuildState =
            new(record => record.nodeId);

        private readonly BuildPlacementGhostPresenter _ghostPresenter = new();

        private MapPlanningInputStateAdapter _inputState;
        private Func<PlanningDraftCache> _draftCacheProvider;
        private Func<PlanningIntentService> _planningIntentServiceProvider;
        private Func<string> _localOwnerIdProvider;
        private Func<string, string> _backendBuildingTypeResolver;
        private Action<NodeView> _restoreNodeHighlight;
        private NodeView _hoverNode;
        private MapPlanningInputController.BuildPlacementRule _buildRule;
        private string _buildType = string.Empty;
        private string _activeBuildCityId = string.Empty;
        private string _hoverBuildPreviewNodeId = string.Empty;
        private float _nextBuildPreviewRequestAt;
        private int _buildPreviewRequestSequence;

        public IReadOnlyList<MapPlanningInputController.PendingBuildRecord> PendingBuilds => _pendingBuildState.Records;

        public string ActiveBuildType => _buildType;

        public string ActiveBuildCityId => _activeBuildCityId;

        public void Configure(
            MapPlanningInputStateAdapter inputState,
            Func<PlanningDraftCache> draftCacheProvider,
            Func<PlanningIntentService> planningIntentServiceProvider,
            Func<string> localOwnerIdProvider,
            Func<string, string> backendBuildingTypeResolver,
            Action<NodeView> restoreNodeHighlight)
        {
            _inputState = inputState;
            _draftCacheProvider = draftCacheProvider;
            _planningIntentServiceProvider = planningIntentServiceProvider;
            _localOwnerIdProvider = localOwnerIdProvider;
            _backendBuildingTypeResolver = backendBuildingTypeResolver;
            _restoreNodeHighlight = restoreNodeHighlight;
        }

        public bool EnterBuildPlacement(
            string buildingType,
            string cityId,
            MapPlanningInputController.BuildPlacementRule rule,
            MapBuildPlacementVisualSettings settings,
            Action<string> showUserError)
        {
            _buildType = ResolveBackendBuildingType(Normalize(buildingType));
            if (IsManualPlacementBlocked(_buildType, settings))
            {
                Debug.Log($"[MapPlanningInputController] {_buildType} is pre-placed by map config and cannot be manually built.");
                ExitBuildMode();
                return false;
            }

            _activeBuildCityId = string.IsNullOrWhiteSpace(cityId) ? string.Empty : cityId.Trim();
            if (string.IsNullOrEmpty(_activeBuildCityId))
            {
                Debug.LogWarning($"[MapPlanningInputController] Missing build city context before entering build mode. building={_buildType}");
                showUserError?.Invoke("缺少建造城市上下文，无法进入建造模式");
                ExitBuildMode();
                return false;
            }

            _buildRule = rule;
            _inputState?.SetBuildModeActive(true, _buildType, _activeBuildCityId, _buildRule);
            ClearHoverGhost();
            return true;
        }

        public void ExitBuildMode()
        {
            _restoreNodeHighlight?.Invoke(_hoverNode);

            _inputState?.SetBuildModeActive(false, _buildType, _activeBuildCityId, _buildRule);
            _buildType = string.Empty;
            _activeBuildCityId = string.Empty;
            _hoverNode = null;
            ClearHoverGhost();
            ClearBuildPreviewState();
        }

        public void ClearHoverState()
        {
            _restoreNodeHighlight?.Invoke(_hoverNode);
            _hoverNode = null;
            ClearHoverGhost();
            ClearBuildPreviewState();
        }

        public bool IsCurrentHoverNode(NodeView node)
        {
            return _hoverNode == node;
        }

        public void MoveHoverTo(NodeView node, MapBuildPlacementVisualSettings settings)
        {
            _restoreNodeHighlight?.Invoke(_hoverNode);
            _hoverNode = node;
            RecreateHoverGhost(node, settings);
        }

        public Color ResolveBuildPreviewColor(string nodeId, MapBuildPlacementVisualSettings settings)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return settings.InvalidColor;
            }

            if (HasPendingBuild(nodeId))
            {
                return settings.PlacedGhostColor;
            }

            if (TryGetCurrentBuildPreview(nodeId, out var preview) && preview != null)
            {
                return preview.Valid ? settings.ValidColor : settings.InvalidColor;
            }

            return string.Equals(_hoverBuildPreviewNodeId, nodeId, StringComparison.Ordinal)
                ? settings.PendingColor
                : settings.InvalidColor;
        }

        public bool ShouldRequestBuildPreview(string nodeId)
        {
            return !TryGetCurrentBuildPreview(nodeId, out _) &&
                   Time.unscaledTime >= _nextBuildPreviewRequestAt;
        }

        public void RenderHover(NodeView node, Color highlightColor)
        {
            if (node != null)
            {
                node.SetHighlight(true, highlightColor);
            }
        }

        public void RenderGhost(Color highlightColor)
        {
            _ghostPresenter.Render(highlightColor);
        }

        public void RefreshPreviewVisuals(MapBuildPlacementVisualSettings settings)
        {
            if (!(_inputState?.IsBuildModeActive() ?? false) || _hoverNode == null)
            {
                return;
            }

            var color = ResolveBuildPreviewColor(_hoverNode.NodeId, settings);
            _hoverNode.SetHighlight(true, color);
            _ghostPresenter.Render(color);
        }

        public void RequestBuildPreview(string nodeId, MapBuildPlacementVisualSettings settings)
        {
            if (string.IsNullOrWhiteSpace(nodeId) ||
                string.IsNullOrWhiteSpace(_buildType) ||
                string.IsNullOrWhiteSpace(_activeBuildCityId))
            {
                return;
            }

            _hoverBuildPreviewNodeId = nodeId.Trim();
            _inputState?.SetBuildPreviewTarget(_hoverBuildPreviewNodeId);
            _nextBuildPreviewRequestAt = Time.unscaledTime + Mathf.Max(0.02f, settings.PreviewRequestThrottleSeconds);
            _buildPreviewRequestSequence++;
            var requestId = $"build-preview-{_buildType}-{_buildPreviewRequestSequence}";
            var draftCache = PlanningDraftCache.EnsureInstance();
            draftCache?.TrackBuildPreviewRequest(requestId, _hoverBuildPreviewNodeId, _buildType, _activeBuildCityId);
            _planningIntentServiceProvider?.Invoke()?.PreviewBuild(requestId, _hoverBuildPreviewNodeId, _buildType, _activeBuildCityId);
        }

        public bool TryCommitBuildPlacement(
            NodeView node,
            MapBuildPlacementVisualSettings settings,
            Action<string> showUserError,
            Action<string, string> buildCommandSent)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return false;
            }

            var backendBuildingType = ResolveBackendBuildingType(_buildType);
            if (TryGetCurrentBuildPreview(node.NodeId, out var preview) &&
                preview != null &&
                !preview.Valid)
            {
                showUserError?.Invoke(BuildPreviewPresenter.ResolveMessage(preview));
            }

            if (!SendBuildCommand(backendBuildingType, node.NodeId, showUserError, buildCommandSent))
            {
                return false;
            }

            var ownerId = ResolveLocalOwnerId();
            if (ShouldRenderPendingBuildGhost(node.NodeId))
            {
                map.ApplyBuildingPlacement(node.NodeId, backendBuildingType, ownerId, true, 100, settings.PlacedGhostColor);
            }

            _pendingBuildState.Add(new MapPlanningInputController.PendingBuildRecord
            {
                buildingType = backendBuildingType,
                nodeId = node.NodeId,
                ownerId = ownerId,
                isGhost = true
            });
            ExitBuildMode();
            return true;
        }

        public void ApplyBackendBuildCommand(string buildingType, string nodeId, bool isGhost, string ownerId, int hp, Color placedGhostColor)
        {
            if (MapRenderer.Instance == null)
            {
                return;
            }

            MapRenderer.Instance.ApplyBuildingPlacement(nodeId, buildingType, ownerId, isGhost, hp, placedGhostColor);
            if (GameStateCache.Instance != null)
            {
                var cacheNode = GameStateCache.Instance.GetNode(nodeId);
                if (cacheNode != null)
                {
                    cacheNode.BuildingType = buildingType ?? string.Empty;
                    cacheNode.Owner = ownerId ?? string.Empty;
                    cacheNode.BuildingHp = hp;
                }
            }

            if (!isGhost)
            {
                RemovePendingBuild(nodeId);
            }
        }

        public void RemovePendingBuild(string nodeId)
        {
            if (!string.IsNullOrEmpty(nodeId))
            {
                _pendingBuildState.RemoveNode(nodeId);
            }
        }

        public string GetLastPendingBuildNodeId()
        {
            return _pendingBuildState.GetLastNodeId();
        }

        public void RememberPendingBuild(MapPlanningInputController.PendingBuildRecord record)
        {
            _pendingBuildState.Add(record);
        }

        public void RollbackPendingBuild(string nodeId)
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

        public bool HasPendingBuild(string nodeId)
        {
            return !string.IsNullOrWhiteSpace(nodeId) && _pendingBuildState.HasNode(nodeId);
        }

        public void ClearBuildPreviewState()
        {
            _hoverBuildPreviewNodeId = string.Empty;
            _nextBuildPreviewRequestAt = 0f;
            _inputState?.ClearBuildPreviewTarget();
            (_draftCacheProvider?.Invoke() ?? PlanningDraftCache.Instance)?.ClearBuildPreview();
        }

        private void RecreateHoverGhost(NodeView node, MapBuildPlacementVisualSettings settings)
        {
            ClearHoverGhost();
            if (node == null || string.IsNullOrEmpty(_buildType))
            {
                return;
            }

            _ghostPresenter.Recreate(node, _buildType, ResolveLocalOwnerId(), settings.ValidColor);
        }

        private void ClearHoverGhost()
        {
            _ghostPresenter.Clear();
        }

        private bool SendBuildCommand(
            string buildingType,
            string nodeId,
            Action<string> showUserError,
            Action<string, string> buildCommandSent)
        {
            buildingType = ResolveBackendBuildingType(buildingType);
            if (string.IsNullOrWhiteSpace(_activeBuildCityId))
            {
                Debug.LogWarning($"[MapPlanningInputController] Missing build city context. node={nodeId} building={buildingType}");
                showUserError?.Invoke("Current node is missing city context.");
                return false;
            }

            _planningIntentServiceProvider?.Invoke()?.BuildToken(nodeId, buildingType, _activeBuildCityId);
            buildCommandSent?.Invoke(buildingType, nodeId);
            return true;
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

            var draftCache = _draftCacheProvider?.Invoke() ?? PlanningDraftCache.Instance;
            preview = draftCache != null ? draftCache.CurrentBuildPreview : null;
            return preview != null &&
                   string.Equals(preview.NodeId, nodeId.Trim(), StringComparison.Ordinal) &&
                   string.Equals(preview.BuildingTypeId, _buildType, StringComparison.Ordinal) &&
                   string.Equals(preview.CityId, _activeBuildCityId, StringComparison.Ordinal);
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

        private string ResolveBackendBuildingType(string buildingType)
        {
            return _backendBuildingTypeResolver != null
                ? _backendBuildingTypeResolver(buildingType)
                : Normalize(buildingType);
        }

        private string ResolveLocalOwnerId()
        {
            return _localOwnerIdProvider != null ? _localOwnerIdProvider() : string.Empty;
        }

        private static bool IsManualPlacementBlocked(string buildingType, MapBuildPlacementVisualSettings settings)
        {
            return BuildPlacementInputMode.IsManualPlacementBlocked(
                buildingType,
                settings.DisallowManualCityCorePlacement,
                settings.ManualPlacementBlockedBuildingTypes);
        }

        private static string Normalize(string value)
        {
            return MapInputTokens.Normalize(value);
        }
    }
}
