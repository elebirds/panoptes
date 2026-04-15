/*************************************************
 * Project: Panoptes
 * File: MapRenderer.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Map + unit runtime rendering manager.
 *************************************************/

using System.Collections.Generic;
using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Presentation.UI.Common;
using Panoptes.Presentation.UI.HUD;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class MapRenderer : MonoBehaviour
    {
        [System.Serializable]
        private sealed class MapJsonEnvelope
        {
            public MapJsonConfig map;
        }

        [System.Serializable]
        private sealed class MapJsonConfig
        {
            public string mapId;
            public int width;
            public int height;
            public string defaultTerrain = "plain";
            public MapJsonNode[] nodes;
            public MapJsonUnit[] units;
        }

        [System.Serializable]
        private sealed class MapJsonNode
        {
            public string id;
            public int x;
            public int y;
            public string terrain;
            public bool hasRoad;
            public bool has_road;
            public bool isResourcePoint;
            public bool is_resource_point;
            public string resourceType;
            public string resource_type;
            public string buildingType;
            public string building_type;
            public int buildingHp;
            public int building_hp;
            public string owner;
            public string territoryOwner;
            public string territory_owner;
        }

        [System.Serializable]
        private sealed class MapJsonUnit
        {
            public string id;
            public string faction;
            public string unitType;
            public int hp = 100;
            public int maxHp = 100;
            public int x;
            public int y;
        }

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
        [SerializeField] private bool autoFocusCameraOnMapBuild = false;
        [SerializeField] private bool autoFocusCameraOnMyBaseVehicleOnMapBuild = true;
        [SerializeField] private bool autoApplyCameraBoundsOnMapBuild = false;
        [SerializeField] private bool autoForceGameplayCameraPoseOnMapBuild = false;

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

        private const int DebugMapSize = 30;
        private const int DebugTerritorySize = 3;

        private readonly Dictionary<string, NodeView> _tileViews = new();
        private readonly Dictionary<Vector2Int, NodeView> _tileViewsByGrid = new();
        private readonly Dictionary<string, NodeDto> _nodeStates = new();

        private readonly Dictionary<string, UnitView> _unitViews = new();
        private readonly Dictionary<string, string> _unitNodeById = new();
        private readonly Dictionary<string, HashSet<string>> _unitsByNodeId = new();
        private UnitView _baseVehiclePrefabCache;

        private readonly List<UnitDto> _jsonUnits = new();
        private StaticCatalogCache _catalogCache;
        private ConfigCache _configCache;

        public IReadOnlyDictionary<string, NodeView> TileViews => _tileViews;
        public IReadOnlyDictionary<string, UnitView> UnitViews => _unitViews;
        public float TileSize => tileSize;

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
            if (UnityEngine.Object.FindAnyObjectByType<MapInputHandler>() == null)
            {
                var go = new GameObject("MapInputHandler");
                go.AddComponent<MapInputHandler>();
            }

            var cam = Camera.main;
            if (cam != null && cam.GetComponent<TopDownCameraController>() == null)
            {
                cam.gameObject.AddComponent<TopDownCameraController>();
            }

            if (UnityEngine.Object.FindAnyObjectByType<CastleHpBarOverlayController>() == null)
            {
                var go = new GameObject("CastleHpBarOverlayController");
                go.AddComponent<CastleHpBarOverlayController>();
            }

            var unitInfoPanel = UnityEngine.Object.FindAnyObjectByType<UnitInfoPanelController>();
            if (unitInfoPanel == null)
            {
                var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                var go = new GameObject(
                    "UnitInfoPanel",
                    typeof(RectTransform),
                    typeof(UnitInfoActionRegistry),
                    typeof(SettlerUnitActionRegistrar),
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
            if (unitInfoPanel.GetComponent<SettlerUnitActionRegistrar>() == null)
            {
                unitInfoPanel.gameObject.AddComponent<SettlerUnitActionRegistrar>();
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
                if (!preferLocalMapWhenBackendHasNoTerritory || HasToolSceneTerritorySnapshot(backendNodes))
                {
                    Debug.Log($"[MapRenderer] Rebuild from backend nodes: {backendNodes.Count}");
                    PrepareRuntimeRoots();
                    BuildFromNodes(backendNodes);
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
                BuildFromNodes(backendNodes);
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
            BuildFromNodes(backendNodes);
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
                !string.Equals(NormalizeToken(key), NormalizeToken(serverMapConfigKey), System.StringComparison.Ordinal))
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
            if (cache == null || string.IsNullOrWhiteSpace(serverMapConfigKey))
            {
                return false;
            }

            if (!cache.TryGetJson(serverMapConfigKey.Trim(), out var json) || string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            return LoadMapFromJsonString(json);
        }

        private bool TryLoadToolSceneMapFromStaticCatalog()
        {
            var cache = _catalogCache != null ? _catalogCache : StaticCatalogCache.Instance;
            if (cache == null)
            {
                return false;
            }

            if (!cache.TryGetDefaultMap(out var mapBundle) || mapBundle?.nodes == null || mapBundle.nodes.Length == 0)
            {
                return false;
            }

            var nodes = new List<NodeDto>(mapBundle.nodes.Length);
            for (var i = 0; i < mapBundle.nodes.Length; i++)
            {
                var node = mapBundle.nodes[i];
                if (node == null)
                {
                    continue;
                }

                var buildingType = NormalizeToken(node.building_type);
                nodes.Add(new NodeDto
                {
                    Id = string.IsNullOrWhiteSpace(node.id) ? $"N_{node.x}_{node.y}" : node.id.Trim(),
                    X = node.x,
                    Y = node.y,
                    Terrain = NormalizeToken(node.terrain),
                    HasRoad = node.has_road,
                    IsResourcePoint = node.is_resource_point,
                    ResourceType = NormalizeToken(node.resource_type),
                    Owner = NormalizeToken(node.owner),
                    TerritoryOwner = NormalizeToken(node.territory_owner),
                    BuildingType = buildingType,
                    BuildingHp = string.IsNullOrEmpty(buildingType) ? 0 : Mathf.Max(0, node.building_hp)
                });
            }

            if (nodes.Count == 0)
            {
                return false;
            }

            BuildFromNodes(nodes);
            return true;
        }

        private bool TryLoadToolSceneMapFromLocalFallback()
        {
            if (string.IsNullOrWhiteSpace(localFallbackMapResourcePath))
            {
                return false;
            }

            var asset = Resources.Load<TextAsset>(localFallbackMapResourcePath.Trim());
            if (asset == null)
            {
                return false;
            }

            return LoadMapFromJsonAsset(asset);
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
            var inputHandler = UnityEngine.Object.FindAnyObjectByType<MapInputHandler>();
            if (inputHandler != null)
            {
                inputHandler.enabled = false;
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

        private static bool HasToolSceneTerritorySnapshot(List<NodeDto> nodes)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return false;
            }

            var hasTerritory = false;
            var cityCoreCount = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(node.TerritoryOwner))
                {
                    hasTerritory = true;
                }

                if (string.Equals(NormalizeToken(node.BuildingType), "city_core", System.StringComparison.Ordinal))
                {
                    cityCoreCount++;
                }
            }

            return hasTerritory && cityCoreCount >= 4;
        }

        public bool LoadMapFromJsonString(string json)
        {
            if (!TryParseNodesFromJson(json, out var nodes))
            {
                return false;
            }

            BuildFromNodes(nodes);
            return true;
        }

        public bool LoadMapFromJsonAsset(TextAsset jsonAsset)
        {
            if (jsonAsset == null)
            {
                Debug.LogWarning("[MapRenderer] LoadMapFromJsonAsset failed: asset is null.");
                return false;
            }

            return LoadMapFromJsonString(jsonAsset.text);
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

            territoryOwner = NormalizeToken(nodeState.TerritoryOwner);
            if (string.IsNullOrEmpty(territoryOwner))
            {
                // Backward compatibility: old protocol may not send territory_owner.
                territoryOwner = NormalizeToken(nodeState.Owner);
            }
            return !string.IsNullOrEmpty(territoryOwner);
        }

        public bool IsNodeInTerritory(string nodeId, string ownerId)
        {
            if (!TryGetNodeTerritoryOwner(nodeId, out var territoryOwner))
            {
                return false;
            }

            return string.Equals(territoryOwner, NormalizeToken(ownerId), System.StringComparison.OrdinalIgnoreCase);
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

            var terrain = NormalizeToken(state.Terrain);
            return terrain != "river" && terrain != "mountain";
        }

        public bool IsNodeBuildBaseAvailable(string nodeId)
        {
            if (!TryGetNodeState(nodeId, out var state))
            {
                return false;
            }

            return string.IsNullOrEmpty(NormalizeToken(state.BuildingType));
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

            nodeView.SetBuilding(buildingType, ownerId, hp, false);

            if (_nodeStates.TryGetValue(nodeId, out var state) && state != null)
            {
                state.BuildingType = buildingType ?? string.Empty;
                state.Owner = ownerId ?? string.Empty;
                state.BuildingHp = hp;
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
                    cachedUnit.X = targetNode.GridPos.x;
                    cachedUnit.Y = targetNode.GridPos.y;
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

        public Vector3 GridToWorld(int x, int y)
        {
            return new Vector3(x * tileSize, 0f, y * tileSize);
        }

        private Vector3 GridToWorldWithTerrain(int x, int y, string terrain)
        {
            if (!applyTerrainElevation)
            {
                return GridToWorld(x, y);
            }

            var elevation = GetTerrainElevation(terrain, x, y);
            return new Vector3(x * tileSize, elevation, y * tileSize);
        }

        private float GetTerrainElevation(string terrain, int x, int y)
        {
            float value;
            switch (NormalizeToken(terrain))
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
            var nodes = CreateDebugNodes();
            BuildFromNodes(nodes);
        }

        private List<NodeDto> CreateDebugNodes()
        {
            var mapWidth = Mathf.Max(DebugMapSize, debugMapWidth);
            var mapHeight = Mathf.Max(DebugMapSize, debugMapHeight);
            var nodes = new List<NodeDto>(mapWidth * mapHeight);
            var rng = new System.Random(randomSeed);

            for (var y = 0; y < mapHeight; y++)
            {
                for (var x = 0; x < mapWidth; x++)
                {
                    var nodeId = $"N_{x}_{y}";
                    var terrain = RollTerrain(rng);

                    var node = new NodeDto
                    {
                        Id = nodeId,
                        X = x,
                        Y = y,
                        Terrain = terrain,
                        BuildingType = string.Empty,
                        BuildingHp = 0,
                        Owner = string.Empty,
                        TerritoryOwner = string.Empty,
                        HasRoad = false,
                        IsResourcePoint = false,
                        ResourceType = string.Empty
                    };

                    nodes.Add(node);
                }
            }

            if (generateDebugTerritoriesOnStart)
            {
                ApplyDebugTerritories(nodes, mapWidth, mapHeight);
            }

            if (generateDebugBuildingsOnStart)
            {
                PlaceDebugBuildings(nodes, rng);
            }
            return nodes;
        }

        private void ApplyDebugTerritories(List<NodeDto> nodes, int mapWidth, int mapHeight)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }

            var centers = GetDebugTerritoryCenters(mapWidth, mapHeight);
            var owners = GetDebugTerritoryOwners();
            var territorySizeHalf = Mathf.Max(1, DebugTerritorySize / 2);

            for (var i = 0; i < centers.Length && i < owners.Length; i++)
            {
                var center = centers[i];
                var owner = NormalizeToken(owners[i]);
                if (string.IsNullOrEmpty(owner))
                {
                    continue;
                }

                for (var y = center.y - territorySizeHalf; y <= center.y + territorySizeHalf; y++)
                {
                    for (var x = center.x - territorySizeHalf; x <= center.x + territorySizeHalf; x++)
                    {
                        if (!TryGetDebugNode(nodes, mapWidth, mapHeight, x, y, out var node) || node == null)
                        {
                            continue;
                        }

                        node.TerritoryOwner = owner;
                        node.Terrain = "plain";
                        node.HasRoad = true;
                        node.Owner = string.IsNullOrEmpty(node.Owner) ? string.Empty : node.Owner;
                        node.IsResourcePoint = false;
                        node.ResourceType = string.Empty;
                        node.BuildingType = string.Empty;
                        node.BuildingHp = 0;
                    }
                }

                if (TryGetDebugNode(nodes, mapWidth, mapHeight, center.x, center.y, out var cityCoreNode) && cityCoreNode != null)
                {
                    cityCoreNode.TerritoryOwner = owner;
                    cityCoreNode.Owner = owner;
                    cityCoreNode.BuildingType = "city_core";
                    cityCoreNode.BuildingHp = 200;
                }
            }
        }

        private static Vector2Int[] GetDebugTerritoryCenters(int mapWidth, int mapHeight)
        {
            var leftCenterX = Mathf.Clamp(5, 1, Mathf.Max(1, mapWidth - 2));
            var rightCenterX = Mathf.Clamp(mapWidth - 6, 1, Mathf.Max(1, mapWidth - 2));
            var topCenterY = Mathf.Clamp(5, 1, Mathf.Max(1, mapHeight - 2));
            var bottomCenterY = Mathf.Clamp(mapHeight - 6, 1, Mathf.Max(1, mapHeight - 2));

            return new[]
            {
                new Vector2Int(leftCenterX, topCenterY),
                new Vector2Int(rightCenterX, topCenterY),
                new Vector2Int(leftCenterX, bottomCenterY),
                new Vector2Int(rightCenterX, bottomCenterY)
            };
        }

        private static string[] GetDebugTerritoryOwners()
        {
            return new[] { "blue", "red", "green", "yellow" };
        }

        private static bool TryGetDebugNode(List<NodeDto> nodes, int mapWidth, int mapHeight, int x, int y, out NodeDto node)
        {
            node = null;
            if (nodes == null || x < 0 || y < 0 || x >= mapWidth || y >= mapHeight)
            {
                return false;
            }

            var index = y * mapWidth + x;
            if (index < 0 || index >= nodes.Count)
            {
                return false;
            }

            node = nodes[index];
            return node != null;
        }

        private string RollTerrain(System.Random rng)
        {
            var r = rng.NextDouble();
            if (r < 0.08d) return "river";
            if (r < 0.24d) return "forest";
            if (r < 0.40d) return "mountain";
            if (r < 0.54d) return "snow";
            return "plain";
        }

        private void PlaceDebugBuildings(List<NodeDto> nodes, System.Random rng)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }

            var requiredTypes = new[]
            {
                "farm",
                "lumberyard",
                "smelter",
                "engineer",
                "workshop",
                "archery",
                "barracks",
                "blacksmith",
                "tower",
                "watchtower"
            };

            var perType = Mathf.Max(1, debugBuildingsPerType);
            for (int i = 0; i < requiredTypes.Length; i++)
            {
                PlaceDebugBuildingType(nodes, rng, requiredTypes[i], perType);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || !string.IsNullOrEmpty(node.BuildingType))
                {
                    continue;
                }

                if (!IsBuildableTerrainForDebug(node.Terrain))
                {
                    continue;
                }

                if (rng.NextDouble() > debugExtraBuildingSpawnRate)
                {
                    continue;
                }

                var randomType = requiredTypes[rng.Next(requiredTypes.Length)];
                if (CanPlaceDebugBuilding(node, randomType))
                {
                    ApplyDebugBuilding(node, randomType, rng);
                }
            }
        }

        private void PlaceDebugBuildingType(List<NodeDto> nodes, System.Random rng, string buildingType, int count)
        {
            if (nodes == null || nodes.Count == 0 || count <= 0)
            {
                return;
            }

            var target = Mathf.Max(1, count);
            var placed = 0;
            var maxAttempts = Mathf.Max(nodes.Count * 4, 64);
            var attempts = 0;

            while (placed < target && attempts < maxAttempts)
            {
                attempts++;
                var node = nodes[rng.Next(nodes.Count)];
                if (!CanPlaceDebugBuilding(node, buildingType))
                {
                    continue;
                }

                ApplyDebugBuilding(node, buildingType, rng);
                placed++;
            }
        }

        private static bool CanPlaceDebugBuilding(NodeDto node, string buildingType)
        {
            if (node == null || string.IsNullOrEmpty(buildingType))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(node.BuildingType))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(node.TerritoryOwner))
            {
                return false;
            }

            return IsBuildableTerrainForDebug(node.Terrain);
        }

        private static bool IsBuildableTerrainForDebug(string terrain)
        {
            var normalized = NormalizeToken(terrain);
            return normalized != "river"
                   && normalized != "water"
                   && normalized != "forbidden"
                   && normalized != "blocked";
        }

        private static void ApplyDebugBuilding(NodeDto node, string buildingType, System.Random rng)
        {
            node.BuildingType = buildingType;
            node.BuildingHp = 100;

            if (IsResourceBuilding(buildingType))
            {
                node.IsResourcePoint = true;
                node.ResourceType = GetResourceTypeForBuilding(buildingType);
            }
            else if (!node.IsResourcePoint)
            {
                node.ResourceType = string.Empty;
            }

            node.Owner = GetDebugOwner(rng);
        }

        private static bool IsResourceBuilding(string buildingType)
        {
            switch (NormalizeToken(buildingType))
            {
                case "farm":
                case "lumberyard":
                case "smelter":
                case "mine":
                case "lumber":
                    return true;
                default:
                    return false;
            }
        }

        private static string GetResourceTypeForBuilding(string buildingType)
        {
            switch (NormalizeToken(buildingType))
            {
                case "farm":
                    return "food";
                case "lumberyard":
                case "lumber":
                    return "wood";
                case "smelter":
                case "mine":
                    return "ore";
                default:
                    return string.Empty;
            }
        }

        private static string GetDebugOwner(System.Random rng)
        {
            var owners = new[] { "blue", "red", "green", "yellow" };
            return owners[rng.Next(owners.Length)];
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
                tile.transform.localPosition = GridToWorldWithTerrain(node.X, node.Y, node.Terrain);
                tile.Bind(node);

                _tileViews[node.Id] = tile;
                _tileViewsByGrid[tile.GridPos] = tile;
                _nodeStates[node.Id] = node;
            }

            RebuildUnitsForCurrentSource();

            var focusedOnBaseVehicle = false;
            if (autoFocusCameraOnMyBaseVehicleOnMapBuild)
            {
                focusedOnBaseVehicle = TryFocusCameraOnMyBaseVehicle();
            }

            if (!focusedOnBaseVehicle && autoFocusCameraOnMapBuild)
            {
                FocusCameraToCenter();
            }
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
                    X = p.x,
                    Y = p.y,
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

            var terrain = NormalizeToken(node.Terrain);
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

            var gridPos = new Vector2Int(unit.X, unit.Y);
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

            var unitType = NormalizeToken(unit.Type);
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

            var normalized = NormalizeToken(unitType);
            for (var i = 0; i < baseVehicleUnitTypes.Length; i++)
            {
                if (string.Equals(normalized, NormalizeToken(baseVehicleUnitTypes[i]), System.StringComparison.Ordinal))
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

        private void FocusCameraToCenter()
        {
            if (_tileViews.Count == 0)
            {
                return;
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            foreach (var pair in _tileViews)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                var pos = pair.Value.transform.position;
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.z < minZ) minZ = pos.z;
                if (pos.z > maxZ) maxZ = pos.z;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            if (autoForceGameplayCameraPoseOnMapBuild)
            {
                EnsureGameplayCameraPose(cam);
            }
            var center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            var camPos = cam.transform.position;

            var cameraController = cam.GetComponent<TopDownCameraController>();
            if (autoApplyCameraBoundsOnMapBuild)
            {
                cameraController?.SetWorldBounds(minX, maxX, minZ, maxZ);
                cameraController?.SetBoundsGroundY(plainElevation);
            }

            if (cam.orthographic)
            {
                if (Mathf.Abs(cam.transform.forward.y) > 0.5f)
                {
                    cam.transform.position = new Vector3(center.x, camPos.y, center.z);
                }
                else
                {
                    cam.transform.position = new Vector3(center.x, camPos.y, center.z - 10f);
                }
            }
            else
            {
                cam.transform.position = new Vector3(center.x, camPos.y, center.z - 6f);
            }

            if (cameraController != null)
            {
                cameraController.SnapTargetToCurrentPosition();
            }
        }

        private bool TryFocusCameraOnMyBaseVehicle()
        {
            var cache = GameStateCache.Instance;
            if (cache == null || string.IsNullOrWhiteSpace(cache.MyPlayerID))
            {
                return false;
            }

            var myPlayerId = NormalizeToken(cache.MyPlayerID);
            UnitView targetUnit = null;

            foreach (var pair in _unitViews)
            {
                var unitView = pair.Value;
                if (unitView == null)
                {
                    continue;
                }

                if (!string.Equals(NormalizeToken(unitView.Faction), myPlayerId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (!IsBaseVehicleUnitType(unitView.UnitType))
                {
                    continue;
                }

                targetUnit = unitView;
                break;
            }

            if (targetUnit == null)
            {
                return false;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                return false;
            }

            if (autoForceGameplayCameraPoseOnMapBuild)
            {
                EnsureGameplayCameraPose(cam);
            }

            var targetPos = targetUnit.transform.position;
            var cameraController = cam.GetComponent<TopDownCameraController>();
            if (cameraController != null)
            {
                if (autoApplyCameraBoundsOnMapBuild && TryGetWorldBounds(out var minX, out var maxX, out var minZ, out var maxZ))
                {
                    cameraController.SetWorldBounds(minX, maxX, minZ, maxZ);
                    cameraController.SetBoundsGroundY(plainElevation);
                }

                return cameraController.FocusWorldPoint(targetPos, true);
            }

            cam.transform.position = new Vector3(targetPos.x, cam.transform.position.y, targetPos.z);
            return true;
        }

        private bool TryGetWorldBounds(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            minZ = float.MaxValue;
            maxZ = float.MinValue;

            if (_tileViews.Count == 0)
            {
                return false;
            }

            foreach (var pair in _tileViews)
            {
                var tile = pair.Value;
                if (tile == null)
                {
                    continue;
                }

                var pos = tile.transform.position;
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.z < minZ) minZ = pos.z;
                if (pos.z > maxZ) maxZ = pos.z;
            }

            return minX <= maxX && minZ <= maxZ;
        }

        private static void EnsureGameplayCameraPose(Camera cam)
        {
            if (cam == null)
            {
                return;
            }

            var forwardYAbs = Mathf.Abs(cam.transform.forward.y);
            var looksStraightForward = forwardYAbs < 0.2f;
            if (!looksStraightForward)
            {
                return;
            }

            cam.orthographic = false;
            cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, 35f, 60f);
            cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            var pos = cam.transform.position;
            if (pos.y < 2f)
            {
                cam.transform.position = new Vector3(pos.x, 3.4f, pos.z);
            }
        }

        private bool TryParseNodesFromJson(string json, out List<NodeDto> nodes)
        {
            nodes = null;
            _jsonUnits.Clear();

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[MapRenderer] Map JSON is empty.");
                return false;
            }

            MapJsonConfig config = null;

            try
            {
                config = JsonUtility.FromJson<MapJsonConfig>(json);
            }
            catch
            {
                // Fall through to envelope parse.
            }

            if (config == null || config.nodes == null || config.nodes.Length == 0)
            {
                try
                {
                    var envelope = JsonUtility.FromJson<MapJsonEnvelope>(json);
                    config = envelope?.map;
                }
                catch
                {
                    // Keep null config; handled below.
                }
            }

            if (config == null || config.nodes == null || config.nodes.Length == 0)
            {
                Debug.LogWarning("[MapRenderer] Failed to parse map JSON or JSON has no nodes.");
                return false;
            }

            nodes = BuildNodesFromJsonConfig(config);
            if (nodes.Count == 0)
            {
                Debug.LogWarning("[MapRenderer] Parsed map JSON but got 0 valid nodes.");
                return false;
            }

            BuildUnitsFromJsonConfig(config, _jsonUnits);
            Debug.Log($"[MapRenderer] Loaded map JSON: mapId='{config.mapId}', nodes={nodes.Count}, units={_jsonUnits.Count}.");
            return true;
        }

        private List<NodeDto> BuildNodesFromJsonConfig(MapJsonConfig config)
        {
            var result = new List<NodeDto>();
            if (config == null || config.nodes == null)
            {
                return result;
            }

            var width = Mathf.Max(0, config.width);
            var height = Mathf.Max(0, config.height);
            var defaultTerrain = NormalizeToken(config.defaultTerrain);
            if (string.IsNullOrEmpty(defaultTerrain))
            {
                defaultTerrain = "plain";
            }

            var nodeByKey = new Dictionary<string, MapJsonNode>();
            for (var i = 0; i < config.nodes.Length; i++)
            {
                var jsonNode = config.nodes[i];
                if (jsonNode == null)
                {
                    continue;
                }

                if (width > 0 && (jsonNode.x < 0 || jsonNode.x >= width))
                {
                    continue;
                }

                if (height > 0 && (jsonNode.y < 0 || jsonNode.y >= height))
                {
                    continue;
                }

                var key = MakeCoordKey(jsonNode.x, jsonNode.y);
                nodeByKey[key] = jsonNode;
            }

            if (autoFillMissingJsonTiles && width > 0 && height > 0)
            {
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var key = MakeCoordKey(x, y);
                        if (!nodeByKey.ContainsKey(key))
                        {
                            nodeByKey[key] = new MapJsonNode
                            {
                                x = x,
                                y = y,
                                terrain = defaultTerrain
                            };
                        }
                    }
                }
            }

            foreach (var pair in nodeByKey)
            {
                var jsonNode = pair.Value;
                var terrain = NormalizeToken(jsonNode.terrain);
                if (string.IsNullOrEmpty(terrain))
                {
                    terrain = defaultTerrain;
                }

                var buildingType = NormalizeToken(jsonNode.buildingType);
                if (string.IsNullOrEmpty(buildingType))
                {
                    buildingType = NormalizeToken(jsonNode.building_type);
                }

                var resourceType = NormalizeToken(jsonNode.resourceType);
                if (string.IsNullOrEmpty(resourceType))
                {
                    resourceType = NormalizeToken(jsonNode.resource_type);
                }

                var hasRoad = jsonNode.hasRoad || jsonNode.has_road;
                var isResourcePoint = jsonNode.isResourcePoint || jsonNode.is_resource_point || !string.IsNullOrEmpty(resourceType);
                var hasBuilding = !string.IsNullOrEmpty(buildingType);
                var buildingHp = jsonNode.buildingHp > 0 ? jsonNode.buildingHp : jsonNode.building_hp;
                var territoryOwner = NormalizeToken(jsonNode.territoryOwner);
                if (string.IsNullOrEmpty(territoryOwner))
                {
                    territoryOwner = NormalizeToken(jsonNode.territory_owner);
                }

                var node = new NodeDto
                {
                    Id = string.IsNullOrWhiteSpace(jsonNode.id) ? $"N_{jsonNode.x}_{jsonNode.y}" : jsonNode.id.Trim(),
                    X = jsonNode.x,
                    Y = jsonNode.y,
                    Terrain = terrain,
                    HasRoad = hasRoad,
                    IsResourcePoint = isResourcePoint,
                    ResourceType = isResourcePoint ? resourceType : string.Empty,
                    BuildingType = buildingType,
                    BuildingHp = hasBuilding ? (buildingHp > 0 ? buildingHp : 100) : 0,
                    Owner = (jsonNode.owner ?? string.Empty).Trim(),
                    TerritoryOwner = territoryOwner
                };

                result.Add(node);
            }

            return result;
        }

        private static void BuildUnitsFromJsonConfig(MapJsonConfig config, List<UnitDto> output)
        {
            output.Clear();
            if (config?.units == null)
            {
                return;
            }

            for (int i = 0; i < config.units.Length; i++)
            {
                var src = config.units[i];
                if (src == null)
                {
                    continue;
                }

                output.Add(new UnitDto
                {
                    Id = string.IsNullOrWhiteSpace(src.id) ? $"U_{src.x}_{src.y}_{i}" : src.id.Trim(),
                    Owner = (src.faction ?? string.Empty).Trim(),
                    Type = string.IsNullOrWhiteSpace(src.unitType) ? "infantry" : src.unitType.Trim(),
                    Hp = Mathf.Max(0, src.hp),
                    MaxHp = Mathf.Max(1, src.maxHp),
                    X = src.x,
                    Y = src.y,
                });
            }
        }

        private static string MakeCoordKey(int x, int y)
        {
            return $"{x}_{y}";
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
