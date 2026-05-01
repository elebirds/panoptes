/*************************************************
 * Project: Panoptes
 * File: MapPlanningInputController.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Unity scene entry for map-related planning input, selection, and backend playback bridging.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Presentation.Animation;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.Map.InputAdapter;
using Panoptes.Presentation.Planning.Input.Intents;
using Panoptes.Presentation.Planning.Input.Modes;
using Panoptes.Presentation.Planning.Feedback;
using Panoptes.Presentation.Planning.Input.State;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.UI.Common;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class MapPlanningInputController : MonoBehaviour
    {
        public enum BuildPlacementRule
        {
            AnyTerrain = 0,
            ResourceOnly = 1,
            CityOnly = 2
        }

        [Serializable]
        public struct PendingBuildRecord
        {
            public string buildingType;
            public string nodeId;
            public string ownerId;
            public bool isGhost;
        }

        [Serializable]
        private struct CityZone
        {
            public string ownerId;
            public Vector2Int center;
            public Vector2Int size;
        }

        private enum Mode
        {
            None = 0,
            Build = 1
        }

        // Legacy enum name kept for existing planning action semantics.
        public enum CombatActionMode
        {
            None = 0,
            Move = 1,
            Attack = 2,
            Charge = 3
        }

        public static MapPlanningInputController Instance { get; private set; }

        [Header("Raycast")]
        [SerializeField] private Camera inputCamera;
        [SerializeField] private LayerMask raycastMask = ~0;
        [SerializeField] private float raycastDistance = 200f;
        
        [Header("Input Gate")]
        [SerializeField] private float modeSwitchInputBlockSeconds = 0.12f;

        [Header("Combat Preview")]
        [SerializeField] private Color moveHighlightColor = new Color(0.35f, 1f, 0.45f, 0.9f);
        [SerializeField] private Color moveFirstTurnColor = new Color(1f, 0.85f, 0.3f, 0.95f);
        [SerializeField] private Color moveFutureTurnColor = new Color(0.35f, 0.75f, 1f, 0.95f);
        [SerializeField] private Color moveInvalidColor = new Color(1f, 0.35f, 0.35f, 0.95f);
        [SerializeField] private float movePreviewRequestThrottleSeconds = 0.1f;
        [SerializeField] private float moveTurnMarkerHeight = 0.65f;
        [SerializeField] private bool onlyControlOwnUnits = true;
        [SerializeField] private Color movePathArrowColor = new Color(0.35f, 1f, 0.45f, 0.92f);
        [SerializeField] private Color movePathDestinationColor = new Color(0.25f, 0.95f, 0.55f, 0.95f);
        [SerializeField] private Color attackRangeHighlightColor = new Color(1f, 0.45f, 0.25f, 0.9f);

        [Header("Move Preview Ghost")]
        [SerializeField] private bool enableMovePreviewGhost = true;
        [SerializeField] private Color movePreviewGhostColor = new Color(0.35f, 1f, 0.45f, 0.72f);
        [SerializeField] private float movePreviewTravelDuration = 0.22f;
        [SerializeField] private float movePreviewArcHeight = 0.12f;
        [SerializeField] private float movePreviewTargetYOffset = 0.02f;
        [SerializeField] private bool movePreviewUseLightweightProxy = true;
        [SerializeField] private bool movePreviewAlwaysMatchUnitVisual = true;
        [SerializeField] private Vector3 movePreviewProxyScale = new Vector3(0.34f, 0.72f, 0.34f);
        [SerializeField] private float movePreviewProxyYOffset = 0.36f;
        [SerializeField] private bool movePreviewProxyCastShadow = false;
        [Range(0f, 1f)] [SerializeField] private float movePreviewTintStrength = 0.28f;
        [SerializeField] private bool movePreviewKeepTextureColor = true;

        [Header("Build")]
        [SerializeField] private Color buildValidColor = new Color(0.35f, 1f, 0.35f, 0.92f);
        [SerializeField] private Color buildInvalidColor = new Color(1f, 0.3f, 0.3f, 0.92f);
        [SerializeField] private Color buildPendingColor = new Color(1f, 0.82f, 0.35f, 0.92f);
        [SerializeField] private Color buildPlacedGhostColor = new Color(0.6f, 1f, 0.6f, 0.92f);
        [SerializeField] private Color territoryHighlightColor = new Color(0.28f, 0.72f, 1f, 0.72f);
        [SerializeField] private float buildPreviewRequestThrottleSeconds = 0.1f;
        [SerializeField] private bool logInvalidBuildClick = true;
        [SerializeField] private string localOwnerIdOverride = string.Empty;
        [SerializeField] private bool useSafeZoneFallbackForCityPlacement = true;
        [SerializeField] private bool disallowManualCityCorePlacement = true;
        [SerializeField] private string[] manualPlacementBlockedBuildingTypes = { "city_core" };
        [SerializeField] private bool autoCreateCornerCityZones = true;
        [SerializeField] private int cornerInset = 2;
        [SerializeField] private CityZone[] cityZones;

        [Header("Damage Popup")]
        [SerializeField] private bool enableUnitDamagePopupFallback = true;
        [SerializeField] private float damagePopupRepeatCooldownSeconds = 0.12f;
        [SerializeField] private DamageNumberPopupController damagePopupController;

        [Header("Deploy")]
        [SerializeField] private string[] territoryExpansionUnitTypes =
        {
            "settler",
            "pioneer",
            "expander",
            "engineer"
        };

        private readonly HashSet<string> _highlightNodeIds = new();
        private readonly HashSet<string> _territoryHighlightNodeIds = new();
        private readonly PendingBuildState<PendingBuildRecord> _pendingBuildState = new(record => record.nodeId);
        private readonly PendingDeployState _pendingDeployState = new();
        private readonly MovePreviewGhostPresenter _movePreviewGhostPresenter = new();
        private readonly PendingMoveState _pendingMoveState = new();
        private readonly List<UnitView> _nodeClickUnits = new();
        private readonly Dictionary<string, int> _knownUnitHpByUnitId = new();
        private readonly Dictionary<string, float> _lastDamagePopupTimeByUnitId = new();
        private readonly MapPointerInput _pointerInput = new();
        private readonly IPlanningIntentSender _intentSender = new GamePlanningIntentSender();
        private IMapSelectionSurface _selectionSurface;

        private Mode _mode = Mode.None;
        private CombatActionMode _combatActionMode = CombatActionMode.None;
        private UnitView _selectedUnit;
        private BuildPlacementRule _buildRule;
        private string _buildType = string.Empty;
        private string _activeBuildCityId = string.Empty;
        private NodeView _hoverNode;
        private BuildingView _hoverGhost;
        private GameStateCache _cache;
        private PlanningDraftCache _draftCache;
        private MovePathOverlayController _movePathOverlay;
        private MovePreviewOverlayController _movePreviewOverlay;
        private bool _cacheEventsSubscribed;
        private float _ignoreInputUntilTime;
        private float _nextMovePreviewRequestAt;
        private float _nextBuildPreviewRequestAt;
        private int _movePreviewRequestSequence;
        private int _buildPreviewRequestSequence;
        private string _hoverPreviewNodeId = string.Empty;
        private string _hoverBuildPreviewNodeId = string.Empty;
        private UnitInfoPanelController _unitInfoPanelController;
        private UnitView _buildingInfoProxy;
        private StaticCatalogCache _staticCatalogCache;

        public IReadOnlyList<PendingBuildRecord> PendingBuilds => _pendingBuildState.Records;
        public UnitView SelectedUnit => _selectedUnit;
        public CombatActionMode CurrentCombatActionMode => _combatActionMode;
        public string CurrentCombatPrompt => GetCombatPrompt();

        public event Action<string, string> MoveCommandSent;
        public event Action<string, string> BuildCommandSent;
        public event Action CombatSelectionChanged;
        public event Action<UnitView> UnitSelectionChanged;
        public event Action NonBuildingMapClicked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }

            _movePathOverlay = new MovePathOverlayController(transform);
            _movePreviewOverlay = new MovePreviewOverlayController(transform);
            _selectionSurface = new MapSelectionSurface(_pointerInput, () => inputCamera, () => raycastMask, () => raycastDistance);
        }

        private void OnEnable()
        {
            SubscribeCacheEvents();
            SubscribeDraftCacheEvents();
        }

        private void OnDisable()
        {
            UnsubscribeCacheEvents();
            UnsubscribeDraftCacheEvents();
            ClearMovePreviewState();
            ClearAllMovePreviews();
            _movePathOverlay?.ClearAllMovePathMarkers();
            _movePreviewOverlay?.ClearPreview();
            _movePreviewGhostPresenter.DisposeMaterial();
            ClearAllPendingDeployGhosts();
            ClearTerritoryHighlights();
            _knownUnitHpByUnitId.Clear();
            _lastDamagePopupTimeByUnitId.Clear();
            if (_buildingInfoProxy != null)
            {
                Destroy(_buildingInfoProxy.gameObject);
                _buildingInfoProxy = null;
            }
        }

        private void Update()
        {
            if (!_cacheEventsSubscribed)
            {
                SubscribeCacheEvents();
            }

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }

            if (inputCamera == null || !HasMouse())
            {
                return;
            }

            if (Time.unscaledTime < _ignoreInputUntilTime)
            {
                return;
            }

            if (_mode == Mode.Build)
            {
                UpdateBuildMode();
                return;
            }

            UpdateCombatMode();

            if (GetLeftMouseButtonDown())
            {
                if (IsPointerOverUI())
                {
                    return;
                }

                // In attack targeting mode, consume click as combat command first.
                // This prevents building/resource info panels from hijacking the click.
                if (_combatActionMode == CombatActionMode.Attack)
                {
                    ClearTerritoryHighlights();
                    NonBuildingMapClicked?.Invoke();

                    if (TryGetClickedNodeContext(out var attackNode, out _) &&
                        attackNode != null &&
                        !string.IsNullOrWhiteSpace(attackNode.NodeId) &&
                        IsEnemyStructureNode(attackNode.NodeId))
                    {
                        if (TryIssueStructureTargetOrder(attackNode.NodeId))
                        {
                            _combatActionMode = CombatActionMode.None;
                            NotifyCombatSelectionChanged();
                        }
                        return;
                    }

                    HandleCombatSelectionClick();
                    return;
                }

                if (_combatActionMode == CombatActionMode.Move)
                {
                    ClearTerritoryHighlights();
                    NonBuildingMapClicked?.Invoke();
                    HandleMoveSelectionClick();
                    return;
                }

                if (ShouldPrioritizeStructureAttackClick())
                {
                    ClearTerritoryHighlights();
                    NonBuildingMapClicked?.Invoke();
                    HandleCombatSelectionClick();
                    return;
                }

                if (TrySelectOwnedUnitFromNodeClick())
                {
                    return;
                }

                if (TryOpenBuildingInfoFromClick())
                {
                    return;
                }

                ClearTerritoryHighlights();
                NonBuildingMapClicked?.Invoke();
                HandleCombatSelectionClick();
            }

            if (GetRightMouseButtonDown())
            {
                HandleCombatCancel();
            }
        }

        #region Commands

        public void EnterBuildPlacementAny(string buildingType, string cityId)
        {
            EnterBuildPlacement(buildingType, cityId, BuildPlacementRule.AnyTerrain);
        }

        public void EnterBuildPlacementResource(string buildingType, string cityId)
        {
            EnterBuildPlacement(buildingType, cityId, BuildPlacementRule.ResourceOnly);
        }

        public void EnterBuildPlacementCity(string buildingType, string cityId)
        {
            EnterBuildPlacement(buildingType, cityId, BuildPlacementRule.CityOnly);
        }

        public void CancelCurrentMode()
        {
            ExitBuildMode();
            ClearCombatSelection();
            BlockInputAfterModeSwitch();
        }

        public void BeginMoveSelection()
        {
            if (_selectedUnit == null)
            {
                return;
            }

            ExitBuildMode();
            _combatActionMode = CombatActionMode.Move;
            RefreshPreviewVisuals();
            NotifyCombatSelectionChanged();
            BlockInputAfterModeSwitch();
        }

        public void BeginAttackSelection()
        {
            if (_selectedUnit == null || !CanSelectedUnitAttack())
            {
                return;
            }

            ExitBuildMode();
            ClearMovePreviewState();
            _combatActionMode = CombatActionMode.Attack;
            RefreshAttackRangeHighlights();
            NotifyCombatSelectionChanged();
            BlockInputAfterModeSwitch();
        }

        public void BeginChargeSelection()
        {
            if (_selectedUnit == null || !CanSelectedUnitCharge())
            {
                return;
            }

            ExitBuildMode();
            ClearMovePreviewState();
            _combatActionMode = CombatActionMode.Charge;
            NotifyCombatSelectionChanged();
            BlockInputAfterModeSwitch();
        }

        public void IssueHoldOrder()
        {
            if (_selectedUnit == null)
            {
                return;
            }

            ExitBuildMode();
            ClearMovePreviewState();
            _combatActionMode = CombatActionMode.None;
            _intentSender.HoldUnit(_selectedUnit.UnitId);
            NotifyCombatSelectionChanged();
        }

        public void ClearCombatSelection()
        {
            ClearMoveSelection();
            ClearMovePreviewState();
            _combatActionMode = CombatActionMode.None;
            NotifyCombatSelectionChanged();
        }

        public bool RequestExpandTerritoryForSelectedUnit()
        {
            if (_selectedUnit == null)
            {
                return false;
            }

            var centerNodeId = ResolveExpandCenterNodeId(_selectedUnit.UnitId, _selectedUnit.GridPos);
            if (string.IsNullOrWhiteSpace(centerNodeId))
            {
                ClearPendingDeployCityCoreGhostForUnit(_selectedUnit.UnitId);
                ShowUserError("Missing deploy target node.");
                return false;
            }

            ShowPendingDeployCityCoreGhost(_selectedUnit.UnitId, centerNodeId);
            _intentSender.ExpandTerritory(_selectedUnit.UnitId, centerNodeId);
            var phase = _cache != null ? _cache.Phase : string.Empty;
            Debug.Log($"[MapPlanningInputController] territory action sent. unit={_selectedUnit.UnitId} center={centerNodeId} phase={phase}");
            return true;
        }

        public bool RequestExpandTerritory(string unitId, string centerNodeId = null)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            var resolvedCenterNodeId = centerNodeId;
            if (string.IsNullOrWhiteSpace(resolvedCenterNodeId))
            {
                resolvedCenterNodeId = ResolveExpandCenterNodeId(unitId, default);
            }

            if (string.IsNullOrWhiteSpace(resolvedCenterNodeId))
            {
                ClearPendingDeployCityCoreGhostForUnit(unitId);
                ShowUserError("Missing deploy target node.");
                return false;
            }

            ShowPendingDeployCityCoreGhost(unitId, resolvedCenterNodeId);
            _intentSender.ExpandTerritory(unitId, resolvedCenterNodeId);
            var phase = _cache != null ? _cache.Phase : string.Empty;
            Debug.Log($"[MapPlanningInputController] territory action sent. unit={unitId} center={resolvedCenterNodeId} phase={phase}");
            return true;
        }

        public bool IsUnitMovePending(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            return _pendingMoveState.Has(unitId);
        }

        public void ApplyBackendMoveCommand(string unitId, string targetNodeId, bool enqueue = true, bool followCamera = true, IReadOnlyList<string> pathNodeIds = null)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            RemoveMovePreview(unitId);

            if (enqueue)
            {
                var queue = AnimationQueue.Instance;
                if (queue == null)
                {
                    var go = new GameObject("AnimationQueue");
                    queue = go.AddComponent<AnimationQueue>();
                }

                if (queue != null)
                {
                    queue.EnqueueUnitMove(unitId, targetNodeId, followCamera, pathNodeIds);
                    return;
                }
            }

            if (MapRenderer.Instance != null)
            {
                MapRenderer.Instance.SetUnitNode(unitId, targetNodeId);
            }
        }

        public void ApplyBackendBuildCommand(string buildingType, string nodeId, bool isGhost, string ownerId, int hp = 100)
        {
            if (MapRenderer.Instance == null)
            {
                return;
            }

            MapRenderer.Instance.ApplyBuildingPlacement(nodeId, buildingType, ownerId, isGhost, hp, buildPlacedGhostColor);
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
        #endregion

        #region Build

        private void EnterBuildPlacement(string buildingType, string cityId, BuildPlacementRule rule)
        {
            _buildType = ResolveBackendBuildingType(NormalizeToken(buildingType));
            if (IsManualPlacementBlocked(_buildType))
            {
                Debug.Log($"[MapPlanningInputController] {_buildType} is pre-placed by map config and cannot be manually built.");
                ExitBuildMode();
                return;
            }
            _activeBuildCityId = string.IsNullOrWhiteSpace(cityId) ? string.Empty : cityId.Trim();
            if (string.IsNullOrEmpty(_activeBuildCityId))
            {
                Debug.LogWarning($"[MapPlanningInputController] Missing build city context before entering build mode. building={_buildType}");
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
                    Debug.LogWarning("[MapPlanningInputController] Invalid build target: cursor is outside map tile.");
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
                _pendingBuildState.Add(new PendingBuildRecord
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
                Debug.LogWarning($"[MapPlanningInputController] Missing build city context. node={nodeId} building={buildingType}");
                ShowUserError("Current node is missing city context.");
                return false;
            }

            _intentSender.BuildToken(nodeId, buildingType, _activeBuildCityId);
            BuildCommandSent?.Invoke(buildingType, nodeId);
            return true;
        }

        private void RemovePendingBuild(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            _pendingBuildState.RemoveNode(nodeId);
        }

        private string GetLastPendingBuildNodeId()
        {
            return _pendingBuildState.GetLastNodeId();
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

            return _pendingBuildState.HasNode(nodeId);
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
            _intentSender.PreviewBuild(requestId, _hoverBuildPreviewNodeId, _buildType, _activeBuildCityId);
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
        #endregion

        #region Combat

        private void HandleMoveSelectionClick()
        {
            if (_selectedUnit == null)
            {
                _combatActionMode = CombatActionMode.None;
                NotifyCombatSelectionChanged();
                return;
            }

            if (!TryGetClickedNodeContext(out var node, out _) ||
                node == null ||
                string.IsNullOrWhiteSpace(node.NodeId))
            {
                ClearMovePreviewState();
                return;
            }

            if (TryIssueAuthoritativeMoveOrder(node.NodeId))
            {
                _combatActionMode = CombatActionMode.None;
                NotifyCombatSelectionChanged();
            }
        }

        private bool ShouldPrioritizeStructureAttackClick()
        {
            if (_selectedUnit == null ||
                _combatActionMode != CombatActionMode.Attack ||
                !CanSelectedUnitAttackStructures() ||
                !TryRaycastNode(out var node) ||
                node == null ||
                string.IsNullOrWhiteSpace(node.NodeId))
            {
                return false;
            }

            return true;
        }

        private void HandleCombatSelectionClick()
        {
            if (_combatActionMode == CombatActionMode.Attack &&
                TryGetClickedNodeContext(out var attackNodeView, out _) &&
                attackNodeView != null &&
                !string.IsNullOrWhiteSpace(attackNodeView.NodeId) &&
                IsEnemyStructureNode(attackNodeView.NodeId))
            {
                if (TryIssueStructureTargetOrder(attackNodeView.NodeId))
                {
                    _combatActionMode = CombatActionMode.None;
                    NotifyCombatSelectionChanged();
                }
                return;
            }

            if (TryRaycastUnit(out var unit) && unit != null)
            {
                if (IsHostileTarget(unit))
                {
                    TryIssueUnitTargetOrder(unit);
                    return;
                }

                SelectUnit(unit);
                return;
            }

            if (!IsCombatPhase())
            {
                if (TryRaycastNode(out _))
                {
                    CloseCurrentInfoSelection();
                }
                return;
            }

            if (_selectedUnit != null &&
                TryRaycastNode(out var node) &&
                node != null)
            {
                if (!string.IsNullOrEmpty(node.NodeId))
                {
                    if (_combatActionMode == CombatActionMode.Attack)
                    {
                        if (TryIssueStructureTargetOrder(node.NodeId))
                        {
                            _combatActionMode = CombatActionMode.None;
                            NotifyCombatSelectionChanged();
                        }
                        return;
                    }
                    if (_combatActionMode == CombatActionMode.Move)
                    {
                        if (TryIssueAuthoritativeMoveOrder(node.NodeId))
                        {
                            _combatActionMode = CombatActionMode.None;
                            NotifyCombatSelectionChanged();
                        }
                        return;
                    }
                }
            }

            if (TryRaycastNode(out _))
            {
                if (_combatActionMode == CombatActionMode.None)
                {
                    CloseCurrentInfoSelection();
                }
                return;
            }

            ClearCombatSelection();
        }

        private void UpdateCombatMode()
        {
            if (_selectedUnit == null)
            {
                if (!string.IsNullOrEmpty(_hoverPreviewNodeId))
                {
                    ClearMovePreviewState();
                }

                if (_highlightNodeIds.Count > 0)
                {
                    ClearNodeHighlights();
                }
                return;
            }

            if (_combatActionMode == CombatActionMode.Attack)
            {
                if (_highlightNodeIds.Count == 0)
                {
                    RefreshAttackRangeHighlights();
                }
                return;
            }

            if (_combatActionMode != CombatActionMode.Move)
            {
                if (_highlightNodeIds.Count > 0)
                {
                    ClearNodeHighlights();
                }
                if (!string.IsNullOrEmpty(_hoverPreviewNodeId))
                {
                    ClearMovePreviewState();
                }
                return;
            }

            if (IsPointerOverUI())
            {
                ClearMovePreviewState();
                return;
            }

            if (!TryRaycastNode(out var node) || node == null || string.IsNullOrEmpty(node.NodeId))
            {
                ClearMovePreviewState();
                return;
            }

            if (string.Equals(_hoverPreviewNodeId, node.NodeId, StringComparison.Ordinal))
            {
                return;
            }

            if (Time.unscaledTime < _nextMovePreviewRequestAt)
            {
                return;
            }

            RequestMovePreview(node.NodeId);
        }

        private void HandleCombatCancel()
        {
            if (_mode == Mode.Build)
            {
                ExitBuildMode();
                BlockInputAfterModeSwitch();
                return;
            }

            if (_combatActionMode != CombatActionMode.None)
            {
                _combatActionMode = CombatActionMode.None;
                ClearMovePreviewState();
                NotifyCombatSelectionChanged();
                BlockInputAfterModeSwitch();
                return;
            }

            if (_selectedUnit != null)
            {
                ClearCombatSelection();
                BlockInputAfterModeSwitch();
            }
        }

        private void RequestMovePreview(string targetNodeId)
        {
            if (_selectedUnit == null || string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            ClearNodeHighlights();
            _hoverPreviewNodeId = targetNodeId;
            _nextMovePreviewRequestAt = Time.unscaledTime + Mathf.Max(0.02f, movePreviewRequestThrottleSeconds);
            _movePreviewRequestSequence++;
            var requestId = $"move-preview-{_selectedUnit.UnitId}-{_movePreviewRequestSequence}";
            PlanningDraftCache.EnsureInstance()?.TrackPreviewRequest(requestId, _selectedUnit.UnitId, "move", targetNodeId);
            Debug.Log($"[MapPlanningInputController] 请求路径预览 unit={_selectedUnit.UnitId} hover_node={targetNodeId} request={requestId}");
            _intentSender.PreviewMove(requestId, _selectedUnit.UnitId, targetNodeId);
        }

        private bool TryIssueAuthoritativeMoveOrder(string targetNodeId)
        {
            if (_selectedUnit == null || string.IsNullOrWhiteSpace(targetNodeId))
            {
                return false;
            }

            if (!TryGetCurrentMovePreview(_selectedUnit.UnitId, targetNodeId, out var preview))
            {
                RequestMovePreview(targetNodeId);
                preview = new PathPreviewDto
                {
                    Valid = true,
                    PathNodeIds = new List<string>()
                };
            }

                if (preview != null && !preview.Valid)
                {
                    ShowUserError(MovePreviewPresenter.ResolveErrorMessage(preview, targetNodeId));
                    return false;
                }

            Debug.Log($"[MapPlanningInputController] 涓嬭揪绉诲姩鍛戒护 unit={_selectedUnit.UnitId} target={targetNodeId} preview_valid={preview.Valid} preview_nodes={preview.PathNodeIds.Count}");
            SendMoveCommand(_selectedUnit.UnitId, targetNodeId);
            return true;
        }

        private string GetCombatPrompt()
        {
            if (_selectedUnit == null)
            {
                return "Select your unit to start issuing commands.";
            }

            return _combatActionMode switch
            {
                CombatActionMode.Move => "悬停节点预览路径，点击后下达移动指令。",
                CombatActionMode.Attack => "点击敌方单位或敌方建筑下达攻击指令。",
                CombatActionMode.Charge => "点击敌方单位下达冲锋指令。",
                _ => "选择动作后再指定目标"
            };
        }

        private void NotifyCombatSelectionChanged()
        {
            CombatSelectionChanged?.Invoke();
        }
        #endregion

        #region Targeting

        private bool TryIssueUnitTargetOrder(UnitView targetUnit)
        {
            if (_selectedUnit == null || targetUnit == null)
            {
                return false;
            }

            switch (_combatActionMode)
            {
                case CombatActionMode.Attack:
                    TryResolvePlannedMoveTargetNodeId(_selectedUnit.UnitId, out var plannedMoveTargetNodeId);
                    ClearPendingMoveStateForUnit(_selectedUnit.UnitId);
                    _intentSender.AttackUnit(_selectedUnit.UnitId, targetUnit.UnitId, plannedMoveTargetNodeId);
                    PlaySelectedAttackFeedback();
                    _combatActionMode = CombatActionMode.None;
                    NotifyCombatSelectionChanged();
                    return true;
                case CombatActionMode.Charge:
                    var map = MapRenderer.Instance;
                    if (map == null || !map.TryGetNodeIdByGrid(targetUnit.GridPos, out var targetNodeId))
                    {
                        return false;
                    }
                    _intentSender.ChargeUnit(_selectedUnit.UnitId, targetNodeId, targetUnit.UnitId);
                    _combatActionMode = CombatActionMode.None;
                    NotifyCombatSelectionChanged();
                    return true;
                default:
                    return false;
            }
        }

        private bool IsHostileTarget(UnitView unit)
        {
            if (unit == null || _selectedUnit == null || unit == _selectedUnit)
            {
                return false;
            }

            if (_combatActionMode != CombatActionMode.Attack && _combatActionMode != CombatActionMode.Charge)
            {
                return false;
            }

            return !CanControlUnit(unit);
        }

        private bool CanSelectedUnitAttack()
        {
            return TryGetSelectedUnitCatalog(out var entry) && !HasTag(entry, "civilian");
        }

        private bool CanSelectedUnitAttackStructures()
        {
            return TryGetSelectedUnitCatalog(out var entry)
                   && entry != null
                   && entry.flags != null
                   && entry.flags.can_attack_structures;
        }

        private bool CanSelectedUnitCharge()
        {
            return TryGetSelectedUnitCatalog(out var entry) && HasTag(entry, "charge");
        }

        private bool TryGetSelectedUnitCatalog(out StaticCatalogCache.UnitEntryJson entry)
        {
            entry = null;
            return _selectedUnit != null &&
                   StaticCatalogCache.EnsureInstance() != null &&
                   StaticCatalogCache.Instance.TryGetUnit(_selectedUnit.UnitType, out entry);
        }

        private bool TryIssueStructureTargetOrder(string nodeId)
        {
            if (_selectedUnit == null ||
                _combatActionMode != CombatActionMode.Attack ||
                string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            TryResolvePlannedMoveTargetNodeId(_selectedUnit.UnitId, out var plannedMoveTargetNodeId);
            ClearPendingMoveStateForUnit(_selectedUnit.UnitId);
            _intentSender.AttackNode(_selectedUnit.UnitId, nodeId, plannedMoveTargetNodeId);
            PlaySelectedAttackFeedback();
            _combatActionMode = CombatActionMode.None;
            NotifyCombatSelectionChanged();
            return true;
        }

        private void PlaySelectedAttackFeedback()
        {
            if (_selectedUnit == null)
            {
                return;
            }

            _selectedUnit.PlayAttackAnimation();
        }

        private bool IsEnemyStructureNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetNodeState(nodeId.Trim(), out var nodeState) || nodeState == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(nodeState.BuildingType))
            {
                return false;
            }

            var localOwner = NormalizeToken(GetLocalOwnerId());
            var owner = NormalizeToken(nodeState.Owner);
            if (string.IsNullOrWhiteSpace(owner))
            {
                return false;
            }

            return !string.Equals(owner, localOwner, StringComparison.Ordinal);
        }

        private void RefreshAttackRangeHighlights()
        {
            ClearNodeHighlights();

            if (_selectedUnit == null || _combatActionMode != CombatActionMode.Attack)
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null || map.TileViews == null || map.TileViews.Count == 0)
            {
                return;
            }

            if (!TryResolveSelectedAttackOrigin(out var originGrid, out _))
            {
                return;
            }

            var attackRange = ResolveSelectedUnitAttackRange();
            if (attackRange <= 0)
            {
                return;
            }

            foreach (var pair in map.TileViews)
            {
                var nodeId = pair.Key;
                var nodeView = pair.Value;
                if (nodeView == null || string.IsNullOrWhiteSpace(nodeId))
                {
                    continue;
                }

                if (HexGrid.AxialDistance(originGrid, nodeView.GridPos) > attackRange)
                {
                    continue;
                }

                nodeView.SetHighlight(true, attackRangeHighlightColor);
                _highlightNodeIds.Add(nodeId);
            }
        }

        private bool IsNodeWithinSelectedAttackRange(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetNodeView(nodeId.Trim(), out var nodeView) || nodeView == null)
            {
                return false;
            }

            return IsGridWithinSelectedAttackRange(nodeView.GridPos);
        }

        private bool IsGridWithinSelectedAttackRange(Vector2Int targetGrid)
        {
            if (!TryResolveSelectedAttackOrigin(out var originGrid, out _))
            {
                return false;
            }

            return HexGrid.AxialDistance(originGrid, targetGrid) <= ResolveSelectedUnitAttackRange();
        }

        private bool TryResolveSelectedAttackOrigin(out Vector2Int originGrid, out string originNodeId)
        {
            originGrid = _selectedUnit != null ? _selectedUnit.GridPos : default;
            originNodeId = string.Empty;

            if (_selectedUnit == null)
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return true;
            }

            if (TryResolvePlannedMoveTargetNodeId(_selectedUnit.UnitId, out var plannedTargetNodeId) &&
                map.TryGetNodeView(plannedTargetNodeId, out var plannedNode) &&
                plannedNode != null)
            {
                originGrid = plannedNode.GridPos;
                originNodeId = plannedNode.NodeId;
                return true;
            }

            if (map.TryGetNodeIdByGrid(_selectedUnit.GridPos, out var currentNodeId))
            {
                originNodeId = currentNodeId;
            }

            return true;
        }

        private int ResolveSelectedUnitAttackRange()
        {
            if (TryGetSelectedUnitCatalog(out var entry) && entry != null)
            {
                return Mathf.Max(1, entry.attack_range);
            }

            return 1;
        }
        #endregion

        #region MoveState

        private void SendMoveCommand(string unitId, string targetNodeId)
        {
            if (!string.IsNullOrWhiteSpace(unitId))
            {
                var normalizedUnitId = unitId.Trim();
                // Move command should cancel any pending deploy intent for the same unit.
                ClearPendingDeployCityCoreGhostForUnit(normalizedUnitId);
                _pendingMoveState.MarkPending(normalizedUnitId, targetNodeId);
                if (TryGetCurrentMovePreview(normalizedUnitId, targetNodeId, out var preview) &&
                    preview != null &&
                    preview.Valid)
                {
                    _pendingMoveState.RememberPath(normalizedUnitId, preview.PathNodeIds);
                }
            }

            RemoveMovePreview(unitId);
            if (!TryApplyAuthoritativeMovePathMarkers(unitId, targetNodeId))
            {
                _movePathOverlay?.ClearMovePathMarkersForUnit(unitId);
            }
            Debug.Log($"[MapPlanningInputController] 发送移动消息 unit={unitId} target={targetNodeId}");
            _intentSender.MoveUnit(unitId, targetNodeId);
            MoveCommandSent?.Invoke(unitId, targetNodeId);
            ClearNodeHighlights();
        }

        private bool TryResolvePlannedMoveTargetNodeId(string unitId, out string targetNodeId)
        {
            var draftCache = _draftCache ?? PlanningDraftCache.Instance;
            return MoveSelectionInputMode.TryResolvePlannedTargetNodeId(
                unitId,
                _pendingMoveState,
                draftCache?.OrdersByUnitId,
                out targetNodeId);
        }

        private void ClearPendingMoveStateForUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            var normalizedUnitId = unitId.Trim();
            _pendingMoveState.ClearUnit(normalizedUnitId);
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
            return MoveSelectionInputMode.MatchesPreview(preview, unitId, targetNodeId);
        }

        private IReadOnlyList<string> GetQueuedMovePathNodeIds(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            var draftCache = _draftCache ?? PlanningDraftCache.Instance;
            return MoveSelectionInputMode.GetQueuedMovePathNodeIds(unitId, draftCache?.OrdersByUnitId);
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

            _pendingMoveState.RememberPath(unitId, pathNodeIds);
        }

        private IReadOnlyList<string> GetRememberedMovePathNodeIds(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            return _pendingMoveState.GetRememberedPath(unitId);
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
        #endregion

        #region Deploy

        private void ShowPendingDeployCityCoreGhost(string unitId, string centerNodeId)
        {
            var normalizedUnitId = NormalizeToken(unitId);
            var normalizedNodeId = string.IsNullOrWhiteSpace(centerNodeId) ? string.Empty : centerNodeId.Trim();
            if (string.IsNullOrEmpty(normalizedUnitId) || string.IsNullOrEmpty(normalizedNodeId))
            {
                return;
            }

            if (_pendingDeployState.TryGetGhostNode(normalizedUnitId, out var oldNodeId)
                && !string.IsNullOrWhiteSpace(oldNodeId)
                && !string.Equals(oldNodeId, normalizedNodeId, StringComparison.Ordinal))
            {
                TryClearPendingDeployGhostNode(oldNodeId);
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            if (!ShouldRenderPendingBuildGhost(normalizedNodeId))
            {
                _pendingDeployState.RemoveUnit(normalizedUnitId);
                return;
            }

            map.ApplyBuildingPlacement(normalizedNodeId, "city_core", GetLocalOwnerId(), true, 100, buildPlacedGhostColor);
            _pendingDeployState.SetGhostNode(normalizedUnitId, normalizedNodeId);
        }

        private void ClearPendingDeployCityCoreGhostForUnit(string unitId)
        {
            var normalizedUnitId = NormalizeToken(unitId);
            if (string.IsNullOrEmpty(normalizedUnitId))
            {
                return;
            }

            if (!_pendingDeployState.TryRemoveUnit(normalizedUnitId, out var nodeId))
            {
                return;
            }

            TryClearPendingDeployGhostNode(nodeId);
        }

        private void ClearAllPendingDeployGhosts()
        {
            if (_pendingDeployState.Count == 0)
            {
                return;
            }

            var nodeIDs = _pendingDeployState.SnapshotGhostNodes();
            _pendingDeployState.Clear();

            foreach (var nodeId in nodeIDs)
            {
                TryClearPendingDeployGhostNode(nodeId);
            }
        }

        private void TryClearPendingDeployGhostNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            // Keep real buildings intact; only clear stale ghost markers.
            if (map.TryGetNodeState(nodeId, out var state) && state != null &&
                !string.IsNullOrWhiteSpace(state.BuildingType))
            {
                return;
            }

            map.ApplyBuildingPlacement(nodeId.Trim(), string.Empty, string.Empty, false, 0);
        }

        private void TryResolvePendingDeployGhostByNode(string nodeId, NodeDto node)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || node == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(node.BuildingType))
            {
                return;
            }

            if (_pendingDeployState.Count == 0)
            {
                return;
            }

            var normalizedNodeId = nodeId.Trim();
            if (_pendingDeployState.TryFindUnitByNode(normalizedNodeId, out var unitIdToRemove))
            {
                _pendingDeployState.RemoveUnit(unitIdToRemove);
            }
        }

        private string ResolveExpandCenterNodeId(string unitId, Vector2Int fallbackGrid)
        {
            var normalizedUnitId = string.IsNullOrWhiteSpace(unitId) ? string.Empty : unitId.Trim();
            if (!string.IsNullOrEmpty(normalizedUnitId)
                && _pendingMoveState.TryGetTargetNodeId(normalizedUnitId, out var pendingNodeId))
            {
                return pendingNodeId;
            }

            if (MapRenderer.Instance != null
                && !string.IsNullOrEmpty(normalizedUnitId)
                && MapRenderer.Instance.TryGetUnitView(normalizedUnitId, out var unitView)
                && unitView != null)
            {
                var fromView = ResolveNodeIdByGrid(unitView.GridPos);
                if (!string.IsNullOrWhiteSpace(fromView))
                {
                    return fromView;
                }
            }

            if (fallbackGrid != default)
            {
                var fromFallback = ResolveNodeIdByGrid(fallbackGrid);
                if (!string.IsNullOrWhiteSpace(fromFallback))
                {
                    return fromFallback;
                }
            }

            return string.Empty;
        }
        #endregion

        #region Selection

        private bool TryOpenBuildingInfoFromClick()
        {
            if (!TryGetClickedNodeContext(out var node, out var nodeState))
            {
                return false;
            }

            if (!TryGetInspectableNodeInfo(nodeState, out var buildingType, out var isResourcePoint))
            {
                return false;
            }

            if (string.Equals(buildingType, "city_core", StringComparison.Ordinal))
            {
                HighlightTerritoryForNode(nodeState);
            }
            else
            {
                ClearTerritoryHighlights();
            }

            var proxy = GetOrCreateNodeInfoProxy(node, nodeState, buildingType, isResourcePoint);
            if (proxy == null)
            {
                return false;
            }

            ClearMoveSelection(false);
            NotifyUnitSelectionChanged(proxy);
            NotifyUnitInfoPanel(proxy);
            return true;
        }

        private bool TrySelectOwnedUnitFromNodeClick()
        {
            if (!TryGetClickedNodeContext(out var nodeView, out var nodeState))
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map == null || nodeView == null || string.IsNullOrWhiteSpace(nodeView.NodeId))
            {
                return false;
            }

            if (!map.TryGetUnitsOnNode(nodeView.NodeId, _nodeClickUnits))
            {
                return false;
            }

            UnitView selectedOwnedUnit = null;
            for (var i = 0; i < _nodeClickUnits.Count; i++)
            {
                var candidate = _nodeClickUnits[i];
                if (candidate == null || !CanControlUnit(candidate))
                {
                    continue;
                }

                if (selectedOwnedUnit == null)
                {
                    selectedOwnedUnit = candidate;
                }

                if (IsTerritoryExpansionUnitType(candidate.UnitType))
                {
                    selectedOwnedUnit = candidate;
                    break;
                }
            }

            if (selectedOwnedUnit == null)
            {
                return false;
            }

            if (ReferenceEquals(_selectedUnit, selectedOwnedUnit)
                && TryGetInspectableNodeInfo(nodeState, out _, out _))
            {
                return false;
            }

            SelectUnit(selectedOwnedUnit);
            return true;
        }

        private bool TryGetClickedNodeContext(out NodeView nodeView, out NodeDto nodeState)
        {
            EnsureSelectionSurface();
            return _selectionSurface.TryGetClickedNodeContext(out nodeView, out nodeState);
        }

        private static bool TryGetInspectableNodeInfo(NodeDto nodeState, out string buildingType, out bool isResourcePoint)
        {
            buildingType = string.Empty;
            isResourcePoint = false;
            if (nodeState == null)
            {
                return false;
            }

            buildingType = NormalizeToken(nodeState.BuildingType);
            isResourcePoint = nodeState.IsResourcePoint;
            return !string.IsNullOrEmpty(buildingType) || isResourcePoint;
        }

        private UnitView GetOrCreateNodeInfoProxy(NodeView nodeView, NodeDto nodeState, string normalizedBuildingType, bool isResourcePoint)
        {
            if (nodeView == null || nodeState == null)
            {
                return null;
            }

            if (_buildingInfoProxy == null)
            {
                var proxyGo = new GameObject("BuildingInfoProxy");
                _buildingInfoProxy = proxyGo.AddComponent<UnitView>();
                proxyGo.hideFlags = HideFlags.DontSave;
            }

            var infoType = normalizedBuildingType;
            if (string.IsNullOrEmpty(infoType) && isResourcePoint)
            {
                var resourceType = NormalizeToken(nodeState.ResourceType);
                infoType = string.IsNullOrEmpty(resourceType) ? "resource_point" : $"resource_{resourceType}";
            }

            if (string.IsNullOrEmpty(infoType))
            {
                return null;
            }

            var hp = nodeState.BuildingHp > 0 ? nodeState.BuildingHp : (isResourcePoint ? 1 : 100);
            var maxHp = ResolveNodeInfoMaxHp(nodeView, nodeState, infoType, hp, isResourcePoint);
            var unit = new UnitDto
            {
                Id = nodeState.Id ?? string.Empty,
                Type = infoType,
                Owner = !string.IsNullOrWhiteSpace(nodeState.Owner) ? nodeState.Owner : nodeState.TerritoryOwner,
                Q = nodeState.Q,
                R = nodeState.R,
                Hp = hp,
                MaxHp = maxHp
            };

            var worldPos = nodeView.BuildingAnchor != null
                ? nodeView.BuildingAnchor.position
                : nodeView.transform.position;

            _buildingInfoProxy.gameObject.SetActive(true);
            _buildingInfoProxy.Bind(unit, worldPos);
            _buildingInfoProxy.SetSelected(false);
            var collider = _buildingInfoProxy.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
            _buildingInfoProxy.gameObject.SetActive(false);
            return _buildingInfoProxy;
        }

        private static int ResolveNodeInfoMaxHp(NodeView nodeView, NodeDto nodeState, string infoType, int hp, bool isResourcePoint)
        {
            if (isResourcePoint)
            {
                return Mathf.Max(1, hp);
            }

            var maxHp = nodeState != null ? nodeState.BuildingMaxHp : 0;
            if (maxHp <= 0 && nodeView != null && nodeView.BuildingInstance != null)
            {
                maxHp = nodeView.BuildingInstance.MaxHitPoints;
            }

            if (maxHp <= 0)
            {
                var catalog = StaticCatalogCache.EnsureInstance();
                var normalizedType = NormalizeToken(infoType);
                if (catalog != null)
                {
                    if (string.Equals(normalizedType, "city_core", StringComparison.OrdinalIgnoreCase) &&
                        catalog.Rules != null &&
                        catalog.Rules.city_core_max_hp > 0)
                    {
                        maxHp = catalog.Rules.city_core_max_hp;
                    }
                    else if (catalog.TryGetBuilding(normalizedType, out var buildingEntry) && buildingEntry != null)
                    {
                        maxHp = buildingEntry.max_hp;
                    }
                }
            }

            return Mathf.Max(1, Mathf.Max(maxHp, hp));
        }

        private void CloseCurrentInfoSelection()
        {
            if (_selectedUnit != null)
            {
                ClearMoveSelection();
                return;
            }

            NotifyUnitSelectionChanged(null);
            NotifyUnitInfoPanel(null);
        }

        private void SelectUnit(UnitView unit)
        {
            if (unit == null)
            {
                return;
            }

            var canControl = !onlyControlOwnUnits || CanControlUnit(unit);

            ClearTerritoryHighlights();
            ClearMoveSelection(false);
            _selectedUnit = unit;
            _selectedUnit.SetSelected(true);
            _combatActionMode = CombatActionMode.None;
            ClearMovePreviewState();
            NotifyCombatSelectionChanged();
            NotifyUnitSelectionChanged(_selectedUnit);
            NotifyUnitInfoPanel(_selectedUnit);

            if (!canControl || !IsCombatPhase())
            {
                ClearNodeHighlights();
                return;
            }
        }

        private void ClearMoveSelection(bool notify = true)
        {
            var changed = _selectedUnit != null;
            if (_selectedUnit != null)
            {
                _selectedUnit.SetSelected(false);
            }

            _selectedUnit = null;
            ClearNodeHighlights();

            if (notify && changed)
            {
                NotifyUnitSelectionChanged(null);
            }

            if (changed)
            {
                NotifyUnitInfoPanel(null);
            }
        }

        private void NotifyUnitSelectionChanged(UnitView unit)
        {
            try
            {
                UnitSelectionChanged?.Invoke(unit);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapPlanningInputController] UnitSelectionChanged callback failed: {ex.Message}");
            }
        }

        private void NotifyUnitInfoPanel(UnitView unit)
        {
            if (_unitInfoPanelController == null)
            {
                _unitInfoPanelController = SceneObjectFinder.FindFirstSceneObject<UnitInfoPanelController>();
            }

            if (_unitInfoPanelController == null)
            {
                return;
            }

            if (unit == null)
            {
                _unitInfoPanelController.Close();
                return;
            }

            _unitInfoPanelController.OpenForUnit(unit);
        }
        #endregion

        #region Highlights

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
        #endregion

        #region MovePreviewGhost

        private void CreateOrUpdateMovePreview(string unitId, string targetNodeId)
        {
            _movePreviewGhostPresenter.CreateOrUpdate(unitId, targetNodeId, CreateMovePreviewGhostSettings(), this);
        }

        private MovePreviewGhostPresenter.Settings CreateMovePreviewGhostSettings()
        {
            return new MovePreviewGhostPresenter.Settings
            {
                Enable = enableMovePreviewGhost,
                GhostColor = movePreviewGhostColor,
                TravelDuration = movePreviewTravelDuration,
                ArcHeight = movePreviewArcHeight,
                TargetYOffset = movePreviewTargetYOffset,
                UseLightweightProxy = movePreviewUseLightweightProxy,
                AlwaysMatchUnitVisual = movePreviewAlwaysMatchUnitVisual,
                ProxyScale = movePreviewProxyScale,
                ProxyYOffset = movePreviewProxyYOffset,
                ProxyCastShadow = movePreviewProxyCastShadow,
                TintStrength = movePreviewTintStrength,
                KeepTextureColor = movePreviewKeepTextureColor
            };
        }

        private void RemoveMovePreview(string unitId)
        {
            _movePreviewGhostPresenter.Remove(unitId);
        }

        private void ClearAllMovePreviews()
        {
            _movePreviewGhostPresenter.ClearAll();
        }
        #endregion

        #region CacheEvents

        private void SubscribeCacheEvents()
        {
            if (_cacheEventsSubscribed)
            {
                return;
            }

            _cache = GameStateCache.Instance;
            _draftCache = PlanningDraftCache.EnsureInstance();
            if (_cache == null)
            {
                return;
            }

            _cache.OnTurnSettled += OnTurnSettled;
            _cache.OnNodeChanged += OnNodeChanged;
            _cache.OnUnitsChanged += OnUnitsChanged;
            _cache.OnTokenResult += OnTokenResult;
            _cache.OnPlanningCommandResult += OnPlanningCommandResult;
            _cacheEventsSubscribed = true;
        }

        private void SubscribeDraftCacheEvents()
        {
            _draftCache = PlanningDraftCache.EnsureInstance();
            _movePathOverlay ??= new MovePathOverlayController(transform);
            _movePreviewOverlay ??= new MovePreviewOverlayController(transform);
            _movePreviewOverlay.SetHostTransform(transform);
            if (_draftCache == null)
            {
                return;
            }

            _draftCache.PreviewChanged -= OnPreviewChanged;
            _draftCache.PreviewChanged += OnPreviewChanged;
            _draftCache.BuildPreviewChanged -= OnBuildPreviewChanged;
            _draftCache.BuildPreviewChanged += OnBuildPreviewChanged;
            _draftCache.OrdersChanged -= OnOrdersChanged;
            _draftCache.OrdersChanged += OnOrdersChanged;
            RefreshQueuedMovePathMarkers();
        }

        private void UnsubscribeCacheEvents()
        {
            if (!_cacheEventsSubscribed)
            {
                return;
            }

            if (_cache != null)
            {
                _cache.OnTurnSettled -= OnTurnSettled;
                _cache.OnNodeChanged -= OnNodeChanged;
                _cache.OnUnitsChanged -= OnUnitsChanged;
                _cache.OnTokenResult -= OnTokenResult;
                _cache.OnPlanningCommandResult -= OnPlanningCommandResult;
            }

            _cache = null;
            _cacheEventsSubscribed = false;
        }

        private void UnsubscribeDraftCacheEvents()
        {
            if (_draftCache == null)
            {
                return;
            }

            _draftCache.PreviewChanged -= OnPreviewChanged;
            _draftCache.BuildPreviewChanged -= OnBuildPreviewChanged;
            _draftCache.OrdersChanged -= OnOrdersChanged;
            _draftCache = null;
        }

        private void OnTurnSettled(TurnSettledEvent settledEvent)
        {
            ClearCombatSelection();
            _pendingMoveState.ClearAll();
            _movePathOverlay?.ClearAllMovePathMarkers();

            var settlement = settledEvent?.Settlement;
            if (settlement?.Sections != null)
            {
                for (var sectionIndex = 0; sectionIndex < settlement.Sections.Count; sectionIndex++)
                {
                    var section = settlement.Sections[sectionIndex];
                    if (section?.Events == null)
                    {
                        continue;
                    }

                    for (var eventIndex = 0; eventIndex < section.Events.Count; eventIndex++)
                    {
                        var eventItem = section.Events[eventIndex];
                        if (eventItem == null || eventItem.Type != "unit_moved")
                        {
                            continue;
                        }

                        var settledPathNodeIds = GetRememberedMovePathNodeIds(eventItem.UnitId);

                        if (!string.IsNullOrWhiteSpace(eventItem.UnitId))
                        {
                            var normalizedUnitId = eventItem.UnitId.Trim();
                            _pendingMoveState.ClearUnit(normalizedUnitId);
                            _movePathOverlay?.ClearMovePathMarkersForUnit(normalizedUnitId);
                        }

                        if (MapRenderer.Instance == null)
                        {
                            continue;
                        }

                        var grid = new Vector2Int(eventItem.ToQ, eventItem.ToR);
                        if (!MapRenderer.Instance.TryGetNodeIdByGrid(grid, out var targetNodeId))
                        {
                            continue;
                        }

                        ApplyBackendMoveCommand(eventItem.UnitId, targetNodeId, true, true, settledPathNodeIds);
                    }
                }
            }
            _pendingMoveState.ClearRememberedPaths();

            var builtBuildings = settlement?.BuiltBuildings;
            if (builtBuildings == null || builtBuildings.Count == 0)
            {
                return;
            }

            for (int i = 0; i < builtBuildings.Count; i++)
            {
                var built = builtBuildings[i];
                if (built == null || string.IsNullOrWhiteSpace(built.NodeId) || string.IsNullOrWhiteSpace(built.BuildingType))
                {
                    continue;
                }

                var hp = built.BuildingHp > 0 ? built.BuildingHp : 100;
                ApplyBackendBuildCommand(
                    built.BuildingType,
                    built.NodeId,
                    false,
                    built.OwnerId,
                    hp);
            }
        }

        private void OnTokenResult(TokenResultEvent e)
        {
            if (e == null)
            {
                return;
            }

            var action = NormalizeToken(e.Action);
            if (string.Equals(action, "expand_territory", StringComparison.Ordinal))
            {
                if (!e.Success)
                {
                    ClearAllPendingDeployGhosts();
                }
                return;
            }

            if (string.Equals(action, "build", StringComparison.Ordinal) && !e.Success)
            {
                RollbackPendingBuild(GetLastPendingBuildNodeId());
            }
        }

        private void OnPlanningCommandResult(PlanningCommandResultEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            if (string.Equals(NormalizeToken(evt.CommandType), "build", StringComparison.Ordinal))
            {
                if (!evt.Success)
                {
                    RollbackPendingBuild(evt.PrimaryId);
                }
                return;
            }

            if (!string.Equals(NormalizeToken(evt.CommandType), "unit_order", StringComparison.Ordinal) || evt.Success)
            {
                return;
            }

            var action = NormalizeToken(evt.Action);
            if (string.Equals(action, "move", StringComparison.Ordinal))
            {
                var unitId = evt.PrimaryId?.Trim();
                if (!string.IsNullOrWhiteSpace(unitId))
                {
                    _pendingMoveState.ClearUnit(unitId);
                    _movePathOverlay?.ClearMovePathMarkersForUnit(unitId);
                    RemoveMovePreview(unitId);
                }

                return;
            }

            if (string.Equals(action, "settle_city", StringComparison.Ordinal))
            {
                ClearPendingDeployCityCoreGhostForUnit(evt.PrimaryId);
            }
        }

        private void OnNodeChanged(NodeChangedEvent evt)
        {
            if (evt == null || evt.Node == null || string.IsNullOrWhiteSpace(evt.NodeID))
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            map.ApplyNodeSnapshot(evt.Node);
            TryResolvePendingDeployGhostByNode(evt.NodeID, evt.Node);
            if (map.TryGetNodeView(evt.NodeID, out var nodeView) && nodeView != null)
            {
                if (_territoryHighlightNodeIds.Contains(evt.NodeID))
                {
                    nodeView.SetHighlight(true, territoryHighlightColor);
                }
                else if (_highlightNodeIds.Contains(evt.NodeID))
                {
                    nodeView.SetHighlight(true, attackRangeHighlightColor);
                }
                else if (_movePreviewOverlay != null && _movePreviewOverlay.TryRestorePreviewHighlight(evt.NodeID, nodeView))
                {
                }
            }
        }

        private void OnUnitsChanged(UnitsChangedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            if (evt.RemovedIDs != null)
            {
                for (var i = 0; i < evt.RemovedIDs.Count; i++)
                {
                    var removedId = evt.RemovedIDs[i];
                    if (string.IsNullOrWhiteSpace(removedId))
                    {
                        continue;
                    }

                    map.RemoveRuntimeUnit(removedId, false);
                    RemoveMovePreview(removedId);
                    var normalizedRemovedId = removedId.Trim();
                    _pendingMoveState.ClearUnit(normalizedRemovedId);
                    _movePathOverlay?.ClearMovePathMarkersForUnit(normalizedRemovedId);
                    ClearPendingDeployCityCoreGhostForUnit(normalizedRemovedId);
                    _knownUnitHpByUnitId.Remove(normalizedRemovedId);
                    _lastDamagePopupTimeByUnitId.Remove(normalizedRemovedId);
                    if (_selectedUnit != null && string.Equals(_selectedUnit.UnitId, removedId, StringComparison.Ordinal))
                    {
                        ClearMoveSelection();
                    }
                }
            }

            if (evt.Added != null)
            {
                for (var i = 0; i < evt.Added.Count; i++)
                {
                    var added = evt.Added[i];
                    if (added == null)
                    {
                        continue;
                    }

                    map.TrySpawnRuntimeUnit(added, true, false);
                    var addedId = string.IsNullOrWhiteSpace(added.Id) ? string.Empty : added.Id.Trim();
                    if (!string.IsNullOrEmpty(addedId))
                    {
                        _knownUnitHpByUnitId[addedId] = Mathf.Max(0, added.Hp);
                    }
                }
            }

            if (evt.Moved != null)
            {
                var allowFallbackPopup = enableUnitDamagePopupFallback &&
                                         !string.Equals(evt.ChangeType, "settlement", StringComparison.OrdinalIgnoreCase);
                for (var i = 0; i < evt.Moved.Count; i++)
                {
                    var moved = evt.Moved[i];
                    if (moved == null || string.IsNullOrWhiteSpace(moved.Id))
                    {
                        continue;
                    }

                    var unitId = moved.Id.Trim();
                    var hpAfter = Mathf.Max(0, moved.Hp);

                    if (!_knownUnitHpByUnitId.TryGetValue(unitId, out var hpBefore))
                    {
                        if (map.TryGetUnitView(unitId, out var unitView) && unitView != null)
                        {
                            hpBefore = Mathf.Max(0, unitView.HitPoints);
                        }
                        else
                        {
                            hpBefore = hpAfter;
                        }
                    }

                    _knownUnitHpByUnitId[unitId] = hpAfter;

                    var damage = hpBefore - hpAfter;
                    if (allowFallbackPopup && damage > 0)
                    {
                        TryShowUnitDamagePopup(unitId, damage);
                    }
                }
            }

            if (_selectedUnit != null && _combatActionMode == CombatActionMode.Attack)
            {
                RefreshAttackRangeHighlights();
            }
        }

        private void TryShowUnitDamagePopup(string unitId, int damage)
        {
            if (string.IsNullOrWhiteSpace(unitId) || damage <= 0)
            {
                return;
            }

            var normalizedUnitId = unitId.Trim();
            if (_lastDamagePopupTimeByUnitId.TryGetValue(normalizedUnitId, out var lastPopupAt))
            {
                var cooldown = Mathf.Max(0f, damagePopupRepeatCooldownSeconds);
                if (Time.unscaledTime - lastPopupAt < cooldown)
                {
                    return;
                }
            }

            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetUnitView(normalizedUnitId, out var unitView) || unitView == null)
            {
                return;
            }

            if (damagePopupController == null)
            {
                damagePopupController = SceneObjectFinder.FindFirstSceneObject<DamageNumberPopupController>();
            }

            if (damagePopupController == null)
            {
                var popupRoot = new GameObject("DamageNumberPopupController_Fallback");
                damagePopupController = popupRoot.AddComponent<DamageNumberPopupController>();
            }

            damagePopupController.ShowDamage(unitView.transform, damage, isBuilding: false);
            _lastDamagePopupTimeByUnitId[normalizedUnitId] = Time.unscaledTime;
        }

        private void OnPreviewChanged()
        {
            RefreshPreviewVisuals();
        }

        private void OnBuildPreviewChanged()
        {
            RefreshBuildPreviewVisuals();
        }

        private void OnOrdersChanged()
        {
            RememberQueuedMovePaths();
            RefreshQueuedMovePathMarkers();
            if (_selectedUnit != null && _combatActionMode == CombatActionMode.Attack)
            {
                RefreshAttackRangeHighlights();
            }
        }
        #endregion

        #region Context

        private string GetLocalOwnerId()
        {
            if (!string.IsNullOrEmpty(localOwnerIdOverride))
            {
                return localOwnerIdOverride.Trim();
            }

            if (GameStateCache.Instance != null && !string.IsNullOrEmpty(GameStateCache.Instance.MyPlayerID))
            {
                return GameStateCache.Instance.MyPlayerID;
            }

            return "blue";
        }

        private bool IsCityCoreNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map != null && map.TryGetNodeState(nodeId, out var mapNode) && mapNode != null)
            {
                return string.Equals(NormalizeToken(mapNode.BuildingType), "city_core", StringComparison.Ordinal);
            }

            if (GameStateCache.Instance == null)
            {
                return false;
            }

            var cacheNode = GameStateCache.Instance.GetNode(nodeId);
            return cacheNode != null && string.Equals(NormalizeToken(cacheNode.BuildingType), "city_core", StringComparison.Ordinal);
        }

        private bool IsCityCoreOwnedByLocalPlayer(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            var ownerId = NormalizeToken(GetLocalOwnerId());
            var map = MapRenderer.Instance;
            if (map != null && map.TryGetNodeState(nodeId, out var mapNode) && mapNode != null)
            {
                var owner = NormalizeToken(mapNode.Owner);
                var territoryOwner = NormalizeToken(mapNode.TerritoryOwner);
                if ((!string.IsNullOrEmpty(owner) && string.Equals(owner, ownerId, StringComparison.Ordinal))
                    || (!string.IsNullOrEmpty(territoryOwner) && string.Equals(territoryOwner, ownerId, StringComparison.Ordinal)))
                {
                    return true;
                }

                return useSafeZoneFallbackForCityPlacement && mapNode.IsSafeZone;
            }

            if (GameStateCache.Instance == null)
            {
                return false;
            }

            var cacheNode = GameStateCache.Instance.GetNode(nodeId);
            if (cacheNode == null)
            {
                return false;
            }

            var cacheOwner = NormalizeToken(cacheNode.Owner);
            var cacheTerritoryOwner = NormalizeToken(cacheNode.TerritoryOwner);
            if ((!string.IsNullOrEmpty(cacheOwner) && string.Equals(cacheOwner, ownerId, StringComparison.Ordinal))
                || (!string.IsNullOrEmpty(cacheTerritoryOwner) && string.Equals(cacheTerritoryOwner, ownerId, StringComparison.Ordinal)))
            {
                return true;
            }

            return useSafeZoneFallbackForCityPlacement && cacheNode.IsSafeZone;
        }

        private bool CanControlUnit(UnitView unit)
        {
            if (unit == null)
            {
                return false;
            }

            var localOwner = GetLocalOwnerId();
            return string.Equals(NormalizeToken(unit.Faction), NormalizeToken(localOwner), StringComparison.Ordinal);
        }

        private bool IsCombatPhase()
        {
            var phase = _cache != null ? _cache.Phase : string.Empty;
            if (string.IsNullOrWhiteSpace(phase) && GameStateCache.Instance != null)
            {
                phase = GameStateCache.Instance.Phase;
            }

            var normalized = NormalizeToken(phase);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            return string.Equals(normalized, NormalizeToken(GamePhases.Planning), StringComparison.Ordinal);
        }

        private static string ResolveNodeIdByGrid(Vector2Int gridPos)
        {
            var map = MapRenderer.Instance;
            if (map == null)
            {
                return string.Empty;
            }

            return map.TryGetNodeIdByGrid(gridPos, out var nodeId) ? nodeId : string.Empty;
        }

        private string ResolveBackendBuildingType(string buildingType)
        {
            var normalized = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (TryGetBuildingConfig(normalized, out var entry, out var resolvedId) && entry != null)
            {
                return NormalizeToken(string.IsNullOrWhiteSpace(entry.id) ? resolvedId : entry.id);
            }

            return normalized;
        }

        private bool TryGetServerPlacementRule(string buildingType, out string placementRule, out string requiredResourceType)
        {
            placementRule = string.Empty;
            requiredResourceType = string.Empty;

            if (!TryGetBuildingConfig(buildingType, out var entry, out _ ) || entry == null)
            {
                return false;
            }

            placementRule = NormalizeToken(entry.placement_kind);
            requiredResourceType = NormalizeToken(entry.required_resource_type);
            return !string.IsNullOrEmpty(placementRule);
        }

        private bool TryGetBuildingConfig(string buildingType, out StaticCatalogCache.BuildingEntryJson entry, out string resolvedId)
        {
            entry = null;
            resolvedId = string.Empty;

            var key = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            var cache = ResolveStaticCatalogCache();
            if (cache == null)
            {
                return false;
            }

            if (cache.TryGetBuilding(key, out entry) && entry != null)
            {
                resolvedId = key;
                return true;
            }

            var aliases = GetBuildingAliasKeys(key);
            for (var i = 0; i < aliases.Length; i++)
            {
                var alias = aliases[i];
                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                if (cache.TryGetBuilding(alias, out entry) && entry != null)
                {
                    resolvedId = alias;
                    return true;
                }
            }

            return false;
        }

        private StaticCatalogCache ResolveStaticCatalogCache()
        {
            if (_staticCatalogCache != null)
            {
                return _staticCatalogCache;
            }

            _staticCatalogCache = StaticCatalogCache.Instance;
            if (_staticCatalogCache == null)
            {
                _staticCatalogCache = StaticCatalogCache.EnsureInstance();
            }

            return _staticCatalogCache;
        }

        private static string[] GetBuildingAliasKeys(string key)
        {
            switch (NormalizeToken(key))
            {
                case "lumberyard":
                    return new[] { "lumber" };
                case "lumber":
                    return new[] { "lumberyard" };
                case "engineer":
                    return new[] { "engineer_camp" };
                case "engineer_camp":
                    return new[] { "engineer" };
                case "archery":
                    return new[] { "barracks" };
                case "barracks":
                    return new[] { "archery" };
                case "blacksmith":
                case "backsmith":
                    return new[] { "workshop" };
                default:
                    return Array.Empty<string>();
            }
        }

        private static string NormalizeToken(string value)
        {
            return MapInputTokens.Normalize(value);
        }

        private static bool HasTag(StaticCatalogCache.UnitEntryJson entry, string tag)
        {
            return MapInputTokens.HasTag(entry, tag);
        }

        private bool IsTerritoryExpansionUnitType(string unitType)
        {
            return TerritoryDeployInputMode.IsTerritoryExpansionUnitType(unitType, territoryExpansionUnitTypes);
        }

        private static void ShowUserError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (ErrorToast.Instance != null)
            {
                ErrorToast.Instance.Show(message, false);
                return;
            }

            Debug.LogWarning($"[MapPlanningInputController] {message}");
        }

        private void BlockInputAfterModeSwitch()
        {
            _ignoreInputUntilTime = Time.unscaledTime + Mathf.Max(0f, modeSwitchInputBlockSeconds);
        }
        #endregion

        #region Pointer

        private bool TryRaycastNode(out NodeView nodeView)
        {
            EnsureSelectionSurface();
            return _selectionSurface.TryRaycastNode(out nodeView);
        }

        private bool TryRaycastUnit(out UnitView unitView)
        {
            EnsureSelectionSurface();
            return _selectionSurface.TryRaycastUnit(out unitView);
        }

        private bool IsPointerOverUI()
        {
            return _pointerInput.IsPointerOverUI();
        }

        private bool HasMouse()
        {
            return _pointerInput.HasPointer();
        }

        private Vector3 GetMousePosition()
        {
            return _pointerInput.GetPointerPosition();
        }

        private bool GetLeftMouseButtonDown()
        {
            return _pointerInput.IsPrimaryPressedThisFrame();
        }

        private bool GetRightMouseButtonDown()
        {
            return _pointerInput.IsCancelPressedThisFrame();
        }

        private void EnsureSelectionSurface()
        {
            _selectionSurface ??= new MapSelectionSurface(_pointerInput, () => inputCamera, () => raycastMask, () => raycastDistance);
        }
        #endregion
    }
}
