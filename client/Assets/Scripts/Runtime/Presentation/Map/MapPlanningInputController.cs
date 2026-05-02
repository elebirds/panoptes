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
using Panoptes.Core.Application.Services;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.Map.InputAdapter;
using Panoptes.Presentation.Planning.Input.Modes;
using Panoptes.Presentation.Planning.Feedback;
using Panoptes.Presentation.Planning.Input.State;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.UI.Common;
using Panoptes.Presentation.ViewModels;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.Map
{
    public sealed class MapPlanningInputController : MonoBehaviour, IMapPlanningInputCoordinatorContext, IMapBuildPlacementCoordinatorContext
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
        private readonly PendingBuildState<PendingBuildRecord> _pendingBuildState = new(record => record.nodeId);
        private readonly BuildPlacementGhostPresenter _buildPlacementGhostPresenter = new();
        private readonly MapMovePreviewPresentationController _movePreviewPresentation = new();
        private readonly MapPendingDeployGhostController _pendingDeployGhosts = new();
        private readonly MapTerritoryHighlightPresenter _territoryHighlights = new();
        private readonly PendingMoveState _pendingMoveState = new();
        private readonly List<UnitView> _nodeClickUnits = new();
        private readonly Dictionary<string, int> _knownUnitHpByUnitId = new();
        private readonly Dictionary<string, float> _lastDamagePopupTimeByUnitId = new();
        private readonly MapPointerInput _pointerInput = new();
        private readonly MapPlanningInputCoordinator _inputCoordinator = new();
        private readonly MapBuildPlacementCoordinator _buildPlacementCoordinator = new();
        private readonly MapPlanningInputStateAdapter _inputState = new();
        private readonly MapNodeInfoProxyFactory _nodeInfoProxyFactory = new();
        private readonly MapBuildingCatalogResolver _buildingCatalogResolver = new();
        private readonly MapAttackRangePresenter _attackRangePresenter = new();
        private PlanningIntentService _planningIntentService;
        private IMapSelectionSurface _selectionSurface;

        private UnitView _selectedUnit;
        private BuildPlacementRule _buildRule;
        private string _buildType = string.Empty;
        private string _activeBuildCityId = string.Empty;
        private NodeView _hoverNode;
        private GameStateCache _cache;
        private PlanningDraftCache _draftCache;
        private bool _cacheEventsSubscribed;
        private float _ignoreInputUntilTime;
        private float _nextMovePreviewRequestAt;
        private float _nextBuildPreviewRequestAt;
        private int _movePreviewRequestSequence;
        private int _buildPreviewRequestSequence;
        private string _hoverPreviewNodeId = string.Empty;
        private string _hoverBuildPreviewNodeId = string.Empty;
        private UnitInfoPanelController _unitInfoPanelController;

        public IReadOnlyList<PendingBuildRecord> PendingBuilds => _pendingBuildState.Records;
        public UnitView SelectedUnit => _selectedUnit;
        public CombatActionMode CurrentCombatActionMode => _inputState.CombatActionMode;
        public string CurrentCombatPrompt => _selectedUnit == null
            ? "Select your unit to start issuing commands."
            : _inputState.CurrentPrompt;

        public event Action<string, string> MoveCommandSent;
        public event Action<string, string> BuildCommandSent;
        public event Action CombatSelectionChanged;
        public event Action<UnitView> UnitSelectionChanged;
        public event Action NonBuildingMapClicked;

        [Inject]
        private void Construct(
            PlanningIntentService planningIntentService,
            PlanningToolService planningToolService,
            SelectionService selectionService,
            PlanningToolViewModel planningToolViewModel)
        {
            _planningIntentService = planningIntentService;
            _inputState.Configure(planningToolService, selectionService, planningToolViewModel);
        }

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

            _movePreviewPresentation.Ensure(transform);
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
            _movePreviewPresentation.ClearAllMovePathMarkers();
            _movePreviewPresentation.ClearPreview(null);
            _movePreviewPresentation.DisposeGhostMaterial();
            ClearAllPendingDeployGhosts();
            ClearTerritoryHighlights();
            _inputState.ClearToolAndSelection();
            _knownUnitHpByUnitId.Clear();
            _lastDamagePopupTimeByUnitId.Clear();
            _nodeInfoProxyFactory.DestroyProxy();
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

            _inputCoordinator.Tick(this, GetLeftMouseButtonDown(), GetRightMouseButtonDown());
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
            _inputState.SetCombatActionMode(CombatActionMode.Move);
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
            _inputState.SetCombatActionMode(CombatActionMode.Attack);
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
            _inputState.SetCombatActionMode(CombatActionMode.Charge);
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
            _inputState.SetCombatActionMode(CombatActionMode.None);
            _planningIntentService?.HoldUnit(_selectedUnit.UnitId);
            NotifyCombatSelectionChanged();
        }

        public void ClearCombatSelection()
        {
            ClearCombatSelection(preserveToolMode: false);
        }

        private void ClearCombatSelection(bool preserveToolMode)
        {
            ClearMoveSelection();
            ClearMovePreviewState();
            _inputState.SetCombatActionMode(CombatActionMode.None, publishToolState: !preserveToolMode);
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
            _planningIntentService?.ExpandTerritory(_selectedUnit.UnitId, centerNodeId);
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
            _planningIntentService?.ExpandTerritory(unitId, resolvedCenterNodeId);
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

            _buildRule = rule;
            _inputState.SetBuildModeActive(true, _buildType, _activeBuildCityId, _buildRule);
            BlockInputAfterModeSwitch();

            ClearCombatSelection(preserveToolMode: true);
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

            _inputState.SetBuildModeActive(false, _buildType, _activeBuildCityId, _buildRule);
            _buildType = string.Empty;
            _activeBuildCityId = string.Empty;
            _hoverNode = null;
            DestroyHoverGhost();
            ClearBuildPreviewState();
        }

        private void UpdateBuildMode(bool leftMouseDown, bool rightMouseDown)
        {
            _buildPlacementCoordinator.Tick(this, leftMouseDown, rightMouseDown);
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

            _buildPlacementGhostPresenter.Recreate(node, _buildType, GetLocalOwnerId(), buildValidColor);
        }

        private void DestroyHoverGhost()
        {
            _buildPlacementGhostPresenter.Clear();
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

            _planningIntentService?.BuildToken(nodeId, buildingType, _activeBuildCityId);
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
            if (!_inputState.IsBuildModeActive() || _hoverNode == null)
            {
                return;
            }

            var color = ResolveBuildPreviewColor(_hoverNode.NodeId);
            _hoverNode.SetHighlight(true, color);
            _buildPlacementGhostPresenter.Render(color);
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
            _inputState.SetBuildPreviewTarget(_hoverBuildPreviewNodeId);
            _nextBuildPreviewRequestAt = Time.unscaledTime + Mathf.Max(0.02f, buildPreviewRequestThrottleSeconds);
            _buildPreviewRequestSequence++;
            var requestId = $"build-preview-{_buildType}-{_buildPreviewRequestSequence}";
            var draftCache = PlanningDraftCache.EnsureInstance();
            draftCache?.TrackBuildPreviewRequest(requestId, _hoverBuildPreviewNodeId, _buildType, _activeBuildCityId);
            _planningIntentService?.PreviewBuild(requestId, _hoverBuildPreviewNodeId, _buildType, _activeBuildCityId);
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
            _inputState.ClearBuildPreviewTarget();
            (_draftCache ?? PlanningDraftCache.Instance)?.ClearBuildPreview();
        }

        bool IMapBuildPlacementCoordinatorContext.HasMapRenderer => MapRenderer.Instance != null;

        bool IMapBuildPlacementCoordinatorContext.ShouldLogInvalidBuildClick => logInvalidBuildClick;

        bool IMapBuildPlacementCoordinatorContext.IsPointerOverUI()
        {
            return IsPointerOverUI();
        }

        bool IMapBuildPlacementCoordinatorContext.TryRaycastBuildNode(out NodeView node)
        {
            return TryRaycastNode(out node);
        }

        bool IMapBuildPlacementCoordinatorContext.IsCurrentHoverNode(NodeView node)
        {
            return _hoverNode == node;
        }

        bool IMapBuildPlacementCoordinatorContext.ShouldRequestBuildPreview(string nodeId)
        {
            return !TryGetCurrentBuildPreview(nodeId, out _) &&
                   Time.unscaledTime >= _nextBuildPreviewRequestAt;
        }

        Color IMapBuildPlacementCoordinatorContext.ResolveBuildPreviewColor(string nodeId)
        {
            return ResolveBuildPreviewColor(nodeId);
        }

        void IMapBuildPlacementCoordinatorContext.ExitBuildMode()
        {
            ExitBuildMode();
        }

        void IMapBuildPlacementCoordinatorContext.ClearBuildHoverState()
        {
            RestoreNodeHighlightAfterHover(_hoverNode);
            _hoverNode = null;
            DestroyHoverGhost();
            ClearBuildPreviewState();
        }

        void IMapBuildPlacementCoordinatorContext.MoveBuildHoverTo(NodeView node)
        {
            RestoreNodeHighlightAfterHover(_hoverNode);
            _hoverNode = node;
            RecreateHoverGhost(node);
        }

        void IMapBuildPlacementCoordinatorContext.RenderBuildHover(NodeView node, Color highlightColor)
        {
            if (node != null)
            {
                node.SetHighlight(true, highlightColor);
            }
        }

        void IMapBuildPlacementCoordinatorContext.RenderBuildGhost(Color highlightColor)
        {
            _buildPlacementGhostPresenter.Render(highlightColor);
        }

        void IMapBuildPlacementCoordinatorContext.RequestBuildPreview(string nodeId)
        {
            RequestBuildPreview(nodeId);
        }

        void IMapBuildPlacementCoordinatorContext.LogInvalidBuildTarget()
        {
            Debug.LogWarning("[MapPlanningInputController] Invalid build target: cursor is outside map tile.");
        }

        void IMapBuildPlacementCoordinatorContext.TryCommitBuildPlacement(NodeView node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null)
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
        #endregion

        #region Combat

        private void HandleMoveSelectionClick()
        {
            if (_selectedUnit == null)
            {
                _inputState.SetCombatActionMode(CombatActionMode.None);
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
                _inputState.SetCombatActionMode(CombatActionMode.None);
                NotifyCombatSelectionChanged();
            }
        }

        private bool ShouldPrioritizeStructureAttackClick()
        {
            if (_selectedUnit == null ||
                _inputState.CombatActionMode != CombatActionMode.Attack ||
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
            if (_inputState.CombatActionMode == CombatActionMode.Attack &&
                TryGetClickedNodeContext(out var attackNodeView, out _) &&
                attackNodeView != null &&
                !string.IsNullOrWhiteSpace(attackNodeView.NodeId) &&
                IsEnemyStructureNode(attackNodeView.NodeId))
            {
                if (TryIssueStructureTargetOrder(attackNodeView.NodeId))
                {
                    _inputState.SetCombatActionMode(CombatActionMode.None);
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
                    if (_inputState.CombatActionMode == CombatActionMode.Attack)
                    {
                        if (TryIssueStructureTargetOrder(node.NodeId))
                        {
                            _inputState.SetCombatActionMode(CombatActionMode.None);
                            NotifyCombatSelectionChanged();
                        }
                        return;
                    }
                    if (_inputState.CombatActionMode == CombatActionMode.Move)
                    {
                        if (TryIssueAuthoritativeMoveOrder(node.NodeId))
                        {
                            _inputState.SetCombatActionMode(CombatActionMode.None);
                            NotifyCombatSelectionChanged();
                        }
                        return;
                    }
                }
            }

            if (TryRaycastNode(out _))
            {
                if (_inputState.CombatActionMode == CombatActionMode.None)
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

            if (_inputState.CombatActionMode == CombatActionMode.Attack)
            {
                if (_highlightNodeIds.Count == 0)
                {
                    RefreshAttackRangeHighlights();
                }
                return;
            }

            if (_inputState.CombatActionMode != CombatActionMode.Move)
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
            if (_inputState.IsBuildModeActive())
            {
                ExitBuildMode();
                BlockInputAfterModeSwitch();
                return;
            }

            if (_inputState.CombatActionMode != CombatActionMode.None)
            {
                _inputState.SetCombatActionMode(CombatActionMode.None);
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
            _inputState.SetMovePreviewTarget(_hoverPreviewNodeId);
            _nextMovePreviewRequestAt = Time.unscaledTime + Mathf.Max(0.02f, movePreviewRequestThrottleSeconds);
            _movePreviewRequestSequence++;
            var requestId = $"move-preview-{_selectedUnit.UnitId}-{_movePreviewRequestSequence}";
            PlanningDraftCache.EnsureInstance()?.TrackPreviewRequest(requestId, _selectedUnit.UnitId, "move", targetNodeId);
            Debug.Log($"[MapPlanningInputController] 请求路径预览 unit={_selectedUnit.UnitId} hover_node={targetNodeId} request={requestId}");
            _planningIntentService?.PreviewMove(requestId, _selectedUnit.UnitId, targetNodeId);
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

        private void NotifyCombatSelectionChanged()
        {
            CombatSelectionChanged?.Invoke();
        }

        bool IMapPlanningInputCoordinatorContext.IsBuildModeActive => _inputState.IsBuildModeActive();

        CombatActionMode IMapPlanningInputCoordinatorContext.CombatActionMode => _inputState.CombatActionMode;

        void IMapPlanningInputCoordinatorContext.UpdateBuildMode(bool leftMouseDown, bool rightMouseDown)
        {
            UpdateBuildMode(leftMouseDown, rightMouseDown);
        }

        void IMapPlanningInputCoordinatorContext.UpdateCombatMode()
        {
            UpdateCombatMode();
        }

        bool IMapPlanningInputCoordinatorContext.IsPointerOverUI()
        {
            return IsPointerOverUI();
        }

        void IMapPlanningInputCoordinatorContext.PrepareMapCommandClick()
        {
            ClearTerritoryHighlights();
            NonBuildingMapClicked?.Invoke();
        }

        bool IMapPlanningInputCoordinatorContext.TryIssueAttackStructureFromCurrentClick()
        {
            if (TryGetClickedNodeContext(out var attackNode, out _) &&
                attackNode != null &&
                !string.IsNullOrWhiteSpace(attackNode.NodeId) &&
                IsEnemyStructureNode(attackNode.NodeId) &&
                TryIssueStructureTargetOrder(attackNode.NodeId))
            {
                _inputState.SetCombatActionMode(CombatActionMode.None);
                NotifyCombatSelectionChanged();
                return true;
            }

            return false;
        }

        void IMapPlanningInputCoordinatorContext.HandleCombatSelectionClick()
        {
            HandleCombatSelectionClick();
        }

        void IMapPlanningInputCoordinatorContext.HandleMoveSelectionClick()
        {
            HandleMoveSelectionClick();
        }

        bool IMapPlanningInputCoordinatorContext.ShouldPrioritizeStructureAttackClick()
        {
            return ShouldPrioritizeStructureAttackClick();
        }

        bool IMapPlanningInputCoordinatorContext.TrySelectOwnedUnitFromNodeClick()
        {
            return TrySelectOwnedUnitFromNodeClick();
        }

        bool IMapPlanningInputCoordinatorContext.TryOpenBuildingInfoFromClick()
        {
            return TryOpenBuildingInfoFromClick();
        }

        void IMapPlanningInputCoordinatorContext.HandleCombatCancel()
        {
            HandleCombatCancel();
        }
        #endregion

        #region Targeting

        private bool TryIssueUnitTargetOrder(UnitView targetUnit)
        {
            if (_selectedUnit == null || targetUnit == null)
            {
                return false;
            }

            switch (_inputState.CombatActionMode)
            {
                case CombatActionMode.Attack:
                    TryResolvePlannedMoveTargetNodeId(_selectedUnit.UnitId, out var plannedMoveTargetNodeId);
                    ClearPendingMoveStateForUnit(_selectedUnit.UnitId);
                    _planningIntentService?.AttackUnit(_selectedUnit.UnitId, targetUnit.UnitId, plannedMoveTargetNodeId);
                    PlaySelectedAttackFeedback();
                    _inputState.SetCombatActionMode(CombatActionMode.None);
                    NotifyCombatSelectionChanged();
                    return true;
                case CombatActionMode.Charge:
                    var map = MapRenderer.Instance;
                    if (map == null || !map.TryGetNodeIdByGrid(targetUnit.GridPos, out var targetNodeId))
                    {
                        return false;
                    }
                    _planningIntentService?.ChargeUnit(_selectedUnit.UnitId, targetNodeId, targetUnit.UnitId);
                    _inputState.SetCombatActionMode(CombatActionMode.None);
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

            if (_inputState.CombatActionMode != CombatActionMode.Attack && _inputState.CombatActionMode != CombatActionMode.Charge)
            {
                return false;
            }

            return !CanControlUnit(unit);
        }

        private bool CanSelectedUnitAttack()
        {
            return TryGetSelectedUnitCatalog(out var entry) &&
                   MapCombatTargetingResolver.CanAttack(entry);
        }

        private bool CanSelectedUnitAttackStructures()
        {
            return TryGetSelectedUnitCatalog(out var entry) &&
                   MapCombatTargetingResolver.CanAttackStructures(entry);
        }

        private bool CanSelectedUnitCharge()
        {
            return TryGetSelectedUnitCatalog(out var entry) &&
                   MapCombatTargetingResolver.CanCharge(entry);
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
                _inputState.CombatActionMode != CombatActionMode.Attack ||
                string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            TryResolvePlannedMoveTargetNodeId(_selectedUnit.UnitId, out var plannedMoveTargetNodeId);
            ClearPendingMoveStateForUnit(_selectedUnit.UnitId);
            _planningIntentService?.AttackNode(_selectedUnit.UnitId, nodeId, plannedMoveTargetNodeId);
            PlaySelectedAttackFeedback();
            _inputState.SetCombatActionMode(CombatActionMode.None);
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

            return MapCombatTargetingResolver.IsEnemyStructure(nodeState, GetLocalOwnerId());
        }

        private void RefreshAttackRangeHighlights()
        {
            ClearNodeHighlights();

            if (_selectedUnit == null || _inputState.CombatActionMode != CombatActionMode.Attack)
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

            _attackRangePresenter.Refresh(
                map.TileViews,
                _highlightNodeIds,
                originGrid,
                attackRange,
                attackRangeHighlightColor);
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

            return _attackRangePresenter.IsGridInRange(
                originGrid,
                targetGrid,
                ResolveSelectedUnitAttackRange());
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
                _movePreviewPresentation.ClearMovePathMarkersForUnit(unitId);
            }
            Debug.Log($"[MapPlanningInputController] 发送移动消息 unit={unitId} target={targetNodeId}");
            _planningIntentService?.MoveUnit(unitId, targetNodeId);
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
            _movePreviewPresentation.ClearMovePathMarkersForUnit(normalizedUnitId);
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
            _movePreviewPresentation.RefreshPreview(
                preview,
                _hoverPreviewNodeId,
                _selectedUnit?.UnitId,
                MapRenderer.Instance != null,
                moveInvalidColor,
                moveHighlightColor,
                moveFirstTurnColor,
                moveFutureTurnColor,
                moveTurnMarkerHeight);
        }

        private void ClearMovePreviewState()
        {
            var hadHover = !string.IsNullOrEmpty(_hoverPreviewNodeId);
            _hoverPreviewNodeId = string.Empty;
            _inputState.ClearMovePreviewTarget();
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

            return _movePreviewPresentation.TryApplyAuthoritativeMovePathMarkers(
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
            _movePreviewPresentation.RefreshQueuedMovePathMarkers(
                draftCache?.OrdersByUnitId,
                movePathArrowColor,
                movePathDestinationColor);
        }
        #endregion

        #region Deploy

        private void ShowPendingDeployCityCoreGhost(string unitId, string centerNodeId)
        {
            _pendingDeployGhosts.ShowCityCoreGhost(
                unitId,
                centerNodeId,
                GetLocalOwnerId(),
                buildPlacedGhostColor);
        }

        private void ClearPendingDeployCityCoreGhostForUnit(string unitId)
        {
            _pendingDeployGhosts.ClearForUnit(unitId);
        }

        private void ClearAllPendingDeployGhosts()
        {
            _pendingDeployGhosts.ClearAll();
        }

        private void TryResolvePendingDeployGhostByNode(string nodeId, NodeDto node)
        {
            _pendingDeployGhosts.ResolveCommittedNode(nodeId, node);
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

            if (!MapNodeInfoProxyFactory.TryGetInspectableNodeInfo(nodeState, out var buildingType, out var isResourcePoint))
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

            var proxy = _nodeInfoProxyFactory.GetOrCreate(node, nodeState, buildingType, isResourcePoint);
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
                && MapNodeInfoProxyFactory.TryGetInspectableNodeInfo(nodeState, out _, out _))
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
            _inputState.PublishSelectedUnit(unit.UnitId);
            _inputState.SetCombatActionMode(CombatActionMode.None);
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
            _inputState.PublishSelectedUnit(string.Empty);
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
                _movePreviewPresentation.ClearPreview(RestoreTerritoryHighlightAfterPreviewOverlayClear);
                return;
            }

            foreach (var nodeId in _highlightNodeIds)
            {
                if (map.TryGetNodeView(nodeId, out var node))
                {
                    if (_territoryHighlights.Contains(nodeId))
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
            _movePreviewPresentation.ClearPreview(RestoreTerritoryHighlightAfterPreviewOverlayClear);
        }

        private bool RestoreTerritoryHighlightAfterPreviewOverlayClear(string nodeId, NodeView node)
        {
            return _territoryHighlights.TryRestore(nodeId, node, territoryHighlightColor);
        }

        private void RestoreNodeHighlightAfterHover(NodeView node)
        {
            if (node == null)
            {
                return;
            }

            var nodeId = node.NodeId;
            if (_territoryHighlights.TryRestore(nodeId, node, territoryHighlightColor))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(nodeId) && _highlightNodeIds.Contains(nodeId))
            {
                node.SetHighlight(true, attackRangeHighlightColor);
                return;
            }

            if (_movePreviewPresentation.TryRestorePreviewHighlight(nodeId, node))
            {
                return;
            }

            node.SetHighlightVisible(false);
        }

        private void HighlightTerritoryForNode(NodeDto centerNode)
        {
            _territoryHighlights.HighlightForNode(centerNode, territoryHighlightColor);
        }

        private void ClearTerritoryHighlights()
        {
            _territoryHighlights.Clear(
                _highlightNodeIds,
                attackRangeHighlightColor,
                _movePreviewPresentation.TryRestorePreviewHighlight);
        }
        #endregion

        #region MovePreviewGhost

        private void CreateOrUpdateMovePreview(string unitId, string targetNodeId)
        {
            _movePreviewPresentation.CreateOrUpdateGhost(unitId, targetNodeId, CreateMovePreviewGhostSettings(), this);
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
            _movePreviewPresentation.RemoveGhost(unitId);
        }

        private void ClearAllMovePreviews()
        {
            _movePreviewPresentation.ClearAllGhosts();
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
            _movePreviewPresentation.Ensure(transform);
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
            _movePreviewPresentation.ClearAllMovePathMarkers();

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
                            _movePreviewPresentation.ClearMovePathMarkersForUnit(normalizedUnitId);
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
                    _movePreviewPresentation.ClearMovePathMarkersForUnit(unitId);
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
                if (_territoryHighlights.TryRestore(evt.NodeID, nodeView, territoryHighlightColor))
                {
                }
                else if (_highlightNodeIds.Contains(evt.NodeID))
                {
                    nodeView.SetHighlight(true, attackRangeHighlightColor);
                }
                else if (_movePreviewPresentation.TryRestorePreviewHighlight(evt.NodeID, nodeView))
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
                    _movePreviewPresentation.ClearMovePathMarkersForUnit(normalizedRemovedId);
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

            if (_selectedUnit != null && _inputState.CombatActionMode == CombatActionMode.Attack)
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
            if (_selectedUnit != null && _inputState.CombatActionMode == CombatActionMode.Attack)
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
            return _buildingCatalogResolver.ResolveBackendBuildingType(buildingType);
        }

        private bool TryGetServerPlacementRule(string buildingType, out string placementRule, out string requiredResourceType)
        {
            return _buildingCatalogResolver.TryGetServerPlacementRule(
                buildingType,
                out placementRule,
                out requiredResourceType);
        }

        private bool TryGetBuildingConfig(string buildingType, out StaticCatalogCache.BuildingEntryJson entry, out string resolvedId)
        {
            return _buildingCatalogResolver.TryGetBuildingConfig(buildingType, out entry, out resolvedId);
        }

        private static string NormalizeToken(string value)
        {
            return MapInputTokens.Normalize(value);
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
