/*************************************************
 * Project: Panoptes
 * File: MapRenderer.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Map rendering manager.
 *************************************************/

using System.Collections.Generic;
using Panoptes.Runtime.Cache;
using UnityEngine;
using ProtoNodeView = Panoptes.Protocol.V1.NodeView;
using ProtoPosition = Panoptes.Protocol.V1.Position;

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

        public static MapRenderer Instance { get; private set; }

        [Header("Map Source")]
        [SerializeField] private bool useJsonMapOnStart = false;
        [SerializeField] private TextAsset startupMapJson;
        [SerializeField] private bool autoFillMissingJsonTiles = false;

        [Header("Prefab")]
        [SerializeField] private NodeView nodeTilePrefab;
        [SerializeField] private Transform tilesRoot;
        [SerializeField] private float tileSize = 1f;

        [Header("Debug Generation (Local Only)")]
        [SerializeField] private bool generateDebugMapOnStart = true;
        [SerializeField] private int debugMapWidth = 20;
        [SerializeField] private int debugMapHeight = 20;
        [Range(0f, 1f)] [SerializeField] private float farmSpawnRate = 0.07f;
        [Range(0f, 1f)] [SerializeField] private float barracksSpawnRate = 0.04f;
        [SerializeField] private int randomSeed = 20260405;

        private readonly Dictionary<string, NodeView> _tileViews = new();
        private readonly Dictionary<string, ProtoNodeView> _nodeStates = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (useJsonMapOnStart && startupMapJson != null && LoadMapFromJsonString(startupMapJson.text))
            {
                return;
            }

            if (generateDebugMapOnStart)
            {
                BuildDebugMap();
            }
        }

        public void RebuildMap()
        {
            if (useJsonMapOnStart && startupMapJson != null && LoadMapFromJsonString(startupMapJson.text))
            {
                return;
            }

            // Local debug map mode for early visual iteration.
            if (generateDebugMapOnStart)
            {
                BuildDebugMap();
                return;
            }

            if (GameStateCache.Instance == null)
            {
                Debug.LogWarning("[MapRenderer] GameStateCache is missing.");
                return;
            }

            BuildFromNodes(GameStateCache.Instance.Nodes.Values);
        }

        /// <summary>
        /// Build map from JSON string. Can be used by backend message handlers directly.
        /// Supported JSON:
        /// 1) { "mapId":"...", "width":20, "height":20, "nodes":[...] }
        /// 2) { "map": { ...same fields... } }
        /// </summary>
        public bool LoadMapFromJsonString(string json)
        {
            if (!TryParseNodesFromJson(json, out var nodes))
            {
                return false;
            }

            BuildFromNodes(nodes);
            return true;
        }

        /// <summary>
        /// Convenience overload for TextAsset usage in editor.
        /// </summary>
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

        private void BuildDebugMap()
        {
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
                        // Keep debug buildings neutral so original material colors stay readable.
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
                _nodeStates[node.Id] = node;
            }

            FocusCameraToCenter();
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

        private Vector3 GridToWorld(int x, int y)
        {
            return new Vector3(x * tileSize, 0f, y * tileSize);
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
            _nodeStates.Clear();
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

            var center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            var camPos = cam.transform.position;

            var cameraController = cam.GetComponent<TopDownCameraController>();
            if (cameraController != null)
            {
                cameraController.SetWorldBounds(minX, maxX, minZ, maxZ, 0f);
            }

            if (cam.orthographic)
            {
                cam.transform.position = new Vector3(center.x, camPos.y, center.z - 10f);
            }
            else
            {
                cam.transform.position = new Vector3(center.x, camPos.y, center.z - 6f);
            }
        }

        private bool TryParseNodesFromJson(string json, out List<ProtoNodeView> nodes)
        {
            nodes = null;
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

            Debug.Log($"[MapRenderer] Loaded map JSON: mapId='{config.mapId}', nodes={nodes.Count}.");
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
                    Debug.LogWarning($"[MapRenderer] Node x out of range and skipped: x={jsonNode.x}, width={width}.");
                    continue;
                }

                if (height > 0 && (jsonNode.y < 0 || jsonNode.y >= height))
                {
                    Debug.LogWarning($"[MapRenderer] Node y out of range and skipped: y={jsonNode.y}, height={height}.");
                    continue;
                }

                var key = MakeCoordKey(jsonNode.x, jsonNode.y);
                if (nodeByKey.ContainsKey(key))
                {
                    Debug.LogWarning($"[MapRenderer] Duplicate node at ({jsonNode.x},{jsonNode.y}); last one wins.");
                }

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
