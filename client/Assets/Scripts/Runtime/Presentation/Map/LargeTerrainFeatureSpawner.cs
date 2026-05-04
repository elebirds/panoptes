using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Panoptes.Presentation.Map
{
    /// <summary>
    /// Builds multi-tile terrain meshes from backend terrain ids.
    /// </summary>
    public sealed class LargeTerrainFeatureSpawner : MonoBehaviour
    {
        private const int HillFootprintWidth = 4;
        private const int HillFootprintHeight = 4;

        [Header("Hill")]
        [SerializeField] private string hillTerrainId = "hill";
        [SerializeField] private float hillHeight = 0.28f;
        [SerializeField] private float hillFootprintPadding = 0.72f;
        [SerializeField] private int hillMeshResolution = 10;
        [SerializeField] private Color hillColor = new Color(0.36f, 0.48f, 0.26f, 1f);
        [SerializeField] private Color hillRockColor = new Color(0.42f, 0.39f, 0.34f, 1f);
        [SerializeField] private bool spawnRockRim = true;
        [SerializeField] private int rockRimCount = 16;
        [SerializeField] private Vector2 rockScaleRange = new Vector2(0.08f, 0.18f);

        [Header("Visibility")]
        [SerializeField] private bool hideWhenAllFootprintNodesUnknown = true;

        [Header("Runtime")]
        [SerializeField] private Transform featureRoot;

        private readonly List<GameObject> _spawned = new();
        private readonly Dictionary<string, List<GameObject>> _spawnedByNodeId =
            new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);
        private Material _hillMaterial;
        private Material _rockMaterial;

        public void RebuildFeatures(IReadOnlyList<NodeDto> nodes, IReadOnlyDictionary<string, NodeView> tileViews)
        {
            ClearFeatures();

            if (nodes == null || tileViews == null || nodes.Count == 0)
            {
                return;
            }

            EnsureFeatureRoot();
            if (featureRoot == null)
            {
                return;
            }

            SpawnHillFootprints(nodes, tileViews);
        }

        public void ApplyObservationState(IReadOnlyDictionary<string, NodeDto> nodeStates, bool hideUnknownDetails)
        {
            if (!hideWhenAllFootprintNodesUnknown || !hideUnknownDetails || nodeStates == null)
            {
                SetAllVisible(true);
                return;
            }

            for (var i = 0; i < _spawned.Count; i++)
            {
                var feature = _spawned[i];
                if (feature == null)
                {
                    continue;
                }

                feature.SetActive(IsFeatureKnown(feature, nodeStates));
            }
        }

        public void ClearFeatures()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Destroy(_spawned[i]);
                }
            }

            _spawned.Clear();
            _spawnedByNodeId.Clear();

            if (featureRoot == null)
            {
                return;
            }

            for (var i = featureRoot.childCount - 1; i >= 0; i--)
            {
                var child = featureRoot.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void SpawnHillFootprints(IReadOnlyList<NodeDto> nodes, IReadOnlyDictionary<string, NodeView> tileViews)
        {
            var terrainId = Normalize(hillTerrainId);
            if (string.IsNullOrEmpty(terrainId))
            {
                terrainId = "hill";
            }

            var nodesByOffset = new Dictionary<Vector2Int, NodeDto>();
            var min = new Vector2Int(int.MaxValue, int.MaxValue);
            var max = new Vector2Int(int.MinValue, int.MinValue);

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                var offset = HexGrid.AxialToOffset(node.Q, node.R);
                nodesByOffset[offset] = node;
                min.x = Mathf.Min(min.x, offset.x);
                min.y = Mathf.Min(min.y, offset.y);
                max.x = Mathf.Max(max.x, offset.x);
                max.y = Mathf.Max(max.y, offset.y);
            }

            if (nodesByOffset.Count == 0)
            {
                return;
            }

            var consumed = new HashSet<Vector2Int>();
            for (var y = min.y; y <= max.y - HillFootprintHeight + 1; y++)
            {
                for (var x = min.x; x <= max.x - HillFootprintWidth + 1; x++)
                {
                    var topLeft = new Vector2Int(x, y);
                    if (!TryCollectFootprint(topLeft, terrainId, nodesByOffset, tileViews, consumed, out var footprint))
                    {
                        continue;
                    }

                    SpawnHill(topLeft, footprint);
                    for (var i = 0; i < footprint.Count; i++)
                    {
                        consumed.Add(HexGrid.AxialToOffset(footprint[i].node.Q, footprint[i].node.R));
                    }
                }
            }
        }

        private bool TryCollectFootprint(
            Vector2Int topLeft,
            string terrainId,
            IReadOnlyDictionary<Vector2Int, NodeDto> nodesByOffset,
            IReadOnlyDictionary<string, NodeView> tileViews,
            HashSet<Vector2Int> consumed,
            out List<(NodeDto node, NodeView tile)> footprint)
        {
            footprint = new List<(NodeDto node, NodeView tile)>(HillFootprintWidth * HillFootprintHeight);

            for (var dy = 0; dy < HillFootprintHeight; dy++)
            {
                for (var dx = 0; dx < HillFootprintWidth; dx++)
                {
                    var offset = new Vector2Int(topLeft.x + dx, topLeft.y + dy);
                    if (consumed.Contains(offset) || !nodesByOffset.TryGetValue(offset, out var node) || node == null)
                    {
                        return false;
                    }

                    if (!string.Equals(Normalize(node.Terrain), terrainId, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    if (!tileViews.TryGetValue(node.Id, out var tile) || tile == null)
                    {
                        return false;
                    }

                    footprint.Add((node, tile));
                }
            }

            return footprint.Count == HillFootprintWidth * HillFootprintHeight;
        }

        private void SpawnHill(Vector2Int topLeft, IReadOnlyList<(NodeDto node, NodeView tile)> footprint)
        {
            if (footprint == null || footprint.Count == 0)
            {
                return;
            }

            var bounds = BuildFootprintBounds(footprint);
            var center = bounds.center;
            var hill = new GameObject($"Hill4x4_{topLeft.x}_{topLeft.y}");
            hill.transform.SetParent(featureRoot, false);
            hill.transform.position = center;

            var meshFilter = hill.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = BuildHillMesh(bounds.size.x * 0.5f, bounds.size.z * 0.5f);
            var renderer = hill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = EnsureHillMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (spawnRockRim)
            {
                SpawnHillRockRim(hill.transform, bounds.size.x * 0.5f, bounds.size.z * 0.5f, topLeft);
            }

            _spawned.Add(hill);
            RegisterFootprintNodes(hill, footprint);
        }

        private Bounds BuildFootprintBounds(IReadOnlyList<(NodeDto node, NodeView tile)> footprint)
        {
            var bounds = new Bounds(footprint[0].tile.transform.position, Vector3.zero);
            for (var i = 1; i < footprint.Count; i++)
            {
                bounds.Encapsulate(footprint[i].tile.transform.position);
            }

            var pad = Mathf.Max(0.05f, hillFootprintPadding);
            var expanded = bounds.size;
            expanded.x += pad * 2f;
            expanded.z += pad * 2f;
            expanded.y = Mathf.Max(0.01f, expanded.y);
            bounds.size = expanded;
            return bounds;
        }

        private Mesh BuildHillMesh(float radiusX, float radiusZ)
        {
            var resolution = Mathf.Clamp(hillMeshResolution, 4, 24);
            var vertexCount = (resolution + 1) * (resolution + 1);
            var vertices = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            var triangles = new int[resolution * resolution * 6];
            var height = Mathf.Max(0.02f, hillHeight);
            var safeRadiusX = Mathf.Max(0.1f, radiusX);
            var safeRadiusZ = Mathf.Max(0.1f, radiusZ);

            var index = 0;
            for (var z = 0; z <= resolution; z++)
            {
                var vz = (z / (float)resolution - 0.5f) * 2f;
                for (var x = 0; x <= resolution; x++)
                {
                    var vx = (x / (float)resolution - 0.5f) * 2f;
                    var radius = Mathf.Sqrt(vx * vx + vz * vz);
                    var plateau = Mathf.Clamp01(1f - Mathf.InverseLerp(0.18f, 1f, radius));
                    var rounded = plateau * plateau * (3f - 2f * plateau);
                    vertices[index] = new Vector3(vx * safeRadiusX, rounded * height, vz * safeRadiusZ);
                    uv[index] = new Vector2(x / (float)resolution, z / (float)resolution);
                    index++;
                }
            }

            var tri = 0;
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var a = z * (resolution + 1) + x;
                    var b = a + 1;
                    var c = a + resolution + 1;
                    var d = c + 1;

                    triangles[tri++] = a;
                    triangles[tri++] = c;
                    triangles[tri++] = b;
                    triangles[tri++] = b;
                    triangles[tri++] = c;
                    triangles[tri++] = d;
                }
            }

            var mesh = new Mesh
            {
                name = "Hill4x4_ProceduralMesh",
                vertices = vertices,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void SpawnHillRockRim(Transform parent, float radiusX, float radiusZ, Vector2Int seed)
        {
            var count = Mathf.Clamp(rockRimCount, 0, 48);
            if (count == 0)
            {
                return;
            }

            var random = BuildRandom(seed.x, seed.y);
            for (var i = 0; i < count; i++)
            {
                var t = (i + (float)random.NextDouble() * 0.35f) / count;
                var angle = t * Mathf.PI * 2f;
                var radial = Mathf.Lerp(0.78f, 0.98f, (float)random.NextDouble());
                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = $"HillRock_{i:00}";
                rock.transform.SetParent(parent, false);
                rock.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radiusX * radial,
                    0.025f,
                    Mathf.Sin(angle) * radiusZ * radial);
                rock.transform.localRotation = Quaternion.Euler(
                    0f,
                    (float)random.NextDouble() * 360f,
                    (float)random.NextDouble() * 18f - 9f);
                var scale = Mathf.Lerp(rockScaleRange.x, rockScaleRange.y, (float)random.NextDouble());
                rock.transform.localScale = new Vector3(scale * 1.45f, scale * 0.55f, scale);

                var collider = rock.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                var renderer = rock.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = EnsureRockMaterial();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
            }
        }

        private void RegisterFootprintNodes(GameObject feature, IReadOnlyList<(NodeDto node, NodeView tile)> footprint)
        {
            for (var i = 0; i < footprint.Count; i++)
            {
                var nodeId = footprint[i].node.Id;
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    continue;
                }

                if (!_spawnedByNodeId.TryGetValue(nodeId, out var features))
                {
                    features = new List<GameObject>();
                    _spawnedByNodeId[nodeId] = features;
                }

                features.Add(feature);
            }
        }

        private bool IsFeatureKnown(GameObject feature, IReadOnlyDictionary<string, NodeDto> nodeStates)
        {
            foreach (var pair in _spawnedByNodeId)
            {
                var features = pair.Value;
                if (features == null || !features.Contains(feature))
                {
                    continue;
                }

                if (nodeStates.TryGetValue(pair.Key, out var node) && node != null && (node.IsVisible || node.IsMemory))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetAllVisible(bool visible)
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    _spawned[i].SetActive(visible);
                }
            }
        }

        private void EnsureFeatureRoot()
        {
            if (featureRoot != null)
            {
                return;
            }

            var existing = transform.Find("LargeTerrainFeatures");
            if (existing != null)
            {
                featureRoot = existing;
                return;
            }

            var root = new GameObject("LargeTerrainFeatures");
            root.transform.SetParent(transform, false);
            featureRoot = root.transform;
        }

        private Material EnsureHillMaterial()
        {
            if (_hillMaterial == null)
            {
                _hillMaterial = CreateMaterial("HillTerrain_Runtime", hillColor);
            }

            return _hillMaterial;
        }

        private Material EnsureRockMaterial()
        {
            if (_rockMaterial == null)
            {
                _rockMaterial = CreateMaterial("HillRock_Runtime", hillRockColor);
            }

            return _rockMaterial;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            return material;
        }

        private static System.Random BuildRandom(int x, int y)
        {
            unchecked
            {
                var seed = 20260426;
                seed = seed * 486187739 + x * 92821;
                seed = seed * 16777619 + y * 68917;
                return new System.Random(seed);
            }
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
