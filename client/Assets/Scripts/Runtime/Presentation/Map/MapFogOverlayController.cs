using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Panoptes.Presentation.Map
{
    /// <summary>
    /// Global observation fog layer (single renderer) to avoid per-tile fog seams.
    /// </summary>
    public sealed class MapFogOverlayController : MonoBehaviour
    {
        [Header("General")]
        [SerializeField] private bool enabledOnBuild = true;
        [SerializeField] private bool hideUnknownNodeDetails = true;
        [SerializeField] private bool hideGroundWhenUnknown = true;
        [SerializeField] private bool disablePerTileObservationFog = true;
        [SerializeField] private float overlayHeightOffset = 0.4f;
        [SerializeField] private float rebuildThrottleSeconds = 0.05f;

        [Header("Fog Appearance")]
        [SerializeField] private Color unknownFogColor = new Color(0.19f, 0.25f, 0.34f, 1f);
        [SerializeField] private Color memoryFogColor = new Color(0.40f, 0.48f, 0.58f, 1f);
        [SerializeField] private Color visibleFogColor = new Color(0.78f, 0.84f, 0.92f, 1f);
        [Range(0f, 1f)] [SerializeField] private float unknownAlpha = 1f;
        [Range(0f, 1f)] [SerializeField] private float memoryAlpha = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float visibleAlpha = 0f;
        [SerializeField] private int pixelsPerTile = 24;
        [SerializeField] private int minTextureSize = 256;
        [SerializeField] private int maxTextureSize = 2048;
        [Range(0f, 1f)] [SerializeField] private float noiseStrength = 0.12f;
        [SerializeField] private float noiseScale = 0.06f;
        [Range(0.6f, 1.8f)] [SerializeField] private float fogBrightness = 1f;

        private readonly Dictionary<string, NodeDto> _nodesById =
            new Dictionary<string, NodeDto>(StringComparer.Ordinal);
        private readonly Dictionary<Vector2Int, NodeDto> _nodesByGrid = new();
        private IReadOnlyDictionary<string, NodeView> _tileViews;
        private Texture2D _fogTexture;
        private Material _fogMaterial;
        private Renderer _fogRenderer;
        private Transform _fogTransform;
        private float _tileSize = 1f;
        private int _minGridX;
        private int _maxGridX;
        private int _minGridY;
        private int _maxGridY;
        private bool _hasGridBounds;
        private bool _dirty;
        private float _nextRebuildTime;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private struct FogSample
        {
            public Color color;
            public float alpha;
        }

        private void LateUpdate()
        {
            if (!_dirty || !enabledOnBuild)
            {
                return;
            }

            if (Time.unscaledTime < _nextRebuildTime)
            {
                return;
            }

            _nextRebuildTime = Time.unscaledTime + Mathf.Max(0.01f, rebuildThrottleSeconds);
            _dirty = false;
            RebuildOverlayTexture();
        }

        public void Rebuild(
            IReadOnlyDictionary<string, NodeView> tileViews,
            IReadOnlyDictionary<string, NodeDto> nodeStates,
            float tileSize)
        {
            _tileViews = tileViews;
            _tileSize = Mathf.Max(0.1f, tileSize);
            _nodesById.Clear();
            _nodesByGrid.Clear();

            if (nodeStates != null)
            {
                foreach (var pair in nodeStates)
                {
                    var node = pair.Value;
                    if (node == null || string.IsNullOrWhiteSpace(node.Id))
                    {
                        continue;
                    }

                    _nodesById[node.Id] = node;
                    _nodesByGrid[new Vector2Int(node.X, node.Y)] = node;
                }
            }

            RefreshGridBounds();
            ApplyNodeDetailCullingToAllTiles();

            if (!enabledOnBuild || !_hasGridBounds || _nodesById.Count == 0)
            {
                SetOverlayVisible(false);
                return;
            }

            EnsureOverlayRenderer();
            UpdateOverlayTransform();
            SetOverlayVisible(true);
            MarkDirty();
        }

        public void ConfigureUnknownCulling(bool hideUnknownDetailsEnabled, bool hideUnknownGroundEnabled)
        {
            hideUnknownNodeDetails = hideUnknownDetailsEnabled;
            hideGroundWhenUnknown = hideUnknownGroundEnabled;
            ApplyNodeDetailCullingToAllTiles();
            MarkDirty();
        }

        public void ApplyNodeSnapshot(NodeDto node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id))
            {
                return;
            }

            _nodesById[node.Id] = node;
            _nodesByGrid[new Vector2Int(node.X, node.Y)] = node;
            RefreshGridBounds();
            ApplyNodeDetailCulling(node.Id);
            MarkDirty();
        }

        public void ClearOverlay()
        {
            _nodesById.Clear();
            _nodesByGrid.Clear();
            _hasGridBounds = false;
            _dirty = false;
            SetOverlayVisible(false);
        }

        public bool IsHidingUnknownDetails => hideUnknownNodeDetails;

        public bool IsHidingUnknownGround => hideGroundWhenUnknown;

        private void MarkDirty()
        {
            _dirty = true;
            _nextRebuildTime = Time.unscaledTime;
        }

        private void SetOverlayVisible(bool visible)
        {
            if (_fogRenderer != null)
            {
                _fogRenderer.enabled = visible;
            }
        }

        private void EnsureOverlayRenderer()
        {
            if (_fogRenderer != null && _fogTransform != null)
            {
                EnsureFogMaterial();
                return;
            }

            var existing = transform.Find("GlobalObservationFog");
            if (existing != null)
            {
                _fogTransform = existing;
                _fogRenderer = existing.GetComponent<Renderer>();
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "GlobalObservationFog";
                go.transform.SetParent(transform, false);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                _fogTransform = go.transform;
                _fogRenderer = go.GetComponent<Renderer>();
            }

            EnsureFogMaterial();
            if (_fogRenderer != null && _fogMaterial != null)
            {
                _fogRenderer.sharedMaterial = _fogMaterial;
                _fogRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _fogRenderer.receiveShadows = false;
            }
        }

        private void EnsureFogMaterial()
        {
            if (_fogMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return;
            }

            _fogMaterial = new Material(shader)
            {
                name = "GlobalObservationFogMat_Runtime",
                hideFlags = HideFlags.DontSave
            };

            if (_fogMaterial.HasProperty("_Surface"))
            {
                _fogMaterial.SetFloat("_Surface", 1f);
            }
            if (_fogMaterial.HasProperty("_Blend"))
            {
                _fogMaterial.SetFloat("_Blend", 0f);
            }
            if (_fogMaterial.HasProperty("_SrcBlend"))
            {
                _fogMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }
            if (_fogMaterial.HasProperty("_DstBlend"))
            {
                _fogMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
            if (_fogMaterial.HasProperty("_ZWrite"))
            {
                _fogMaterial.SetFloat("_ZWrite", 0f);
            }
            if (_fogMaterial.HasProperty("_Cull"))
            {
                _fogMaterial.SetFloat("_Cull", 0f);
            }

            _fogMaterial.renderQueue = (int)RenderQueue.Transparent;
        }

        private void UpdateOverlayTransform()
        {
            if (_fogTransform == null || _tileViews == null || _tileViews.Count == 0)
            {
                return;
            }

            var halfTile = _tileSize * 0.5f;
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;
            var maxY = float.MinValue;

            foreach (var pair in _tileViews)
            {
                var nodeView = pair.Value;
                if (nodeView == null)
                {
                    continue;
                }

                var pos = nodeView.transform.position;
                minX = Mathf.Min(minX, pos.x - halfTile);
                maxX = Mathf.Max(maxX, pos.x + halfTile);
                minZ = Mathf.Min(minZ, pos.z - halfTile);
                maxZ = Mathf.Max(maxZ, pos.z + halfTile);
                maxY = Mathf.Max(maxY, pos.y);
            }

            if (minX > maxX || minZ > maxZ)
            {
                return;
            }

            var width = Mathf.Max(0.1f, maxX - minX);
            var height = Mathf.Max(0.1f, maxZ - minZ);
            _fogTransform.position = new Vector3(
                (minX + maxX) * 0.5f,
                maxY + overlayHeightOffset,
                (minZ + maxZ) * 0.5f);
            _fogTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _fogTransform.localScale = new Vector3(width, height, 1f);
        }

        private void RebuildOverlayTexture()
        {
            if (_fogRenderer == null || _fogMaterial == null || !_hasGridBounds)
            {
                return;
            }

            var widthInTiles = _maxGridX - _minGridX + 1;
            var heightInTiles = _maxGridY - _minGridY + 1;
            if (widthInTiles <= 0 || heightInTiles <= 0)
            {
                return;
            }

            var texWidth = Mathf.Clamp(widthInTiles * Mathf.Max(2, pixelsPerTile), minTextureSize, maxTextureSize);
            var texHeight = Mathf.Clamp(heightInTiles * Mathf.Max(2, pixelsPerTile), minTextureSize, maxTextureSize);

            EnsureFogTexture(texWidth, texHeight);
            if (_fogTexture == null)
            {
                return;
            }

            var colors = new Color32[texWidth * texHeight];
            for (var y = 0; y < texHeight; y++)
            {
                var gy = ((y + 0.5f) / texHeight) * heightInTiles - 0.5f;
                for (var x = 0; x < texWidth; x++)
                {
                    var gx = ((x + 0.5f) / texWidth) * widthInTiles - 0.5f;
                    var sample = SampleFogBilinear(gx, gy, widthInTiles, heightInTiles);

                    var noise = Mathf.PerlinNoise(x * noiseScale + 13.7f, y * noiseScale + 5.3f);
                    var shade = Mathf.Lerp(1f - noiseStrength, 1f + noiseStrength, noise);
                    var r = Mathf.Clamp01(sample.color.r * shade * fogBrightness);
                    var g = Mathf.Clamp01(sample.color.g * shade * fogBrightness);
                    var b = Mathf.Clamp01(sample.color.b * shade * fogBrightness);
                    var a = Mathf.Clamp01(sample.alpha);

                    colors[y * texWidth + x] = new Color(r, g, b, a);
                }
            }

            _fogTexture.SetPixels32(colors);
            _fogTexture.Apply(false, false);

            _fogMaterial.SetTexture(BaseMapId, _fogTexture);
            _fogMaterial.SetTexture(MainTexId, _fogTexture);
            _fogMaterial.SetColor(BaseColorId, Color.white);
            _fogMaterial.SetColor(ColorId, Color.white);
        }

        private FogSample SampleFogBilinear(float gx, float gy, int widthInTiles, int heightInTiles)
        {
            var x0 = Mathf.Clamp(Mathf.FloorToInt(gx), 0, widthInTiles - 1);
            var y0 = Mathf.Clamp(Mathf.FloorToInt(gy), 0, heightInTiles - 1);
            var x1 = Mathf.Clamp(x0 + 1, 0, widthInTiles - 1);
            var y1 = Mathf.Clamp(y0 + 1, 0, heightInTiles - 1);

            var fx = Mathf.Clamp01(gx - Mathf.Floor(gx));
            var fy = Mathf.Clamp01(gy - Mathf.Floor(gy));

            var s00 = GetNodeFogSampleByGridIndex(x0, y0);
            var s10 = GetNodeFogSampleByGridIndex(x1, y0);
            var s01 = GetNodeFogSampleByGridIndex(x0, y1);
            var s11 = GetNodeFogSampleByGridIndex(x1, y1);

            var row0 = LerpSample(s00, s10, fx);
            var row1 = LerpSample(s01, s11, fx);
            return LerpSample(row0, row1, fy);
        }

        private FogSample GetNodeFogSampleByGridIndex(int indexX, int indexY)
        {
            var gridX = _minGridX + indexX;
            var gridY = _minGridY + indexY;
            if (!_nodesByGrid.TryGetValue(new Vector2Int(gridX, gridY), out var node) || node == null)
            {
                return new FogSample
                {
                    color = unknownFogColor,
                    alpha = unknownAlpha
                };
            }

            if (node.IsVisible)
            {
                return new FogSample
                {
                    color = visibleFogColor,
                    alpha = visibleAlpha
                };
            }

            var memory = node.IsMemory;
            return new FogSample
            {
                color = memory ? memoryFogColor : unknownFogColor,
                alpha = memory ? memoryAlpha : unknownAlpha
            };
        }

        private static FogSample LerpSample(FogSample a, FogSample b, float t)
        {
            return new FogSample
            {
                color = Color.Lerp(a.color, b.color, t),
                alpha = Mathf.Lerp(a.alpha, b.alpha, t)
            };
        }

        private void EnsureFogTexture(int width, int height)
        {
            if (_fogTexture != null && _fogTexture.width == width && _fogTexture.height == height)
            {
                return;
            }

            if (_fogTexture != null)
            {
                Destroy(_fogTexture);
                _fogTexture = null;
            }

            _fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "GlobalObservationFogTex_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        private void RefreshGridBounds()
        {
            _hasGridBounds = false;
            _minGridX = 0;
            _maxGridX = 0;
            _minGridY = 0;
            _maxGridY = 0;

            foreach (var pair in _nodesById)
            {
                var node = pair.Value;
                if (node == null)
                {
                    continue;
                }

                if (!_hasGridBounds)
                {
                    _minGridX = _maxGridX = node.X;
                    _minGridY = _maxGridY = node.Y;
                    _hasGridBounds = true;
                    continue;
                }

                _minGridX = Mathf.Min(_minGridX, node.X);
                _maxGridX = Mathf.Max(_maxGridX, node.X);
                _minGridY = Mathf.Min(_minGridY, node.Y);
                _maxGridY = Mathf.Max(_maxGridY, node.Y);
            }
        }

        private void ApplyNodeDetailCullingToAllTiles()
        {
            if (_tileViews == null)
            {
                return;
            }

            foreach (var pair in _tileViews)
            {
                var tile = pair.Value;
                if (tile == null)
                {
                    continue;
                }

                if (disablePerTileObservationFog)
                {
                    tile.SetPerTileObservationFogEnabled(false);
                }
                tile.SetUnknownDetailCulling(hideUnknownNodeDetails, hideGroundWhenUnknown);
            }
        }

        private void ApplyNodeDetailCulling(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || _tileViews == null)
            {
                return;
            }

            if (!_tileViews.TryGetValue(nodeId, out var tile) || tile == null)
            {
                return;
            }

            if (disablePerTileObservationFog)
            {
                tile.SetPerTileObservationFogEnabled(false);
            }
            tile.SetUnknownDetailCulling(hideUnknownNodeDetails, hideGroundWhenUnknown);
        }

        private void OnDestroy()
        {
            if (_fogTexture != null)
            {
                Destroy(_fogTexture);
                _fogTexture = null;
            }
        }
    }
}
