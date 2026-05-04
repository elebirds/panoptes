/*************************************************
 * Project: Panoptes
 * File: NodeView.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: 3D tile node view.
 *************************************************/

using UnityEngine;
using Panoptes.Core.Domain;
using System.Collections.Generic;

namespace Panoptes.Presentation.Map
{
    /// <summary>
    /// Runtime visual controller for one grid tile.
    /// Attach this on NodeTile3D prefab root.
    /// </summary>
    public sealed class NodeView : MonoBehaviour
    {
        [System.Serializable]
        private struct BuildingPrefabEntry
        {
            public string buildingType;
            public BuildingView prefab;
        }

        [Header("Core References")]
        [SerializeField] private Renderer groundRenderer;
        [SerializeField] private GameObject roadOverlay;
        [SerializeField] private GameObject highlight;
        [SerializeField] private Renderer highlightRenderer;
        [SerializeField] private bool forceHighlightBelowDetails = true;
        [SerializeField] private float highlightLocalY = 0.015f;
        [SerializeField] private Transform resourceAnchor;
        [SerializeField] private Transform buildingAnchor;
        [SerializeField] private Transform unitAnchor;

        [Header("Terrain Materials")]
        [SerializeField] private Material plainMaterial;
        [SerializeField] private Material mountainMaterial;
        [SerializeField] private Material forestMaterial;
        [SerializeField] private Material riverMaterial;
        [SerializeField] private Material snowMaterial;
        [SerializeField] private Material forbiddenMaterial;
        
        [Header("Terrain UV Continuity")]
        [SerializeField] private bool useContinuousTerrainUv = true;
        [SerializeField] private float plainUvWorldSize = 2.6f;
        [SerializeField] private float mountainUvWorldSize = 2.1f;
        [SerializeField] private float forestUvWorldSize = 2.4f;
        [SerializeField] private float riverUvWorldSize = 3.2f;
        [SerializeField] private float snowUvWorldSize = 2.8f;
        [SerializeField] private float forbiddenUvWorldSize = 2.2f;

        [Header("Terrain Color Variation")]
        [SerializeField] private bool useTerrainColorVariation = false;
        [Range(0f, 0.3f)]
        [SerializeField] private float terrainColorVariationStrength = 0.02f;

        [Header("Observation")]
        [SerializeField] private bool enableObservationTint = true;
        [SerializeField] private Color visibleObservationTint = Color.white;
        [SerializeField] private Color memoryObservationTint = new Color(0.72f, 0.72f, 0.72f, 1f);
        [SerializeField] private Color unknownObservationTint = new Color(0.46f, 0.46f, 0.46f, 1f);
        [SerializeField] private bool hideUnknownDetails = true;
        [SerializeField] private bool hideGroundWhenUnknown = true;

        [Header("Observation Fog Overlay")]
        [SerializeField] private bool enableObservationFogOverlay = false;
        [SerializeField] private bool autoCreateFogOverlay = true;
        [SerializeField] private Renderer fogOverlayRenderer;
        [SerializeField] private Texture2D fogOverlayTexture;
        [SerializeField] private string fogOverlayTextureResourcesPath = "Textures/Fog/fog01";
        [SerializeField] private Color fogOverlayColor = new Color(1f, 1f, 1f, 1f);
        [Range(0f, 1f)] [SerializeField] private float fogUnknownAlpha = 0.82f;
        [Range(0f, 1f)] [SerializeField] private float fogMemoryAlpha = 0.42f;
        [Range(0f, 1f)] [SerializeField] private float fogVisibleAlpha = 0f;
        [SerializeField] private float fogUvWorldSize = 3.2f;
        [SerializeField] private Vector2 fogUvScrollSpeed = new Vector2(0.01f, 0.006f);
        [SerializeField] private float fogUvUpdateInterval = 0.08f;
        [SerializeField] private float fogOverlayHeight = 0.03f;
        [SerializeField] private Vector2 fogOverlayScale = new Vector2(1f, 1f);

        [Header("Resource")]
        [SerializeField] private ResourcePointView resourcePointPrefab;

        [Header("Building Prefabs")]
        [SerializeField] private BuildingView defaultBuildingPrefab;
        [SerializeField] private BuildingPrefabEntry[] buildingPrefabs;
        [SerializeField] private string buildingPrefabResourcesRoot = "Prefabs/Buildings";

        [Header("Move Preview Marker")]
        [SerializeField] private bool autoCreateMoveMarker = true;
        [SerializeField] private float moveMarkerY = 0.13f;
        [SerializeField] private Vector3 moveArrowScale = new Vector3(0.12f, 0.01f, 0.42f);
        [SerializeField] private float moveArrowHeadScale = 0.15f;
        [SerializeField] private Sprite moveArrowSprite;
        [SerializeField] private string moveArrowSpriteResourcesPath = "Textures/Map/GreenMoveArrow";
        [SerializeField] private Vector2 moveArrowSpriteScale = new Vector2(0.72f, 0.72f);
        [SerializeField] private float moveDestinationScale = 0.24f;
        [SerializeField] private Color moveMarkerDefaultColor = new Color(0.35f, 1f, 0.45f, 0.9f);

        public string NodeId { get; private set; } = string.Empty;
        public Vector2Int GridPos { get; private set; }
        public Transform UnitAnchor => unitAnchor;
        public Transform ResourceAnchor => resourceAnchor;
        public Transform BuildingAnchor => buildingAnchor;
        public BuildingView BuildingInstance => _buildingInstance;
        public string BuildingType => _buildingType;
        public string BuildingStatus { get; private set; } = string.Empty;
        public string CityId { get; private set; } = string.Empty;
        public string ServiceCityId { get; private set; } = string.Empty;
        public int TakeoverProgress { get; private set; }
        public int TakeoverRequired { get; private set; }
        public bool IsCityCoreNode { get; private set; }
        public bool IsSafeZoneNode { get; private set; }
        public bool IsCurrentlyVisible => _isCurrentlyVisible;
        public bool IsMemoryVisible => _isMemoryVisible;

        private ResourcePointView _resourceInstance;
        private string _resourceType = string.Empty;
        private BuildingView _buildingInstance;
        private string _buildingType = string.Empty;
        private string _localPlayerId = string.Empty;
        private bool _roadVisibleWanted;
        private bool _resourceVisibleWanted;
        private bool _isCurrentlyVisible = true;
        private bool _isMemoryVisible;
        private MaterialPropertyBlock _highlightBlock;
        private Transform _moveMarkerRoot;
        private Transform _moveArrowRoot;
        private Renderer _moveArrowShaftRenderer;
        private Renderer _moveArrowHeadLeftRenderer;
        private Renderer _moveArrowHeadRightRenderer;
        private Renderer _moveArrowSpriteRenderer;
        private Renderer _moveDestinationRenderer;
        private MaterialPropertyBlock _moveMarkerBlock;
        private Material _moveMarkerMaterial;
        private Material _moveMarkerSpriteMaterial;
        private MaterialPropertyBlock _groundBlock;
        private MaterialPropertyBlock _fogOverlayBlock;
        private Material _fogOverlayMaterial;
        private Mesh _fogOverlayMeshInstance;
        private Vector2[] _fogOverlayBaseUv;
        private float _currentFogAlpha;
        private float _nextFogUvUpdateTime;
        private bool _fogTextureLoadAttempted;
        private bool _moveArrowSpriteLoadAttempted;
        private readonly System.Collections.Generic.Dictionary<string, BuildingView> _runtimeBuildingPrefabCache =
            new System.Collections.Generic.Dictionary<string, BuildingView>(System.StringComparer.OrdinalIgnoreCase);
        private IReadOnlyDictionary<string, CatalogBuildingDto> _buildingCatalog;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int BumpMapStId = Shader.PropertyToID("_BumpMap_ST");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static Material SharedFogOverlayMaterial;

        private void Awake()
        {
            EnsureHighlightBlock();
            if (enableObservationFogOverlay)
            {
                EnsureObservationFogOverlay();
            }
        }

        private void Update()
        {
            if (enableObservationFogOverlay)
            {
                UpdateObservationFogAnimation();
            }
        }

        private void OnDestroy()
        {
            _fogOverlayMaterial = null;
            if (_fogOverlayMeshInstance != null)
            {
                Destroy(_fogOverlayMeshInstance);
                _fogOverlayMeshInstance = null;
            }
            _fogOverlayBaseUv = null;
        }

        /// <summary>
        /// Bind visual from node dto data.
        /// </summary>
        public void Bind(NodeDto node)
        {
            if (node == null)
            {
                Debug.LogWarning("[NodeView] Bind called with null node.");
                return;
            }

            NodeId = node.Id ?? string.Empty;
            GridPos = new Vector2Int(node.Q, node.R);
            name = $"Node_{NodeId}";
            BuildingStatus = NormalizeToken(node.BuildingStatus);
            CityId = node.CityId ?? string.Empty;
            ServiceCityId = node.ServiceCityId ?? string.Empty;
            TakeoverProgress = node.TakeoverProgress;
            TakeoverRequired = node.TakeoverRequired;
            IsCityCoreNode = node.IsCityCore;
            IsSafeZoneNode = node.IsSafeZone;

            SetTerrain(string.IsNullOrWhiteSpace(node.Terrain) ? node.Type : node.Terrain);
            SetRoadVisible(node.HasRoad);
            SetResource(node.IsResourcePoint, node.ResourceType);
            SetBuilding(node.BuildingType, node.Owner, node.BuildingHp, node.BuildingMaxHp, false);
            if (_buildingInstance != null)
            {
                _buildingInstance.ApplyRuntimeState(node);
                _buildingInstance.SetObservationState(node.IsVisible, node.IsMemory);
            }
            ApplyObservationState(node);
            SetHighlightVisible(false);
        }

        public void SetLocalPlayerId(string localPlayerId)
        {
            _localPlayerId = localPlayerId ?? string.Empty;
            if (_buildingInstance != null)
            {
                _buildingInstance.SetLocalPlayerId(_localPlayerId);
            }
        }

        public void SetBuildingCatalog(IReadOnlyDictionary<string, CatalogBuildingDto> buildingCatalog)
        {
            _buildingCatalog = buildingCatalog;
            _runtimeBuildingPrefabCache.Clear();
        }

        /// <summary>
        /// Set tile terrain material by protocol terrain string.
        /// </summary>
        public void SetTerrain(string terrain)
        {
            if (groundRenderer == null)
            {
                Debug.LogWarning($"[NodeView:{NodeId}] Missing ground renderer.");
                return;
            }

            var material = GetTerrainMaterial(terrain);
            if (material != null)
            {
                groundRenderer.sharedMaterial = material;
            }

            ApplyContinuousTerrainUv(terrain);
        }

        public void SetRoadVisible(bool isVisible)
        {
            _roadVisibleWanted = isVisible;
            if (roadOverlay != null)
            {
                roadOverlay.SetActive(isVisible);
            }
            ApplyObservationDetailVisibility();
        }

        private void ApplyObservationState(NodeDto node)
        {
            if (node == null)
            {
                return;
            }

            _isCurrentlyVisible = node.IsVisible;
            _isMemoryVisible = node.IsMemory;

            if (enableObservationTint && groundRenderer != null)
            {
                if (_groundBlock == null)
                {
                    _groundBlock = new MaterialPropertyBlock();
                }

                var baseColor = Color.white;
                var groundMaterial = groundRenderer.sharedMaterial;
                if (groundMaterial != null)
                {
                    if (groundMaterial.HasProperty(BaseColorId))
                    {
                        baseColor = groundMaterial.GetColor(BaseColorId);
                    }
                    else if (groundMaterial.HasProperty(ColorId))
                    {
                        baseColor = groundMaterial.GetColor(ColorId);
                    }
                }

                var tint = visibleObservationTint;
                if (!node.IsVisible)
                {
                    tint = node.IsMemory ? memoryObservationTint : unknownObservationTint;
                }
                var finalColor = new Color(
                    Mathf.Clamp01(baseColor.r * tint.r),
                    Mathf.Clamp01(baseColor.g * tint.g),
                    Mathf.Clamp01(baseColor.b * tint.b),
                    baseColor.a);

                groundRenderer.GetPropertyBlock(_groundBlock);
                _groundBlock.SetColor(BaseColorId, finalColor);
                _groundBlock.SetColor(ColorId, finalColor);
                groundRenderer.SetPropertyBlock(_groundBlock);
            }

            ApplyObservationFogState(ResolveObservationFogAlpha(node), forceUvRefresh: true);
            ApplyObservationDetailVisibility();
        }

        private float ResolveObservationFogAlpha(NodeDto node)
        {
            if (node == null)
            {
                return fogUnknownAlpha;
            }

            if (node.IsVisible)
            {
                return Mathf.Clamp01(fogVisibleAlpha);
            }

            return Mathf.Clamp01(node.IsMemory ? fogMemoryAlpha : fogUnknownAlpha);
        }

        private void ApplyObservationFogState(float alpha, bool forceUvRefresh)
        {
            _currentFogAlpha = Mathf.Clamp01(alpha);

            if (!enableObservationFogOverlay)
            {
                if (fogOverlayRenderer != null)
                {
                    fogOverlayRenderer.enabled = false;
                }
                return;
            }

            if (!EnsureObservationFogOverlay())
            {
                return;
            }

            UpdateFogOverlayVisual(_currentFogAlpha, forceUvRefresh);
            if (fogOverlayRenderer != null)
            {
                fogOverlayRenderer.enabled = _currentFogAlpha > 0.001f;
            }
        }

        private void UpdateObservationFogAnimation()
        {
            if (!enableObservationFogOverlay || fogOverlayRenderer == null || !fogOverlayRenderer.enabled)
            {
                return;
            }

            if (Mathf.Abs(fogUvScrollSpeed.x) <= 0.00001f && Mathf.Abs(fogUvScrollSpeed.y) <= 0.00001f)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now < _nextFogUvUpdateTime)
            {
                return;
            }

            _nextFogUvUpdateTime = now + Mathf.Max(0.01f, fogUvUpdateInterval);
            UpdateFogOverlayVisual(_currentFogAlpha, forceUvRefresh: true);
        }

        private bool EnsureObservationFogOverlay()
        {
            if (fogOverlayRenderer == null && autoCreateFogOverlay)
            {
                var fogGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                fogGo.name = "ObservationFogOverlay";
                fogGo.transform.SetParent(transform, false);
                fogGo.transform.localPosition = new Vector3(0f, fogOverlayHeight, 0f);
                fogGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                fogGo.transform.localScale = new Vector3(
                    Mathf.Max(0.01f, fogOverlayScale.x),
                    Mathf.Max(0.01f, fogOverlayScale.y),
                    1f);
                DestroyRuntimeCollider(fogGo);
                fogOverlayRenderer = fogGo.GetComponent<Renderer>();
            }

            if (fogOverlayRenderer == null)
            {
                return false;
            }

            EnsureFogOverlayMaterial();
            if (_fogOverlayMaterial == null)
            {
                return false;
            }

            EnsureFogOverlayMeshUv();
            fogOverlayRenderer.sharedMaterial = _fogOverlayMaterial;
            fogOverlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fogOverlayRenderer.receiveShadows = false;
            return true;
        }

        private void EnsureFogOverlayMeshUv()
        {
            if (fogOverlayRenderer == null)
            {
                return;
            }

            var meshFilter = fogOverlayRenderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            if (_fogOverlayMeshInstance == null)
            {
                _fogOverlayMeshInstance = Instantiate(meshFilter.sharedMesh);
                _fogOverlayMeshInstance.name = "NodeFogOverlayMesh_Runtime";
                _fogOverlayMeshInstance.hideFlags = HideFlags.DontSave;
                meshFilter.sharedMesh = _fogOverlayMeshInstance;
                var originalUv = _fogOverlayMeshInstance.uv;
                if (originalUv != null && originalUv.Length > 0)
                {
                    _fogOverlayBaseUv = (Vector2[])originalUv.Clone();
                }
            }

            var sourceUv = _fogOverlayBaseUv;
            if (sourceUv == null || sourceUv.Length == 0)
            {
                return;
            }

            var worldSize = Mathf.Max(0.01f, fogUvWorldSize);

            var tileHalfX = Mathf.Max(0.01f, fogOverlayScale.x) * 0.5f;
            var tileHalfY = Mathf.Max(0.01f, fogOverlayScale.y) * 0.5f;
            var worldPos = transform.position;
            var minX = worldPos.x - tileHalfX;
            var maxX = worldPos.x + tileHalfX;
            var minZ = worldPos.z - tileHalfY;
            var maxZ = worldPos.z + tileHalfY;

            var u0 = minX / worldSize;
            var u1 = maxX / worldSize;
            var v0 = minZ / worldSize;
            var v1 = maxZ / worldSize;

            var mappedUv = new Vector2[sourceUv.Length];
            for (int i = 0; i < sourceUv.Length; i++)
            {
                var uv = sourceUv[i];
                mappedUv[i] = new Vector2(
                    Mathf.Lerp(u0, u1, uv.x),
                    Mathf.Lerp(v0, v1, uv.y));
            }

            _fogOverlayMeshInstance.uv = mappedUv;
        }

        private void EnsureFogOverlayMaterial()
        {
            if (_fogOverlayMaterial == null && SharedFogOverlayMaterial != null)
            {
                _fogOverlayMaterial = SharedFogOverlayMaterial;
            }

            if (_fogOverlayMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
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

            _fogOverlayMaterial = new Material(shader)
            {
                name = "NodeFogOverlayMat_Runtime",
                hideFlags = HideFlags.DontSave
            };
            SharedFogOverlayMaterial = _fogOverlayMaterial;

            if (_fogOverlayMaterial.HasProperty("_Surface"))
            {
                _fogOverlayMaterial.SetFloat("_Surface", 1f);
            }
            if (_fogOverlayMaterial.HasProperty("_Blend"))
            {
                _fogOverlayMaterial.SetFloat("_Blend", 0f);
            }
            if (_fogOverlayMaterial.HasProperty("_SrcBlend"))
            {
                _fogOverlayMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }
            if (_fogOverlayMaterial.HasProperty("_DstBlend"))
            {
                _fogOverlayMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            if (_fogOverlayMaterial.HasProperty("_ZWrite"))
            {
                _fogOverlayMaterial.SetFloat("_ZWrite", 0f);
            }
            if (_fogOverlayMaterial.HasProperty("_Cull"))
            {
                _fogOverlayMaterial.SetFloat("_Cull", 0f);
            }
            if (_fogOverlayMaterial.HasProperty(BaseMapId))
            {
                _fogOverlayMaterial.SetTexture(BaseMapId, ResolveFogOverlayTexture());
            }
            if (_fogOverlayMaterial.HasProperty(MainTexId))
            {
                _fogOverlayMaterial.SetTexture(MainTexId, ResolveFogOverlayTexture());
            }
            if (_fogOverlayMaterial.HasProperty(BaseColorId))
            {
                _fogOverlayMaterial.SetColor(BaseColorId, Color.white);
            }
            if (_fogOverlayMaterial.HasProperty(ColorId))
            {
                _fogOverlayMaterial.SetColor(ColorId, Color.white);
            }
        }

        private void UpdateFogOverlayVisual(float alpha, bool forceUvRefresh)
        {
            if (fogOverlayRenderer == null)
            {
                return;
            }

            if (_fogOverlayBlock == null)
            {
                _fogOverlayBlock = new MaterialPropertyBlock();
            }

            var now = Time.unscaledTime;
            var scroll = fogUvScrollSpeed * now;
            var st = new Vector4(
                1f,
                1f,
                Mathf.Repeat(scroll.x, 1f),
                Mathf.Repeat(scroll.y, 1f));

            fogOverlayRenderer.GetPropertyBlock(_fogOverlayBlock);
            if (forceUvRefresh)
            {
                _fogOverlayBlock.SetVector(BaseMapStId, st);
                _fogOverlayBlock.SetVector(MainTexStId, st);
            }

            var fogTexture = ResolveFogOverlayTexture();
            _fogOverlayBlock.SetTexture(BaseMapId, fogTexture);
            _fogOverlayBlock.SetTexture(MainTexId, fogTexture);

            var finalColor = fogOverlayColor;
            finalColor.a = Mathf.Clamp01(finalColor.a * Mathf.Clamp01(alpha));
            _fogOverlayBlock.SetColor(BaseColorId, finalColor);
            _fogOverlayBlock.SetColor(ColorId, finalColor);
            fogOverlayRenderer.SetPropertyBlock(_fogOverlayBlock);
        }

        private Texture2D ResolveFogOverlayTexture()
        {
            if (fogOverlayTexture != null)
            {
                return fogOverlayTexture;
            }

            if (!_fogTextureLoadAttempted && !string.IsNullOrWhiteSpace(fogOverlayTextureResourcesPath))
            {
                _fogTextureLoadAttempted = true;
                fogOverlayTexture = Resources.Load<Texture2D>(fogOverlayTextureResourcesPath.Trim());
            }

            return fogOverlayTexture != null ? fogOverlayTexture : Texture2D.whiteTexture;
        }

        public void SetHighlightVisible(bool isVisible)
        {
            if (highlight != null)
            {
                if (isVisible)
                {
                    KeepHighlightBelowDetails();
                }

                highlight.SetActive(isVisible);
            }
        }

        public void SetHighlight(bool isVisible, Color color)
        {
            SetHighlightVisible(isVisible);
            SetHighlightColor(color);
        }

        public void SetHighlightColor(Color color)
        {
            EnsureHighlightBlock();

            if (highlightRenderer == null && highlight != null)
            {
                highlightRenderer = highlight.GetComponentInChildren<Renderer>();
            }

            if (highlightRenderer == null || _highlightBlock == null)
            {
                return;
            }

            var mats = highlightRenderer.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                return;
            }

            for (int i = 0; i < mats.Length; i++)
            {
                highlightRenderer.GetPropertyBlock(_highlightBlock, i);
                _highlightBlock.SetColor("_BaseColor", color);
                _highlightBlock.SetColor("_Color", color);
                highlightRenderer.SetPropertyBlock(_highlightBlock, i);
            }
        }

        public void ShowMovePathArrow(Vector2Int direction, Color? colorOverride = null)
        {
            if (!EnsureMoveMarkerVisual())
            {
                return;
            }

            var dir = HexGrid.AxialToWorld(direction.x, direction.y, 1f);
            if (dir.sqrMagnitude <= 0.0001f)
            {
                dir = Vector3.forward;
            }
            dir.Normalize();

            if (_moveArrowRoot != null)
            {
                _moveArrowRoot.gameObject.SetActive(true);
                _moveArrowRoot.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }

            if (_moveDestinationRenderer != null)
            {
                _moveDestinationRenderer.gameObject.SetActive(false);
            }

            SetMoveMarkerColor(colorOverride ?? moveMarkerDefaultColor);
        }

        public void ShowMovePathDestination(Color? colorOverride = null)
        {
            if (!EnsureMoveMarkerVisual())
            {
                return;
            }

            if (_moveArrowRoot != null)
            {
                _moveArrowRoot.gameObject.SetActive(false);
            }

            if (_moveDestinationRenderer != null)
            {
                _moveDestinationRenderer.gameObject.SetActive(true);
            }

            SetMoveMarkerColor(colorOverride ?? moveMarkerDefaultColor);
        }

        public void ClearMovePathMarker()
        {
            if (_moveArrowRoot != null)
            {
                _moveArrowRoot.gameObject.SetActive(false);
            }

            if (_moveDestinationRenderer != null)
            {
                _moveDestinationRenderer.gameObject.SetActive(false);
            }
        }

        private void EnsureHighlightBlock()
        {
            if (_highlightBlock == null)
            {
                _highlightBlock = new MaterialPropertyBlock();
            }
        }

        private bool EnsureMoveMarkerVisual()
        {
            if (!autoCreateMoveMarker)
            {
                return false;
            }

            if (_moveMarkerRoot == null)
            {
                var root = new GameObject("MoveMarkerRoot");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = new Vector3(0f, moveMarkerY, 0f);
                _moveMarkerRoot = root.transform;
            }

            if (_moveArrowRoot == null)
            {
                var arrowRoot = new GameObject("Arrow");
                arrowRoot.transform.SetParent(_moveMarkerRoot, false);
                arrowRoot.transform.localPosition = Vector3.zero;

                var arrowSprite = ResolveMoveArrowSprite();
                if (arrowSprite != null)
                {
                    var spriteGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    spriteGo.name = "GreenArrowSprite";
                    spriteGo.transform.SetParent(arrowRoot.transform, false);
                    spriteGo.transform.localPosition = Vector3.zero;
                    spriteGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    spriteGo.transform.localScale = new Vector3(moveArrowSpriteScale.x, moveArrowSpriteScale.y, 1f);
                    _moveArrowSpriteRenderer = spriteGo.GetComponent<Renderer>();
                    ApplyMoveMarkerSpriteMaterial(_moveArrowSpriteRenderer, arrowSprite);
                    _moveArrowSpriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    _moveArrowSpriteRenderer.receiveShadows = false;
                    DestroyRuntimeCollider(spriteGo);
                }
                else
                {
                    var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    shaft.name = "Shaft";
                    shaft.transform.SetParent(arrowRoot.transform, false);
                    shaft.transform.localPosition = new Vector3(0f, 0f, -0.04f);
                    shaft.transform.localScale = moveArrowScale;
                    _moveArrowShaftRenderer = shaft.GetComponent<Renderer>();
                    DestroyRuntimeCollider(shaft);

                    var headLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    headLeft.name = "HeadLeft";
                    headLeft.transform.SetParent(arrowRoot.transform, false);
                    headLeft.transform.localPosition = new Vector3(-0.06f, 0f, 0.14f);
                    headLeft.transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
                    headLeft.transform.localScale = new Vector3(moveArrowHeadScale, moveArrowScale.y, moveArrowHeadScale);
                    _moveArrowHeadLeftRenderer = headLeft.GetComponent<Renderer>();
                    DestroyRuntimeCollider(headLeft);

                    var headRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    headRight.name = "HeadRight";
                    headRight.transform.SetParent(arrowRoot.transform, false);
                    headRight.transform.localPosition = new Vector3(0.06f, 0f, 0.14f);
                    headRight.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    headRight.transform.localScale = new Vector3(moveArrowHeadScale, moveArrowScale.y, moveArrowHeadScale);
                    _moveArrowHeadRightRenderer = headRight.GetComponent<Renderer>();
                    DestroyRuntimeCollider(headRight);
                }

                _moveArrowRoot = arrowRoot.transform;
                _moveArrowRoot.gameObject.SetActive(false);
            }

            if (_moveDestinationRenderer == null)
            {
                var destination = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                destination.name = "Destination";
                destination.transform.SetParent(_moveMarkerRoot, false);
                destination.transform.localPosition = Vector3.zero;
                destination.transform.localScale = new Vector3(moveDestinationScale, moveArrowScale.y, moveDestinationScale);
                _moveDestinationRenderer = destination.GetComponent<Renderer>();
                DestroyRuntimeCollider(destination);
                destination.SetActive(false);
            }

            ApplyMoveMarkerMaterial(_moveArrowShaftRenderer);
            ApplyMoveMarkerMaterial(_moveArrowHeadLeftRenderer);
            ApplyMoveMarkerMaterial(_moveArrowHeadRightRenderer);
            ApplyMoveMarkerMaterial(_moveDestinationRenderer);
            return true;
        }

        private void ApplyMoveMarkerMaterial(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (_moveMarkerMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader == null)
                {
                    return;
                }

                _moveMarkerMaterial = new Material(shader);
                _moveMarkerMaterial.name = "MoveMarkerMat_Runtime";
                _moveMarkerMaterial.hideFlags = HideFlags.DontSave;
            }

            renderer.sharedMaterial = _moveMarkerMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void ApplyMoveMarkerSpriteMaterial(Renderer renderer, Sprite sprite)
        {
            if (renderer == null || sprite == null)
            {
                return;
            }

            if (_moveMarkerSpriteMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

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
                    return;
                }

                _moveMarkerSpriteMaterial = new Material(shader);
                _moveMarkerSpriteMaterial.name = "MoveMarkerSpriteMat_Runtime";
                _moveMarkerSpriteMaterial.hideFlags = HideFlags.DontSave;
                _moveMarkerSpriteMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                if (_moveMarkerSpriteMaterial.HasProperty("_Cull"))
                {
                    _moveMarkerSpriteMaterial.SetFloat("_Cull", 0f);
                }
                if (_moveMarkerSpriteMaterial.HasProperty("_Surface"))
                {
                    _moveMarkerSpriteMaterial.SetFloat("_Surface", 1f);
                }
                if (_moveMarkerSpriteMaterial.HasProperty("_ZWrite"))
                {
                    _moveMarkerSpriteMaterial.SetFloat("_ZWrite", 0f);
                }
            }

            if (_moveMarkerSpriteMaterial.HasProperty("_BaseMap"))
            {
                _moveMarkerSpriteMaterial.SetTexture("_BaseMap", sprite.texture);
            }
            if (_moveMarkerSpriteMaterial.HasProperty("_MainTex"))
            {
                _moveMarkerSpriteMaterial.SetTexture("_MainTex", sprite.texture);
            }

            renderer.sharedMaterial = _moveMarkerSpriteMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void SetMoveMarkerColor(Color color)
        {
            if (_moveMarkerBlock == null)
            {
                _moveMarkerBlock = new MaterialPropertyBlock();
            }

            ApplyMoveMarkerColor(_moveArrowShaftRenderer, color);
            ApplyMoveMarkerColor(_moveArrowHeadLeftRenderer, color);
            ApplyMoveMarkerColor(_moveArrowHeadRightRenderer, color);
            ApplyMoveMarkerColor(_moveArrowSpriteRenderer, color);
            ApplyMoveMarkerColor(_moveDestinationRenderer, color);
        }

        private void ApplyMoveMarkerColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(_moveMarkerBlock);
            _moveMarkerBlock.SetColor("_BaseColor", color);
            _moveMarkerBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(_moveMarkerBlock);
        }

        private Sprite ResolveMoveArrowSprite()
        {
            if (moveArrowSprite != null)
            {
                return moveArrowSprite;
            }

            if (!_moveArrowSpriteLoadAttempted && !string.IsNullOrWhiteSpace(moveArrowSpriteResourcesPath))
            {
                _moveArrowSpriteLoadAttempted = true;
                moveArrowSprite = Resources.Load<Sprite>(moveArrowSpriteResourcesPath.Trim());
            }

            return moveArrowSprite;
        }

        private void KeepHighlightBelowDetails()
        {
            if (!forceHighlightBelowDetails || highlight == null)
            {
                return;
            }

            var highlightTransform = highlight.transform;
            var localPosition = highlightTransform.localPosition;
            localPosition.y = Mathf.Min(localPosition.y, Mathf.Max(0f, highlightLocalY));
            highlightTransform.localPosition = localPosition;

            if (highlightRenderer == null)
            {
                highlightRenderer = highlight.GetComponentInChildren<Renderer>();
            }

            if (highlightRenderer != null)
            {
                highlightRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                highlightRenderer.receiveShadows = false;
            }
        }

        private static void DestroyRuntimeCollider(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }
        }

        /// <summary>
        /// Update/clear resource visual according to resource point state.
        /// </summary>
        public void SetResource(bool isResourcePoint, string resourceType, bool isHighValue = false)
        {
            _resourceVisibleWanted = isResourcePoint;
            if (!isResourcePoint)
            {
                ClearResource();
                return;
            }

            var normalized = NormalizeToken(resourceType);
            if (resourcePointPrefab == null || resourceAnchor == null)
            {
                _resourceType = normalized;
                return;
            }

            if (_resourceInstance == null)
            {
                _resourceInstance = Instantiate(resourcePointPrefab, resourceAnchor, false);
            }

            _resourceInstance.SetType(normalized);
            _resourceInstance.SetHighValue(isHighValue);
            _resourceType = normalized;
            ApplyObservationDetailVisibility();
        }

        /// <summary>
        /// Update/clear building visual according to building_type.
        /// </summary>
        public void SetBuilding(string buildingType, string ownerId, int buildingHp, bool isGhost)
        {
            SetBuilding(buildingType, ownerId, buildingHp, 0, isGhost);
        }

        /// <summary>
        /// Update/clear building visual according to building_type with optional max HP.
        /// </summary>
        public void SetBuilding(string buildingType, string ownerId, int buildingHp, int buildingMaxHp, bool isGhost)
        {
            var normalized = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(normalized))
            {
                ClearBuilding();
                return;
            }

            if (buildingAnchor == null)
            {
                _buildingType = normalized;
                return;
            }

            if (_buildingInstance == null || _buildingType != normalized)
            {
                ReplaceBuildingInstance(normalized);
            }

            if (_buildingInstance != null)
            {
                _buildingInstance.SetBuildingType(normalized);
                _buildingInstance.SetLocalPlayerId(_localPlayerId);
                _buildingInstance.SetOwner(ownerId);
                _buildingInstance.SetHitPoints(buildingHp, buildingMaxHp);
                _buildingInstance.SetPlacementGhost(isGhost);
            }

            _buildingType = normalized;
            ApplyObservationDetailVisibility();
        }

        public void SetBuildingGhost(string buildingType, string ownerId, Color ghostColor)
        {
            var normalized = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(normalized))
            {
                ClearBuilding();
                return;
            }

            if (buildingAnchor == null)
            {
                _buildingType = normalized;
                return;
            }

            if (_buildingInstance == null || _buildingType != normalized)
            {
                ReplaceBuildingInstance(normalized);
            }

            if (_buildingInstance != null)
            {
                _buildingInstance.SetBuildingType(normalized);
                _buildingInstance.SetLocalPlayerId(_localPlayerId);
                _buildingInstance.SetOwner(ownerId);
                _buildingInstance.SetPlacementGhost(true, ghostColor);
            }

            _buildingType = normalized;
            ApplyObservationDetailVisibility();
        }

        public void ClearResource()
        {
            if (_resourceInstance != null)
            {
                Destroy(_resourceInstance.gameObject);
                _resourceInstance = null;
            }
            _resourceType = string.Empty;
            _resourceVisibleWanted = false;
            ApplyObservationDetailVisibility();
        }

        public void ClearBuilding()
        {
            if (_buildingInstance != null)
            {
                Destroy(_buildingInstance.gameObject);
                _buildingInstance = null;
            }
            _buildingType = string.Empty;
            ApplyObservationDetailVisibility();
        }

        private void ReplaceBuildingInstance(string buildingType)
        {
            ClearBuilding();

            var prefab = GetBuildingPrefab(buildingType);
            if (prefab == null)
            {
                return;
            }

            _buildingInstance = Instantiate(prefab, buildingAnchor, false);
            _buildingInstance.SetLocalPlayerId(_localPlayerId);
        }

        private Material GetTerrainMaterial(string terrain)
        {
            switch (NormalizeToken(terrain))
            {
                case "plain":
                    return plainMaterial;
                case "hill":
                    return mountainMaterial != null ? mountainMaterial : plainMaterial;
                case "mountain":
                    return mountainMaterial;
                case "forest":
                    return forestMaterial;
                case "river":
                case "water":
                    return riverMaterial;
                case "snow":
                    return snowMaterial != null ? snowMaterial : plainMaterial;
                case "forbidden":
                case "blocked":
                    return forbiddenMaterial != null ? forbiddenMaterial : mountainMaterial;
                default:
                    return plainMaterial;
            }
        }

        private void ApplyContinuousTerrainUv(string terrain)
        {
            if (groundRenderer == null)
            {
                return;
            }

            if (!useContinuousTerrainUv)
            {
                if (_groundBlock != null)
                {
                    _groundBlock.Clear();
                    groundRenderer.SetPropertyBlock(_groundBlock);
                }
                return;
            }

            if (_groundBlock == null)
            {
                _groundBlock = new MaterialPropertyBlock();
            }

            var worldSize = Mathf.Max(0.01f, GetTerrainUvWorldSize(terrain));
            var scale = 1f / worldSize;
            var worldPos = transform.position;
            var offset = new Vector2(
                Mathf.Repeat(worldPos.x * scale, 1f),
                Mathf.Repeat(worldPos.z * scale, 1f));
            var st = new Vector4(scale, scale, offset.x, offset.y);

            groundRenderer.GetPropertyBlock(_groundBlock);
            _groundBlock.SetVector(BaseMapStId, st);
            _groundBlock.SetVector(MainTexStId, st);
            _groundBlock.SetVector(BumpMapStId, st);

            var groundMaterial = groundRenderer.sharedMaterial;
            var baseColor = Color.white;
            if (groundMaterial != null)
            {
                if (groundMaterial.HasProperty(BaseColorId))
                {
                    baseColor = groundMaterial.GetColor(BaseColorId);
                }
                else if (groundMaterial.HasProperty(ColorId))
                {
                    baseColor = groundMaterial.GetColor(ColorId);
                }
            }

            if (useTerrainColorVariation && terrainColorVariationStrength > 0f)
            {
                var noise = Mathf.PerlinNoise(
                    (GridPos.x + 37.13f) * 0.311f,
                    (GridPos.y - 11.77f) * 0.311f);
                var shade = Mathf.Lerp(1f - terrainColorVariationStrength, 1f + terrainColorVariationStrength, noise);
                var variedColor = new Color(
                    Mathf.Clamp01(baseColor.r * shade),
                    Mathf.Clamp01(baseColor.g * shade),
                    Mathf.Clamp01(baseColor.b * shade),
                    baseColor.a);

                _groundBlock.SetColor(BaseColorId, variedColor);
                _groundBlock.SetColor(ColorId, variedColor);
            }
            else
            {
                _groundBlock.SetColor(BaseColorId, baseColor);
                _groundBlock.SetColor(ColorId, baseColor);
            }

            groundRenderer.SetPropertyBlock(_groundBlock);
        }

        private float GetTerrainUvWorldSize(string terrain)
        {
            switch (NormalizeToken(terrain))
            {
                case "hill":
                case "mountain":
                    return mountainUvWorldSize;
                case "forest":
                    return forestUvWorldSize;
                case "river":
                case "water":
                    return riverUvWorldSize;
                case "snow":
                    return snowUvWorldSize;
                case "forbidden":
                case "blocked":
                    return forbiddenUvWorldSize;
                default:
                    return plainUvWorldSize;
            }
        }

        private BuildingView GetBuildingPrefab(string buildingType)
        {
            var lookupKey = NormalizeBuildingTypeKey(buildingType);
            if (buildingPrefabs != null)
            {
                for (int i = 0; i < buildingPrefabs.Length; i++)
                {
                    if (NormalizeBuildingTypeKey(buildingPrefabs[i].buildingType) == lookupKey)
                    {
                        return buildingPrefabs[i].prefab;
                    }
                }
            }

            var runtimeLoaded = TryLoadBuildingPrefabFromResources(lookupKey);
            if (runtimeLoaded != null)
            {
                return runtimeLoaded;
            }

            return defaultBuildingPrefab;
        }

        private BuildingView TryLoadBuildingPrefabFromResources(string buildingType)
        {
            var lookupKey = NormalizeBuildingTypeKey(buildingType);
            if (string.IsNullOrWhiteSpace(lookupKey))
            {
                return null;
            }

            if (_runtimeBuildingPrefabCache.TryGetValue(lookupKey, out var cachedPrefab))
            {
                return cachedPrefab;
            }

            var prefab = LoadBuildingPrefabByPath($"{buildingPrefabResourcesRoot}/{lookupKey}");
            if (prefab == null)
            {
                if (_buildingCatalog != null &&
                    _buildingCatalog.TryGetValue(lookupKey, out var entry) &&
                    entry != null &&
                    !string.IsNullOrWhiteSpace(entry.PrefabKey))
                {
                    prefab = LoadBuildingPrefabByPath($"{buildingPrefabResourcesRoot}/{NormalizeToken(entry.PrefabKey)}");
                    if (prefab == null)
                    {
                        prefab = LoadBuildingPrefabByPath(NormalizeToken(entry.PrefabKey));
                    }
                }
            }

            _runtimeBuildingPrefabCache[lookupKey] = prefab;
            return prefab;
        }

        private static BuildingView LoadBuildingPrefabByPath(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            return Resources.Load<BuildingView>(resourcePath.Trim().Trim('/'));
        }

        public BuildingView ResolveBuildingPrefab(string buildingType)
        {
            return GetBuildingPrefab(NormalizeBuildingTypeKey(buildingType));
        }

        private static string NormalizeBuildingTypeKey(string value)
        {
            var token = NormalizeToken(value);
            switch (token)
            {
                case "atktower":
                    return "tower";
                case "viewtower":
                    return "watchtower";
                case "lumber":
                    return "lumberyard";
                case "mine":
                    return "smelter";
                case "wall":
                    return "tower";
                case "engineer_camp":
                    return "engineer";
                case "city_core":
                    return "city_core";
                default:
                    return token;
            }
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        public void SetPerTileObservationFogEnabled(bool enabled)
        {
            enableObservationFogOverlay = enabled;
            if (!enabled && fogOverlayRenderer != null)
            {
                fogOverlayRenderer.enabled = false;
            }
            else if (enabled)
            {
                EnsureObservationFogOverlay();
                ApplyObservationFogState(_currentFogAlpha, forceUvRefresh: true);
            }
        }

        public void SetUnknownDetailCulling(bool enabled, bool hideGround)
        {
            hideUnknownDetails = enabled;
            hideGroundWhenUnknown = hideGround;
            ApplyObservationDetailVisibility();
        }

        private void ApplyObservationDetailVisibility()
        {
            var known = _isCurrentlyVisible || _isMemoryVisible;
            var hideDetails = hideUnknownDetails && !known;

            if (groundRenderer != null)
            {
                groundRenderer.enabled = !(hideDetails && hideGroundWhenUnknown);
            }

            if (roadOverlay != null)
            {
                roadOverlay.SetActive(_roadVisibleWanted && !hideDetails);
            }

            if (_resourceInstance != null)
            {
                _resourceInstance.gameObject.SetActive(_resourceVisibleWanted && !hideDetails);
            }

            if (_buildingInstance != null)
            {
                _buildingInstance.gameObject.SetActive(!string.IsNullOrEmpty(_buildingType) && !hideDetails);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (groundRenderer == null)
            {
                groundRenderer = GetComponentInChildren<Renderer>();
            }

            if (resourceAnchor == null)
            {
                var anchor = transform.Find("ResourceAnchor");
                if (anchor != null)
                {
                    resourceAnchor = anchor;
                }
            }

            if (unitAnchor == null)
            {
                var anchor = transform.Find("UnitAnchor");
                if (anchor != null)
                {
                    unitAnchor = anchor;
                }
            }

            if (buildingAnchor == null)
            {
                var anchor = transform.Find("BuildingAnchor");
                if (anchor != null)
                {
                    buildingAnchor = anchor;
                }
            }

            if (highlightRenderer == null && highlight != null)
            {
                highlightRenderer = highlight.GetComponentInChildren<Renderer>();
            }
        }
#endif
    }
}
