/*************************************************
 * Project: Panoptes
 * File: BuildingView.cs
 * Author: Panoptes Team
 * Date: 2026-04-05
 * Description: Building visual controller.
 *************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.UI.HUD;

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
        [SerializeField] private Color neutralOwnerColor = Color.white;
        [SerializeField] private Color friendlyOwnerColor = new Color(0.26f, 0.78f, 1f, 1f);
        [SerializeField] private Color enemyOwnerColor = new Color(1f, 0.35f, 0.35f, 1f);
        [Range(0f, 1f)] [SerializeField] private float ownerTintStrength = 0.45f;
        [SerializeField] private bool useHashedColorWhenNoMyPlayerId = false;
        [SerializeField] private Color defaultGhostColor = new Color(0.6f, 1f, 0.6f, 0.9f);
        [Range(0f, 1f)] [SerializeField] private float ghostTintStrength = 0.85f;

        [Header("Damage Threshold")]
        [SerializeField] private int lowHitPointThreshold = 30;
        [SerializeField] private int defaultCastleMaxHp = 100;

        [Header("Castle HP Bar")]
        [SerializeField] private bool enableCastleHpBar = true;
        [SerializeField] private CastleHPBar castleHpBarPrefab;
        [SerializeField] private bool autoLoadCastleHpBarPrefab = true;
        [SerializeField] private string castleHpBarResourcesPath = "Prefabs/UI/CastleHPBar";
        [SerializeField] private string castleDisplayName = "City Core";
        [SerializeField] private float castleHpBarWidth = 1.8f;
        [SerializeField] private float castleHpBarHeight = 0.2f;
        [SerializeField] private float castleHpBarVerticalPadding = 0.35f;
        [SerializeField] private float castleHpBarScreenScale = 0.001f;
        [SerializeField] private float castleHpBarMinScale = 0.0035f;
        [SerializeField] private float castleHpBarMaxScale = 0.016f;
        [SerializeField] private bool lockCastleHpBarWorldRotation = true;
        [SerializeField] private bool castleHpBarUseCameraUpVector = false;
        [SerializeField] private bool castleHpBarAllowRoll = false;
        [SerializeField] private float castleHpBarFacingYawOffset = 180f;
        [SerializeField] private float castleHpBarFixedYaw = 0f;
        [SerializeField] private Color castleHpBarFillColor = new Color(0.2f, 0.95f, 0.35f, 1f);
        [SerializeField] private Color castleHpBarBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        public string BuildingType => buildingType;
        public string OwnerId { get; private set; } = string.Empty;
        public int HitPoints { get; private set; }
        public int MaxHitPoints { get; private set; }
        public bool IsGhost { get; private set; }
        public bool IsCastle => IsCastleBuildingType();
        public bool IsCastleHpBarEnabled => enableCastleHpBar;

        private Renderer[] _allRenderers;
        private Transform _castleHpBarRoot;
        private CastleHPBar _castleHpBarView;
        private float _castleHpNormalized = 1f;

        private void LateUpdate()
        {
            UpdateCastleHpBarTransform();
        }

        public void SetBuildingType(string value)
        {
            buildingType = NormalizeToken(value);
            if (!string.IsNullOrEmpty(buildingType))
            {
                name = $"Building_{buildingType}";
            }

            EnsureCastleHpBarState();
            UpdateCastleHpBarName();
        }

        public void SetOwner(string ownerId)
        {
            OwnerId = ownerId ?? string.Empty;
            var color = ResolveOwnerColor(OwnerId);

            ApplyOwnerTint(color);
            if (_castleHpBarView != null)
            {
                _castleHpBarView.SetFactionColor(color);
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
                if (IsCastleBuildingType() && HitPoints <= 0)
                {
                    MaxHitPoints = Mathf.Max(1, defaultCastleMaxHp);
                }
                else
                {
                    MaxHitPoints = Mathf.Max(1, HitPoints);
                }
            }

            UpdateDamageMark();
            UpdateCastleHpBarValue();
        }

        public void SetCastleHpBarEnabled(bool enabled)
        {
            enableCastleHpBar = enabled;
            EnsureCastleHpBarState();
        }

        public void SetSelected(bool isSelected)
        {
            if (selectedRing != null)
            {
                selectedRing.SetActive(isSelected);
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
                UpdateCastleHpBarVisibility();
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

            UpdateCastleHpBarVisibility();
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

                    if (_castleHpBarRoot != null && renderer.transform.IsChildOf(_castleHpBarRoot))
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

            var myPlayerId = GameStateCache.Instance != null
                ? GameStateCache.Instance.MyPlayerID
                : string.Empty;

            if (!string.IsNullOrEmpty(myPlayerId))
            {
                return ownerId == myPlayerId ? friendlyOwnerColor : enemyOwnerColor;
            }

            return useHashedColorWhenNoMyPlayerId
                ? GetColorFromOwnerId(ownerId)
                : enemyOwnerColor;
        }

        private void EnsureCastleHpBarState()
        {
            if (!enableCastleHpBar)
            {
                DestroyCastleHpBar();
                return;
            }

            if (IsCastleBuildingType())
            {
                CreateCastleHpBarIfNeeded();
                UpdateCastleHpBarValue();
                UpdateCastleHpBarVisibility();
            }
            else
            {
                DestroyCastleHpBar();
            }
        }

        private void CreateCastleHpBarIfNeeded()
        {
            if (_castleHpBarView != null && _castleHpBarRoot != null)
            {
                return;
            }

            var prefab = ResolveCastleHpBarPrefab();
            if (prefab != null)
            {
                _castleHpBarView = Instantiate(prefab, transform);
            }
            else
            {
                var fallbackRoot = new GameObject("CastleHpBarRoot", typeof(RectTransform));
                fallbackRoot.transform.SetParent(transform, false);
                _castleHpBarView = fallbackRoot.AddComponent<CastleHPBar>();
                _castleHpBarView.EditorRebuildUiForPrefab();
            }

            _castleHpBarRoot = _castleHpBarView != null ? _castleHpBarView.transform : null;
            if (_castleHpBarView != null)
            {
                _castleHpBarView.SetBarColors(castleHpBarBackgroundColor, castleHpBarFillColor);
                _castleHpBarView.SetFactionColor(ResolveOwnerColor(OwnerId));
                UpdateCastleHpBarName();
                _castleHpBarView.SetHpRatio(_castleHpNormalized);
            }

            _allRenderers = null;
        }

        private void DestroyCastleHpBar()
        {
            if (_castleHpBarRoot != null)
            {
                Destroy(_castleHpBarRoot.gameObject);
                _castleHpBarRoot = null;
                _castleHpBarView = null;
                _allRenderers = null;
            }
        }

        private void UpdateCastleHpBarValue()
        {
            var maxHp = Mathf.Max(1, MaxHitPoints);
            _castleHpNormalized = Mathf.Clamp01(HitPoints / (float)maxHp);

            if (_castleHpBarView != null)
            {
                _castleHpBarView.SetHpRatio(_castleHpNormalized);
            }
        }

        private void UpdateCastleHpBarVisibility()
        {
            if (_castleHpBarRoot == null)
            {
                return;
            }

            var visible = enableCastleHpBar && IsCastleBuildingType() && !IsGhost;
            _castleHpBarRoot.gameObject.SetActive(visible);
        }

        private void UpdateCastleHpBarTransform()
        {
            if (_castleHpBarRoot == null || !_castleHpBarRoot.gameObject.activeSelf)
            {
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            var anchorPosition = transform.position + Vector3.up * Mathf.Max(0.1f, castleHpBarVerticalPadding);
            if (TryGetVisualBounds(out var bounds))
            {
                anchorPosition = new Vector3(bounds.center.x, bounds.max.y + Mathf.Max(0.05f, castleHpBarVerticalPadding), bounds.center.z);
            }

            _castleHpBarRoot.position = anchorPosition;

            var toCameraForScale = _castleHpBarRoot.position - cam.transform.position;
            if (lockCastleHpBarWorldRotation)
            {
                // Face camera with height (pitch + yaw), and keep stable up vector to prevent odd letter tilt.
                var toCamera = cam.transform.position - _castleHpBarRoot.position;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    var up = castleHpBarUseCameraUpVector ? cam.transform.up : Vector3.up;
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

                    var rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(0f, castleHpBarFacingYawOffset, 0f);
                    if (!castleHpBarAllowRoll)
                    {
                        var euler = rotation.eulerAngles;
                        rotation = Quaternion.Euler(euler.x, euler.y, 0f);
                    }

                    _castleHpBarRoot.rotation = rotation;
                }
            }
            else
            {
                // Optional fallback: fixed world yaw.
                _castleHpBarRoot.rotation = Quaternion.Euler(0f, castleHpBarFixedYaw, 0f);
            }

            var sizeFactor = Mathf.Max(0.001f, castleHpBarScreenScale);
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

            worldScale = Mathf.Clamp(worldScale, castleHpBarMinScale, castleHpBarMaxScale);
            _castleHpBarRoot.localScale = Vector3.one * worldScale;
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

        private CastleHPBar ResolveCastleHpBarPrefab()
        {
            if (castleHpBarPrefab != null)
            {
                return castleHpBarPrefab;
            }

            if (!autoLoadCastleHpBarPrefab || string.IsNullOrWhiteSpace(castleHpBarResourcesPath))
            {
                return null;
            }

            castleHpBarPrefab = Resources.Load<CastleHPBar>(castleHpBarResourcesPath);
            return castleHpBarPrefab;
        }

        private void UpdateCastleHpBarName()
        {
            if (_castleHpBarView == null)
            {
                return;
            }

            var label = string.IsNullOrWhiteSpace(castleDisplayName)
                ? "City Core"
                : castleDisplayName.Trim();
            _castleHpBarView.SetName(label);
        }

        private bool IsCastleBuildingType()
        {
            return string.Equals(buildingType, "city_core", StringComparison.Ordinal);
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
            castleHpBarWidth = Mathf.Max(0.1f, castleHpBarWidth);
            castleHpBarHeight = Mathf.Max(0.05f, castleHpBarHeight);
            castleHpBarVerticalPadding = Mathf.Max(0.01f, castleHpBarVerticalPadding);
            castleHpBarScreenScale = Mathf.Max(0.00005f, castleHpBarScreenScale);
            castleHpBarMinScale = Mathf.Max(0.0001f, castleHpBarMinScale);
            castleHpBarMaxScale = Mathf.Max(castleHpBarMinScale, castleHpBarMaxScale);
            castleHpBarFacingYawOffset = NormalizeAngle180(castleHpBarFacingYawOffset);
            castleHpBarFixedYaw = NormalizeAngle180(castleHpBarFixedYaw);
            castleDisplayName = string.IsNullOrWhiteSpace(castleDisplayName) ? "Castle" : castleDisplayName.Trim();
            defaultCastleMaxHp = Mathf.Max(1, defaultCastleMaxHp);
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
