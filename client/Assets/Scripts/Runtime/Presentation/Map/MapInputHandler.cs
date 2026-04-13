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
using Panoptes.Presentation.UI.Domestic;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.UI.Common;
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
            Move = 1,
            Build = 2
        }

        public static MapInputHandler Instance { get; private set; }

        [Header("Raycast")]
        [SerializeField] private Camera inputCamera;
        [SerializeField] private LayerMask raycastMask = ~0;
        [SerializeField] private float raycastDistance = 200f;
        
        [Header("Input Gate")]
        [SerializeField] private float modeSwitchInputBlockSeconds = 0.12f;

        [Header("Move")]
        [SerializeField] private int moveRange = 4;
        [SerializeField] private Color moveHighlightColor = new Color(0.35f, 1f, 0.45f, 0.9f);
        [SerializeField] private bool onlyControlOwnUnits = true;
        [SerializeField] private Color movePathArrowColor = new Color(0.35f, 1f, 0.45f, 0.92f);
        [SerializeField] private Color movePathDestinationColor = new Color(0.25f, 0.95f, 0.55f, 0.95f);

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
        [SerializeField] private Color territoryHighlightColor = new Color(0.28f, 0.72f, 1f, 0.72f);
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

        [Header("Deploy")]
        [SerializeField] private string[] territoryExpansionUnitTypes =
        {
            "settler",
            "pioneer",
            "expander",
            "engineer"
        };

        [Header("Castle Panel")]
        [SerializeField] private CastleProductionPanel castleProductionPanel;
        [SerializeField] private bool autoFindCastleProductionPanel = true;
        [SerializeField] private bool autoSpawnCastleProductionPanelIfMissing = true;
        [SerializeField] private string castleProductionPanelResourcesPath = "Prefabs/UI/CastleProductionPanel";

        private readonly HashSet<string> _highlightNodeIds = new();
        private readonly HashSet<string> _territoryHighlightNodeIds = new();
        private readonly List<PendingBuildRecord> _pendingBuilds = new();
        private readonly Queue<string> _pendingBuildTokenNodeQueue = new();
        private readonly Dictionary<string, string> _pendingDeployGhostNodeByUnitId = new();
        private readonly Dictionary<string, GameObject> _movePreviewByUnitId = new();
        private readonly HashSet<string> _pendingMoveUnitIds = new();
        private readonly Dictionary<string, string> _pendingMoveTargetNodeByUnitId = new();
        private readonly Dictionary<string, List<string>> _movePathNodeIdsByUnitId = new();
        private Material _movePreviewProxyMaterial;

        private Mode _mode = Mode.None;
        private UnitView _selectedUnit;
        private BuildPlacementRule _buildRule;
        private string _buildType = string.Empty;
        private string _activeBuildCastleId = string.Empty;
        private NodeView _hoverNode;
        private BuildingView _hoverGhost;
        private GameStateCache _cache;
        private bool _cacheEventsSubscribed;
        private float _ignoreInputUntilTime;
        private readonly List<RaycastResult> _uiRaycastResults = new();
        private UnitInfoPanelController _unitInfoPanelController;
        private UnitView _buildingInfoProxy;
        private StaticCatalogCache _staticCatalogCache;

        private sealed class MoveGhostTag : MonoBehaviour
        {
        }

        public IReadOnlyList<PendingBuildRecord> PendingBuilds => _pendingBuilds;
        public UnitView SelectedUnit => _selectedUnit;

        public event Action<string, string> MoveCommandSent;
        public event Action<string, string> BuildCommandSent;
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
        }

        private void OnEnable()
        {
            SubscribeCacheEvents();
        }

        private void OnDisable()
        {
            UnsubscribeCacheEvents();
            ClearAllMovePreviews();
            ClearAllMovePathMarkers();
            DisposeMovePreviewProxyMaterial();
            _pendingBuildTokenNodeQueue.Clear();
            ClearAllPendingDeployGhosts();
            ClearTerritoryHighlights();
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

            if (GetLeftMouseButtonDown())
            {
                if (IsPointerOverUI())
                {
                    return;
                }

                if (TryOpenBuildingInfoFromClick())
                {
                    return;
                }

                ClearTerritoryHighlights();
                NonBuildingMapClicked?.Invoke();
                HandleMoveSelectionClick();
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

        public void SetBuildCastleContext(string castleNodeId)
        {
            _activeBuildCastleId = string.IsNullOrWhiteSpace(castleNodeId) ? string.Empty : castleNodeId.Trim();
        }

        public void CancelCurrentMode()
        {
            ExitBuildMode();
            ClearMoveSelection();
            ClearNodeHighlights();
            BlockInputAfterModeSwitch();
        }

        public bool RequestExpandTerritoryForSelectedUnit()
        {
            if (_selectedUnit == null)
            {
                return false;
            }

            var centerNodeId = ResolveExpandCenterNodeId(_selectedUnit.UnitId, _selectedUnit.GridPos);
            if (!TryValidateTerritoryExpandRequest(_selectedUnit.UnitId, centerNodeId, out var validationError))
            {
                ClearPendingDeployCastleGhostForUnit(_selectedUnit.UnitId);
                ShowUserError(validationError);
                return false;
            }

            ShowPendingDeployCastleGhost(_selectedUnit.UnitId, centerNodeId);
            GameIntents.ExpandTerritory(_selectedUnit.UnitId, centerNodeId);
            var phase = _cache != null ? _cache.Phase : string.Empty;
            Debug.Log($"[MapInputHandler] territory action sent. unit={_selectedUnit.UnitId} center={centerNodeId} phase={phase}");
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

            if (!TryValidateTerritoryExpandRequest(unitId, resolvedCenterNodeId, out var validationError))
            {
                ClearPendingDeployCastleGhostForUnit(unitId);
                ShowUserError(validationError);
                return false;
            }

            ShowPendingDeployCastleGhost(unitId, resolvedCenterNodeId);
            GameIntents.ExpandTerritory(unitId, resolvedCenterNodeId);
            var phase = _cache != null ? _cache.Phase : string.Empty;
            Debug.Log($"[MapInputHandler] territory action sent. unit={unitId} center={resolvedCenterNodeId} phase={phase}");
            return true;
        }

        public bool IsUnitMovePending(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            return _pendingMoveUnitIds.Contains(unitId.Trim());
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
            _buildType = ResolveBackendBuildingType(NormalizeToken(buildingType));
            if (disallowManualCastlePlacement && string.Equals(_buildType, "castle", StringComparison.Ordinal))
            {
                Debug.Log("[MapInputHandler] Castle is pre-placed by map config and cannot be manually built.");
                ExitBuildMode();
                return;
            }

            _mode = Mode.Build;
            _buildRule = rule;
            BlockInputAfterModeSwitch();

            ClearMoveSelection();
            ClearNodeHighlights();
            DestroyHoverGhost();
            EnsureCityZones();
        }

        private void ExitBuildMode()
        {
            RestoreNodeHighlightAfterHover(_hoverNode);

            _mode = Mode.None;
            _buildType = string.Empty;
            _activeBuildCastleId = string.Empty;
            _hoverNode = null;
            DestroyHoverGhost();
            ClearNodeHighlights();
        }

        private void HandleMoveSelectionClick()
        {
            if (TryRaycastUnit(out var unit))
            {
                SelectUnit(unit);
                return;
            }

            if (!IsCombatPhase())
            {
                return;
            }

            if (_selectedUnit != null && TryRaycastNode(out var node))
            {
                if (_highlightNodeIds.Contains(node.NodeId))
                {
                    SendMoveCommand(_selectedUnit.UnitId, node.NodeId);
                    return;
                }
            }

            ClearMoveSelection();
        }

        private bool TryOpenBuildingInfoFromClick()
        {
            if (!TryRaycastNode(out var node))
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetNodeState(node.NodeId, out var nodeState) || nodeState == null)
            {
                return false;
            }

            var buildingType = NormalizeToken(nodeState.BuildingType);
            if (string.IsNullOrEmpty(buildingType))
            {
                return false;
            }

            if (string.Equals(buildingType, "castle", StringComparison.Ordinal))
            {
                HighlightTerritoryForNode(nodeState);
            }
            else
            {
                ClearTerritoryHighlights();
            }

            var proxy = GetOrCreateBuildingInfoProxy(node, nodeState);
            if (proxy == null)
            {
                return false;
            }

            ClearMoveSelection(false);
            NotifyUnitSelectionChanged(proxy);
            NotifyUnitInfoPanel(proxy);
            return true;
        }

        private UnitView GetOrCreateBuildingInfoProxy(NodeView nodeView, NodeDto nodeState)
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

            var hp = nodeState.BuildingHp > 0 ? nodeState.BuildingHp : 100;
            var unit = new UnitDto
            {
                Id = nodeState.Id ?? string.Empty,
                Type = NormalizeToken(nodeState.BuildingType),
                Owner = !string.IsNullOrWhiteSpace(nodeState.Owner) ? nodeState.Owner : nodeState.TerritoryOwner,
                X = nodeState.X,
                Y = nodeState.Y,
                Hp = hp,
                MaxHp = Mathf.Max(1, hp)
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
            NotifyUnitSelectionChanged(_selectedUnit);
            NotifyUnitInfoPanel(_selectedUnit);

            if (!canControl || !IsCombatPhase())
            {
                ClearNodeHighlights();
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            foreach (var pair in map.TileViews)
            {
                var node = pair.Value;
                if (node == null || node.GridPos == _selectedUnit.GridPos)
                {
                    continue;
                }

                if (!map.IsNodePassableForMove(node.NodeId))
                {
                    continue;
                }

                var dist = Mathf.Abs(node.GridPos.x - _selectedUnit.GridPos.x) + Mathf.Abs(node.GridPos.y - _selectedUnit.GridPos.y);
                if (dist > moveRange)
                {
                    continue;
                }

                node.SetHighlight(true, moveHighlightColor);
                _highlightNodeIds.Add(node.NodeId);
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
                Debug.LogError($"[MapInputHandler] UnitSelectionChanged callback failed: {ex.Message}");
            }
        }

        private void NotifyUnitInfoPanel(UnitView unit)
        {
            if (_unitInfoPanelController == null)
            {
                _unitInfoPanelController = UnityEngine.Object.FindAnyObjectByType<UnitInfoPanelController>();
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
                RestoreNodeHighlightAfterHover(_hoverNode);
                _hoverNode = null;
                DestroyHoverGhost();
                return;
            }

            var hasNode = TryRaycastNode(out var node);
            if (!hasNode)
            {
                RestoreNodeHighlightAfterHover(_hoverNode);
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
                RestoreNodeHighlightAfterHover(_hoverNode);

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
                var backendBuildingType = ResolveBackendBuildingType(_buildType);
                map.ApplyBuildingPlacement(node.NodeId, backendBuildingType, ownerId, true, 100, buildPlacedGhostColor);
                _pendingBuilds.Add(new PendingBuildRecord
                {
                    buildingType = backendBuildingType,
                    nodeId = node.NodeId,
                    ownerId = ownerId,
                    isGhost = true
                });

                SendBuildCommand(backendBuildingType, node.NodeId);
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

            if (HasPendingBuild(nodeId))
            {
                return false;
            }

            if (!map.IsNodeBuildBaseAvailable(nodeId))
            {
                return false;
            }

            var backendBuildingType = ResolveBackendBuildingType(_buildType);
            if (TryGetServerPlacementRule(backendBuildingType, out var placementRule, out var requiredResourceType))
            {
                switch (placementRule)
                {
                    case "resource_only":
                        return map.IsNodeResourcePoint(nodeId)
                               && IsNodeResourceTypeMatch(nodeId, requiredResourceType);
                    case "city_only":
                        return CanPlaceCityBuilding(nodeId);
                    case "any_terrain":
                        return true;
                }
            }

            if (IsTerritoryOnlyBuildingType(backendBuildingType) && !CanPlaceCityBuilding(nodeId))
            {
                return false;
            }

            if (IsGlobalPlacementBuildingType(backendBuildingType))
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
            return IsInsideLocalTerritory(nodeId);
        }

        private bool IsNodeResourceTypeMatch(string nodeId, string requiredResourceType)
        {
            var required = NormalizeToken(requiredResourceType);
            if (string.IsNullOrEmpty(required))
            {
                return true;
            }

            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetNodeState(nodeId, out var node) || node == null)
            {
                return false;
            }

            return string.Equals(NormalizeToken(node.ResourceType), required, StringComparison.Ordinal);
        }

        private bool IsInsideLocalTerritory(string nodeId)
        {
            var map = MapRenderer.Instance;
            if (map == null || string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            if (!map.TryGetNodeState(nodeId, out var node) || node == null)
            {
                return false;
            }

            var ownerId = NormalizeToken(GetLocalOwnerId());
            var territoryOwner = NormalizeToken(node.TerritoryOwner);
            if (string.IsNullOrEmpty(territoryOwner))
            {
                return false;
            }

            return string.Equals(territoryOwner, ownerId, StringComparison.Ordinal);
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
                node.SetHighlight(true, moveHighlightColor);
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
                        nodeView.SetHighlight(true, moveHighlightColor);
                    }
                    else
                    {
                        nodeView.SetHighlightVisible(false);
                    }
                }
            }

            _territoryHighlightNodeIds.Clear();
        }

        private void SendMoveCommand(string unitId, string targetNodeId)
        {
            if (!string.IsNullOrWhiteSpace(unitId))
            {
                var normalizedUnitId = unitId.Trim();
                // Move command should cancel any pending deploy intent for the same unit.
                ClearPendingDeployCastleGhostForUnit(normalizedUnitId);
                _pendingMoveUnitIds.Add(normalizedUnitId);
                _pendingMoveTargetNodeByUnitId[normalizedUnitId] = targetNodeId ?? string.Empty;
            }

            RemoveMovePreview(unitId);
            ApplyMovePathPreviewMarkers(unitId, targetNodeId);
            GameIntents.MoveUnit(unitId, targetNodeId);
            MoveCommandSent?.Invoke(unitId, targetNodeId);
            ClearNodeHighlights();
        }

        private void SendBuildCommand(string buildingType, string nodeId)
        {
            buildingType = ResolveBackendBuildingType(buildingType);
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                _pendingBuildTokenNodeQueue.Enqueue(nodeId.Trim());
            }

            GameIntents.BuildToken(nodeId, buildingType, _activeBuildCastleId);
            BuildCommandSent?.Invoke(buildingType, nodeId);
        }

        private void SubscribeCacheEvents()
        {
            if (_cacheEventsSubscribed)
            {
                return;
            }

            _cache = GameStateCache.Instance;
            if (_cache == null)
            {
                return;
            }

            _cache.OnCombatSettled += OnCombatSettled;
            _cache.OnDomesticSettled += OnDomesticSettled;
            _cache.OnNodeChanged += OnNodeChanged;
            _cache.OnUnitsChanged += OnUnitsChanged;
            _cache.OnTokenResult += OnTokenResult;
            _cacheEventsSubscribed = true;
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
                _cache.OnNodeChanged -= OnNodeChanged;
                _cache.OnUnitsChanged -= OnUnitsChanged;
                _cache.OnTokenResult -= OnTokenResult;
            }

            _cache = null;
            _cacheEventsSubscribed = false;
        }

        private void OnCombatSettled(CombatSettledEvent settledEvent)
        {
            var events = settledEvent?.Settlement?.Events;
            if (events == null || events.Count == 0)
            {
                return;
            }

            for (int i = 0; i < events.Count; i++)
            {
                var eventItem = events[i];
                if (eventItem == null || eventItem.Type != "unit_move")
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(eventItem.UnitId))
                {
                    var normalizedUnitId = eventItem.UnitId.Trim();
                    _pendingMoveUnitIds.Remove(normalizedUnitId);
                    _pendingMoveTargetNodeByUnitId.Remove(normalizedUnitId);
                    ClearMovePathMarkersForUnit(normalizedUnitId);
                }

                if (MapRenderer.Instance == null)
                {
                    continue;
                }

                var grid = new Vector2Int(eventItem.ToX, eventItem.ToY);
                if (!MapRenderer.Instance.TryGetNodeIdByGrid(grid, out var targetNodeId))
                {
                    continue;
                }

                ApplyBackendMoveCommand(eventItem.UnitId, targetNodeId, true, true);
            }
        }

        private void OnDomesticSettled(DomesticSettledEvent e)
        {
            _pendingMoveUnitIds.Clear();
            _pendingMoveTargetNodeByUnitId.Clear();
            _pendingBuildTokenNodeQueue.Clear();
            ClearAllMovePathMarkers();

            var builtBuildings = e?.Settlement?.BuiltBuildings;
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

            if (!string.Equals(action, "build", StringComparison.Ordinal))
            {
                return;
            }

            string nodeId = null;
            if (_pendingBuildTokenNodeQueue.Count > 0)
            {
                nodeId = _pendingBuildTokenNodeQueue.Dequeue();
            }

            if (e.Success)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(nodeId))
            {
                nodeId = GetLastPendingBuildNodeId();
            }

            RollbackPendingBuild(nodeId);
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
                    nodeView.SetHighlight(true, moveHighlightColor);
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
                    _pendingMoveUnitIds.Remove(normalizedRemovedId);
                    _pendingMoveTargetNodeByUnitId.Remove(normalizedRemovedId);
                    ClearMovePathMarkersForUnit(normalizedRemovedId);
                    ClearPendingDeployCastleGhostForUnit(normalizedRemovedId);
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
                }
            }
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

            if (string.Equals(normalized, "combat", StringComparison.Ordinal) ||
                string.Equals(normalized, NormalizeToken(GamePhases.CombatPlanning), StringComparison.Ordinal))
            {
                return true;
            }

            // Compatibility with merged/new phase naming variants.
            if (normalized.IndexOf("combat", StringComparison.Ordinal) >= 0 &&
                (normalized.IndexOf("planning", StringComparison.Ordinal) >= 0 ||
                 normalized.IndexOf("deploy", StringComparison.Ordinal) >= 0))
            {
                return true;
            }

            return false;
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

            placementRule = NormalizeToken(entry.placement_rule);
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

        private void ShowPendingDeployCastleGhost(string unitId, string centerNodeId)
        {
            var normalizedUnitId = NormalizeToken(unitId);
            var normalizedNodeId = string.IsNullOrWhiteSpace(centerNodeId) ? string.Empty : centerNodeId.Trim();
            if (string.IsNullOrEmpty(normalizedUnitId) || string.IsNullOrEmpty(normalizedNodeId))
            {
                return;
            }

            if (_pendingDeployGhostNodeByUnitId.TryGetValue(normalizedUnitId, out var oldNodeId)
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

            map.ApplyBuildingPlacement(normalizedNodeId, "castle", GetLocalOwnerId(), true, 100, buildPlacedGhostColor);
            _pendingDeployGhostNodeByUnitId[normalizedUnitId] = normalizedNodeId;
        }

        private void ClearPendingDeployCastleGhostForUnit(string unitId)
        {
            var normalizedUnitId = NormalizeToken(unitId);
            if (string.IsNullOrEmpty(normalizedUnitId))
            {
                return;
            }

            if (!_pendingDeployGhostNodeByUnitId.TryGetValue(normalizedUnitId, out var nodeId))
            {
                return;
            }

            _pendingDeployGhostNodeByUnitId.Remove(normalizedUnitId);
            TryClearPendingDeployGhostNode(nodeId);
        }

        private void ClearAllPendingDeployGhosts()
        {
            if (_pendingDeployGhostNodeByUnitId.Count == 0)
            {
                return;
            }

            var nodeIDs = new HashSet<string>(_pendingDeployGhostNodeByUnitId.Values);
            _pendingDeployGhostNodeByUnitId.Clear();

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

            if (_pendingDeployGhostNodeByUnitId.Count == 0)
            {
                return;
            }

            var normalizedNodeId = nodeId.Trim();
            string unitIdToRemove = null;
            foreach (var pair in _pendingDeployGhostNodeByUnitId)
            {
                if (string.Equals(pair.Value, normalizedNodeId, StringComparison.Ordinal))
                {
                    unitIdToRemove = pair.Key;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(unitIdToRemove))
            {
                _pendingDeployGhostNodeByUnitId.Remove(unitIdToRemove);
            }
        }

        private string ResolveExpandCenterNodeId(string unitId, Vector2Int fallbackGrid)
        {
            var normalizedUnitId = string.IsNullOrWhiteSpace(unitId) ? string.Empty : unitId.Trim();
            if (!string.IsNullOrEmpty(normalizedUnitId)
                && _pendingMoveTargetNodeByUnitId.TryGetValue(normalizedUnitId, out var pendingNodeId)
                && !string.IsNullOrWhiteSpace(pendingNodeId))
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

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private bool TryValidateTerritoryExpandRequest(string unitId, string centerNodeId, out string errorMessage)
        {
            errorMessage = string.Empty;

            var map = MapRenderer.Instance;
            if (map == null)
            {
                errorMessage = "Deploy failed: map is not initialized.";
                return false;
            }

            var normalizedUnitId = string.IsNullOrWhiteSpace(unitId) ? string.Empty : unitId.Trim();
            if (string.IsNullOrEmpty(normalizedUnitId))
            {
                errorMessage = "Deploy failed: unit id is empty.";
                return false;
            }

            if (!map.TryGetUnitView(normalizedUnitId, out var unitView) || unitView == null)
            {
                errorMessage = "Deploy failed: settler unit not found.";
                return false;
            }

            if (!CanControlUnit(unitView))
            {
                errorMessage = "Deploy failed: only your own settler can deploy.";
                return false;
            }

            if (!IsTerritoryExpansionUnitType(unitView.UnitType))
            {
                errorMessage = "Deploy failed: this unit type cannot expand territory.";
                return false;
            }

            var normalizedCenterNodeId = string.IsNullOrWhiteSpace(centerNodeId) ? string.Empty : centerNodeId.Trim();
            if (string.IsNullOrEmpty(normalizedCenterNodeId))
            {
                errorMessage = "Deploy failed: invalid target node.";
                return false;
            }

            if (!map.TryGetNodeView(normalizedCenterNodeId, out var centerNode) || centerNode == null)
            {
                errorMessage = "Cannot deploy here: target node does not exist.";
                return false;
            }

            var centerGrid = centerNode.GridPos;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var grid = new Vector2Int(centerGrid.x + dx, centerGrid.y + dy);
                    if (!map.TryGetNodeViewByGrid(grid, out var node) || node == null)
                    {
                        errorMessage = "Cannot deploy here: 3x3 territory is out of map bounds.";
                        return false;
                    }

                    if (!map.TryGetNodeState(node.NodeId, out var nodeState) || nodeState == null)
                    {
                        errorMessage = "Cannot deploy here: target node state is unavailable.";
                        return false;
                    }

                    if (nodeState.IsResourcePoint)
                    {
                        errorMessage = "Cannot deploy here: 3x3 territory contains resource points.";
                        return false;
                    }

                    var buildingType = NormalizeToken(nodeState.BuildingType);
                    if (!string.IsNullOrEmpty(buildingType))
                    {
                        // Allow already-expanded center castle only for idempotent retry.
                        var allowCenterCastle = dx == 0 && dy == 0 && string.Equals(buildingType, "castle", StringComparison.Ordinal);
                        if (!allowCenterCastle)
                        {
                            errorMessage = "Cannot deploy here: 3x3 territory contains existing buildings.";
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private bool IsTerritoryExpansionUnitType(string unitType)
        {
            if (string.IsNullOrWhiteSpace(unitType) || territoryExpansionUnitTypes == null || territoryExpansionUnitTypes.Length == 0)
            {
                return false;
            }

            var normalized = NormalizeToken(unitType);
            for (var i = 0; i < territoryExpansionUnitTypes.Length; i++)
            {
                if (string.Equals(normalized, NormalizeToken(territoryExpansionUnitTypes[i]), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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

            Debug.LogWarning($"[MapInputHandler] {message}");
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

        private void ApplyMovePathPreviewMarkers(string unitId, string targetNodeId)
        {
            var normalizedUnitId = NormalizeToken(unitId);
            if (string.IsNullOrEmpty(normalizedUnitId))
            {
                return;
            }

            ClearMovePathMarkersForUnit(normalizedUnitId);

            var map = MapRenderer.Instance;
            if (map == null || string.IsNullOrWhiteSpace(targetNodeId))
            {
                return;
            }

            if (!map.TryGetUnitView(unitId, out var unitView) || unitView == null)
            {
                return;
            }

            if (!map.TryGetNodeView(targetNodeId, out var targetNode) || targetNode == null)
            {
                return;
            }

            var path = BuildMovePath(unitView.GridPos, targetNode.GridPos);
            if (path == null || path.Count < 2)
            {
                return;
            }

            var usedNodeIds = new List<string>(path.Count);
            for (var i = 1; i < path.Count; i++)
            {
                var node = path[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                {
                    continue;
                }

                if (i == path.Count - 1)
                {
                    node.ShowMovePathDestination(movePathDestinationColor);
                }
                else
                {
                    var nextNode = path[i + 1];
                    if (nextNode == null)
                    {
                        continue;
                    }

                    var dir = nextNode.GridPos - node.GridPos;
                    node.ShowMovePathArrow(dir, movePathArrowColor);
                }

                usedNodeIds.Add(node.NodeId);
            }

            if (usedNodeIds.Count > 0)
            {
                _movePathNodeIdsByUnitId[normalizedUnitId] = usedNodeIds;
            }
        }

        private List<NodeView> BuildMovePath(Vector2Int start, Vector2Int goal)
        {
            var map = MapRenderer.Instance;
            if (map == null)
            {
                return null;
            }

            if (start == goal)
            {
                if (map.TryGetNodeViewByGrid(start, out var sameNode) && sameNode != null)
                {
                    return new List<NodeView> { sameNode };
                }

                return null;
            }

            var queue = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(start);
            visited.Add(start);

            var directions = new[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            var reached = false;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == goal)
                {
                    reached = true;
                    break;
                }

                for (var i = 0; i < directions.Length; i++)
                {
                    var next = current + directions[i];
                    if (visited.Contains(next))
                    {
                        continue;
                    }

                    var distanceFromStart = Mathf.Abs(next.x - start.x) + Mathf.Abs(next.y - start.y);
                    if (distanceFromStart > moveRange)
                    {
                        continue;
                    }

                    if (!map.TryGetNodeViewByGrid(next, out var nextNode) || nextNode == null)
                    {
                        continue;
                    }

                    if (next != goal && !map.IsNodePassableForMove(nextNode.NodeId))
                    {
                        continue;
                    }

                    visited.Add(next);
                    cameFrom[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (!reached)
            {
                return null;
            }

            var gridPath = new List<Vector2Int> { goal };
            var walker = goal;
            while (walker != start)
            {
                if (!cameFrom.TryGetValue(walker, out var parent))
                {
                    return null;
                }

                walker = parent;
                gridPath.Add(walker);
            }
            gridPath.Reverse();

            var nodePath = new List<NodeView>(gridPath.Count);
            for (var i = 0; i < gridPath.Count; i++)
            {
                if (map.TryGetNodeViewByGrid(gridPath[i], out var node) && node != null)
                {
                    nodePath.Add(node);
                }
            }

            return nodePath;
        }

        private void ClearMovePathMarkersForUnit(string unitId)
        {
            var normalizedUnitId = NormalizeToken(unitId);
            if (string.IsNullOrEmpty(normalizedUnitId))
            {
                return;
            }

            if (!_movePathNodeIdsByUnitId.TryGetValue(normalizedUnitId, out var nodeIds) || nodeIds == null)
            {
                _movePathNodeIdsByUnitId.Remove(normalizedUnitId);
                return;
            }

            var map = MapRenderer.Instance;
            if (map != null)
            {
                for (var i = 0; i < nodeIds.Count; i++)
                {
                    var nodeId = nodeIds[i];
                    if (string.IsNullOrWhiteSpace(nodeId))
                    {
                        continue;
                    }

                    if (map.TryGetNodeView(nodeId, out var node) && node != null)
                    {
                        node.ClearMovePathMarker();
                    }
                }
            }

            _movePathNodeIdsByUnitId.Remove(normalizedUnitId);
        }

        private void ClearAllMovePathMarkers()
        {
            if (_movePathNodeIdsByUnitId.Count == 0)
            {
                return;
            }

            var keys = new List<string>(_movePathNodeIdsByUnitId.Keys);
            for (var i = 0; i < keys.Count; i++)
            {
                ClearMovePathMarkersForUnit(keys[i]);
            }
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
