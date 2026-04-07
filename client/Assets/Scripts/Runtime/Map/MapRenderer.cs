/*************************************************
 * Project: Panoptes
 * File: MapRenderer.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Map + unit runtime rendering manager.
 *************************************************/

using System.Collections.Generic;
using Panoptes.Runtime.App;
using Panoptes.Runtime.Cache;
using UnityEngine;
using ProtoNodeView = Panoptes.Protocol.V1.NodeView;
using ProtoPosition = Panoptes.Protocol.V1.Position;
using ProtoUnitView = Panoptes.Protocol.V1.UnitView;

namespace Panoptes.Runtime.Map
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
            public bool isResourcePoint;
            public string resourceType;
            public string buildingType;
            public int buildingHp;
            public string owner;
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
        [SerializeField] private bool preferServerPushedMapConfig = true;
        [SerializeField] private string serverMapConfigKey = "mapconfig";
        [SerializeField] private bool listenServerMapConfigUpdates = true;

        [Header("Prefab")]
        [SerializeField] private NodeView nodeTilePrefab;
        [SerializeField] private Transform tilesRoot;
        [SerializeField] private float tileSize = 1f;

        [Header("Units")]
        [SerializeField] private UnitView unitPrefab;
        [SerializeField] private Transform unitsRoot;
        [SerializeField] private bool spawnDebugUnitsWhenNoUnits = true;
        [SerializeField] private string[] debugFactions = { "blue", "red", "green", "yellow" };

        [Header("Debug Generation (Local Only)")]
        [SerializeField] private bool generateDebugMapOnStart = true;
        [SerializeField] private int debugMapWidth = 20;
        [SerializeField] private int debugMapHeight = 20;
        [Range(0f, 1f)] [SerializeField] private float farmSpawnRate = 0.07f;
        [Range(0f, 1f)] [SerializeField] private float barracksSpawnRate = 0.04f;
        [SerializeField] private int randomSeed = 20260405;

        private readonly Dictionary<string, NodeView> _tileViews = new();
        private readonly Dictionary<Vector2Int, NodeView> _tileViewsByGrid = new();
        private readonly Dictionary<string, ProtoNodeView> _nodeStates = new();

        private readonly Dictionary<string, UnitView> _unitViews = new();
        private readonly Dictionary<string, string> _unitNodeById = new();
        private readonly Dictionary<string, HashSet<string>> _unitsByNodeId = new();

        private readonly List<ProtoUnitView> _jsonUnits = new();
        private StaticCatalogCache _catalogCache;

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
            // Map source is backend pushed game state only.
        }

        private void OnDisable()
        {
            // Map source is backend pushed game state only.
        }

        private void Start()
        {
            if (!ShouldBuildOnStart())
            {
                return;
            }

            EnsureRuntimeControllers();
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
            if (UnityEngine.Object.FindObjectOfType<MapInputHandler>() == null)
            {
                var go = new GameObject("MapInputHandler");
                go.AddComponent<MapInputHandler>();
            }

            var cam = Camera.main;
            if (cam != null && cam.GetComponent<TopDownCameraController>() == null)
            {
                cam.gameObject.AddComponent<TopDownCameraController>();
            }
        }

        public void RebuildMap()
        {
            if (GameStateCache.Instance == null)
            {
                Debug.LogError("[MapRenderer] Cannot build map: GameStateCache is missing.");
                return;
            }

            if (GameStateCache.Instance.Nodes == null || GameStateCache.Instance.Nodes.Count == 0)
            {
                Debug.LogError("[MapRenderer] Cannot build map: backend node list is empty.");
                return;
            }

            Debug.Log($"[MapRenderer] Rebuild from backend nodes: {GameStateCache.Instance.Nodes.Count}");
            PrepareRuntimeRoots();
            BuildFromNodes(GameStateCache.Instance.Nodes.Values);
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
        }

        private void UnsubscribeServerMapConfig()
        {
            if (_catalogCache != null)
            {
                _catalogCache.CatalogChanged -= OnServerMapCatalogChanged;
                _catalogCache = null;
            }
        }

        private void OnServerMapCatalogChanged()
        {
            if (GameStateCache.Instance != null && GameStateCache.Instance.Nodes.Count > 0)
            {
                return;
            }
            TryLoadMapFromStaticCatalog();
        }

        private bool TryLoadMapFromStaticCatalog()
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

            var nodes = new List<ProtoNodeView>(mapBundle.nodes.Length);
            for (var i = 0; i < mapBundle.nodes.Length; i++)
            {
                var node = mapBundle.nodes[i];
                if (node == null)
                {
                    continue;
                }

                var buildingType = NormalizeToken(node.building_type);
                nodes.Add(new ProtoNodeView
                {
                    Id = string.IsNullOrWhiteSpace(node.id) ? $"N_{node.x}_{node.y}" : node.id.Trim(),
                    Pos = new ProtoPosition { X = node.x, Y = node.y },
                    Terrain = NormalizeToken(node.terrain),
                    HasRoad = node.has_road,
                    IsResourcePoint = node.is_resource_point,
                    ResourceType = NormalizeToken(node.resource_type),
                    Owner = NormalizeToken(node.owner),
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

        public bool TryGetNodeView(string nodeId, out NodeView nodeView)
        {
            return _tileViews.TryGetValue(nodeId, out nodeView) && nodeView != null;
        }

        public bool TryGetNodeViewByGrid(Vector2Int gridPos, out NodeView nodeView)
        {
            return _tileViewsByGrid.TryGetValue(gridPos, out nodeView) && nodeView != null;
        }

        public bool TryGetNodeState(string nodeId, out ProtoNodeView nodeState)
        {
            return _nodeStates.TryGetValue(nodeId, out nodeState) && nodeState != null;
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
            }
            else
            {
                nodeView.SetBuilding(buildingType, ownerId, hp, false);
            }

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
                var protocolUnit = GameStateCache.Instance.GetUnit(unitId);
                if (protocolUnit != null)
                {
                    protocolUnit.Pos = new ProtoPosition
                    {
                        X = targetNode.GridPos.x,
                        Y = targetNode.GridPos.y
                    };
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

        private void BuildDebugMap()
        {
            _jsonUnits.Clear();
            var nodes = CreateDebugNodes();
            BuildFromNodes(nodes);
        }

        private List<ProtoNodeView> CreateDebugNodes()
        {
            var nodes = new List<ProtoNodeView>(debugMapWidth * debugMapHeight);
            var rng = new System.Random(randomSeed);

            for (var y = 0; y < debugMapHeight; y++)
            {
                for (var x = 0; x < debugMapWidth; x++)
                {
                    var nodeId = $"N_{x}_{y}";
                    var terrain = RollTerrain(rng);
                    var buildingType = RollBuildingType(rng, terrain);

                    var node = new ProtoNodeView
                    {
                        Id = nodeId,
                        Pos = new ProtoPosition { X = x, Y = y },
                        Terrain = terrain,
                        BuildingType = buildingType,
                        BuildingHp = string.IsNullOrEmpty(buildingType) ? 0 : 100,
                        Owner = string.Empty,
                        HasRoad = false,
                        IsResourcePoint = false,
                        ResourceType = string.Empty
                    };

                    nodes.Add(node);
                }
            }

            return nodes;
        }

        private string RollTerrain(System.Random rng)
        {
            var r = rng.NextDouble();
            if (r < 0.08d) return "river";
            if (r < 0.22d) return "forest";
            if (r < 0.34d) return "mountain";
            return "plain";
        }

        private string RollBuildingType(System.Random rng, string terrain)
        {
            if (terrain == "river" || terrain == "mountain")
            {
                return string.Empty;
            }

            var roll = rng.NextDouble();
            if (roll < farmSpawnRate)
            {
                return "farm";
            }

            if (roll < farmSpawnRate + barracksSpawnRate)
            {
                return "barracks";
            }

            return string.Empty;
        }

        private void BuildFromNodes(IEnumerable<ProtoNodeView> nodes)
        {
            if (nodeTilePrefab == null)
            {
                Debug.LogError("[MapRenderer] NodeTile prefab is not assigned.");
                return;
            }

            PrepareRuntimeRoots();
            ClearMap();

            foreach (var node in nodes)
            {
                if (node == null || node.Pos == null || string.IsNullOrEmpty(node.Id))
                {
                    continue;
                }

                var tile = Instantiate(nodeTilePrefab, EnsureTilesRoot(), false);
                tile.transform.localPosition = GridToWorld(node.Pos.X, node.Pos.Y);
                tile.Bind(node);

                _tileViews[node.Id] = tile;
                _tileViewsByGrid[tile.GridPos] = tile;
                _nodeStates[node.Id] = node;
            }

            FocusCameraToCenter();
            RebuildUnitsForCurrentSource();
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

        private List<ProtoUnitView> CreateDebugUnits()
        {
            var result = new List<ProtoUnitView>();
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

                result.Add(new ProtoUnitView
                {
                    Id = $"U_DEBUG_{i + 1}",
                    Faction = faction,
                    UnitType = "infantry",
                    Hp = 100,
                    MaxHp = 100,
                    Pos = new ProtoPosition { X = p.x, Y = p.y }
                });
            }

            return result;
        }

        private void BuildUnitsFromState(IEnumerable<ProtoUnitView> units)
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
                if (unit == null || unit.Pos == null || string.IsNullOrEmpty(unit.Id))
                {
                    continue;
                }

                var gridPos = new Vector2Int(unit.Pos.X, unit.Pos.Y);
                if (!TryGetNodeViewByGrid(gridPos, out var nodeView) || nodeView == null)
                {
                    continue;
                }

                var instance = CreateUnitInstance();
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

                unitCache?.Register(instance);
            }
        }

        private UnitView CreateUnitInstance()
        {
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

            EnsureGameplayCameraPose(cam);
            var center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            var camPos = cam.transform.position;

            // Keep bounds from TopDownCameraController inspector settings.
            var cameraController = cam.GetComponent<TopDownCameraController>();

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

        private bool TryParseNodesFromJson(string json, out List<ProtoNodeView> nodes)
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

        private List<ProtoNodeView> BuildNodesFromJsonConfig(MapJsonConfig config)
        {
            var result = new List<ProtoNodeView>();
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
                var resourceType = NormalizeToken(jsonNode.resourceType);
                var hasBuilding = !string.IsNullOrEmpty(buildingType);
                var isResourcePoint = jsonNode.isResourcePoint || !string.IsNullOrEmpty(resourceType);

                var node = new ProtoNodeView
                {
                    Id = string.IsNullOrWhiteSpace(jsonNode.id) ? $"N_{jsonNode.x}_{jsonNode.y}" : jsonNode.id.Trim(),
                    Pos = new ProtoPosition { X = jsonNode.x, Y = jsonNode.y },
                    Terrain = terrain,
                    HasRoad = jsonNode.hasRoad,
                    IsResourcePoint = isResourcePoint,
                    ResourceType = isResourcePoint ? resourceType : string.Empty,
                    BuildingType = buildingType,
                    BuildingHp = hasBuilding ? (jsonNode.buildingHp > 0 ? jsonNode.buildingHp : 100) : 0,
                    Owner = (jsonNode.owner ?? string.Empty).Trim()
                };

                result.Add(node);
            }

            return result;
        }

        private static void BuildUnitsFromJsonConfig(MapJsonConfig config, List<ProtoUnitView> output)
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

                output.Add(new ProtoUnitView
                {
                    Id = string.IsNullOrWhiteSpace(src.id) ? $"U_{src.x}_{src.y}_{i}" : src.id.Trim(),
                    Faction = (src.faction ?? string.Empty).Trim(),
                    UnitType = string.IsNullOrWhiteSpace(src.unitType) ? "infantry" : src.unitType.Trim(),
                    Hp = Mathf.Max(0, src.hp),
                    MaxHp = Mathf.Max(1, src.maxHp),
                    Pos = new ProtoPosition { X = src.x, Y = src.y }
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
