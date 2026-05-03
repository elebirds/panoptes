/*************************************************
 * Project: Panoptes
 * File: MapRenderer.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Map + unit runtime rendering manager.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.UI.Common;
using Panoptes.Presentation.UI.HUD;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class MapRenderer : MonoBehaviour
    {
        public static MapRenderer Instance { get; private set; }

        [Header("Map Source")]
        [SerializeField] private bool useJsonMapOnStart = false;
        [SerializeField] private TextAsset startupMapJson;
        [SerializeField] private bool autoFillMissingJsonTiles = false;
        [SerializeField] private bool preferLocalMapWhenBackendHasNoTerritory = false;
        [SerializeField] private string localFallbackMapResourcePath = "Data/maps/default.runtime";
        [SerializeField] private bool preferServerPushedMapConfig = true;
        [SerializeField] private string serverMapConfigKey = "mapconfig";
        [SerializeField] private bool listenServerMapConfigUpdates = true;
        
        [Header("Runtime Helpers")]
        [SerializeField] private bool autoEnsureRuntimeControllers = true;

        [Header("Prefab")]
        [SerializeField] private NodeView nodeTilePrefab;
        [SerializeField] private Transform tilesRoot;
        [SerializeField] private float tileSize = 1f;
        
        [Header("Terrain Elevation")]
        [SerializeField] private bool applyTerrainElevation = false;
        [SerializeField] private float plainElevation = 0f;
        [SerializeField] private float forestElevation = 0.06f;
        [SerializeField] private float mountainElevation = 0.28f;
        [SerializeField] private float riverElevation = -0.1f;
        [SerializeField] private float snowElevation = 0.1f;
        [SerializeField] private float forbiddenElevation = 0.2f;
        [SerializeField] private bool applyElevationNoise = true;
        [SerializeField] private float elevationNoiseAmplitude = 0.02f;
        [SerializeField] private float elevationNoiseScale = 0.15f;

        [Header("Units")]
        [SerializeField] private UnitView unitPrefab;
        [SerializeField] private Transform unitsRoot;
        [SerializeField] private bool useDedicatedBaseVehiclePrefab = true;
        [SerializeField] private string baseVehiclePrefabResourcePath = "Prefabs/Units/BaseVehicle";
        [SerializeField] private string[] baseVehicleUnitTypes = { "settler", "pioneer", "expander", "engineer" };
        [SerializeField] private bool spawnDebugUnitsWhenNoUnits = false;
        [SerializeField] private string[] debugFactions = { "blue", "red", "green", "yellow" };

        [Header("Debug Generation (Local Only)")]
        [SerializeField] private bool generateDebugMapOnStart = false;
        [SerializeField] private bool generateDebugTerritoriesOnStart = false;
        [SerializeField] private bool generateDebugBuildingsOnStart = false;
        [SerializeField] private int debugMapWidth = 30;
        [SerializeField] private int debugMapHeight = 30;
        [SerializeField] private int debugBuildingsPerType = 2;
        [Range(0f, 1f)] [SerializeField] private float debugExtraBuildingSpawnRate = 0.04f;
        [SerializeField] private int randomSeed = 20260405;

        [Header("Resource Points")]
        [SerializeField] private bool autoInjectResourcePointsWhenSparse = true;
        [SerializeField] private int minimumResourcePoints = 18;

        [Header("Terrain Decorations")]
        [SerializeField] private bool autoSpawnTerrainDecorations = true;

        [Header("Map Backdrop")]
        [SerializeField] private bool autoSpawnMapBackdrop = true;

        [Header("Observation Fog")]
        [SerializeField] private bool useGlobalObservationFog = true;
        [SerializeField] private bool hideUnknownNodeDetails = true;

        private readonly Dictionary<string, NodeView> _tileViews = new();
        private readonly Dictionary<Vector2Int, NodeView> _tileViewsByGrid = new();
        private readonly Dictionary<string, NodeDto> _nodeStates = new();

        private readonly Dictionary<string, UnitView> _unitViews = new();
        private readonly Dictionary<string, string> _unitNodeById = new();
        private readonly Dictionary<string, HashSet<string>> _unitsByNodeId = new();
        private UnitView _baseVehiclePrefabCache;
        private TerrainDecorationSpawner _terrainDecorationSpawner;
        private MapBackdropSpawner _mapBackdropSpawner;
        private MapFogOverlayController _mapFogOverlayController;
        private MapCameraContext _currentCameraContext;
        private bool _hasCameraContext;

        private readonly List<UnitDto> _jsonUnits = new();
        private readonly MapCameraContextBuilder _cameraContextBuilder = new();
        private StaticCatalogCache _catalogCache;
        private ConfigCache _configCache;

        public IReadOnlyDictionary<string, NodeView> TileViews => _tileViews;
        public IReadOnlyDictionary<string, UnitView> UnitViews => _unitViews;
        public float TileSize => tileSize;
        public event Action<MapCameraContext> CameraContextReady;

        public bool TryGetCameraContext(out MapCameraContext context)
        {
            context = _currentCameraContext;
            return _hasCameraContext && context.IsValid;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            if (IsGameRuntime())
            {
                return;
            }

            SubscribeServerMapConfig();
        }

        private void OnDisable()
        {
            UnsubscribeServerMapConfig();
        }

        private void Start()
        {
            if (!ShouldBuildOnStart())
            {
                return;
            }

            if (generateDebugMapOnStart)
            {
                if (autoEnsureRuntimeControllers)
                {
                    EnsureRuntimeControllers();
                }

                BuildDebugMap();
                return;
            }

            if (IsGameRuntime())
            {
                if (!BuildBackendGameMap())
                {
                    return;
                }

                if (autoEnsureRuntimeControllers)
                {
                    EnsureRuntimeControllers();
                }
                return;
            }

            if (autoEnsureRuntimeControllers)
            {
                EnsureRuntimeControllers();
            }

            if (useJsonMapOnStart && startupMapJson != null && LoadMapFromJsonAsset(startupMapJson))
            {
                return;
            }

            RebuildMap();
        }

        private static bool ShouldBuildOnStart()
        {
            var cache = GameStateCache.Instance;
            if (cache != null && cache.Nodes != null && cache.Nodes.Count > 0)
            {
                return true;
            }

            var app = AppManager.Instance;
            if (app != null && app.State != AppState.Game)
            {
                return false;
            }

            return true;
        }

        private static void EnsureRuntimeControllers()
        {
            if (SceneObjectFinder.FindFirstSceneObject<MapPlanningInputController>() == null)
            {
                var go = new GameObject("MapPlanningInputController");
                go.AddComponent<MapPlanningInputController>();
            }

            SettlementPlaybackController.EnsureInstance();

            var cameraAnchor = GameObject.Find("CameraAnchor");
            if (cameraAnchor == null)
            {
                cameraAnchor = new GameObject("CameraAnchor");
            }

            if (cameraAnchor.GetComponent<CinemachineMapCameraController>() == null)
            {
                cameraAnchor.AddComponent<CinemachineMapCameraController>();
            }

            if (UnityEngine.Object.FindAnyObjectByType<CityCoreHpBarOverlayController>() == null)
            {
                var go = new GameObject("CityCoreHpBarOverlayController");
                go.AddComponent<CityCoreHpBarOverlayController>();
            }

            if (UnityEngine.Object.FindAnyObjectByType<BuildingConstructionOverlayController>() == null)
            {
                var go = new GameObject("BuildingConstructionOverlayController");
                go.AddComponent<BuildingConstructionOverlayController>();
            }

            var unitInfoPanel = UnityEngine.Object.FindAnyObjectByType<UnitInfoPanelController>();
            if (unitInfoPanel == null)
            {
                var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                var go = new GameObject(
                    "UnitInfoPanel",
                    typeof(RectTransform),
                    typeof(UnitInfoActionRegistry),
                    typeof(UnitInfoPanelController));
                if (canvas != null)
                {
                    go.transform.SetParent(canvas.transform, false);
                }
                return;
            }

            if (unitInfoPanel.GetComponent<UnitInfoActionRegistry>() == null)
            {
                unitInfoPanel.gameObject.AddComponent<UnitInfoActionRegistry>();
            }
        }

        public void RebuildMap()
        {
            if (GameStateCache.Instance == null)
            {
                Debug.LogError("[MapRenderer] Cannot build map: GameStateCache is missing.");
                return;
            }

            if (IsGameRuntime())
            {
                BuildBackendGameMap();
                return;
            }

            if (GameStateCache.Instance.Nodes != null && GameStateCache.Instance.Nodes.Count > 0)
            {
                var backendNodes = new List<NodeDto>(GameStateCache.Instance.Nodes.Values);
                if (!preferLocalMapWhenBackendHasNoTerritory || MapSourceResolver.HasTerritorySnapshot(backendNodes))
                {
                    Debug.Log($"[MapRenderer] Rebuild from backend nodes: {backendNodes.Count}");
                    PrepareRuntimeRoots();
                    BuildFromBackendNodes(backendNodes);
                    return;
                }
            }

            if (TryLoadToolSceneConfiguredMap())
            {
                return;
            }

            if (GameStateCache.Instance.Nodes != null && GameStateCache.Instance.Nodes.Count > 0)
            {
                var backendNodes = new List<NodeDto>(GameStateCache.Instance.Nodes.Values);
                Debug.LogWarning($"[MapRenderer] Using backend nodes despite incomplete territory snapshot: {backendNodes.Count}");
                PrepareRuntimeRoots();
                BuildFromBackendNodes(backendNodes);
                return;
            }

            Debug.LogError("[MapRenderer] Cannot build map: no backend nodes or fallback map available.");
        }

        private bool BuildBackendGameMap()
        {
            var cache = GameStateCache.Instance;
            if (cache == null)
            {
                ReportBackendGameMapFailure("GameStateCache 未就绪，无法渲染服务端地图。");
                return false;
            }

            if (cache.Nodes == null || cache.Nodes.Count == 0)
            {
                ReportBackendGameMapFailure("服务端未下发地图节点，无法进入对局。");
                return false;
            }

            var backendNodes = new List<NodeDto>(cache.Nodes.Values);
            Debug.Log($"[MapRenderer] Rebuild game map from backend nodes: {backendNodes.Count}");
            BuildFromBackendNodes(backendNodes);
            return true;
        }

        private void SubscribeServerMapConfig()
        {
            if (!listenServerMapConfigUpdates)
            {
                return;
            }

            _catalogCache = StaticCatalogCache.EnsureInstance();
            if (_catalogCache != null)
            {
                _catalogCache.CatalogChanged += OnServerMapCatalogChanged;
            }

            _configCache = ConfigCache.EnsureInstance();
            if (_configCache != null)
            {
                _configCache.ConfigUpdated += OnServerMapConfigUpdated;
            }
        }

        private void UnsubscribeServerMapConfig()
        {
            if (_catalogCache != null)
            {
                _catalogCache.CatalogChanged -= OnServerMapCatalogChanged;
                _catalogCache = null;
            }

            if (_configCache != null)
            {
                _configCache.ConfigUpdated -= OnServerMapConfigUpdated;
                _configCache = null;
            }
        }

        private void OnServerMapCatalogChanged()
        {
            if (IsGameRuntime())
            {
                return;
            }

            if (GameStateCache.Instance != null && GameStateCache.Instance.Nodes.Count > 0)
            {
                return;
            }

            RebuildMap();
        }

        private void OnServerMapConfigUpdated(string key)
        {
            if (IsGameRuntime())
            {
                return;
            }

            if (!listenServerMapConfigUpdates ||
                !string.Equals(MapRenderTokens.Normalize(key), MapRenderTokens.Normalize(serverMapConfigKey), System.StringComparison.Ordinal))
            {
                return;
            }

            if (GameStateCache.Instance != null && GameStateCache.Instance.Nodes.Count > 0)
            {
                return;
            }

            RebuildMap();
        }

        private bool TryLoadToolSceneConfiguredMap()
        {
            if (preferServerPushedMapConfig && TryLoadToolSceneMapFromServerConfig())
            {
                return true;
            }

            if (TryLoadToolSceneMapFromStaticCatalog())
            {
                return true;
            }

            return TryLoadToolSceneMapFromLocalFallback();
        }

        private bool TryLoadToolSceneMapFromServerConfig()
        {
            var cache = _configCache != null ? _configCache : ConfigCache.Instance;
            return CreateSourceResolver().TryResolveServerConfig(cache, out var snapshot) &&
                   BuildFromSourceSnapshot(snapshot);
        }

        private bool TryLoadToolSceneMapFromStaticCatalog()
        {
            var cache = _catalogCache != null ? _catalogCache : StaticCatalogCache.Instance;
            return CreateSourceResolver().TryResolveStaticCatalog(cache, out var snapshot) &&
                   BuildFromSourceSnapshot(snapshot);
        }

        private bool TryLoadToolSceneMapFromLocalFallback()
        {
            return CreateSourceResolver().TryResolveLocalFallback(out var snapshot) &&
                   BuildFromSourceSnapshot(snapshot);
        }

        private void ReportBackendGameMapFailure(string message)
        {
            ClearMap();
            ClearUnits();
            DisableGameplayInput();

            var resolvedMessage = string.IsNullOrWhiteSpace(message)
                ? "服务端地图加载失败。"
                : message.Trim();
            Debug.LogError($"[MapRenderer] {resolvedMessage}");

            if (ErrorToast.Instance != null)
            {
                ErrorToast.Instance.Show(resolvedMessage, false);
            }
        }

        private static void DisableGameplayInput()
        {
            var planningInputController = SceneObjectFinder.FindFirstSceneObject<MapPlanningInputController>();
            if (planningInputController != null)
            {
                planningInputController.enabled = false;
            }

            var unitInfoPanel = UnityEngine.Object.FindAnyObjectByType<UnitInfoPanelController>();
            if (unitInfoPanel != null)
            {
                unitInfoPanel.gameObject.SetActive(false);
            }
        }

        private static bool IsGameRuntime()
        {
            return AppManager.Instance != null && AppManager.Instance.State == AppState.Game;
        }

        public bool LoadMapFromJsonString(string json)
        {
            return CreateSourceResolver().TryParseJsonString(json, out var snapshot) &&
                   BuildFromSourceSnapshot(snapshot);
        }

        public bool LoadMapFromJsonAsset(TextAsset jsonAsset)
        {
            return CreateSourceResolver().TryParseJsonAsset(jsonAsset, out var snapshot) &&
                   BuildFromSourceSnapshot(snapshot);
        }

        private MapSourceResolver CreateSourceResolver()
        {
            return new MapSourceResolver(new MapSourceResolver.Options
            {
                AutoFillMissingJsonTiles = autoFillMissingJsonTiles,
                LocalFallbackMapResourcePath = localFallbackMapResourcePath,
                ServerMapConfigKey = serverMapConfigKey
            });
        }

        private bool BuildFromSourceSnapshot(MapSourceSnapshot snapshot)
        {
            if (!snapshot.HasNodes)
            {
                return false;
            }

            _jsonUnits.Clear();
            if (snapshot.Units != null && snapshot.Units.Count > 0)
            {
                _jsonUnits.AddRange(snapshot.Units);
            }

            BuildFromNodes(snapshot.Nodes);
            return true;
        }

        private void BuildFromBackendNodes(List<NodeDto> nodes)
        {
            _jsonUnits.Clear();
            BuildFromNodes(nodes);
        }

        public void RefreshNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            if (!_tileViews.TryGetValue(nodeId, out var view) || view == null)
            {
                return;
            }

            if (_nodeStates.TryGetValue(nodeId, out var node))
            {
                view.Bind(node);
            }
        }

        public bool ApplyNodeSnapshot(NodeDto node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id))
            {
                return false;
            }

            _nodeStates[node.Id] = node;

            if (_tileViews.TryGetValue(node.Id, out var view) && view != null)
            {
                view.Bind(node);
            }

            RefreshObservationPresentation(fullRebuildFog: false, snapshotNode: node);

            return true;
        }

        public bool TryGetNodeView(string nodeId, out NodeView nodeView)
        {
            return _tileViews.TryGetValue(nodeId, out nodeView) && nodeView != null;
        }

        public bool TryGetNodeViewByGrid(Vector2Int gridPos, out NodeView nodeView)
        {
            return _tileViewsByGrid.TryGetValue(gridPos, out nodeView) && nodeView != null;
        }

        public bool TryGetNodeState(string nodeId, out NodeDto nodeState)
        {
            return _nodeStates.TryGetValue(nodeId, out nodeState) && nodeState != null;
        }

        public bool TryGetNodeTerritoryOwner(string nodeId, out string territoryOwner)
        {
            territoryOwner = string.Empty;
            if (!TryGetNodeState(nodeId, out var nodeState))
            {
                return false;
            }

            territoryOwner = MapRenderTokens.Normalize(nodeState.TerritoryOwner);
            if (string.IsNullOrEmpty(territoryOwner))
            {
                // Backward compatibility: old protocol may not send territory_owner.
                territoryOwner = MapRenderTokens.Normalize(nodeState.Owner);
            }
            return !string.IsNullOrEmpty(territoryOwner);
        }

        public bool IsNodeInTerritory(string nodeId, string ownerId)
        {
            if (!TryGetNodeTerritoryOwner(nodeId, out var territoryOwner))
            {
                return false;
            }

            return string.Equals(territoryOwner, MapRenderTokens.Normalize(ownerId), System.StringComparison.OrdinalIgnoreCase);
        }

        public bool IsNodeInSafeZone(string nodeId)
        {
            return TryGetNodeState(nodeId, out var nodeState) && nodeState.IsSafeZone;
        }

        public bool TryGetNodeIdByGrid(Vector2Int gridPos, out string nodeId)
        {
            nodeId = string.Empty;
            if (!_tileViewsByGrid.TryGetValue(gridPos, out var nodeView) || nodeView == null)
            {
                return false;
            }

            nodeId = nodeView.NodeId;
            return !string.IsNullOrEmpty(nodeId);
        }

        public bool TryGetUnitView(string unitId, out UnitView unitView)
        {
            return _unitViews.TryGetValue(unitId, out unitView) && unitView != null;
        }

        public bool TryGetUnitsOnNode(string nodeId, List<UnitView> units)
        {
            if (units == null)
            {
                return false;
            }

            units.Clear();
            if (string.IsNullOrWhiteSpace(nodeId) || !_unitsByNodeId.TryGetValue(nodeId, out var unitIds) || unitIds == null)
            {
                return false;
            }

            foreach (var unitId in unitIds)
            {
                if (string.IsNullOrWhiteSpace(unitId))
                {
                    continue;
                }

                if (_unitViews.TryGetValue(unitId, out var unitView) && unitView != null)
                {
                    units.Add(unitView);
                }
            }

            return units.Count > 0;
        }

        public bool TrySpawnRuntimeUnit(UnitDto unit, bool replaceIfExists = false, bool updateCache = true)
        {
            return TrySpawnUnitInternal(unit, replaceIfExists, updateCache);
        }

        public bool RemoveRuntimeUnit(string unitId, bool updateCache = true)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return false;
            }

            if (!_unitViews.TryGetValue(unitId, out var unitView) || unitView == null)
            {
                return false;
            }

            if (_unitNodeById.TryGetValue(unitId, out var oldNodeId) &&
                _unitsByNodeId.TryGetValue(oldNodeId, out var oldSet))
            {
                oldSet.Remove(unitId);
            }

            if (UnitCache.Instance != null)
            {
                UnitCache.Instance.Unregister(unitView);
            }

            Destroy(unitView.gameObject);
            _unitViews.Remove(unitId);
            _unitNodeById.Remove(unitId);

            if (updateCache && GameStateCache.Instance != null)
            {
                GameStateCache.Instance.RemoveRuntimeUnit(unitId);
            }

            return true;
        }

        public bool IsNodeResourcePoint(string nodeId)
        {
            return TryGetNodeState(nodeId, out var state) && state.IsResourcePoint;
        }

        public bool IsNodePassableForMove(string nodeId)
        {
            if (!TryGetNodeState(nodeId, out var state))
            {
                return false;
            }

            var terrain = MapRenderTokens.Normalize(state.Terrain);
            return terrain != "river" && terrain != "mountain";
        }

        public bool IsNodeBuildBaseAvailable(string nodeId)
        {
            if (!TryGetNodeState(nodeId, out var state))
            {
                return false;
            }

            return string.IsNullOrEmpty(MapRenderTokens.Normalize(state.BuildingType));
        }

        public bool ApplyBuildingPlacement(string nodeId, string buildingType, string ownerId, bool isGhost, int hp = 100, Color? ghostColor = null)
        {
            if (!TryGetNodeView(nodeId, out var nodeView))
            {
                return false;
            }

            if (isGhost)
            {
                nodeView.SetBuildingGhost(buildingType, ownerId, ghostColor ?? new Color(0.6f, 1f, 0.6f, 0.9f));
                return true;
            }

            var maxHp = MapRenderTokens.ResolveBuildingMaxHp(buildingType, hp);
            nodeView.SetBuilding(buildingType, ownerId, hp, maxHp, false);

            if (_nodeStates.TryGetValue(nodeId, out var state) && state != null)
            {
                state.BuildingType = buildingType ?? string.Empty;
                state.Owner = ownerId ?? string.Empty;
                state.BuildingHp = hp;
                state.BuildingMaxHp = maxHp;
            }

            return true;
        }

        public void SetUnitNode(string unitId, string targetNodeId)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            if (!_unitViews.TryGetValue(unitId, out var unitView) || unitView == null)
            {
                return;
            }

            if (!TryGetNodeView(targetNodeId, out var targetNode))
            {
                return;
            }

            if (_unitNodeById.TryGetValue(unitId, out var oldNodeId))
            {
                if (_unitsByNodeId.TryGetValue(oldNodeId, out var unitsAtOld))
                {
                    unitsAtOld.Remove(unitId);
                }
            }

            if (!_unitsByNodeId.TryGetValue(targetNodeId, out var unitsAtNew))
            {
                unitsAtNew = new HashSet<string>();
                _unitsByNodeId[targetNodeId] = unitsAtNew;
            }
            unitsAtNew.Add(unitId);
            _unitNodeById[unitId] = targetNodeId;

            unitView.SetGridPosition(targetNode.GridPos);
            if (targetNode.UnitAnchor != null)
            {
                unitView.transform.position = targetNode.UnitAnchor.position;
            }

            if (GameStateCache.Instance != null)
            {
                var cachedUnit = GameStateCache.Instance.GetUnit(unitId);
                if (cachedUnit != null)
                {
                    cachedUnit.Q = targetNode.GridPos.x;
                    cachedUnit.R = targetNode.GridPos.y;
                }
            }
        }

        public bool TryGetGridBounds(out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            minY = int.MaxValue;
            maxY = int.MinValue;

            if (_tileViewsByGrid.Count == 0)
            {
                return false;
            }

            foreach (var pair in _tileViewsByGrid)
            {
                var pos = pair.Key;
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
            }

            return true;
        }

        public Vector3 GridToWorld(int q, int r)
        {
            return HexGrid.AxialToWorld(q, r, tileSize);
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            var localPosition = tilesRoot != null
                ? tilesRoot.InverseTransformPoint(worldPosition)
                : transform.InverseTransformPoint(worldPosition);
            return HexGrid.WorldToAxial(localPosition, tileSize);
        }

        public bool TryGetNodeViewByWorld(Vector3 worldPosition, out NodeView nodeView)
        {
            return TryGetNodeViewByGrid(WorldToGrid(worldPosition), out nodeView);
        }

        private Vector3 GridToWorldWithTerrain(int q, int r, string terrain)
        {
            if (!applyTerrainElevation)
            {
                return GridToWorld(q, r);
            }

            var basePosition = GridToWorld(q, r);
            var elevation = GetTerrainElevation(terrain, q, r);
            return new Vector3(basePosition.x, elevation, basePosition.z);
        }

        private float GetTerrainElevation(string terrain, int x, int y)
        {
            float value;
            switch (MapRenderTokens.Normalize(terrain))
            {
                case "forest":
                    value = forestElevation;
                    break;
                case "mountain":
                    value = mountainElevation;
                    break;
                case "river":
                case "water":
                    value = riverElevation;
                    break;
                case "snow":
                    value = snowElevation;
                    break;
                case "forbidden":
                case "blocked":
                    value = forbiddenElevation;
                    break;
                default:
                    value = plainElevation;
                    break;
            }

            if (!applyElevationNoise || elevationNoiseAmplitude <= 0.0001f)
            {
                return value;
            }

            var nx = (x + randomSeed * 0.137f) * Mathf.Max(0.0001f, elevationNoiseScale);
            var ny = (y - randomSeed * 0.173f) * Mathf.Max(0.0001f, elevationNoiseScale);
            var noise = Mathf.PerlinNoise(nx, ny) * 2f - 1f;
            return value + noise * elevationNoiseAmplitude;
        }

        private void BuildDebugMap()
        {
            _jsonUnits.Clear();
            var nodes = DebugMapFactory.CreateNodes(new DebugMapFactory.Options
            {
                Width = debugMapWidth,
                Height = debugMapHeight,
                Seed = randomSeed,
                GenerateTerritories = generateDebugTerritoriesOnStart,
                GenerateBuildings = generateDebugBuildingsOnStart,
                BuildingsPerType = debugBuildingsPerType,
                ExtraBuildingSpawnRate = debugExtraBuildingSpawnRate
            });
            BuildFromNodes(nodes);
        }

        private void BuildFromNodes(IEnumerable<NodeDto> nodes)
        {
            if (nodeTilePrefab == null)
            {
                Debug.LogError("[MapRenderer] NodeTile prefab is not assigned.");
                return;
            }

            PrepareRuntimeRoots();
            ClearMap();

            var nodeList = new List<NodeDto>();
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    if (node != null)
                    {
                        nodeList.Add(node);
                    }
                }
            }

            EnsureMinimumResourcePoints(nodeList);

            foreach (var node in nodeList)
            {
                if (string.IsNullOrEmpty(node.Id))
                {
                    continue;
                }

                var tile = Instantiate(nodeTilePrefab, EnsureTilesRoot(), false);
                tile.transform.localPosition = GridToWorldWithTerrain(node.Q, node.R, node.Terrain);
                tile.Bind(node);

                _tileViews[node.Id] = tile;
                _tileViewsByGrid[tile.GridPos] = tile;
                _nodeStates[node.Id] = node;
            }

            RebuildTerrainDecorations(nodeList);
            RebuildMapBackdrop();
            RefreshObservationPresentation(fullRebuildFog: true, snapshotNode: null);

            RebuildUnitsForCurrentSource();
            PublishCameraContext();
        }

        private void PublishCameraContext()
        {
            if (!TryBuildCameraContext(out var context))
            {
                _hasCameraContext = false;
                _currentCameraContext = default;
                return;
            }

            _currentCameraContext = context;
            _hasCameraContext = true;
            CameraContextReady?.Invoke(context);
        }

        private bool TryBuildCameraContext(out MapCameraContext context)
        {
            return _cameraContextBuilder.TryBuild(
                _nodeStates,
                _tileViews,
                _unitViews,
                tileSize,
                plainElevation,
                IsBaseVehicleUnitType,
                out context);
        }

        private void RebuildTerrainDecorations(List<NodeDto> nodeList)
        {
            if (!autoSpawnTerrainDecorations)
            {
                return;
            }

            if (_terrainDecorationSpawner == null)
            {
                _terrainDecorationSpawner = GetComponent<TerrainDecorationSpawner>();
            }

            if (_terrainDecorationSpawner == null)
            {
                _terrainDecorationSpawner = gameObject.AddComponent<TerrainDecorationSpawner>();
            }

            _terrainDecorationSpawner.RebuildDecorations(nodeList, _tileViews);
        }

        private void RefreshObservationPresentation(bool fullRebuildFog, NodeDto snapshotNode)
        {
            var hasObservationData = HasObservationData(_nodeStates.Values);
            var hideUnknownNodeDetailsEffective = hideUnknownNodeDetails && hasObservationData;
            var hideUnknownGroundEffective = false;

            if (useGlobalObservationFog)
            {
                if (_mapFogOverlayController == null)
                {
                    _mapFogOverlayController = GetComponent<MapFogOverlayController>();
                }

                if (_mapFogOverlayController == null)
                {
                    _mapFogOverlayController = gameObject.AddComponent<MapFogOverlayController>();
                }

                _mapFogOverlayController.ConfigureUnknownCulling(
                    hideUnknownNodeDetailsEffective,
                    hideUnknownGroundEffective);

                if (fullRebuildFog)
                {
                    _mapFogOverlayController.Rebuild(_tileViews, _nodeStates, tileSize);
                }
                else if (snapshotNode != null)
                {
                    _mapFogOverlayController.ApplyNodeSnapshot(snapshotNode);
                }
            }
            else
            {
                if (_mapFogOverlayController == null)
                {
                    _mapFogOverlayController = GetComponent<MapFogOverlayController>();
                }

                if (_mapFogOverlayController != null)
                {
                    _mapFogOverlayController.ClearOverlay();
                }

                foreach (var pair in _tileViews)
                {
                    var tile = pair.Value;
                    if (tile == null)
                    {
                        continue;
                    }

                    tile.SetPerTileObservationFogEnabled(true);
                    tile.SetUnknownDetailCulling(false, false);
                }
            }

            if (_terrainDecorationSpawner != null)
            {
                _terrainDecorationSpawner.ApplyObservationState(_nodeStates, hideUnknownNodeDetailsEffective);
            }
        }

        private static bool HasObservationData(IEnumerable<NodeDto> nodes)
        {
            if (nodes == null)
            {
                return false;
            }

            foreach (var node in nodes)
            {
                if (node != null && (node.IsVisible || node.IsMemory || node.LastObservedTurn > 0))
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildMapBackdrop()
        {
            if (!autoSpawnMapBackdrop)
            {
                return;
            }

            if (_mapBackdropSpawner == null)
            {
                _mapBackdropSpawner = GetComponent<MapBackdropSpawner>();
            }

            if (_mapBackdropSpawner == null)
            {
                _mapBackdropSpawner = gameObject.AddComponent<MapBackdropSpawner>();
            }

            _mapBackdropSpawner.RebuildBackdrop(_tileViews);
        }

        private void PrepareRuntimeRoots()
        {
            var tileRoot = EnsureTilesRoot();
            if (tileRoot != null)
            {
                tileRoot.position = Vector3.zero;
                tileRoot.rotation = Quaternion.identity;
                tileRoot.localScale = Vector3.one;
            }

            var unitRoot = EnsureUnitsRoot();
            if (unitRoot != null)
            {
                unitRoot.position = Vector3.zero;
                unitRoot.rotation = Quaternion.identity;
                unitRoot.localScale = Vector3.one;
            }
        }

        private void RebuildUnitsForCurrentSource()
        {
            ClearUnits();

            if (_jsonUnits.Count > 0)
            {
                BuildUnitsFromState(_jsonUnits);
                return;
            }

            if (!generateDebugMapOnStart && GameStateCache.Instance != null && GameStateCache.Instance.Units.Count > 0)
            {
                BuildUnitsFromState(GameStateCache.Instance.Units.Values);
                return;
            }

            if (spawnDebugUnitsWhenNoUnits)
            {
                BuildUnitsFromState(CreateDebugUnits());
            }
        }

        private List<UnitDto> CreateDebugUnits()
        {
            var result = new List<UnitDto>();
            if (!TryGetGridBounds(out var minX, out var maxX, out var minY, out var maxY))
            {
                return result;
            }

            var corners = new[]
            {
                new Vector2Int(minX + 1, minY + 1),
                new Vector2Int(maxX - 1, minY + 1),
                new Vector2Int(minX + 1, maxY - 1),
                new Vector2Int(maxX - 1, maxY - 1)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                var p = corners[i];
                if (!TryGetNodeViewByGrid(p, out _))
                {
                    continue;
                }

                var faction = (debugFactions != null && i < debugFactions.Length)
                    ? debugFactions[i]
                    : $"faction_{i + 1}";

                result.Add(new UnitDto
                {
                    Id = $"U_DEBUG_{i + 1}",
                    Owner = faction,
                    Type = "infantry",
                    Hp = 100,
                    MaxHp = 100,
                    Q = p.x,
                    R = p.y,
                });
            }

            return result;
        }

        private void BuildUnitsFromState(IEnumerable<UnitDto> units)
        {
            if (units == null)
            {
                return;
            }

            var unitCache = UnitCache.Instance;
            if (unitCache == null)
            {
                var cacheGo = new GameObject("UnitCache");
                unitCache = cacheGo.AddComponent<UnitCache>();
            }

            foreach (var unit in units)
            {
                if (unit == null)
                {
                    continue;
                }

                TrySpawnUnitInternal(unit, false, false, unitCache);
            }
        }

        private void EnsureMinimumResourcePoints(List<NodeDto> nodes)
        {
            if (!autoInjectResourcePointsWhenSparse || nodes == null || nodes.Count == 0)
            {
                return;
            }

            var target = Mathf.Max(0, minimumResourcePoints);
            if (target <= 0)
            {
                return;
            }

            var current = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node != null && node.IsResourcePoint)
                {
                    current++;
                }
            }

            if (current >= target)
            {
                return;
            }

            var outsideTerritory = new List<NodeDto>();
            var insideTerritory = new List<NodeDto>();

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!CanInjectResourcePoint(node))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.TerritoryOwner))
                {
                    outsideTerritory.Add(node);
                }
                else
                {
                    insideTerritory.Add(node);
                }
            }

            var rng = new System.Random(randomSeed ^ (nodes.Count * 31 + current * 131));
            var injected = 0;
            while (current + injected < target)
            {
                var selected = TakeRandomNode(outsideTerritory, rng);
                if (selected == null)
                {
                    selected = TakeRandomNode(insideTerritory, rng);
                }

                if (selected == null)
                {
                    break;
                }

                selected.IsResourcePoint = true;
                selected.ResourceType = ResolveInjectedResourceType(current + injected);
                injected++;
            }

            if (injected > 0)
            {
                Debug.Log($"[MapRenderer] Injected {injected} resource points (current={current + injected}, target={target}).");
            }
        }

        private static bool CanInjectResourcePoint(NodeDto node)
        {
            if (node == null)
            {
                return false;
            }

            if (node.IsResourcePoint)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(node.BuildingType))
            {
                return false;
            }

            var terrain = MapRenderTokens.Normalize(node.Terrain);
            return terrain != "river"
                   && terrain != "water"
                   && terrain != "forbidden"
                   && terrain != "blocked";
        }

        private static NodeDto TakeRandomNode(List<NodeDto> list, System.Random rng)
        {
            if (list == null || list.Count == 0 || rng == null)
            {
                return null;
            }

            var index = rng.Next(list.Count);
            var node = list[index];
            list.RemoveAt(index);
            return node;
        }

        private static string ResolveInjectedResourceType(int index)
        {
            switch (index % 3)
            {
                case 1:
                    return "wood";
                case 2:
                    return "ore";
                default:
                    return "food";
            }
        }

        private bool TrySpawnUnitInternal(UnitDto unit, bool replaceIfExists, bool updateCache, UnitCache unitCacheOverride = null)
        {
            if (unit == null || string.IsNullOrEmpty(unit.Id))
            {
                return false;
            }

            if (_unitViews.ContainsKey(unit.Id))
            {
                if (!replaceIfExists)
                {
                    return false;
                }

                RemoveRuntimeUnit(unit.Id, updateCache);
            }

            var gridPos = new Vector2Int(unit.Q, unit.R);
            if (!TryGetNodeViewByGrid(gridPos, out var nodeView) || nodeView == null)
            {
                return false;
            }

            var instance = CreateUnitInstance(unit);
            instance.transform.SetParent(EnsureUnitsRoot(), false);
            var worldPos = nodeView.UnitAnchor != null
                ? nodeView.UnitAnchor.position
                : nodeView.transform.position + Vector3.up * 0.2f;
            instance.Bind(unit, worldPos);

            _unitViews[unit.Id] = instance;
            _unitNodeById[unit.Id] = nodeView.NodeId;

            if (!_unitsByNodeId.TryGetValue(nodeView.NodeId, out var set))
            {
                set = new HashSet<string>();
                _unitsByNodeId[nodeView.NodeId] = set;
            }
            set.Add(unit.Id);

            var unitCache = unitCacheOverride != null ? unitCacheOverride : UnitCache.Instance;
            if (unitCache == null)
            {
                var cacheGo = new GameObject("UnitCache");
                unitCache = cacheGo.AddComponent<UnitCache>();
            }
            unitCache.Register(instance);

            if (updateCache && GameStateCache.Instance != null)
            {
                GameStateCache.Instance.UpsertRuntimeUnit(unit);
            }

            return true;
        }

        private UnitView CreateUnitInstance(UnitDto unit)
        {
            var dedicatedPrefab = ResolveDedicatedUnitPrefab(unit);
            if (dedicatedPrefab != null)
            {
                return Instantiate(dedicatedPrefab);
            }

            if (unitPrefab != null)
            {
                return Instantiate(unitPrefab);
            }

            // Fallback: build a runtime capsule unit if prefab is not assigned yet.
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Unit_Runtime";
            go.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            var view = go.GetComponent<UnitView>();
            if (view == null)
            {
                view = go.AddComponent<UnitView>();
            }
            return view;
        }

        private UnitView ResolveDedicatedUnitPrefab(UnitDto unit)
        {
            if (!useDedicatedBaseVehiclePrefab || unit == null)
            {
                return null;
            }

            var unitType = MapRenderTokens.Normalize(unit.Type);
            if (!IsBaseVehicleUnitType(unitType))
            {
                return null;
            }

            if (_baseVehiclePrefabCache == null && !string.IsNullOrWhiteSpace(baseVehiclePrefabResourcePath))
            {
                _baseVehiclePrefabCache = Resources.Load<UnitView>(baseVehiclePrefabResourcePath.Trim());
            }

            return _baseVehiclePrefabCache;
        }

        private bool IsBaseVehicleUnitType(string unitType)
        {
            if (string.IsNullOrWhiteSpace(unitType) || baseVehicleUnitTypes == null || baseVehicleUnitTypes.Length == 0)
            {
                return false;
            }

            var normalized = MapRenderTokens.Normalize(unitType);
            for (var i = 0; i < baseVehicleUnitTypes.Length; i++)
            {
                if (string.Equals(normalized, MapRenderTokens.Normalize(baseVehicleUnitTypes[i]), System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private Transform EnsureTilesRoot()
        {
            if (tilesRoot != null)
            {
                return tilesRoot;
            }

            var root = new GameObject("TilesRoot");
            root.transform.SetParent(transform, false);
            tilesRoot = root.transform;
            return tilesRoot;
        }

        private Transform EnsureUnitsRoot()
        {
            if (unitsRoot != null)
            {
                return unitsRoot;
            }

            var root = new GameObject("UnitsRoot");
            root.transform.SetParent(transform, false);
            unitsRoot = root.transform;
            return unitsRoot;
        }

        private void ClearMap()
        {
            foreach (var pair in _tileViews)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            _tileViews.Clear();
            _tileViewsByGrid.Clear();
            _nodeStates.Clear();
            if (_mapFogOverlayController != null)
            {
                _mapFogOverlayController.ClearOverlay();
            }
        }

        private void ClearUnits()
        {
            foreach (var pair in _unitViews)
            {
                if (pair.Value != null)
                {
                    if (UnitCache.Instance != null)
                    {
                        UnitCache.Instance.Unregister(pair.Value);
                    }

                    Destroy(pair.Value.gameObject);
                }
            }

            _unitViews.Clear();
            _unitNodeById.Clear();
            _unitsByNodeId.Clear();
        }
    }
}
