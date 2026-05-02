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
        private readonly MapBuildPlacementSession _buildPlacement = new();
        private readonly MapMovePreviewPresentationController _movePreviewPresentation = new();
        private readonly MapPendingDeployGhostController _pendingDeployGhosts = new();
        private readonly MapTerritoryHighlightPresenter _territoryHighlights = new();
        private readonly MapMoveCommandSession _moveCommands = new();
        private readonly List<UnitView> _nodeClickUnits = new();
        private readonly MapUnitDamagePopupPresenter _unitDamagePopups = new();
        private readonly MapPointerInput _pointerInput = new();
        private readonly MapPlanningInputCoordinator _inputCoordinator = new();
        private readonly MapBuildPlacementCoordinator _buildPlacementCoordinator = new();
        private readonly MapPlanningInputStateAdapter _inputState = new();
        private readonly MapPlanningCacheEventBridge _cacheEvents = new();
        private readonly MapNodeInfoProxyFactory _nodeInfoProxyFactory = new();
        private readonly MapBuildingCatalogResolver _buildingCatalogResolver = new();
        private readonly MapAttackRangePresenter _attackRangePresenter = new();
        private PlanningIntentService _planningIntentService;
        private IMapSelectionSurface _selectionSurface;

        private UnitView _selectedUnit;
        private float _ignoreInputUntilTime;
        private UnitInfoPanelController _unitInfoPanelController;

        public IReadOnlyList<PendingBuildRecord> PendingBuilds => _buildPlacement.PendingBuilds;
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
            ConfigureBuildPlacementSession();
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
            _buildPlacement.ClearHoverState();
            _unitDamagePopups.Clear();
            _nodeInfoProxyFactory.DestroyProxy();
        }

        private void Update()
        {
            if (!_cacheEvents.IsGameStateSubscribed)
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
            var phase = _cacheEvents.Cache != null ? _cacheEvents.Cache.Phase : string.Empty;
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
            var phase = _cacheEvents.Cache != null ? _cacheEvents.Cache.Phase : string.Empty;
            Debug.Log($"[MapPlanningInputController] territory action sent. unit={unitId} center={resolvedCenterNodeId} phase={phase}");
            return true;
        }

        public bool IsUnitMovePending(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            return _moveCommands.IsUnitMovePending(unitId);
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
            ConfigureBuildPlacementSession();
            _buildPlacement.ApplyBackendBuildCommand(buildingType, nodeId, isGhost, ownerId, hp, buildPlacedGhostColor);
        }
        #endregion

        #region Build

        private void EnterBuildPlacement(string buildingType, string cityId, BuildPlacementRule rule)
        {
            ConfigureBuildPlacementSession();
            if (!_buildPlacement.EnterBuildPlacement(
                    buildingType,
                    cityId,
                    rule,
                    CreateBuildPlacementVisualSettings(),
                    ShowUserError))
            {
                return;
            }

            BlockInputAfterModeSwitch();
            ClearCombatSelection(preserveToolMode: true);
        }

        private void ExitBuildMode()
        {
            ConfigureBuildPlacementSession();
            _buildPlacement.ExitBuildMode();
        }

        private void UpdateBuildMode(bool leftMouseDown, bool rightMouseDown)
        {
            _buildPlacementCoordinator.Tick(this, leftMouseDown, rightMouseDown);
        }

        private Color ResolveBuildPreviewColor(string nodeId)
        {
            ConfigureBuildPlacementSession();
            return _buildPlacement.ResolveBuildPreviewColor(nodeId, CreateBuildPlacementVisualSettings());
        }

        private void RemovePendingBuild(string nodeId)
        {
            _buildPlacement.RemovePendingBuild(nodeId);
        }

        private string GetLastPendingBuildNodeId()
        {
            return _buildPlacement.GetLastPendingBuildNodeId();
        }

        private void RollbackPendingBuild(string nodeId)
        {
            _buildPlacement.RollbackPendingBuild(nodeId);
        }

        private bool HasPendingBuild(string nodeId)
        {
            return _buildPlacement.HasPendingBuild(nodeId);
        }

        private void RefreshBuildPreviewVisuals()
        {
            ConfigureBuildPlacementSession();
            _buildPlacement.RefreshPreviewVisuals(CreateBuildPlacementVisualSettings());
        }

        private void RequestBuildPreview(string nodeId)
        {
            ConfigureBuildPlacementSession();
            _buildPlacement.RequestBuildPreview(nodeId, CreateBuildPlacementVisualSettings());
        }

        private void ClearBuildPreviewState()
        {
            _buildPlacement.ClearBuildPreviewState();
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
            return _buildPlacement.IsCurrentHoverNode(node);
        }

        bool IMapBuildPlacementCoordinatorContext.ShouldRequestBuildPreview(string nodeId)
        {
            return _buildPlacement.ShouldRequestBuildPreview(nodeId);
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
            _buildPlacement.ClearHoverState();
        }

        void IMapBuildPlacementCoordinatorContext.MoveBuildHoverTo(NodeView node)
        {
            _buildPlacement.MoveHoverTo(node, CreateBuildPlacementVisualSettings());
        }

        void IMapBuildPlacementCoordinatorContext.RenderBuildHover(NodeView node, Color highlightColor)
        {
            _buildPlacement.RenderHover(node, highlightColor);
        }

        void IMapBuildPlacementCoordinatorContext.RenderBuildGhost(Color highlightColor)
        {
            _buildPlacement.RenderGhost(highlightColor);
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
            _buildPlacement.TryCommitBuildPlacement(
                node,
                CreateBuildPlacementVisualSettings(),
                ShowUserError,
                (buildingType, nodeId) => BuildCommandSent?.Invoke(buildingType, nodeId));
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
                if (_moveCommands.HasHoverPreview)
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
                if (_moveCommands.HasHoverPreview)
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

            if (!_moveCommands.ShouldRequestPreview(node.NodeId))
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
            _moveCommands.RequestPreview(
                _selectedUnit.UnitId,
                targetNodeId,
                movePreviewRequestThrottleSeconds,
                _inputState,
                _planningIntentService);
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
                TryGetCurrentMovePreview(normalizedUnitId, targetNodeId, out var preview);
                _moveCommands.MarkPendingMove(normalizedUnitId, targetNodeId, preview);
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
            var draftCache = _cacheEvents.DraftCache ?? PlanningDraftCache.Instance;
            return _moveCommands.TryResolvePlannedTargetNodeId(unitId, draftCache, out targetNodeId);
        }

        private void ClearPendingMoveStateForUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            var normalizedUnitId = unitId.Trim();
            _moveCommands.ClearUnit(normalizedUnitId);
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

            var draftCache = _cacheEvents.DraftCache ?? PlanningDraftCache.Instance;
            return _moveCommands.TryGetCurrentPreview(unitId, targetNodeId, draftCache, out preview);
        }

        private IReadOnlyList<string> GetQueuedMovePathNodeIds(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            var draftCache = _cacheEvents.DraftCache ?? PlanningDraftCache.Instance;
            return _moveCommands.GetQueuedMovePathNodeIds(unitId, draftCache);
        }

        private void RememberQueuedMovePaths()
        {
            var draftCache = _cacheEvents.DraftCache ?? PlanningDraftCache.Instance;
            _moveCommands.RememberQueuedMovePaths(draftCache);
        }

        private void RememberPendingMovePath(string unitId, IReadOnlyList<string> pathNodeIds)
        {
            _moveCommands.RememberPath(unitId, pathNodeIds);
        }

        private IReadOnlyList<string> GetRememberedMovePathNodeIds(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            return _moveCommands.GetRememberedPath(unitId);
        }

        private void RefreshPreviewVisuals()
        {
            ClearNodeHighlights();

            var preview = PlanningDraftCache.Instance != null ? PlanningDraftCache.Instance.CurrentPreview : null;
            _movePreviewPresentation.RefreshPreview(
                preview,
                _moveCommands.HoverPreviewNodeId,
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
            _moveCommands.ClearPreviewState(_inputState);
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
            var draftCache = _cacheEvents.DraftCache ?? PlanningDraftCache.Instance;
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
                && _moveCommands.TryGetPendingTargetNodeId(normalizedUnitId, out var pendingNodeId))
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
            _cacheEvents.SubscribeGameState(
                OnTurnSettled,
                OnNodeChanged,
                OnUnitsChanged,
                OnTokenResult,
                OnPlanningCommandResult);
        }

        private void SubscribeDraftCacheEvents()
        {
            _movePreviewPresentation.Ensure(transform);
            _cacheEvents.SubscribeDraft(
                OnPreviewChanged,
                OnBuildPreviewChanged,
                OnOrdersChanged);
            RefreshQueuedMovePathMarkers();
        }

        private void UnsubscribeCacheEvents()
        {
            _cacheEvents.UnsubscribeGameState(
                OnTurnSettled,
                OnNodeChanged,
                OnUnitsChanged,
                OnTokenResult,
                OnPlanningCommandResult);
        }

        private void UnsubscribeDraftCacheEvents()
        {
            _cacheEvents.UnsubscribeDraft(
                OnPreviewChanged,
                OnBuildPreviewChanged,
                OnOrdersChanged);
        }

        private void OnTurnSettled(TurnSettledEvent settledEvent)
        {
            ClearCombatSelection();
            _moveCommands.ClearAll();
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
                            _moveCommands.ClearUnit(normalizedUnitId);
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
            _moveCommands.ClearRememberedPaths();

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
                    _moveCommands.ClearUnit(unitId);
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
                    _moveCommands.ClearUnit(normalizedRemovedId);
                    _movePreviewPresentation.ClearMovePathMarkersForUnit(normalizedRemovedId);
                    ClearPendingDeployCityCoreGhostForUnit(normalizedRemovedId);
                    _unitDamagePopups.ForgetUnit(normalizedRemovedId);
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
                    _unitDamagePopups.RememberUnit(added);
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

                    _unitDamagePopups.TrackMovedUnit(
                        moved,
                        allowFallbackPopup,
                        damagePopupRepeatCooldownSeconds,
                        ref damagePopupController);
                }
            }

            if (_selectedUnit != null && _inputState.CombatActionMode == CombatActionMode.Attack)
            {
                RefreshAttackRangeHighlights();
            }
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

        private void ConfigureBuildPlacementSession()
        {
            _buildPlacement.Configure(
                _inputState,
                () => _cacheEvents.DraftCache ?? PlanningDraftCache.Instance,
                () => _planningIntentService,
                GetLocalOwnerId,
                ResolveBackendBuildingType,
                RestoreNodeHighlightAfterHover);
        }

        private MapBuildPlacementVisualSettings CreateBuildPlacementVisualSettings()
        {
            return new MapBuildPlacementVisualSettings(
                buildValidColor,
                buildInvalidColor,
                buildPendingColor,
                buildPlacedGhostColor,
                buildPreviewRequestThrottleSeconds,
                disallowManualCityCorePlacement,
                manualPlacementBlockedBuildingTypes);
        }

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
            var phase = _cacheEvents.Cache != null ? _cacheEvents.Cache.Phase : string.Empty;
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
