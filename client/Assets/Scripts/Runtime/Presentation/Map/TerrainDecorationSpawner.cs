using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Panoptes.Presentation.Map
{
    /// <summary>
    /// Spawns lightweight terrain decorations (trees/rocks/plants) after map build.
    /// </summary>
    public sealed class TerrainDecorationSpawner : MonoBehaviour
    {
        [Serializable]
        private struct TerrainDecorRule
        {
            public string terrain;
            [Range(0f, 1f)] public float density;
            public Vector2 scaleRange;
            public GameObject[] prefabs;
        }

        [Header("General")]
        [SerializeField] private bool enabledOnBuild = true;
        [SerializeField] private int seed = 20260414;
        [SerializeField] private float yOffset = 0.05f;
        [SerializeField] private bool randomYaw = true;
        [SerializeField] private float globalScaleMultiplier = 0.25f;
        [SerializeField] private float positionJitter = 0.22f;
        [SerializeField] private bool spawnTerrainTransitions = true;
        [Range(0f, 1f)] [SerializeField] private float transitionDensity = 0.55f;
        [SerializeField] private float transitionYOffset = 0.035f;
        [SerializeField] private float transitionJitter = 0.12f;
        [SerializeField] private float transitionScaleMultiplier = 0.55f;
        [SerializeField] private bool disableDecorColliders = true;
        [SerializeField] private bool optimizeDecorRenderers = true;
        [SerializeField] private bool disableDecorShadows = true;
        [SerializeField] private bool disableDecorProbes = true;
        [SerializeField] private bool enableMaterialInstancing = true;
        [SerializeField] private Transform decorRoot;

        [Header("Rules")]
        [SerializeField] private TerrainDecorRule[] rules;

        private readonly List<GameObject> _spawned = new();
        private readonly Dictionary<string, List<GameObject>> _spawnedByNodeId =
            new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);
        private static readonly Vector2Int[] CardinalDirs =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        public void RebuildDecorations(IReadOnlyList<NodeDto> nodes, IReadOnlyDictionary<string, NodeView> tileViews)
        {
            ClearDecorations();

            if (!enabledOnBuild || nodes == null || tileViews == null || nodes.Count == 0)
            {
                return;
            }

            EnsureDefaultRules();
            EnsureDecorRoot();
            if (rules == null || rules.Length == 0 || decorRoot == null)
            {
                return;
            }

            var nodeByGrid = BuildNodeLookup(nodes);

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!CanDecorateNode(node))
                {
                    continue;
                }

                if (!tileViews.TryGetValue(node.Id, out var tile) || tile == null)
                {
                    continue;
                }

                var rule = ResolveRule(node.Terrain);
                if (!rule.HasValue)
                {
                    continue;
                }

                var ruleValue = rule.Value;
                var prefab = PickPrefab(ruleValue.prefabs, node.X, node.Y);
                if (prefab == null)
                {
                    continue;
                }

                var nodeRandom = BuildNodeRandom(node.X, node.Y);
                SpawnTerrainTransitionDecor(
                    node,
                    tile,
                    nodeByGrid,
                    tileViews,
                    ruleValue,
                    nodeRandom);

                if (nodeRandom.NextDouble() > Mathf.Clamp01(ruleValue.density))
                {
                    continue;
                }

                var instance = Instantiate(prefab, decorRoot, false);
                instance.name = $"{prefab.name}_Decor_{node.X}_{node.Y}";

                var pos = tile.transform.position;
                var jitter = Mathf.Clamp(positionJitter, 0f, 0.45f);
                if (jitter > 0f)
                {
                    var jx = ((float)nodeRandom.NextDouble() * 2f - 1f) * jitter;
                    var jz = ((float)nodeRandom.NextDouble() * 2f - 1f) * jitter;
                    pos.x += jx;
                    pos.z += jz;
                }
                pos.y += yOffset;
                instance.transform.position = pos;

                if (randomYaw)
                {
                    instance.transform.rotation = Quaternion.Euler(0f, (float)(nodeRandom.NextDouble() * 360.0), 0f);
                }

                var scaleRange = ruleValue.scaleRange;
                if (scaleRange.x <= 0f && scaleRange.y <= 0f)
                {
                    scaleRange = new Vector2(0.75f, 1.15f);
                }

                var scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)nodeRandom.NextDouble());
                if (scale <= 0f)
                {
                    scale = 1f;
                }

                var globalScale = globalScaleMultiplier > 0f ? globalScaleMultiplier : 1f;
                instance.transform.localScale *= scale * globalScale;

                if (disableDecorColliders)
                {
                    DisableAllColliders(instance);
                }

                OptimizeDecorInstance(instance);

                _spawned.Add(instance);
                RegisterSpawn(node.Id, instance);
            }
        }

        private static Dictionary<Vector2Int, NodeDto> BuildNodeLookup(IReadOnlyList<NodeDto> nodes)
        {
            var result = new Dictionary<Vector2Int, NodeDto>();
            if (nodes == null)
            {
                return result;
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                result[new Vector2Int(node.X, node.Y)] = node;
            }

            return result;
        }

        private void SpawnTerrainTransitionDecor(
            NodeDto node,
            NodeView tile,
            IReadOnlyDictionary<Vector2Int, NodeDto> nodeByGrid,
            IReadOnlyDictionary<string, NodeView> tileViews,
            TerrainDecorRule ruleValue,
            System.Random nodeRandom)
        {
            if (!spawnTerrainTransitions || node == null || tile == null || nodeByGrid == null || tileViews == null)
            {
                return;
            }

            var density = Mathf.Clamp01(transitionDensity);
            if (density <= 0f)
            {
                return;
            }

            for (var i = 0; i < CardinalDirs.Length; i++)
            {
                var dir = CardinalDirs[i];
                var neighborCoord = new Vector2Int(node.X + dir.x, node.Y + dir.y);
                if (!nodeByGrid.TryGetValue(neighborCoord, out var neighbor) || neighbor == null)
                {
                    continue;
                }

                if (!ShouldSpawnTransitionForEdge(node, neighbor))
                {
                    continue;
                }

                if (nodeRandom.NextDouble() > density)
                {
                    continue;
                }

                if (!tileViews.TryGetValue(neighbor.Id, out var neighborTile) || neighborTile == null)
                {
                    continue;
                }

                var prefab = PickPrefab(ruleValue.prefabs, node.X * 31 + dir.x, node.Y * 31 + dir.y);
                if (prefab == null)
                {
                    continue;
                }

                var edge = neighborTile.transform.position - tile.transform.position;
                edge.y = 0f;
                if (edge.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                edge.Normalize();
                var tangent = Vector3.Cross(Vector3.up, edge).normalized;
                var jitter = ((float)nodeRandom.NextDouble() * 2f - 1f) * Mathf.Clamp(transitionJitter, 0f, 0.45f);

                var pos = (tile.transform.position + neighborTile.transform.position) * 0.5f;
                pos += tangent * jitter;
                pos.y = Mathf.Max(tile.transform.position.y, neighborTile.transform.position.y) + transitionYOffset;

                var instance = Instantiate(prefab, decorRoot, false);
                instance.name = $"{prefab.name}_Transition_{node.X}_{node.Y}_{neighbor.X}_{neighbor.Y}";
                instance.transform.position = pos;

                if (randomYaw)
                {
                    instance.transform.rotation = Quaternion.Euler(0f, (float)(nodeRandom.NextDouble() * 360.0), 0f);
                }

                var scaleRange = ruleValue.scaleRange;
                if (scaleRange.x <= 0f && scaleRange.y <= 0f)
                {
                    scaleRange = new Vector2(0.75f, 1.15f);
                }

                var scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)nodeRandom.NextDouble());
                scale = Mathf.Max(0.05f, scale);
                scale *= Mathf.Max(0.01f, globalScaleMultiplier) * Mathf.Max(0.01f, transitionScaleMultiplier);
                instance.transform.localScale *= scale;

                if (disableDecorColliders)
                {
                    DisableAllColliders(instance);
                }

                OptimizeDecorInstance(instance);

                _spawned.Add(instance);
                RegisterSpawn(node.Id, instance);
                RegisterSpawn(neighbor.Id, instance);
            }
        }

        private bool ShouldSpawnTransitionForEdge(NodeDto node, NodeDto neighbor)
        {
            if (node == null || neighbor == null)
            {
                return false;
            }

            // Deduplicate edge spawn: only spawn once per undirected edge.
            if (neighbor.X < node.X || (neighbor.X == node.X && neighbor.Y <= node.Y))
            {
                return false;
            }

            if (!CanDecorateNode(neighbor))
            {
                return false;
            }

            return !string.Equals(
                Normalize(node.Terrain),
                Normalize(neighbor.Terrain),
                StringComparison.Ordinal);
        }

        public void ClearDecorations()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                var go = _spawned[i];
                if (go != null)
                {
                    Destroy(go);
                }
            }

            _spawned.Clear();
            _spawnedByNodeId.Clear();

            if (decorRoot == null)
            {
                return;
            }

            for (var i = decorRoot.childCount - 1; i >= 0; i--)
            {
                var child = decorRoot.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void EnsureDecorRoot()
        {
            if (decorRoot != null)
            {
                return;
            }

            var child = transform.Find("DecorRoot");
            if (child != null)
            {
                decorRoot = child;
                return;
            }

            var go = new GameObject("DecorRoot");
            go.transform.SetParent(transform, false);
            decorRoot = go.transform;
        }

        private void EnsureDefaultRules()
        {
            if (rules != null && rules.Length > 0 && HasAnyPrefab(rules))
            {
                return;
            }

            rules = new[]
            {
                new TerrainDecorRule
                {
                    terrain = "plain",
                    density = 0.08f,
                    scaleRange = new Vector2(0.65f, 1.0f),
                    prefabs = LoadDefaultPrefabs(
                        "Bush_Common_Flowers.fbx",
                        "Grass_Common_Short.fbx",
                        "Clover_1.fbx",
                        "Pebble_Round_1.fbx")
                },
                new TerrainDecorRule
                {
                    terrain = "forest",
                    density = 0.22f,
                    scaleRange = new Vector2(0.7f, 1.2f),
                    prefabs = LoadDefaultPrefabs(
                        "CommonTree_1.fbx",
                        "CommonTree_3.fbx",
                        "Bush_Common.fbx",
                        "Plant_7.fbx")
                },
                new TerrainDecorRule
                {
                    terrain = "mountain",
                    density = 0.14f,
                    scaleRange = new Vector2(0.75f, 1.3f),
                    prefabs = LoadDefaultPrefabs(
                        "Rock_Medium_1.fbx",
                        "Rock_Medium_2.fbx",
                        "Pebble_Square_3.fbx",
                        "DeadTree_1.fbx")
                },
                new TerrainDecorRule
                {
                    terrain = "river",
                    density = 0.06f,
                    scaleRange = new Vector2(0.6f, 0.95f),
                    prefabs = LoadDefaultPrefabs(
                        "RockPath_Round_Small_1.fbx",
                        "Pebble_Round_3.fbx",
                        "Plant_1.fbx")
                },
                new TerrainDecorRule
                {
                    terrain = "snow",
                    density = 0.15f,
                    scaleRange = new Vector2(0.75f, 1.25f),
                    prefabs = LoadDefaultPrefabs(
                        "Pine_1.fbx",
                        "Pine_3.fbx",
                        "Rock_Medium_3.fbx")
                },
                new TerrainDecorRule
                {
                    terrain = "forbidden",
                    density = 0.04f,
                    scaleRange = new Vector2(0.7f, 1.1f),
                    prefabs = LoadDefaultPrefabs(
                        "DeadTree_4.fbx",
                        "Pebble_Square_6.fbx")
                }
            };
        }

        private static bool HasAnyPrefab(IReadOnlyList<TerrainDecorRule> sourceRules)
        {
            if (sourceRules == null)
            {
                return false;
            }

            for (var i = 0; i < sourceRules.Count; i++)
            {
                var prefabs = sourceRules[i].prefabs;
                if (prefabs == null)
                {
                    continue;
                }

                for (var j = 0; j < prefabs.Length; j++)
                {
                    if (prefabs[j] != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CanDecorateNode(NodeDto node)
        {
            if (node == null)
            {
                return false;
            }

            if (node.IsResourcePoint || node.HasRoad)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(node.BuildingType))
            {
                return false;
            }

            return true;
        }

        private TerrainDecorRule? ResolveRule(string terrain)
        {
            var normalized = Normalize(terrain);
            if (rules == null || rules.Length == 0)
            {
                return null;
            }

            for (var i = 0; i < rules.Length; i++)
            {
                if (Normalize(rules[i].terrain) == normalized)
                {
                    return rules[i];
                }
            }

            return null;
        }

        private System.Random BuildNodeRandom(int x, int y)
        {
            unchecked
            {
                var hash = seed;
                hash = hash * 486187739 + x * 92821;
                hash = hash * 16777619 + y * 68917;
                return new System.Random(hash);
            }
        }

        private static GameObject PickPrefab(GameObject[] candidates, int x, int y)
        {
            if (candidates == null || candidates.Length == 0)
            {
                return null;
            }

            var valid = new List<GameObject>(candidates.Length);
            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null)
                {
                    valid.Add(candidates[i]);
                }
            }

            if (valid.Count == 0)
            {
                return null;
            }

            unchecked
            {
                var idxSeed = x * 73856093 ^ y * 19349663;
                var idx = Mathf.Abs(idxSeed) % valid.Count;
                return valid[idx];
            }
        }

        private static void DisableAllColliders(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private void OptimizeDecorInstance(GameObject instance)
        {
            if (!optimizeDecorRenderers || instance == null)
            {
                return;
            }

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (disableDecorShadows)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                if (disableDecorProbes)
                {
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }

                if (!enableMaterialInstancing)
                {
                    continue;
                }

                var mats = renderer.sharedMaterials;
                if (mats == null)
                {
                    continue;
                }

                for (var m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat != null && !mat.enableInstancing)
                    {
                        mat.enableInstancing = true;
                    }
                }
            }
        }

        public void ApplyObservationState(IReadOnlyDictionary<string, NodeDto> nodesById, bool hideUnknownDecor)
        {
            if (_spawned.Count == 0)
            {
                return;
            }

            if (!hideUnknownDecor || nodesById == null)
            {
                for (var i = 0; i < _spawned.Count; i++)
                {
                    var go = _spawned[i];
                    if (go != null && !go.activeSelf)
                    {
                        go.SetActive(true);
                    }
                }
                return;
            }

            var visibleByObject = new Dictionary<GameObject, bool>(_spawned.Count);
            for (var i = 0; i < _spawned.Count; i++)
            {
                var go = _spawned[i];
                if (go != null)
                {
                    visibleByObject[go] = false;
                }
            }

            foreach (var pair in _spawnedByNodeId)
            {
                if (!nodesById.TryGetValue(pair.Key, out var node) || node == null)
                {
                    continue;
                }

                if (!node.IsVisible && !node.IsMemory)
                {
                    continue;
                }

                var list = pair.Value;
                if (list == null)
                {
                    continue;
                }

                for (var i = 0; i < list.Count; i++)
                {
                    var go = list[i];
                    if (go != null)
                    {
                        visibleByObject[go] = true;
                    }
                }
            }

            foreach (var pair in visibleByObject)
            {
                if (pair.Key != null && pair.Key.activeSelf != pair.Value)
                {
                    pair.Key.SetActive(pair.Value);
                }
            }
        }

        private void RegisterSpawn(string nodeId, GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            var normalizedNodeId = (nodeId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalizedNodeId))
            {
                return;
            }

            if (!_spawnedByNodeId.TryGetValue(normalizedNodeId, out var list) || list == null)
            {
                list = new List<GameObject>();
                _spawnedByNodeId[normalizedNodeId] = list;
            }

            if (!list.Contains(instance))
            {
                list.Add(instance);
            }
        }

        private static GameObject[] LoadDefaultPrefabs(params string[] fileNames)
        {
            var list = new List<GameObject>();

            for (var i = 0; i < fileNames.Length; i++)
            {
                var prefab = LoadDefaultPrefab(fileNames[i]);
                if (prefab != null)
                {
                    list.Add(prefab);
                }
            }

            return list.ToArray();
        }

        private static GameObject LoadDefaultPrefab(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

#if UNITY_EDITOR
            var path = $"Assets/Art/Models/Environment/Nature/Quaternius/FBX (Unity)/{fileName}";
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }
    }
}
