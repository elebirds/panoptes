/*************************************************
 * Project: Panoptes
 * File: BuildingView.cs
 * Author: Panoptes Team
 * Date: 2026-04-05
 * Description: Building visual controller.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using Panoptes.Presentation.UI.HUD;
using UnityEngine;
using UnityEngine.Rendering;

namespace Panoptes.Presentation.Map
{
    public sealed class BuildingView : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string buildingType = string.Empty;

        [Header("Visuals")]
        [Tooltip("Only assign roof renderers here if you want roof-only faction tint.")]
        [SerializeField] private Renderer[] ownerTintRenderers;
        [Tooltip("If enabled, tint only materials that match the filters below.")]
        [SerializeField] private bool tintOnlyMatchingMaterials = true;
        [Tooltip("If set, these material slot indices are tinted directly (e.g. 0,2).")]
        [SerializeField] private int[] tintMaterialIndices;
        [Tooltip("Material name contains any keyword -> tinted (case-insensitive).")]
        [SerializeField] private string[] tintMaterialNameKeywords = { "roof", "tile", "top" };
        [Tooltip("Shader name contains any keyword -> tinted (case-insensitive).")]
        [SerializeField] private string[] tintShaderNameKeywords = { "roof" };
        [SerializeField] private GameObject damagedMark;
        [SerializeField] private GameObject selectedRing;
        [SerializeField] private bool useSelectionBeam = true;
        [SerializeField] private bool autoCreateSelectionBeam = true;
        [SerializeField] private Transform selectionBeamRoot;
        [SerializeField] private float selectionBeamHeight = 3.2f;
        [SerializeField] private float selectionBeamRadius = 0.56f;
        [SerializeField] private float selectionBeamTopOffset = 0.35f;
        [SerializeField] private Color selectionBeamColor = new Color(0.95f, 0.88f, 0.48f, 0.3f);
        [SerializeField] private float selectionBeamLightIntensity = 3.4f;
        [SerializeField] private float selectionBeamLightRange = 6.5f;
        [SerializeField] private float selectionBeamSpotAngle = 42f;
        [SerializeField] private bool useSelectionEdgeGlow = true;
        [SerializeField] private bool autoCreateSelectionEdgeGlow = true;
        [SerializeField] private SelectionEdgeGlow selectionEdgeGlow;
        [SerializeField] private Color neutralOwnerColor = Color.white;
        [SerializeField] private Color friendlyOwnerColor = new Color(0.26f, 0.78f, 1f, 1f);
        [SerializeField] private Color enemyOwnerColor = new Color(1f, 0.35f, 0.35f, 1f);
        [Range(0f, 1f)] [SerializeField] private float ownerTintStrength = 0.45f;
        [SerializeField] private bool useHashedColorWhenNoMyPlayerId = false;
        [SerializeField] private Color defaultGhostColor = new Color(0.6f, 1f, 0.6f, 0.9f);
        [Range(0f, 1f)] [SerializeField] private float ghostTintStrength = 0.85f;
        [SerializeField] private Color memoryTintColor = new Color(0.76f, 0.76f, 0.76f, 1f);
        [SerializeField] private Color unknownTintColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        [Header("Damage Threshold")]
        [SerializeField] private int lowHitPointThreshold = 30;
        [SerializeField] private int defaultCityCoreMaxHp = 100;

        [Header("City Core HP Bar")]
        [SerializeField] private bool enableCityCoreHpBar = true;
        [SerializeField] private CityCoreHPBar cityCoreHpBarPrefab;
        [SerializeField] private bool autoLoadCityCoreHpBarPrefab = true;
        [SerializeField] private string cityCoreHpBarResourcesPath = "Prefabs/UI/CityCoreHPBar";
        [SerializeField] private string cityCoreDisplayName = "City Core";
        [SerializeField] private float cityCoreHpBarWidth = 1.8f;
        [SerializeField] private float cityCoreHpBarHeight = 0.2f;
        [SerializeField] private float cityCoreHpBarVerticalPadding = 0.35f;
        [SerializeField] private float cityCoreHpBarScreenScale = 0.001f;
        [SerializeField] private float cityCoreHpBarMinScale = 0.0035f;
        [SerializeField] private float cityCoreHpBarMaxScale = 0.016f;
        [SerializeField] private bool lockCityCoreHpBarWorldRotation = true;
        [SerializeField] private bool cityCoreHpBarUseCameraUpVector = false;
        [SerializeField] private bool cityCoreHpBarAllowRoll = false;
        [SerializeField] private float cityCoreHpBarFacingYawOffset = 180f;
        [SerializeField] private float cityCoreHpBarFixedYaw = 0f;
        [SerializeField] private Color cityCoreHpBarFillColor = new Color(0.2f, 0.95f, 0.35f, 1f);
        [SerializeField] private Color cityCoreHpBarBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        public string BuildingType => buildingType;
        public string OwnerId { get; private set; } = string.Empty;
        public int HitPoints { get; private set; }
        public int MaxHitPoints { get; private set; }
        public bool IsGhost { get; private set; }
        public bool IsCityCore => IsCityCoreBuildingType();
        public bool IsCityCoreHpBarEnabled => enableCityCoreHpBar;
        public string Status { get; private set; } = string.Empty;
        public string CityId { get; private set; } = string.Empty;
        public string ServiceCityId { get; private set; } = string.Empty;
        public int TakeoverProgress { get; private set; }
        public int TakeoverRequired { get; private set; }
        public bool IsSafeZone { get; private set; }

        private string _localPlayerId = string.Empty;
        private Renderer[] _allRenderers;
        private Transform _cityCoreHpBarRoot;
        private CityCoreHPBar _cityCoreHpBarView;
        private float _cityCoreHpNormalized = 1f;
        private Renderer _selectionBeamRenderer;
        private Material _selectionBeamMaterial;
        private MaterialPropertyBlock _selectionBeamBlock;
        private Light _selectionBeamLight;

        private void Awake()
        {
            EnsureSelectionEdgeGlow();
            HideLegacySelectionVisuals();
        }

        private void LateUpdate()
        {
            UpdateCityCoreHpBarTransform();
        }

        public void SetBuildingType(string value)
        {
            buildingType = NormalizeToken(value);
            if (!string.IsNullOrEmpty(buildingType))
            {
                name = $"Building_{buildingType}";
            }

            EnsureCityCoreHpBarState();
            UpdateCityCoreHpBarName();
        }

        public void SetLocalPlayerId(string localPlayerId)
        {
            _localPlayerId = localPlayerId ?? string.Empty;
            var color = ResolveOwnerColor(OwnerId);
            ApplyOwnerTint(color);
            if (_cityCoreHpBarView != null)
            {
                _cityCoreHpBarView.SetFactionColor(color);
            }
        }

        public void SetOwner(string ownerId)
        {
            OwnerId = ownerId ?? string.Empty;
            var color = ResolveOwnerColor(OwnerId);

            ApplyOwnerTint(color);
            if (_cityCoreHpBarView != null)
            {
                _cityCoreHpBarView.SetFactionColor(color);
            }
        }

        public void SetHitPoints(int currentHp, int maxHp = 0)
        {
            HitPoints = Mathf.Max(0, currentHp);
            if (maxHp > 0)
            {
                MaxHitPoints = maxHp;
            }
            else if (MaxHitPoints <= 0)
            {
                // When protocol doesn't provide max HP, treat first observed HP as max to avoid fake half-HP display.
                // If current HP is zero (e.g. transient state), fallback to city core default.
                if (IsCityCoreBuildingType() && HitPoints <= 0)
                {
                    MaxHitPoints = Mathf.Max(1, defaultCityCoreMaxHp);
                }
                else
                {
                    MaxHitPoints = Mathf.Max(1, HitPoints);
                }
            }

            UpdateDamageMark();
            UpdateCityCoreHpBarValue();
            selectionEdgeGlow?.Refresh();
        }

        public void SetCityCoreHpBarEnabled(bool enabled)
        {
            enableCityCoreHpBar = enabled;
            EnsureCityCoreHpBarState();
        }

        public void SetSelected(bool isSelected)
        {
            if (selectedRing != null)
            {
                selectedRing.SetActive(false);
            }

            if (_selectionBeamRenderer != null)
            {
                _selectionBeamRenderer.gameObject.SetActive(false);
            }

            if (_selectionBeamLight != null)
            {
                _selectionBeamLight.gameObject.SetActive(false);
            }

            if (selectionEdgeGlow != null)
            {
                selectionEdgeGlow.SetSelected(isSelected);
            }
        }

        public void SetPlacementGhost(bool isGhost)
        {
            SetPlacementGhost(isGhost, defaultGhostColor);
        }

        public void SetPlacementGhost(bool isGhost, Color ghostColor)
        {
            IsGhost = isGhost;

            EnsureAllRenderers();
            if (_allRenderers == null || _allRenderers.Length == 0)
            {
                UpdateCityCoreHpBarVisibility();
                return;
            }

            for (int r = 0; r < _allRenderers.Length; r++)
            {
                var renderer = _allRenderers[r];
                if (renderer == null)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                if (materials == null)
                {
                    continue;
                }

                for (int i = 0; i < materials.Length; i++)
                {
                    if (isGhost)
                    {
                        var color = Color.Lerp(Color.white, ghostColor, ghostTintStrength);
                        var block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block, i);
                        block.SetColor("_BaseColor", color);
                        block.SetColor("_Color", color);
                        renderer.SetPropertyBlock(block, i);
                    }
                    else
                    {
                        renderer.SetPropertyBlock(new MaterialPropertyBlock(), i);
                    }
                }
            }

            if (!isGhost)
            {
                // Restore owner tint after leaving ghost mode.
                ApplyOwnerTint(ResolveOwnerColor(OwnerId));
            }

            UpdateCityCoreHpBarVisibility();
        }

        public void ApplyRuntimeState(NodeDto node)
        {
            Status = node != null ? NormalizeToken(node.BuildingStatus) : string.Empty;
            CityId = node != null ? (node.CityId ?? string.Empty) : string.Empty;
            ServiceCityId = node != null ? (node.ServiceCityId ?? string.Empty) : string.Empty;
            TakeoverProgress = node != null ? node.TakeoverProgress : 0;
            TakeoverRequired = node != null ? node.TakeoverRequired : 0;
            IsSafeZone = node != null && node.IsSafeZone;
        }

        public void SetObservationState(bool isVisible, bool isMemory)
        {
            EnsureAllRenderers();
            if (_allRenderers == null || _allRenderers.Length == 0)
            {
                return;
            }

            if (isVisible)
            {
                for (var r = 0; r < _allRenderers.Length; r++)
                {
                    var renderer = _allRenderers[r];
                    if (renderer == null)
                    {
                        continue;
                    }

                    var materials = renderer.sharedMaterials;
                    if (materials == null)
                    {
                        continue;
                    }

                    for (var i = 0; i < materials.Length; i++)
                    {
                        renderer.SetPropertyBlock(new MaterialPropertyBlock(), i);
                    }
                }

                ApplyOwnerTint(ResolveOwnerColor(OwnerId));
                return;
            }

            var tint = isMemory ? memoryTintColor : unknownTintColor;
            for (var r = 0; r < _allRenderers.Length; r++)
            {
                var renderer = _allRenderers[r];
                if (renderer == null)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                if (materials == null)
                {
                    continue;
                }

                for (var i = 0; i < materials.Length; i++)
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, i);
                    block.SetColor("_BaseColor", tint);
                    block.SetColor("_Color", tint);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }

        private void UpdateDamageMark()
        {
            if (damagedMark == null)
            {
                return;
            }

            var isDamaged = false;
            if (MaxHitPoints > 0)
            {
                isDamaged = HitPoints * 100 <= MaxHitPoints * lowHitPointThreshold;
            }
            else
            {
                isDamaged = HitPoints > 0 && HitPoints <= lowHitPointThreshold;
            }

            damagedMark.SetActive(isDamaged);
        }

        private void ApplyOwnerTint(Color color)
        {
            EnsureAllRenderers();

            Renderer[] renderersToTint = ownerTintRenderers;
            if (renderersToTint == null || renderersToTint.Length == 0)
            {
                renderersToTint = _allRenderers;
            }

            if (renderersToTint == null || renderersToTint.Length == 0)
            {
                return;
            }

            var finalColor = Color.Lerp(Color.white, color, ownerTintStrength);

            for (int i = 0; i < renderersToTint.Length; i++)
            {
                var renderer = renderersToTint[i];
                if (renderer == null)
                {
                    continue;
                }

                ApplyTintToRenderer(renderer, finalColor);
            }
        }

        private void EnsureAllRenderers()
        {
            if (_allRenderers == null || _allRenderers.Length == 0)
            {
                var candidates = GetComponentsInChildren<Renderer>(true);
                if (candidates == null || candidates.Length == 0)
                {
                    _allRenderers = Array.Empty<Renderer>();
                    return;
                }

                var renderers = new List<Renderer>(candidates.Length);
                for (int i = 0; i < candidates.Length; i++)
                {
                    var renderer = candidates[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    if (_cityCoreHpBarRoot != null && renderer.transform.IsChildOf(_cityCoreHpBarRoot))
                    {
                        continue;
                    }

                    renderers.Add(renderer);
                }

                _allRenderers = renderers.ToArray();
            }
        }

        private void ApplyTintToRenderer(Renderer renderer, Color finalColor)
        {
            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                return;
            }

            // Always clear previous per-material overrides first.
            for (int matIndex = 0; matIndex < materials.Length; matIndex++)
            {
                renderer.SetPropertyBlock(new MaterialPropertyBlock(), matIndex);
            }

            if (!tintOnlyMatchingMaterials)
            {
                for (int matIndex = 0; matIndex < materials.Length; matIndex++)
                {
                    SetTintBlock(renderer, matIndex, finalColor);
                }
                return;
            }

            for (int matIndex = 0; matIndex < materials.Length; matIndex++)
            {
                if (ShouldTintMaterial(matIndex, materials[matIndex]))
                {
                    SetTintBlock(renderer, matIndex, finalColor);
                }
            }
        }

        private bool ShouldTintMaterial(int materialIndex, Material material)
        {
            if (tintMaterialIndices != null && tintMaterialIndices.Length > 0)
            {
                for (int i = 0; i < tintMaterialIndices.Length; i++)
                {
                    if (tintMaterialIndices[i] == materialIndex)
                    {
                        return true;
                    }
                }
                // Fallback to keyword matching when explicit indices don't match
                // the imported model's material layout.
            }

            var materialName = material != null ? material.name : string.Empty;
            var shaderName = (material != null && material.shader != null) ? material.shader.name : string.Empty;

            if (ContainsAnyToken(materialName, tintMaterialNameKeywords))
            {
                return true;
            }

            if (ContainsAnyToken(shaderName, tintShaderNameKeywords))
            {
                return true;
            }

            return false;
        }

        private void SetTintBlock(Renderer renderer, int materialIndex, Color color)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, materialIndex);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block, materialIndex);
        }

        private static bool ContainsAnyToken(string source, string[] tokens)
        {
            if (string.IsNullOrEmpty(source) || tokens == null || tokens.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private Color ResolveOwnerColor(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                return neutralOwnerColor;
            }

            var myPlayerId = _localPlayerId;

            if (!string.IsNullOrEmpty(myPlayerId))
            {
                return ownerId == myPlayerId ? friendlyOwnerColor : enemyOwnerColor;
            }

            return useHashedColorWhenNoMyPlayerId
                ? GetColorFromOwnerId(ownerId)
                : enemyOwnerColor;
        }

        private void EnsureCityCoreHpBarState()
        {
            if (!enableCityCoreHpBar)
            {
                DestroyCityCoreHpBar();
                return;
            }

            if (IsCityCoreBuildingType())
            {
                CreateCityCoreHpBarIfNeeded();
                UpdateCityCoreHpBarValue();
                UpdateCityCoreHpBarVisibility();
            }
            else
            {
                DestroyCityCoreHpBar();
            }
        }

        private void CreateCityCoreHpBarIfNeeded()
        {
            if (_cityCoreHpBarView != null && _cityCoreHpBarRoot != null)
            {
                return;
            }

            var prefab = ResolveCityCoreHpBarPrefab();
            if (prefab != null)
            {
                _cityCoreHpBarView = Instantiate(prefab, transform);
            }
            else
            {
                var fallbackRoot = new GameObject("CityCoreHpBarRoot", typeof(RectTransform));
                fallbackRoot.transform.SetParent(transform, false);
                _cityCoreHpBarView = fallbackRoot.AddComponent<CityCoreHPBar>();
                _cityCoreHpBarView.EditorRebuildUiForPrefab();
            }

            _cityCoreHpBarRoot = _cityCoreHpBarView != null ? _cityCoreHpBarView.transform : null;
            if (_cityCoreHpBarView != null)
            {
                _cityCoreHpBarView.SetBarColors(cityCoreHpBarBackgroundColor, cityCoreHpBarFillColor);
                _cityCoreHpBarView.SetFactionColor(ResolveOwnerColor(OwnerId));
                UpdateCityCoreHpBarName();
                _cityCoreHpBarView.SetHpRatio(_cityCoreHpNormalized);
            }

            _allRenderers = null;
        }

        private void DestroyCityCoreHpBar()
        {
            if (_cityCoreHpBarRoot != null)
            {
                Destroy(_cityCoreHpBarRoot.gameObject);
                _cityCoreHpBarRoot = null;
                _cityCoreHpBarView = null;
                _allRenderers = null;
            }
        }

        private void UpdateCityCoreHpBarValue()
        {
            var maxHp = Mathf.Max(1, MaxHitPoints);
            _cityCoreHpNormalized = Mathf.Clamp01(HitPoints / (float)maxHp);

            if (_cityCoreHpBarView != null)
            {
                _cityCoreHpBarView.SetHpRatio(_cityCoreHpNormalized);
            }
        }

        private void UpdateCityCoreHpBarVisibility()
        {
            if (_cityCoreHpBarRoot == null)
            {
                return;
            }

            var visible = enableCityCoreHpBar && IsCityCoreBuildingType() && !IsGhost;
            _cityCoreHpBarRoot.gameObject.SetActive(visible);
        }

        private void UpdateCityCoreHpBarTransform()
        {
            if (_cityCoreHpBarRoot == null || !_cityCoreHpBarRoot.gameObject.activeSelf)
            {
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            var anchorPosition = transform.position + Vector3.up * Mathf.Max(0.1f, cityCoreHpBarVerticalPadding);
            if (TryGetVisualBounds(out var bounds))
            {
                anchorPosition = new Vector3(bounds.center.x, bounds.max.y + Mathf.Max(0.05f, cityCoreHpBarVerticalPadding), bounds.center.z);
            }

            _cityCoreHpBarRoot.position = anchorPosition;

            var toCameraForScale = _cityCoreHpBarRoot.position - cam.transform.position;
            if (lockCityCoreHpBarWorldRotation)
            {
                // Face camera with height (pitch + yaw), and keep stable up vector to prevent odd letter tilt.
                var toCamera = cam.transform.position - _cityCoreHpBarRoot.position;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    var up = cityCoreHpBarUseCameraUpVector ? cam.transform.up : Vector3.up;
                    var forward = toCamera.normalized;

                    // Avoid degenerate LookRotation when up is nearly parallel to forward.
                    if (Mathf.Abs(Vector3.Dot(forward, up.normalized)) > 0.98f)
                    {
                        up = Vector3.Cross(cam.transform.right, forward);
                        if (up.sqrMagnitude < 0.0001f)
                        {
                            up = Vector3.up;
                        }
                    }

                    var rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(0f, cityCoreHpBarFacingYawOffset, 0f);
                    if (!cityCoreHpBarAllowRoll)
                    {
                        var euler = rotation.eulerAngles;
                        rotation = Quaternion.Euler(euler.x, euler.y, 0f);
                    }

                    _cityCoreHpBarRoot.rotation = rotation;
                }
            }
            else
            {
                // Optional fallback: fixed world yaw.
                _cityCoreHpBarRoot.rotation = Quaternion.Euler(0f, cityCoreHpBarFixedYaw, 0f);
            }

            var sizeFactor = Mathf.Max(0.001f, cityCoreHpBarScreenScale);
            float worldScale;
            if (cam.orthographic)
            {
                worldScale = cam.orthographicSize * sizeFactor;
            }
            else
            {
                var distance = Mathf.Max(0.01f, toCameraForScale.magnitude);
                worldScale = distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * sizeFactor;
            }

            worldScale = Mathf.Clamp(worldScale, cityCoreHpBarMinScale, cityCoreHpBarMaxScale);
            _cityCoreHpBarRoot.localScale = Vector3.one * worldScale;
        }

        private bool TryGetVisualBounds(out Bounds bounds)
        {
            EnsureAllRenderers();
            bounds = default;
            var hasBounds = false;

            if (_allRenderers == null)
            {
                return false;
            }

            for (int i = 0; i < _allRenderers.Length; i++)
            {
                var renderer = _allRenderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private CityCoreHPBar ResolveCityCoreHpBarPrefab()
        {
            if (cityCoreHpBarPrefab != null)
            {
                return cityCoreHpBarPrefab;
            }

            if (!autoLoadCityCoreHpBarPrefab || string.IsNullOrWhiteSpace(cityCoreHpBarResourcesPath))
            {
                return null;
            }

            cityCoreHpBarPrefab = Resources.Load<CityCoreHPBar>(cityCoreHpBarResourcesPath);
            return cityCoreHpBarPrefab;
        }

        private void UpdateCityCoreHpBarName()
        {
            if (_cityCoreHpBarView == null)
            {
                return;
            }

            var label = string.IsNullOrWhiteSpace(cityCoreDisplayName)
                ? "City Core"
                : cityCoreDisplayName.Trim();
            _cityCoreHpBarView.SetName(label);
        }

        private void EnsureSelectionBeam()
        {
            if (!useSelectionBeam)
            {
                return;
            }

            if (selectionBeamRoot == null && autoCreateSelectionBeam)
            {
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beam.name = "SelectedBeam";
                beam.transform.SetParent(transform, false);
                beam.transform.localPosition = new Vector3(0f, selectionBeamTopOffset + selectionBeamHeight * 0.5f, 0f);
                beam.transform.localScale = new Vector3(selectionBeamRadius, selectionBeamHeight * 0.5f, selectionBeamRadius);

                var collider = beam.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                selectionBeamRoot = beam.transform;
                _selectionBeamRenderer = beam.GetComponent<Renderer>();
                _selectionBeamLight = CreateSelectionSpotLight(selectionBeamRoot);
            }
            else if (selectionBeamRoot != null)
            {
                _selectionBeamRenderer = selectionBeamRoot.GetComponentInChildren<Renderer>(true);
                _selectionBeamLight = selectionBeamRoot.GetComponentInChildren<Light>(true);
                if (_selectionBeamLight == null && autoCreateSelectionBeam)
                {
                    _selectionBeamLight = CreateSelectionSpotLight(selectionBeamRoot);
                }
            }

            if (_selectionBeamRenderer != null)
            {
                _selectionBeamMaterial = CreateSelectionBeamMaterial();
                if (_selectionBeamMaterial != null)
                {
                    _selectionBeamRenderer.sharedMaterial = _selectionBeamMaterial;
                }

                _selectionBeamBlock ??= new MaterialPropertyBlock();
                _selectionBeamRenderer.GetPropertyBlock(_selectionBeamBlock);
                _selectionBeamBlock.SetColor("_BaseColor", selectionBeamColor);
                _selectionBeamBlock.SetColor("_Color", selectionBeamColor);
                _selectionBeamRenderer.SetPropertyBlock(_selectionBeamBlock);
                _selectionBeamRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _selectionBeamRenderer.receiveShadows = false;
                _selectionBeamRenderer.gameObject.SetActive(false);
            }

            if (_selectionBeamLight != null)
            {
                ConfigureSelectionSpotLight();
                _selectionBeamLight.gameObject.SetActive(false);
            }
        }

        private Light CreateSelectionSpotLight(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            var lightObject = new GameObject("SelectedBeamLight");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = new Vector3(0f, selectionBeamHeight * 0.5f, 0f);
            lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            return light;
        }

        private void ConfigureSelectionSpotLight()
        {
            if (_selectionBeamLight == null)
            {
                return;
            }

            _selectionBeamLight.type = LightType.Spot;
            _selectionBeamLight.color = selectionBeamColor;
            _selectionBeamLight.intensity = Mathf.Max(0f, selectionBeamLightIntensity);
            _selectionBeamLight.range = Mathf.Max(0.1f, selectionBeamLightRange);
            _selectionBeamLight.spotAngle = Mathf.Clamp(selectionBeamSpotAngle, 1f, 179f);
            _selectionBeamLight.shadows = LightShadows.None;
        }

        private Material CreateSelectionBeamMaterial()
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
                return null;
            }

            var material = new Material(shader)
            {
                name = "BuildingSelectionBeamMat_Runtime",
                hideFlags = HideFlags.DontSave
            };

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }
            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }
            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }
            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private void EnsureSelectionEdgeGlow()
        {
            if (!useSelectionEdgeGlow)
            {
                return;
            }

            if (selectionEdgeGlow == null)
            {
                selectionEdgeGlow = GetComponentInChildren<SelectionEdgeGlow>(true);
            }

            if (selectionEdgeGlow == null && autoCreateSelectionEdgeGlow)
            {
                selectionEdgeGlow = gameObject.AddComponent<SelectionEdgeGlow>();
            }

            selectionEdgeGlow?.SetSelected(false);
        }

        private void HideLegacySelectionVisuals()
        {
            if (selectedRing != null)
            {
                selectedRing.SetActive(false);
            }

            if (selectionBeamRoot != null)
            {
                selectionBeamRoot.gameObject.SetActive(false);
            }

            if (_selectionBeamRenderer != null)
            {
                _selectionBeamRenderer.gameObject.SetActive(false);
            }

            if (_selectionBeamLight != null)
            {
                _selectionBeamLight.gameObject.SetActive(false);
            }
        }

        private bool IsCityCoreBuildingType()
        {
            return string.Equals(buildingType, "city_core", StringComparison.Ordinal);
        }

        private void OnDestroy()
        {
            if (_selectionBeamMaterial != null)
            {
                Destroy(_selectionBeamMaterial);
                _selectionBeamMaterial = null;
            }
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static Color GetColorFromOwnerId(string ownerId)
        {
            var hash = StableHash(ownerId);
            var hue = (hash % 360u) / 360f;
            return Color.HSVToRGB(hue, 0.45f, 0.95f);
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                const uint offset = 2166136261u;
                const uint prime = 16777619u;
                uint hash = offset;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= prime;
                }
                return hash;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Intentionally not auto-filling renderer list.
            // This prevents tinting the whole building by mistake.
            cityCoreHpBarWidth = Mathf.Max(0.1f, cityCoreHpBarWidth);
            cityCoreHpBarHeight = Mathf.Max(0.05f, cityCoreHpBarHeight);
            cityCoreHpBarVerticalPadding = Mathf.Max(0.01f, cityCoreHpBarVerticalPadding);
            cityCoreHpBarScreenScale = Mathf.Max(0.00005f, cityCoreHpBarScreenScale);
            cityCoreHpBarMinScale = Mathf.Max(0.0001f, cityCoreHpBarMinScale);
            cityCoreHpBarMaxScale = Mathf.Max(cityCoreHpBarMinScale, cityCoreHpBarMaxScale);
            cityCoreHpBarFacingYawOffset = NormalizeAngle180(cityCoreHpBarFacingYawOffset);
            cityCoreHpBarFixedYaw = NormalizeAngle180(cityCoreHpBarFixedYaw);
            cityCoreDisplayName = string.IsNullOrWhiteSpace(cityCoreDisplayName) ? "City Core" : cityCoreDisplayName.Trim();
            defaultCityCoreMaxHp = Mathf.Max(1, defaultCityCoreMaxHp);
            EnsureAllRenderers();
        }

        private static float NormalizeAngle180(float angle)
        {
            var normalized = angle % 360f;
            if (normalized > 180f)
            {
                normalized -= 360f;
            }
            if (normalized < -180f)
            {
                normalized += 360f;
            }
            return normalized;
        }
#endif
    }
}
