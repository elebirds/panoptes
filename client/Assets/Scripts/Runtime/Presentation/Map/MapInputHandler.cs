/*************************************************
 * Project: Panoptes
 * File: MapInputHandler.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Map command input (move/build) + backend playback bridge.
 *************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using Panoptes.Core.Application.Intents;
using Panoptes.Presentation.Animation;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.UI.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace Panoptes.Presentation.Map
{
    public sealed partial class MapInputHandler : MonoBehaviour
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

        // Legacy public name kept for existing prefab/test bindings.
        // In Turn V2 this represents planning-phase unit orders.
        public enum CombatActionMode
        {
            None = 0,
            Move = 1,
            Attack = 2,
            Charge = 3
        }

        public static MapInputHandler Instance { get; private set; }

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
        private readonly List<PendingBuildRecord> _pendingBuilds = new();
        private readonly Dictionary<string, string> _pendingDeployGhostNodeByUnitId = new();
        private readonly Dictionary<string, GameObject> _movePreviewByUnitId = new();
        private readonly HashSet<string> _pendingMoveUnitIds = new();
        private readonly Dictionary<string, string> _pendingMoveTargetNodeByUnitId = new();
        private readonly Dictionary<string, List<string>> _pendingMovePathNodeIdsByUnitId = new(StringComparer.Ordinal);
        private readonly List<UnitView> _nodeClickUnits = new();
        private readonly Dictionary<string, int> _knownUnitHpByUnitId = new();
        private readonly Dictionary<string, float> _lastDamagePopupTimeByUnitId = new();
        private Material _movePreviewProxyMaterial;

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

        public IReadOnlyList<PendingBuildRecord> PendingBuilds => _pendingBuilds;
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
            DisposeMovePreviewProxyMaterial();
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

    }
}
