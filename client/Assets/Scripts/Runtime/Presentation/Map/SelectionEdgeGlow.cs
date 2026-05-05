/*************************************************
 * Project: Panoptes
 * File: SelectionEdgeGlow.cs
 * Author: Panoptes Team
 * Date: 2026-05-05
 * Description: Low floor-edge selection glow shared by unit and building views.
 *************************************************/

using UnityEngine;
using UnityEngine.Rendering;

namespace Panoptes.Presentation.Map
{
    public sealed class SelectionEdgeGlow : MonoBehaviour
    {
        private const string RootName = "SelectionEdgeGlow";

        [Header("Shape")]
        [SerializeField] private Vector2 fallbackFootprint = new Vector2(1.1f, 1.1f);
        [SerializeField] private Vector2 footprintPadding = new Vector2(0.18f, 0.18f);
        [SerializeField] private float yOffset = 0.025f;
        [SerializeField] private float verticalGlowHeight = 0.22f;

        [Header("Visual")]
        [SerializeField] private Color glowColor = Color.white;
        [Range(0f, 1f)] [SerializeField] private float floorAlpha = 0.92f;
        [Range(0f, 1f)] [SerializeField] private float verticalAlpha = 0.28f;
        [SerializeField] private bool useGeneratedTexture = true;
        [SerializeField] private string textureResourcesPath = "Textures/Selection/selection_edge_glow";

        private Transform _root;
        private MeshFilter _floorFilter;
        private MeshRenderer _floorRenderer;
        private MeshFilter _verticalFilter;
        private MeshRenderer _verticalRenderer;
        private Mesh _floorMesh;
        private Mesh _verticalMesh;
        private Material _floorMaterial;
        private Material _verticalMaterial;
        private Texture2D _glowTexture;

        public void SetSelected(bool selected)
        {
            EnsureCreated();
            Refresh();

            if (_root != null)
            {
                _root.gameObject.SetActive(selected);
            }
        }

        public void Refresh()
        {
            EnsureCreated();
            ResolveFootprint(out var center, out var halfExtents, out var baseY);
            RebuildFloorMesh(center, halfExtents, baseY);
            RebuildVerticalMesh(center, halfExtents, baseY);
        }

        private void EnsureCreated()
        {
            if (_root != null)
            {
                return;
            }

            var rootObject = new GameObject(RootName);
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            _root = rootObject.transform;

            _floorMesh = new Mesh { name = "SelectionEdgeGlowFloorMesh" };
            _verticalMesh = new Mesh { name = "SelectionEdgeGlowVerticalMesh" };

            var floorObject = new GameObject("FloorTexture");
            floorObject.transform.SetParent(_root, false);
            _floorFilter = floorObject.AddComponent<MeshFilter>();
            _floorRenderer = floorObject.AddComponent<MeshRenderer>();
            _floorFilter.sharedMesh = _floorMesh;

            var verticalObject = new GameObject("LowUpwardGlow");
            verticalObject.transform.SetParent(_root, false);
            _verticalFilter = verticalObject.AddComponent<MeshFilter>();
            _verticalRenderer = verticalObject.AddComponent<MeshRenderer>();
            _verticalFilter.sharedMesh = _verticalMesh;

            _floorMaterial = CreateGlowMaterial(new Color(glowColor.r, glowColor.g, glowColor.b, floorAlpha), ResolveGlowTexture());
            _verticalMaterial = CreateGlowMaterial(new Color(glowColor.r, glowColor.g, glowColor.b, verticalAlpha), null);
            _floorRenderer.sharedMaterial = _floorMaterial;
            _verticalRenderer.sharedMaterial = _verticalMaterial;

            ConfigureRenderer(_floorRenderer);
            ConfigureRenderer(_verticalRenderer);
            _root.gameObject.SetActive(false);
        }

        private Texture2D ResolveGlowTexture()
        {
            if (!useGeneratedTexture || string.IsNullOrWhiteSpace(textureResourcesPath))
            {
                return null;
            }

            if (_glowTexture == null)
            {
                _glowTexture = Resources.Load<Texture2D>(textureResourcesPath.Trim());
            }

            return _glowTexture;
        }

        private void ResolveFootprint(out Vector3 center, out Vector2 halfExtents, out float baseY)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            var min = Vector3.zero;
            var max = Vector3.zero;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.transform == null)
                {
                    continue;
                }

                if (_root != null && renderer.transform.IsChildOf(_root))
                {
                    continue;
                }

                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var bounds = renderer.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var world = new Vector3(
                        (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    var local = transform.InverseTransformPoint(world);

                    if (!hasBounds)
                    {
                        min = local;
                        max = local;
                        hasBounds = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, local);
                        max = Vector3.Max(max, local);
                    }
                }
            }

            if (!hasBounds)
            {
                center = Vector3.zero;
                halfExtents = new Vector2(
                    Mathf.Max(0.05f, fallbackFootprint.x) * 0.5f,
                    Mathf.Max(0.05f, fallbackFootprint.y) * 0.5f);
                baseY = yOffset;
                return;
            }

            center = new Vector3((min.x + max.x) * 0.5f, 0f, (min.z + max.z) * 0.5f);
            halfExtents = new Vector2(
                Mathf.Max(fallbackFootprint.x, (max.x - min.x) + footprintPadding.x * 2f) * 0.5f,
                Mathf.Max(fallbackFootprint.y, (max.z - min.z) + footprintPadding.y * 2f) * 0.5f);
            baseY = min.y + Mathf.Max(0f, yOffset);
        }

        private void RebuildFloorMesh(Vector3 center, Vector2 halfExtents, float baseY)
        {
            if (_floorMesh == null)
            {
                return;
            }

            var y = baseY + 0.002f;
            var vertices = new[]
            {
                new Vector3(center.x - halfExtents.x, y, center.z - halfExtents.y),
                new Vector3(center.x - halfExtents.x, y, center.z + halfExtents.y),
                new Vector3(center.x + halfExtents.x, y, center.z + halfExtents.y),
                new Vector3(center.x + halfExtents.x, y, center.z - halfExtents.y)
            };

            _floorMesh.Clear();
            _floorMesh.vertices = vertices;
            _floorMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            _floorMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _floorMesh.RecalculateBounds();
        }

        private void RebuildVerticalMesh(Vector3 center, Vector2 halfExtents, float baseY)
        {
            if (_verticalMesh == null)
            {
                return;
            }

            var y0 = baseY;
            var y1 = baseY + Mathf.Max(0.02f, verticalGlowHeight);
            var x0 = center.x - halfExtents.x;
            var x1 = center.x + halfExtents.x;
            var z0 = center.z - halfExtents.y;
            var z1 = center.z + halfExtents.y;

            var vertices = new[]
            {
                new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0),
                new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1),
                new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0),
                new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1)
            };

            _verticalMesh.Clear();
            _verticalMesh.vertices = vertices;
            _verticalMesh.triangles = new[]
            {
                0, 1, 2, 0, 2, 3,
                4, 5, 6, 4, 6, 7,
                8, 9, 10, 8, 10, 11,
                12, 13, 14, 12, 14, 15
            };
            _verticalMesh.RecalculateBounds();
        }

        private static Material CreateGlowMaterial(Color color, Texture texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

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
                name = "SelectionEdgeGlowMat_Runtime",
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
            if (texture != null && material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            if (texture != null && material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }
            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 1f);
            }
            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }
            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.One);
            }
            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }
            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)CullMode.Off);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent + 20;
            return material;
        }

        private static void ConfigureRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
        }

        private void OnDestroy()
        {
            if (_floorMaterial != null)
            {
                Destroy(_floorMaterial);
            }
            if (_verticalMaterial != null)
            {
                Destroy(_verticalMaterial);
            }
            if (_floorMesh != null)
            {
                Destroy(_floorMesh);
            }
            if (_verticalMesh != null)
            {
                Destroy(_verticalMesh);
            }
        }
    }
}
