using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Panoptes.Presentation.Map
{
    /// <summary>
    /// Spawns non-interactive backdrop meshes around map bounds to hide outer sky horizon.
    /// </summary>
    public sealed class MapBackdropSpawner : MonoBehaviour
    {
        [Header("General")]
        [SerializeField] private bool enabledOnBuild = true;
        [SerializeField] private float outerPadding = 5f;
        [SerializeField] private float segmentSpacing = 5.6f;
        [Range(0f, 1f)] [SerializeField] private float spawnDensity = 0.78f;
        [SerializeField] private float yOffset = -0.15f;
        [SerializeField] private float globalScaleMultiplier = 0.035f;
        [SerializeField] private float targetHeight = 3.2f;
        [SerializeField] private float targetHeightJitter = 0.6f;
        [SerializeField] private Vector2 scaleRange = new Vector2(0.75f, 0.95f);
        [SerializeField] private bool randomYaw = true;
        [SerializeField] private bool disableBackdropColliders = true;
        [SerializeField] private Transform backdropRoot;

        [Header("Backdrop Prefabs")]
        [SerializeField] private GameObject[] cliffPrefabs;

        private readonly List<GameObject> _spawned = new();
        private static readonly string[] DefaultBackdropAssetPaths =
        {
            "Assets/Art/Models/Environment/Backdrop/PolyHaven/coastal_cliff_04_2k.fbx",
            "Assets/Art/Models/Environment/Backdrop/PolyHaven/mountainside_2k.fbx"
        };

        public void RebuildBackdrop(IReadOnlyDictionary<string, NodeView> tileViews)
        {
            ClearBackdrop();
            if (!enabledOnBuild || tileViews == null || tileViews.Count == 0)
            {
                return;
            }

            EnsureBackdropRoot();
            EnsureDefaultPrefabs();
            if (backdropRoot == null)
            {
                return;
            }

            if (!TryGetBounds(tileViews, out var minX, out var maxX, out var minZ, out var maxZ))
            {
                return;
            }

            minX -= outerPadding;
            maxX += outerPadding;
            minZ -= outerPadding;
            maxZ += outerPadding;

            var center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            var points = BuildPerimeterPoints(minX, maxX, minZ, maxZ, Mathf.Max(1f, segmentSpacing));
            if (points.Count == 0)
            {
                return;
            }

            var hasPrefab = cliffPrefabs != null && cliffPrefabs.Length > 0;
            for (var i = 0; i < points.Count; i++)
            {
                if (Random.value > Mathf.Clamp01(spawnDensity))
                {
                    continue;
                }

                var point = points[i];
                GameObject instance;

                if (hasPrefab)
                {
                    var prefab = cliffPrefabs[i % cliffPrefabs.Length];
                    if (prefab == null)
                    {
                        continue;
                    }

                    instance = Instantiate(prefab, backdropRoot, false);
                }
                else
                {
                    instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    instance.transform.SetParent(backdropRoot, false);
                    instance.name = "BackdropWall_Fallback";
                    instance.transform.localScale = new Vector3(2.2f, 3.8f, 0.8f);
                }

                instance.name = $"{instance.name}_Backdrop_{i}";
                var pos = point;
                pos.y += yOffset;
                instance.transform.position = pos;

                var globalScale = globalScaleMultiplier > 0f ? globalScaleMultiplier : 0.035f;
                instance.transform.localScale *= globalScale;

                var dir = (point - center);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    var yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    if (randomYaw)
                    {
                        yaw += Random.Range(-18f, 18f);
                    }
                    instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                }

                if (TryGetRendererBounds(instance, out var bounds))
                {
                    var desiredHeight = Mathf.Max(1f, targetHeight + Random.Range(-Mathf.Abs(targetHeightJitter), Mathf.Abs(targetHeightJitter)));
                    var currentHeight = Mathf.Max(0.01f, bounds.size.y);
                    var normalizeScale = Mathf.Clamp(desiredHeight / currentHeight, 0.3f, 1.8f);
                    instance.transform.localScale *= normalizeScale;
                }

                var baseScale = Random.Range(scaleRange.x, scaleRange.y);
                if (baseScale <= 0f)
                {
                    baseScale = 1f;
                }
                instance.transform.localScale *= baseScale;

                if (disableBackdropColliders)
                {
                    DisableColliders(instance);
                }

                _spawned.Add(instance);
            }
        }

        public void ClearBackdrop()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Destroy(_spawned[i]);
                }
            }

            _spawned.Clear();

            if (backdropRoot == null)
            {
                return;
            }

            for (var i = backdropRoot.childCount - 1; i >= 0; i--)
            {
                var child = backdropRoot.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void EnsureBackdropRoot()
        {
            if (backdropRoot != null)
            {
                return;
            }

            var existing = transform.Find("BackdropRoot");
            if (existing != null)
            {
                backdropRoot = existing;
                return;
            }

            var go = new GameObject("BackdropRoot");
            go.transform.SetParent(transform, false);
            backdropRoot = go.transform;
        }

        private void EnsureDefaultPrefabs()
        {
            if (cliffPrefabs != null && cliffPrefabs.Length > 0)
            {
                return;
            }

#if UNITY_EDITOR
            var defaults = new List<GameObject>(DefaultBackdropAssetPaths.Length);
            for (var i = 0; i < DefaultBackdropAssetPaths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBackdropAssetPaths[i]);
                if (prefab != null)
                {
                    defaults.Add(prefab);
                }
            }

            cliffPrefabs = defaults.ToArray();
#endif
        }

        private static bool TryGetBounds(
            IReadOnlyDictionary<string, NodeView> tileViews,
            out float minX,
            out float maxX,
            out float minZ,
            out float maxZ)
        {
            minX = float.MaxValue;
            maxX = float.MinValue;
            minZ = float.MaxValue;
            maxZ = float.MinValue;

            if (tileViews == null || tileViews.Count == 0)
            {
                return false;
            }

            foreach (var pair in tileViews)
            {
                var tile = pair.Value;
                if (tile == null)
                {
                    continue;
                }

                var p = tile.transform.position;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }

            return minX <= maxX && minZ <= maxZ;
        }

        private static List<Vector3> BuildPerimeterPoints(float minX, float maxX, float minZ, float maxZ, float spacing)
        {
            var points = new List<Vector3>(256);

            void AddEdge(float fromX, float toX, float z, bool horizontal)
            {
                var length = horizontal ? Mathf.Abs(toX - fromX) : Mathf.Abs(maxZ - minZ);
                var count = Mathf.Max(2, Mathf.CeilToInt(length / spacing));
                for (var i = 0; i <= count; i++)
                {
                    var t = count == 0 ? 0f : i / (float)count;
                    if (horizontal)
                    {
                        var x = Mathf.Lerp(fromX, toX, t);
                        points.Add(new Vector3(x, 0f, z));
                    }
                    else
                    {
                        var zz = Mathf.Lerp(minZ, maxZ, t);
                        points.Add(new Vector3(fromX, 0f, zz));
                    }
                }
            }

            AddEdge(minX, maxX, minZ, true);
            AddEdge(minX, maxX, maxZ, true);
            AddEdge(minX, minX, 0f, false);
            AddEdge(maxX, maxX, 0f, false);
            return points;
        }

        private static void DisableColliders(GameObject go)
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

        private static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            if (go == null)
            {
                return false;
            }

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            var hasBounds = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return hasBounds;
        }
    }
}
