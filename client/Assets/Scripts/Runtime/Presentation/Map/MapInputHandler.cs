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
using Panoptes.Core.Events;
using Panoptes.Presentation.UI.Domestic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Panoptes.Presentation.Map
{
    public sealed class MapInputHandler : MonoBehaviour
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
        [SerializeField] private Color buildPlacedGhostColor = new Color(0.6f, 1f, 0.6f, 0.92f);
        [SerializeField] private bool logInvalidBuildClick = true;
        [SerializeField] private string localOwnerIdOverride = string.Empty;
        [SerializeField] private bool useSafeZoneFallbackForCityPlacement = true;
        [SerializeField] private string[] territoryOnlyBuildingTypes =
        {
            "castle",
            "engineer",
            "engineer_camp",
            "workshop",
            "archery",
            "barracks",
            "blacksmith"
        };
        [SerializeField] private string[] globalPlacementBuildingTypes =
        {
            "tower",
            "atktower",
            "watchtower",
            "viewtower"
        };
        [SerializeField] private bool disallowManualCastlePlacement = true;
        [SerializeField] private bool autoCreateCornerCityZones = true;
        [SerializeField] private int cornerInset = 2;
        [SerializeField] private CityZone[] cityZones;

        [Header("Castle Panel")]
        [SerializeField] private CastleProductionPanel castleProductionPanel;
        [SerializeField] private bool autoFindCastleProductionPanel = true;
        [SerializeField] private bool autoSpawnCastleProductionPanelIfMissing = true;
        [SerializeField] private string castleProductionPanelResourcesPath = "Prefabs/UI/CastleProductionPanel";

        private readonly HashSet<string> _highlightNodeIds = new();
        private readonly Dictionary<string, GameObject> _moveTurnMarkers = new();
        private readonly List<PendingBuildRecord> _pendingBuilds = new();
        private readonly Dictionary<string, GameObject> _movePreviewByUnitId = new();
        private Material _movePreviewProxyMaterial;

        private Mode _mode = Mode.None;
        private CombatActionMode _combatActionMode = CombatActionMode.None;
        private UnitView _selectedUnit;
        private BuildPlacementRule _buildRule;
        private string _buildType = string.Empty;
        private NodeView _hoverNode;
        private BuildingView _hoverGhost;
        private GameStateCache _cache;
        private CombatDraftCache _draftCache;
        private bool _cacheEventsSubscribed;
        private float _ignoreInputUntilTime;
        private float _nextMovePreviewRequestAt;
        private int _movePreviewRequestSequence;
        private string _hoverPreviewNodeId = string.Empty;
        private readonly List<RaycastResult> _uiRaycastResults = new();

        private sealed class MoveGhostTag : MonoBehaviour
        {
        }

        public IReadOnlyList<PendingBuildRecord> PendingBuilds => _pendingBuilds;
        public UnitView SelectedUnit => _selectedUnit;
        public CombatActionMode CurrentCombatActionMode => _combatActionMode;
        public string CurrentCombatPrompt => GetCombatPrompt();

        public event Action<string, string> MoveCommandSent;
        public event Action<string, string> BuildCommandSent;
        public event Action CombatSelectionChanged;

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

            ResolveCastleProductionPanel();
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
            DisposeMovePreviewProxyMaterial();
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

                if (TryOpenCastlePanelFromClick())
                {
                    return;
                }

                HandleCombatSelectionClick();
            }

            if (GetRightMouseButtonDown())
            {
                HandleCombatCancel();
            }
        }

        public void EnterBuildPlacementAny(string buildingType)
        {
            EnterBuildPlacement(buildingType, BuildPlacementRule.AnyTerrain);
        }

        public void EnterBuildPlacementResource(string buildingType)
        {
            EnterBuildPlacement(buildingType, BuildPlacementRule.ResourceOnly);
        }

        public void EnterBuildPlacementCity(string buildingType)
        {
            EnterBuildPlacement(buildingType, BuildPlacementRule.CityOnly);
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
            GameIntents.HoldUnit(_selectedUnit.UnitId);
            NotifyCombatSelectionChanged();
        }

        public void ClearCombatSelection()
        {
            ClearMoveSelection();
            ClearMovePreviewState();
            _combatActionMode = CombatActionMode.None;
            NotifyCombatSelectionChanged();
        }

        public void ApplyBackendMoveCommand(string unitId, string targetNodeId, bool enqueue = true, bool followCamera = true)
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
                    queue.EnqueueUnitMove(unitId, targetNodeId, followCamera);
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

        private void EnterBuildPlacement(string buildingType, BuildPlacementRule rule)
        {
            _buildType = NormalizeToken(buildingType);
            if (disallowManualCastlePlacement && string.Equals(_buildType, "castle", StringComparison.Ordinal))
            {
                Debug.Log("[MapInputHandler] Castle is pre-placed by map config and cannot be manually built.");
                ExitBuildMode();
                return;
            }

            _mode = Mode.Build;
            _buildRule = rule;
            BlockInputAfterModeSwitch();

            ClearCombatSelection();
            DestroyHoverGhost();
            EnsureCityZones();
        }

        private void ExitBuildMode()
        {
            if (_hoverNode != null)
            {
                _hoverNode.SetHighlightVisible(false);
            }

            _mode = Mode.None;
            _buildType = string.Empty;
            _hoverNode = null;
            DestroyHoverGhost();
        }

        private void HandleCombatSelectionClick()
        {
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

            if (_selectedUnit != null &&
                _combatActionMode == CombatActionMode.Move &&
                TryRaycastNode(out var node) &&
                node != null)
            {
                if (!string.IsNullOrEmpty(node.NodeId))
                {
                    SendMoveCommand(_selectedUnit.UnitId, node.NodeId);
                    _combatActionMode = CombatActionMode.None;
                    NotifyCombatSelectionChanged();
                    return;
                }
            }

            if (TryRaycastNode(out _))
            {
                return;
            }

            ClearCombatSelection();
        }

        private void UpdateCombatMode()
        {
            if (_selectedUnit == null || _combatActionMode != CombatActionMode.Move)
            {
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
            CombatDraftCache.EnsureInstance()?.TrackPreviewRequest(requestId, _selectedUnit.UnitId, "move", targetNodeId);
            GameIntents.PreviewCombatMove(requestId, _selectedUnit.UnitId, targetNodeId);
        }

        private bool TryIssueUnitTargetOrder(UnitView targetUnit)
        {
            if (_selectedUnit == null || targetUnit == null)
            {
                return false;
            }

            switch (_combatActionMode)
            {
                case CombatActionMode.Attack:
                    GameIntents.AttackUnit(_selectedUnit.UnitId, targetUnit.UnitId);
                    _combatActionMode = CombatActionMode.None;
                    NotifyCombatSelectionChanged();
                    return true;
                case CombatActionMode.Charge:
                    var map = MapRenderer.Instance;
                    if (map == null || !map.TryGetNodeIdByGrid(targetUnit.GridPos, out var targetNodeId))
                    {
                        return false;
                    }
                    GameIntents.ChargeUnit(_selectedUnit.UnitId, targetNodeId, targetUnit.UnitId);
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

        private bool TryOpenCastlePanelFromClick()
        {
            if (!TryRaycastNode(out var node))
            {
                return false;
            }

            if (!IsCastleNode(node.NodeId))
            {
                return false;
            }

            if (!IsCastleOwnedByLocalPlayer(node.NodeId))
            {
                return false;
            }

            ResolveCastleProductionPanel();
            if (castleProductionPanel == null)
            {
                Debug.LogWarning($"[MapInputHandler] Castle clicked but CastleProductionPanel is missing. node={node.NodeId}");
                return false;
            }

            var opened = castleProductionPanel.OpenForCastle(node.NodeId);
            if (!opened)
            {
                Debug.LogWarning($"[MapInputHandler] Castle panel rejected open request. node={node.NodeId}");
            }

            return opened;
        }

        private void SelectUnit(UnitView unit)
        {
            if (unit == null)
            {
                return;
            }

            if (onlyControlOwnUnits && !CanControlUnit(unit))
            {
                return;
            }

            ClearMoveSelection();
            _selectedUnit = unit;
            _selectedUnit.SetSelected(true);
            _combatActionMode = CombatActionMode.None;
            ClearMovePreviewState();
            NotifyCombatSelectionChanged();
        }

        private void ClearMoveSelection()
        {
            if (_selectedUnit != null)
            {
                _selectedUnit.SetSelected(false);
            }

            _selectedUnit = null;
        }

        private void ResolveCastleProductionPanel()
        {
            if (castleProductionPanel != null || !autoFindCastleProductionPanel)
            {
                return;
            }

            castleProductionPanel = UnityEngine.Object.FindAnyObjectByType<CastleProductionPanel>();
            if (castleProductionPanel != null || !autoSpawnCastleProductionPanelIfMissing)
            {
                return;
            }

            var prefab = Resources.Load<CastleProductionPanel>(castleProductionPanelResourcesPath);
            Transform parent = null;
            var anyCanvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (anyCanvas != null)
            {
                parent = anyCanvas.transform;
            }

            if (prefab != null)
            {
                castleProductionPanel = Instantiate(prefab, parent, false);
                return;
            }

            // Last fallback: create an empty controller object.
            var go = new GameObject("CastleProductionPanel", typeof(RectTransform));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            castleProductionPanel = go.AddComponent<CastleProductionPanel>();
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
                if (_hoverNode != null)
                {
                    _hoverNode.SetHighlightVisible(false);
                }
                _hoverNode = null;
                DestroyHoverGhost();
                return;
            }

            var hasNode = TryRaycastNode(out var node);
            if (!hasNode)
            {
                if (_hoverNode != null)
                {
                    _hoverNode.SetHighlightVisible(false);
                }
                _hoverNode = null;
                DestroyHoverGhost();

                if (GetLeftMouseButtonDown() && !IsPointerOverUI() && logInvalidBuildClick)
                {
                    Debug.LogWarning("[MapInputHandler] Invalid build target: cursor is outside map tile.");
                }
                return;
            }

            var canPlace = CanPlaceBuildingAt(node.NodeId);
            var highlightColor = canPlace ? buildValidColor : buildInvalidColor;

            if (_hoverNode != node)
            {
                if (_hoverNode != null)
                {
                    _hoverNode.SetHighlightVisible(false);
                }

                _hoverNode = node;
                RecreateHoverGhost(node);
            }

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

                if (!canPlace)
                {
                    if (logInvalidBuildClick)
                    {
                        Debug.LogWarning($"[MapInputHandler] Invalid build target at node '{node.NodeId}' for type '{_buildType}'.");
                    }
                    return;
                }

                var ownerId = GetLocalOwnerId();
                map.ApplyBuildingPlacement(node.NodeId, _buildType, ownerId, true, 100, buildPlacedGhostColor);
                _pendingBuilds.Add(new PendingBuildRecord
                {
                    buildingType = _buildType,
                    nodeId = node.NodeId,
                    ownerId = ownerId,
                    isGhost = true
                });

                SendBuildCommand(_buildType, node.NodeId);
                ExitBuildMode();
            }
        }

        private bool CanPlaceBuildingAt(string nodeId)
        {
            var map = MapRenderer.Instance;
            if (map == null || string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            if (!map.IsNodeBuildBaseAvailable(nodeId))
            {
                return false;
            }

            if (IsTerritoryOnlyBuildingType(_buildType) && !CanPlaceCityBuilding(nodeId))
            {
                return false;
            }

            if (IsGlobalPlacementBuildingType(_buildType))
            {
                return true;
            }

            switch (_buildRule)
            {
                case BuildPlacementRule.ResourceOnly:
                    return map.IsNodeResourcePoint(nodeId);
                case BuildPlacementRule.CityOnly:
                    return CanPlaceCityBuilding(nodeId);
                default:
                    return true;
            }
        }

        private bool CanPlaceCityBuilding(string nodeId)
        {
            if (IsInsideLocalTerritory(nodeId))
            {
                return true;
            }

            if (!useSafeZoneFallbackForCityPlacement)
            {
                return false;
            }

            var map = MapRenderer.Instance;
            return map != null && map.IsNodeInSafeZone(nodeId);
        }

        private bool IsInsideLocalTerritory(string nodeId)
        {
            var map = MapRenderer.Instance;
            if (map == null || string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            return map.IsNodeInTerritory(nodeId, GetLocalOwnerId());
        }

        private bool IsTerritoryOnlyBuildingType(string buildingType)
        {
            if (string.IsNullOrWhiteSpace(buildingType) ||
                territoryOnlyBuildingTypes == null ||
                territoryOnlyBuildingTypes.Length == 0)
            {
                return false;
            }

            var normalized = NormalizeToken(buildingType);
            for (int i = 0; i < territoryOnlyBuildingTypes.Length; i++)
            {
                if (string.Equals(normalized, NormalizeToken(territoryOnlyBuildingTypes[i]), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsGlobalPlacementBuildingType(string buildingType)
        {
            if (string.IsNullOrWhiteSpace(buildingType) ||
                globalPlacementBuildingTypes == null ||
                globalPlacementBuildingTypes.Length == 0)
            {
                return false;
            }

            var normalized = NormalizeToken(buildingType);
            for (int i = 0; i < globalPlacementBuildingTypes.Length; i++)
            {
                if (string.Equals(normalized, NormalizeToken(globalPlacementBuildingTypes[i]), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsInsideLocalCityZone(string nodeId)
        {
            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetNodeView(nodeId, out var nodeView) || nodeView == null)
            {
                return false;
            }

            EnsureCityZones();
            var ownerId = GetLocalOwnerId();
            if (cityZones == null || cityZones.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < cityZones.Length; i++)
            {
                var zone = cityZones[i];
                if (!string.IsNullOrEmpty(zone.ownerId) && !string.Equals(zone.ownerId, ownerId, StringComparison.Ordinal))
                {
                    continue;
                }

                var halfX = Mathf.Max(0, zone.size.x / 2);
                var halfY = Mathf.Max(0, zone.size.y / 2);
                if (Mathf.Abs(nodeView.GridPos.x - zone.center.x) <= halfX &&
                    Mathf.Abs(nodeView.GridPos.y - zone.center.y) <= halfY)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureCityZones()
        {
            if (!autoCreateCornerCityZones)
            {
                return;
            }

            if (cityZones != null && cityZones.Length > 0)
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetGridBounds(out var minX, out var maxX, out var minY, out var maxY))
            {
                return;
            }

            var localOwner = GetLocalOwnerId();
            cityZones = new[]
            {
                new CityZone { ownerId = localOwner, center = new Vector2Int(minX + cornerInset, minY + cornerInset), size = new Vector2Int(3, 3) },
                new CityZone { ownerId = "red", center = new Vector2Int(maxX - cornerInset, minY + cornerInset), size = new Vector2Int(3, 3) },
                new CityZone { ownerId = "green", center = new Vector2Int(minX + cornerInset, maxY - cornerInset), size = new Vector2Int(3, 3) },
                new CityZone { ownerId = "yellow", center = new Vector2Int(maxX - cornerInset, maxY - cornerInset), size = new Vector2Int(3, 3) }
            };
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

        private void ClearNodeHighlights()
        {
            var map = MapRenderer.Instance;
            if (map == null)
            {
                _highlightNodeIds.Clear();
                ClearMoveTurnMarkers();
                return;
            }

            foreach (var nodeId in _highlightNodeIds)
            {
                if (map.TryGetNodeView(nodeId, out var node))
                {
                    node.SetHighlightVisible(false);
                }
            }

            _highlightNodeIds.Clear();
            ClearMoveTurnMarkers();
        }

        private void SendMoveCommand(string unitId, string targetNodeId)
        {
            GameIntents.MoveUnit(unitId, targetNodeId);
            MoveCommandSent?.Invoke(unitId, targetNodeId);
        }

        private void SendBuildCommand(string buildingType, string nodeId)
        {
            GameIntents.BuildToken(nodeId, buildingType);
            BuildCommandSent?.Invoke(buildingType, nodeId);
        }

        private void SubscribeCacheEvents()
        {
            if (_cacheEventsSubscribed)
            {
                return;
            }

            _cache = GameStateCache.Instance;
            _draftCache = CombatDraftCache.EnsureInstance();
            if (_cache == null)
            {
                return;
            }

            _cache.OnCombatSettled += OnCombatSettled;
            _cache.OnDomesticSettled += OnDomesticSettled;
            _cacheEventsSubscribed = true;
        }

        private void SubscribeDraftCacheEvents()
        {
            _draftCache = CombatDraftCache.EnsureInstance();
            if (_draftCache == null)
            {
                return;
            }

            _draftCache.PreviewChanged -= OnPreviewChanged;
            _draftCache.PreviewChanged += OnPreviewChanged;
        }

        private void UnsubscribeCacheEvents()
        {
            if (!_cacheEventsSubscribed)
            {
                return;
            }

            if (_cache != null)
            {
                _cache.OnCombatSettled -= OnCombatSettled;
                _cache.OnDomesticSettled -= OnDomesticSettled;
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
            _draftCache = null;
        }

        private void OnCombatSettled(CombatSettledEvent settledEvent)
        {
            ClearCombatSelection();
        }

        private void OnDomesticSettled(DomesticSettledEvent e)
        {
            var builtNodeIds = e?.Settlement?.BuiltNodeIDs;
            if (builtNodeIds == null || builtNodeIds.Count == 0)
            {
                return;
            }

            for (int i = 0; i < builtNodeIds.Count; i++)
            {
                var nodeId = builtNodeIds[i];
                if (string.IsNullOrEmpty(nodeId) || GameStateCache.Instance == null)
                {
                    continue;
                }

                var node = GameStateCache.Instance.GetNode(nodeId);
                if (node == null || string.IsNullOrEmpty(node.BuildingType))
                {
                    continue;
                }

                ApplyBackendBuildCommand(node.BuildingType, nodeId, false, node.Owner, node.BuildingHp);
            }
        }

        private void OnPreviewChanged()
        {
            RefreshPreviewVisuals();
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

        private void RefreshPreviewVisuals()
        {
            ClearNodeHighlights();

            var preview = CombatDraftCache.Instance != null ? CombatDraftCache.Instance.CurrentPreview : null;
            if (preview == null || !preview.Valid)
            {
                if (!string.IsNullOrEmpty(_hoverPreviewNodeId) &&
                    !string.IsNullOrEmpty(_selectedUnit?.UnitId) &&
                    preview != null &&
                    string.Equals(preview.UnitId, _selectedUnit.UnitId, StringComparison.Ordinal) &&
                    string.Equals(preview.TargetNodeId, _hoverPreviewNodeId, StringComparison.Ordinal) &&
                    MapRenderer.Instance != null &&
                    MapRenderer.Instance.TryGetNodeView(_hoverPreviewNodeId, out var invalidNode))
                {
                    invalidNode.SetHighlight(true, moveInvalidColor);
                    _highlightNodeIds.Add(_hoverPreviewNodeId);
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

            if (preview.PathNodeIds != null)
            {
                for (var i = 0; i < preview.PathNodeIds.Count; i++)
                {
                    var nodeId = preview.PathNodeIds[i];
                    if (string.IsNullOrWhiteSpace(nodeId) || !map.TryGetNodeView(nodeId, out var node))
                    {
                        continue;
                    }

                    var color = string.Equals(nodeId, preview.FirstTurnNodeId, StringComparison.Ordinal)
                        ? moveFirstTurnColor
                        : moveHighlightColor;
                    node.SetHighlight(true, color);
                    _highlightNodeIds.Add(nodeId);
                }
            }

            if (preview.TurnStops != null)
            {
                for (var i = 0; i < preview.TurnStops.Count; i++)
                {
                    var stop = preview.TurnStops[i];
                    if (stop == null || string.IsNullOrWhiteSpace(stop.NodeId) || !map.TryGetNodeView(stop.NodeId, out var node))
                    {
                        continue;
                    }

                    node.SetHighlight(true, stop.TurnIndex <= 1 ? moveFirstTurnColor : moveFutureTurnColor);
                    _highlightNodeIds.Add(stop.NodeId);
                    if (stop.TurnIndex >= 2)
                    {
                        CreateMoveTurnMarker(stop.NodeId, node, stop.TurnIndex);
                    }
                }
            }
        }

        private void ClearMovePreviewState()
        {
            var hadHover = !string.IsNullOrEmpty(_hoverPreviewNodeId);
            _hoverPreviewNodeId = string.Empty;
            var previewCache = CombatDraftCache.Instance;
            if (hadHover || previewCache?.CurrentPreview != null)
            {
                previewCache?.ClearPreview();
            }
            ClearNodeHighlights();
        }

        private void CreateMoveTurnMarker(string nodeId, NodeView node, int turnIndex)
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
            marker.transform.SetParent(transform, false);
            marker.transform.position = (node.UnitAnchor != null ? node.UnitAnchor.position : node.transform.position) + Vector3.up * moveTurnMarkerHeight;
            var text = marker.AddComponent<TextMesh>();
            text.text = turnIndex.ToString();
            text.characterSize = 0.18f;
            text.fontSize = 42;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = moveFutureTurnColor;
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
                    Destroy(pair.Value);
                }
            }

            _moveTurnMarkers.Clear();
        }

        private bool IsCastleNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map != null && map.TryGetNodeState(nodeId, out var mapNode) && mapNode != null)
            {
                return string.Equals(NormalizeToken(mapNode.BuildingType), "castle", StringComparison.Ordinal);
            }

            if (GameStateCache.Instance == null)
            {
                return false;
            }

            var cacheNode = GameStateCache.Instance.GetNode(nodeId);
            return cacheNode != null && string.Equals(NormalizeToken(cacheNode.BuildingType), "castle", StringComparison.Ordinal);
        }

        private bool IsCastleOwnedByLocalPlayer(string nodeId)
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
            return string.Equals(unit.Faction, localOwner, StringComparison.Ordinal);
        }

        private bool TryRaycastNode(out NodeView nodeView)
        {
            nodeView = null;
            if (!TryRaycast(out var hit))
            {
                return false;
            }

            nodeView = hit.collider.GetComponentInParent<NodeView>();
            return nodeView != null;
        }

        private bool TryRaycastUnit(out UnitView unitView)
        {
            unitView = null;
            if (!TryRaycast(out var hit))
            {
                return false;
            }

            if (hit.collider.GetComponentInParent<MoveGhostTag>() != null)
            {
                return false;
            }

            unitView = hit.collider.GetComponentInParent<UnitView>();
            return unitView != null;
        }

        private bool TryRaycast(out RaycastHit hit)
        {
            hit = default;

            var ray = inputCamera.ScreenPointToRay(GetMousePosition());
            return Physics.Raycast(ray, out hit, raycastDistance, raycastMask, QueryTriggerInteraction.Ignore);
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static bool HasTag(StaticCatalogCache.UnitEntryJson entry, string tag)
        {
            if (entry == null || entry.tags == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var normalized = NormalizeToken(tag);
            for (var i = 0; i < entry.tags.Length; i++)
            {
                if (string.Equals(NormalizeToken(entry.tags[i]), normalized, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetCombatPrompt()
        {
            if (_selectedUnit == null)
            {
                return "点击己方单位开始下达命令";
            }

            return _combatActionMode switch
            {
                CombatActionMode.Move => "点击地图节点，发送持久行军目标",
                CombatActionMode.Attack => "点击敌方单位，发送攻击命令",
                CombatActionMode.Charge => "点击敌方单位，发送冲锋命令",
                _ => "选择动作后再指定目标"
            };
        }

        private void NotifyCombatSelectionChanged()
        {
            CombatSelectionChanged?.Invoke();
        }

        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null)
            {
                return false;
            }
            
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && EventSystem.current.IsPointerOverGameObject(Mouse.current.deviceId))
            {
                return true;
            }
#endif

            _uiRaycastResults.Clear();
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = GetMousePosition()
            };
            EventSystem.current.RaycastAll(eventData, _uiRaycastResults);
            return _uiRaycastResults.Count > 0;
        }

        private void BlockInputAfterModeSwitch()
        {
            _ignoreInputUntilTime = Time.unscaledTime + Mathf.Max(0f, modeSwitchInputBlockSeconds);
        }

        private bool HasMouse()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null;
#else
            return true;
#endif
        }

        private Vector3 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null ? (Vector3)mouse.position.ReadValue() : Vector3.zero;
#else
            return Input.mousePosition;
#endif
        }

        private bool GetLeftMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private bool GetRightMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(1);
#endif
        }

        private void CreateOrUpdateMovePreview(string unitId, string targetNodeId)
        {
            if (!enableMovePreviewGhost || string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            if (!map.TryGetUnitView(unitId, out var sourceUnit) || sourceUnit == null)
            {
                return;
            }

            if (!map.TryGetNodeView(targetNodeId, out var targetNode) || targetNode == null)
            {
                return;
            }

            RemoveMovePreview(unitId);

            var ghost = CreateMovePreviewObject(sourceUnit);
            if (ghost == null)
            {
                return;
            }

            ghost.name = $"MoveGhost_{unitId}";
            ghost.transform.SetParent(map.transform, true);
            ghost.AddComponent<MoveGhostTag>();

            var useProxy = ShouldUseLightweightProxy(sourceUnit);
            if (useProxy)
            {
                var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
                if (ignoreRaycastLayer >= 0)
                {
                    SetLayerRecursively(ghost.transform, ignoreRaycastLayer);
                }
                ApplyGhostVisual(ghost);
            }
            else
            {
                DisableBehavioursAndColliders(ghost, keepAnimators: true);
                ApplyGhostVisual(ghost);
                SetGhostMoveState(ghost, isMoving: true, normalizedSpeed: 1f);
            }

            _movePreviewByUnitId[unitId] = ghost;

            var destination = targetNode.UnitAnchor != null
                ? targetNode.UnitAnchor.position
                : targetNode.transform.position + Vector3.up * 0.2f;
            destination.y += movePreviewTargetYOffset;

            StartCoroutine(AnimateMovePreview(ghost, destination));
        }

        private bool ShouldUseLightweightProxy(UnitView sourceUnit)
        {
            if (!movePreviewUseLightweightProxy)
            {
                return false;
            }

            if (movePreviewAlwaysMatchUnitVisual)
            {
                return false;
            }

            return sourceUnit == null;
        }

        private GameObject CreateMovePreviewObject(UnitView sourceUnit)
        {
            if (sourceUnit == null)
            {
                return null;
            }

            if (!ShouldUseLightweightProxy(sourceUnit))
            {
                var sourceVisualRoot = sourceUnit.VisualRoot;
                var sourceObject = sourceVisualRoot != null ? sourceVisualRoot.gameObject : sourceUnit.gameObject;
                var clone = Instantiate(sourceObject);
                clone.transform.position = sourceObject.transform.position;
                clone.transform.rotation = sourceObject.transform.rotation;
                return clone;
            }

            var proxy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            proxy.transform.position = sourceUnit.transform.position + Vector3.up * movePreviewProxyYOffset;
            proxy.transform.rotation = sourceUnit.transform.rotation;
            proxy.transform.localScale = movePreviewProxyScale;

            var collider = proxy.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = proxy.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetOrCreateMovePreviewProxyMaterial();
                renderer.shadowCastingMode = movePreviewProxyCastShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = movePreviewProxyCastShadow;
            }

            return proxy;
        }

        private Material GetOrCreateMovePreviewProxyMaterial()
        {
            if (_movePreviewProxyMaterial != null)
            {
                return _movePreviewProxyMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return null;
            }

            _movePreviewProxyMaterial = new Material(shader);
            _movePreviewProxyMaterial.name = "MovePreviewProxyMat_Runtime";
            _movePreviewProxyMaterial.hideFlags = HideFlags.DontSave;

            if (_movePreviewProxyMaterial.HasProperty("_Surface"))
            {
                _movePreviewProxyMaterial.SetFloat("_Surface", 1f);
            }

            if (_movePreviewProxyMaterial.HasProperty("_Blend"))
            {
                _movePreviewProxyMaterial.SetFloat("_Blend", 0f);
            }

            if (_movePreviewProxyMaterial.HasProperty("_SrcBlend"))
            {
                _movePreviewProxyMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (_movePreviewProxyMaterial.HasProperty("_DstBlend"))
            {
                _movePreviewProxyMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (_movePreviewProxyMaterial.HasProperty("_ZWrite"))
            {
                _movePreviewProxyMaterial.SetFloat("_ZWrite", 0f);
            }

            _movePreviewProxyMaterial.renderQueue = (int)RenderQueue.Transparent;
            return _movePreviewProxyMaterial;
        }

        private void DisposeMovePreviewProxyMaterial()
        {
            if (_movePreviewProxyMaterial == null)
            {
                return;
            }

            Destroy(_movePreviewProxyMaterial);
            _movePreviewProxyMaterial = null;
        }

        private IEnumerator AnimateMovePreview(GameObject ghost, Vector3 destination)
        {
            if (ghost == null)
            {
                yield break;
            }

            var start = ghost.transform.position;
            var duration = Mathf.Max(0.01f, movePreviewTravelDuration);
            var elapsed = 0f;

            while (elapsed < duration && ghost != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var pos = Vector3.Lerp(start, destination, t);
                if (movePreviewArcHeight > 0.0001f)
                {
                    pos.y += Mathf.Sin(t * Mathf.PI) * movePreviewArcHeight;
                }

                var moveDir = destination - ghost.transform.position;
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude > 0.0001f)
                {
                    ghost.transform.rotation = Quaternion.Slerp(
                        ghost.transform.rotation,
                        Quaternion.LookRotation(moveDir.normalized, Vector3.up),
                        Time.deltaTime * 18f);
                }

                ghost.transform.position = pos;
                yield return null;
            }

            if (ghost != null)
            {
                ghost.transform.position = destination;
                SetGhostMoveState(ghost, isMoving: false, normalizedSpeed: 0f);
            }
        }

        private void ApplyGhostVisual(GameObject ghostRoot)
        {
            if (ghostRoot == null)
            {
                return;
            }

            var renderers = ghostRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var mats = renderer.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    continue;
                }

                for (var m = 0; m < mats.Length; m++)
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, m);
                    var ghostColor = movePreviewKeepTextureColor
                        ? new Color(1f, 1f, 1f, movePreviewGhostColor.a)
                        : Color.Lerp(Color.white, movePreviewGhostColor, movePreviewTintStrength);
                    block.SetColor("_BaseColor", ghostColor);
                    block.SetColor("_Color", ghostColor);
                    renderer.SetPropertyBlock(block, m);
                }
            }
        }

        private static void DisableBehavioursAndColliders(GameObject root, bool keepAnimators)
        {
            if (root == null)
            {
                return;
            }

            var ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer >= 0)
            {
                SetLayerRecursively(root.transform, ignoreRaycastLayer);
            }

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                behaviour.enabled = false;
            }

            var animators = root.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator != null)
                {
                    animator.enabled = keepAnimators;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }

        private static void SetGhostMoveState(GameObject ghostRoot, bool isMoving, float normalizedSpeed)
        {
            if (ghostRoot == null)
            {
                return;
            }

            var animators = ghostRoot.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                if (HasAnimatorParameter(animator, "isMoving", AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool("isMoving", isMoving);
                }

                if (HasAnimatorParameter(animator, "moveSpeed", AnimatorControllerParameterType.Float))
                {
                    animator.SetFloat("moveSpeed", Mathf.Max(0f, normalizedSpeed));
                }
            }
        }

        private static bool HasAnimatorParameter(Animator animator, string paramName, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(paramName) || animator.parameters == null)
            {
                return false;
            }

            var hash = Animator.StringToHash(paramName);
            var parameters = animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash && parameters[i].type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetLayerRecursively(Transform node, int layer)
        {
            if (node == null)
            {
                return;
            }

            node.gameObject.layer = layer;
            for (var i = 0; i < node.childCount; i++)
            {
                SetLayerRecursively(node.GetChild(i), layer);
            }
        }

        private void RemoveMovePreview(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return;
            }

            if (!_movePreviewByUnitId.TryGetValue(unitId, out var preview) || preview == null)
            {
                _movePreviewByUnitId.Remove(unitId);
                return;
            }

            Destroy(preview);
            _movePreviewByUnitId.Remove(unitId);
        }

        private void ClearAllMovePreviews()
        {
            if (_movePreviewByUnitId.Count == 0)
            {
                return;
            }

            foreach (var pair in _movePreviewByUnitId)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }

            _movePreviewByUnitId.Clear();
        }
    }
}
