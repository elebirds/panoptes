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
        [SerializeField] private bool useWarcraftLikeFogPreset = true;
        [SerializeField] private Color unknownFogColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        [SerializeField] private Color memoryFogColor = new Color(0.11f, 0.11f, 0.13f, 1f);
        [SerializeField] private Color visibleFogColor = new Color(0f, 0f, 0f, 1f);
        [Range(0f, 1f)] [SerializeField] private float unknownAlpha = 1f;
        [Range(0f, 1f)] [SerializeField] private float memoryAlpha = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float visibleAlpha = 0f;
        [SerializeField] private int pixelsPerTile = 24;
        [SerializeField] private int minTextureSize = 256;
        [SerializeField] private int maxTextureSize = 2048;
        [Range(0f, 1f)] [SerializeField] private float noiseStrength = 0.08f;
        [SerializeField] private float noiseScale = 0.06f;
        [Range(0.6f, 1.8f)] [SerializeField] private float fogBrightness = 0.9f;
        [Range(0f, 1f)] [SerializeField] private float edgeSmoothness = 0.72f;
        [SerializeField] private float cloudNoiseScaleA = 0.028f;
        [SerializeField] private float cloudNoiseScaleB = 0.053f;
        [SerializeField] private float cloudScrollSpeedA = 0.014f;
        [SerializeField] private float cloudScrollSpeedB = 0.021f;
        [Range(0f, 1f)] [SerializeField] private float memoryCloudStrength = 0.34f;
        [Range(0f, 1f)] [SerializeField] private float unknownCloudStrength = 0.14f;
        [Range(0f, 1f)] [SerializeField] private float cloudTintStrength = 0.32f;

        [Header("Texture Driven Fog")]
        [SerializeField] private bool useFogPatternTexture = true;
        [SerializeField] private Texture2D fogPatternTexture;
        [SerializeField] private string fogPatternResourcePath = "Textures/Fog/war3_fog_cloud_layers_2k";
        [SerializeField] private string fogPatternFallbackResourcePath = "Textures/Fog/fog01";
        [SerializeField] private float fogPatternTiling = 2.1f;
        [SerializeField] private Vector2 fogPatternScroll = new Vector2(0.012f, -0.009f);
        [Range(0.5f, 3f)] [SerializeField] private float fogPatternContrast = 1.35f;
        [Range(0f, 1f)] [SerializeField] private float fogPatternContribution = 0.82f;

        [Header("Atmosphere (2.5D)")]
        [SerializeField] private bool enableAtmosphereLayers = true;
        [Range(1, 3)] [SerializeField] private int atmosphereLayerCount = 2;
        [SerializeField] private float atmosphereBaseHeightOffset = 0.9f;
        [SerializeField] private float atmosphereLayerHeightStep = 0.35f;
        [SerializeField] private float atmosphereScaleExpand = 1.07f;
        [Range(0f, 1f)] [SerializeField] private float atmosphereBaseAlpha = 0.16f;
        [Range(0f, 1f)] [SerializeField] private float atmosphereAlphaFalloff = 0.62f;
        [SerializeField] private Vector2 atmosphereScrollSpeedA = new Vector2(0.008f, -0.006f);
        [SerializeField] private Vector2 atmosphereScrollSpeedB = new Vector2(-0.005f, 0.007f);
        [Range(0f, 1f)] [SerializeField] private float atmosphereVisibilityMaskStrength = 0.92f;

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
        private bool _warnedFogPatternUnavailable;
        private bool _warnedFogPatternUnreadable;
        private Texture2D _runtimeReadableFogPatternTexture;
        private readonly List<Transform> _atmosphereLayerTransforms = new List<Transform>();
        private readonly List<Renderer> _atmosphereLayerRenderers = new List<Renderer>();
        private readonly List<Material> _atmosphereLayerMaterials = new List<Material>();
        private float _overlayWidth = 1f;
        private float _overlayHeight = 1f;
        private float _overlayTopY;
        private Vector3 _overlayCenter;

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
            if (enabledOnBuild && enableAtmosphereLayers)
            {
                UpdateAtmosphereLayerAnimation(Time.unscaledTime);
            }

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

            for (var i = 0; i < _atmosphereLayerRenderers.Count; i++)
            {
                var layerRenderer = _atmosphereLayerRenderers[i];
                if (layerRenderer != null)
                {
                    layerRenderer.enabled = visible && enableAtmosphereLayers;
                }
            }
        }

        private void EnsureOverlayRenderer()
        {
            if (_fogRenderer != null && _fogTransform != null)
            {
                EnsureFogMaterial();
                EnsureAtmosphereRenderers();
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

            EnsureAtmosphereRenderers();
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

        private void EnsureAtmosphereRenderers()
        {
            if (!enableAtmosphereLayers || _fogTransform == null)
            {
                for (var i = 0; i < _atmosphereLayerRenderers.Count; i++)
                {
                    var renderer = _atmosphereLayerRenderers[i];
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }
                }
                return;
            }

            var desiredCount = Mathf.Clamp(atmosphereLayerCount, 1, 3);
            while (_atmosphereLayerTransforms.Count < desiredCount)
            {
                CreateAtmosphereLayer(_atmosphereLayerTransforms.Count);
            }

            while (_atmosphereLayerTransforms.Count > desiredCount)
            {
                var index = _atmosphereLayerTransforms.Count - 1;
                RemoveAtmosphereLayerAt(index);
            }

            for (var i = 0; i < _atmosphereLayerRenderers.Count; i++)
            {
                var renderer = _atmosphereLayerRenderers[i];
                if (renderer != null)
                {
                    renderer.enabled = true;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }

            UpdateAtmosphereLayerTransforms();
            SyncAtmosphereLayerTextures();
        }

        private void CreateAtmosphereLayer(int index)
        {
            var layerName = $"GlobalObservationFogAtmosphere_{index}";
            var existing = transform.Find(layerName);
            Transform layerTransform;
            Renderer layerRenderer;
            if (existing != null)
            {
                layerTransform = existing;
                layerRenderer = existing.GetComponent<Renderer>();
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = layerName;
                go.transform.SetParent(transform, false);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                layerTransform = go.transform;
                layerRenderer = go.GetComponent<Renderer>();
            }

            if (layerRenderer == null)
            {
                return;
            }

            var shader = _fogMaterial != null ? _fogMaterial.shader : Shader.Find("Universal Render Pipeline/Unlit");
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

            var layerMaterial = new Material(shader)
            {
                name = $"GlobalObservationFogAtmosphereMat_{index}",
                hideFlags = HideFlags.DontSave
            };

            if (layerMaterial.HasProperty("_Surface"))
            {
                layerMaterial.SetFloat("_Surface", 1f);
            }
            if (layerMaterial.HasProperty("_Blend"))
            {
                layerMaterial.SetFloat("_Blend", 0f);
            }
            if (layerMaterial.HasProperty("_SrcBlend"))
            {
                layerMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }
            if (layerMaterial.HasProperty("_DstBlend"))
            {
                layerMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
            if (layerMaterial.HasProperty("_ZWrite"))
            {
                layerMaterial.SetFloat("_ZWrite", 0f);
            }
            if (layerMaterial.HasProperty("_Cull"))
            {
                layerMaterial.SetFloat("_Cull", 0f);
            }

            layerMaterial.renderQueue = (int)RenderQueue.Transparent;
            layerRenderer.sharedMaterial = layerMaterial;

            _atmosphereLayerTransforms.Add(layerTransform);
            _atmosphereLayerRenderers.Add(layerRenderer);
            _atmosphereLayerMaterials.Add(layerMaterial);
        }

        private void RemoveAtmosphereLayerAt(int index)
        {
            if (index < 0 || index >= _atmosphereLayerTransforms.Count)
            {
                return;
            }

            var material = _atmosphereLayerMaterials[index];
            if (material != null)
            {
                Destroy(material);
            }

            var transformToRemove = _atmosphereLayerTransforms[index];
            if (transformToRemove != null)
            {
                Destroy(transformToRemove.gameObject);
            }

            _atmosphereLayerTransforms.RemoveAt(index);
            _atmosphereLayerRenderers.RemoveAt(index);
            _atmosphereLayerMaterials.RemoveAt(index);
        }

        private void UpdateAtmosphereLayerTransforms()
        {
            if (!enableAtmosphereLayers)
            {
                return;
            }

            var width = Mathf.Max(0.1f, _overlayWidth * atmosphereScaleExpand);
            var height = Mathf.Max(0.1f, _overlayHeight * atmosphereScaleExpand);
            for (var i = 0; i < _atmosphereLayerTransforms.Count; i++)
            {
                var layerTransform = _atmosphereLayerTransforms[i];
                if (layerTransform == null)
                {
                    continue;
                }

                var layerScaleFactor = 1f + (i * 0.025f);
                var y = _overlayTopY + atmosphereBaseHeightOffset + (i * atmosphereLayerHeightStep);
                layerTransform.position = new Vector3(_overlayCenter.x, y, _overlayCenter.z);
                layerTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
                layerTransform.localScale = new Vector3(width * layerScaleFactor, height * layerScaleFactor, 1f);
            }
        }

        private void SyncAtmosphereLayerTextures()
        {
            if (!enableAtmosphereLayers || _fogTexture == null)
            {
                return;
            }

            for (var i = 0; i < _atmosphereLayerMaterials.Count; i++)
            {
                var material = _atmosphereLayerMaterials[i];
                if (material == null)
                {
                    continue;
                }

                var alpha = atmosphereBaseAlpha * Mathf.Pow(Mathf.Clamp01(atmosphereAlphaFalloff), i);
                alpha *= Mathf.Clamp01(atmosphereVisibilityMaskStrength);
                var color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

                material.SetTexture(BaseMapId, _fogTexture);
                material.SetTexture(MainTexId, _fogTexture);
                material.SetColor(BaseColorId, color);
                material.SetColor(ColorId, color);
            }
        }

        private void UpdateAtmosphereLayerAnimation(float time)
        {
            if (!enableAtmosphereLayers || _atmosphereLayerMaterials.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _atmosphereLayerMaterials.Count; i++)
            {
                var material = _atmosphereLayerMaterials[i];
                if (material == null)
                {
                    continue;
                }

                var speedMix = Mathf.Lerp(0.45f, 1f, i / Mathf.Max(1f, _atmosphereLayerMaterials.Count - 1f));
                var offset = (atmosphereScrollSpeedA * time) + (atmosphereScrollSpeedB * time * speedMix);
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTextureOffset("_BaseMap", offset);
                }
                if (material.HasProperty("_MainTex"))
                {
                    material.SetTextureOffset("_MainTex", offset);
                }
            }
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
            _overlayCenter = new Vector3(
                (minX + maxX) * 0.5f,
                maxY + overlayHeightOffset,
                (minZ + maxZ) * 0.5f);
            _overlayTopY = maxY + overlayHeightOffset;
            _overlayWidth = width;
            _overlayHeight = height;

            _fogTransform.position = _overlayCenter;
            _fogTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _fogTransform.localScale = new Vector3(width, height, 1f);
            UpdateAtmosphereLayerTransforms();
        }

        private void RebuildOverlayTexture()
        {
            ApplyWarcraftLikeFogPresetIfNeeded();

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
            var t = Time.unscaledTime;
            for (var y = 0; y < texHeight; y++)
            {
                var gy = ((y + 0.5f) / texHeight) * heightInTiles - 0.5f;
                for (var x = 0; x < texWidth; x++)
                {
                    var gx = ((x + 0.5f) / texWidth) * widthInTiles - 0.5f;
                    var sample = SampleFogBilinear(gx, gy, widthInTiles, heightInTiles);
                    sample.alpha = Mathf.Lerp(sample.alpha, Mathf.SmoothStep(0f, 1f, sample.alpha), edgeSmoothness);

                    var detailNoise = Mathf.PerlinNoise(x * noiseScale + 13.7f, y * noiseScale + 5.3f);
                    var cloudA = Mathf.PerlinNoise(
                        x * cloudNoiseScaleA + t * cloudScrollSpeedA,
                        y * cloudNoiseScaleA - t * cloudScrollSpeedA * 0.7f);
                    var cloudB = Mathf.PerlinNoise(
                        x * cloudNoiseScaleB - t * cloudScrollSpeedB * 0.6f,
                        y * cloudNoiseScaleB + t * cloudScrollSpeedB);
                    var cloud = Mathf.Clamp01(cloudA * 0.62f + cloudB * 0.38f);
                    cloud = Mathf.SmoothStep(0.2f, 0.9f, cloud);

                    var patternCloud = SampleFogPattern((x + 0.5f) / texWidth, (y + 0.5f) / texHeight, t);
                    if (patternCloud >= 0f)
                    {
                        cloud = Mathf.Lerp(cloud, patternCloud, fogPatternContribution);
                    }

                    var likelyUnknown = sample.alpha >= (memoryAlpha + unknownAlpha) * 0.5f;
                    var cloudStrength = likelyUnknown ? unknownCloudStrength : memoryCloudStrength;
                    var detailShade = Mathf.Lerp(1f - noiseStrength, 1f + noiseStrength, detailNoise);
                    var cloudShade = Mathf.Lerp(1f - cloudStrength, 1f + cloudStrength, cloud);
                    var baseColor = sample.color;

                    if (!likelyUnknown)
                    {
                        var tint = new Color(0.06f, 0.09f, 0.14f, 1f);
                        baseColor = Color.Lerp(baseColor, baseColor + tint, cloudTintStrength * cloud);
                    }

                    var alpha = sample.alpha;
                    if (alpha > 0.001f)
                    {
                        alpha = Mathf.Clamp01(alpha * Mathf.Lerp(0.9f, 1.08f, cloud * cloudStrength));
                    }

                    var finalShade = detailShade * cloudShade;
                    if (likelyUnknown)
                    {
                        finalShade *= 0.9f;
                    }

                    var r = Mathf.Clamp01(baseColor.r * finalShade * fogBrightness);
                    var g = Mathf.Clamp01(baseColor.g * finalShade * fogBrightness);
                    var b = Mathf.Clamp01(baseColor.b * finalShade * fogBrightness);
                    var a = Mathf.Clamp01(alpha);

                    colors[y * texWidth + x] = new Color(r, g, b, a);
                }
            }

            _fogTexture.SetPixels32(colors);
            _fogTexture.Apply(false, false);

            _fogMaterial.SetTexture(BaseMapId, _fogTexture);
            _fogMaterial.SetTexture(MainTexId, _fogTexture);
            _fogMaterial.SetColor(BaseColorId, Color.white);
            _fogMaterial.SetColor(ColorId, Color.white);
            SyncAtmosphereLayerTextures();
        }

        private void ApplyWarcraftLikeFogPresetIfNeeded()
        {
            if (!useWarcraftLikeFogPreset)
            {
                return;
            }

            unknownFogColor = new Color(0.01f, 0.01f, 0.015f, 1f);
            memoryFogColor = new Color(0.07f, 0.09f, 0.12f, 1f);
            visibleFogColor = new Color(0f, 0f, 0f, 1f);
            unknownAlpha = 0.96f;
            memoryAlpha = 0.52f;
            visibleAlpha = 0f;
            noiseStrength = 0.06f;
            noiseScale = 0.045f;
            fogBrightness = 0.8f;
            edgeSmoothness = 0.78f;
            memoryCloudStrength = 0.36f;
            unknownCloudStrength = 0.12f;
            cloudTintStrength = 0.34f;
            fogPatternTiling = 2.1f;
            fogPatternScroll = new Vector2(0.012f, -0.009f);
            fogPatternContrast = 1.35f;
            fogPatternContribution = 0.82f;
            enableAtmosphereLayers = true;
            atmosphereLayerCount = 2;
            atmosphereBaseHeightOffset = 0.9f;
            atmosphereLayerHeightStep = 0.35f;
            atmosphereScaleExpand = 1.07f;
            atmosphereBaseAlpha = 0.16f;
            atmosphereAlphaFalloff = 0.62f;
            atmosphereScrollSpeedA = new Vector2(0.008f, -0.006f);
            atmosphereScrollSpeedB = new Vector2(-0.005f, 0.007f);
            atmosphereVisibilityMaskStrength = 0.92f;
        }

        private float SampleFogPattern(float u, float v, float t)
        {
            if (!useFogPatternTexture)
            {
                return -1f;
            }

            var texture = ResolveFogPatternTexture();
            if (texture == null)
            {
                return -1f;
            }

            var uv1 = new Vector2(
                Mathf.Repeat(u * fogPatternTiling + t * fogPatternScroll.x, 1f),
                Mathf.Repeat(v * fogPatternTiling + t * fogPatternScroll.y, 1f));
            var uv2 = new Vector2(
                Mathf.Repeat(v * (fogPatternTiling * 0.83f) - t * fogPatternScroll.y * 1.27f + 0.37f, 1f),
                Mathf.Repeat(u * (fogPatternTiling * 0.83f) + t * fogPatternScroll.x * 1.19f + 0.19f, 1f));

            var c1 = texture.GetPixelBilinear(uv1.x, uv1.y);
            var c2 = texture.GetPixelBilinear(uv2.x, uv2.y);
            var lum = Mathf.Clamp01((((c1.r + c1.g + c1.b) / 3f) * 0.62f) + (((c2.r + c2.g + c2.b) / 3f) * 0.38f));
            lum = Mathf.Clamp01((lum - 0.5f) * fogPatternContrast + 0.5f);
            return lum;
        }

        private Texture2D ResolveFogPatternTexture()
        {
            if (fogPatternTexture == null && !string.IsNullOrWhiteSpace(fogPatternResourcePath))
            {
                fogPatternTexture = Resources.Load<Texture2D>(fogPatternResourcePath.Trim());
            }

            if (fogPatternTexture == null && !string.IsNullOrWhiteSpace(fogPatternFallbackResourcePath))
            {
                fogPatternTexture = Resources.Load<Texture2D>(fogPatternFallbackResourcePath.Trim());
            }

            if (fogPatternTexture == null)
            {
                if (!_warnedFogPatternUnavailable)
                {
                    _warnedFogPatternUnavailable = true;
                    Debug.LogWarning($"[MapFogOverlay] Fog pattern not found at Resources/{fogPatternResourcePath} or fallback {fogPatternFallbackResourcePath}. Fallback to procedural fog.");
                }
                return null;
            }

            if (!fogPatternTexture.isReadable)
            {
                var readableCopy = EnsureReadableTextureCopy(fogPatternTexture);
                if (readableCopy != null)
                {
                    return readableCopy;
                }

                if (!_warnedFogPatternUnreadable)
                {
                    _warnedFogPatternUnreadable = true;
                    Debug.LogWarning($"[MapFogOverlay] Fog pattern '{fogPatternTexture.name}' is not Read/Write enabled. Fallback to procedural fog.");
                }

                return null;
            }

            return fogPatternTexture;
        }

        private Texture2D EnsureReadableTextureCopy(Texture2D source)
        {
            if (source == null)
            {
                return null;
            }

            if (_runtimeReadableFogPatternTexture != null &&
                _runtimeReadableFogPatternTexture.width == source.width &&
                _runtimeReadableFogPatternTexture.height == source.height)
            {
                return _runtimeReadableFogPatternTexture;
            }

            if (_runtimeReadableFogPatternTexture != null)
            {
                Destroy(_runtimeReadableFogPatternTexture);
                _runtimeReadableFogPatternTexture = null;
            }

            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                tex.name = $"{source.name}_ReadableCopy";
                tex.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                tex.Apply(false, false);
                _runtimeReadableFogPatternTexture = tex;
                return _runtimeReadableFogPatternTexture;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
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
            if (_runtimeReadableFogPatternTexture != null)
            {
                Destroy(_runtimeReadableFogPatternTexture);
                _runtimeReadableFogPatternTexture = null;
            }

            for (var i = 0; i < _atmosphereLayerMaterials.Count; i++)
            {
                var material = _atmosphereLayerMaterials[i];
                if (material != null)
                {
                    Destroy(material);
                }
            }
            _atmosphereLayerMaterials.Clear();

            for (var i = 0; i < _atmosphereLayerTransforms.Count; i++)
            {
                var layerTransform = _atmosphereLayerTransforms[i];
                if (layerTransform != null)
                {
                    Destroy(layerTransform.gameObject);
                }
            }
            _atmosphereLayerTransforms.Clear();
            _atmosphereLayerRenderers.Clear();

            if (_fogMaterial != null)
            {
                Destroy(_fogMaterial);
                _fogMaterial = null;
            }

            if (_fogTexture != null)
            {
                Destroy(_fogTexture);
                _fogTexture = null;
            }
        }
    }
}
