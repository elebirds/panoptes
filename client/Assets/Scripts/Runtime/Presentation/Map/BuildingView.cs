/*************************************************
 * Project: Panoptes
 * File: BuildingView.cs
 * Author: Panoptes Team
 * Date: 2026-04-05
 * Description: Building visual controller.
 *************************************************/

using System;
using UnityEngine;
using Panoptes.Core.Application.Cache;

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

        public string BuildingType => buildingType;
        public string OwnerId { get; private set; } = string.Empty;
        public int HitPoints { get; private set; }
        public int MaxHitPoints { get; private set; }
        public bool IsGhost { get; private set; }

        private Renderer[] _allRenderers;

        public void SetBuildingType(string value)
        {
            buildingType = NormalizeToken(value);
            if (!string.IsNullOrEmpty(buildingType))
            {
                name = $"Building_{buildingType}";
            }
        }

        public void SetOwner(string ownerId)
        {
            OwnerId = ownerId ?? string.Empty;
            var color = ResolveOwnerColor(OwnerId);

            ApplyOwnerTint(color);
        }

        public void SetHitPoints(int currentHp, int maxHp = 0)
        {
            HitPoints = Mathf.Max(0, currentHp);
            if (maxHp > 0)
            {
                MaxHitPoints = maxHp;
            }

            UpdateDamageMark();
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
            if (ownerTintRenderers == null || ownerTintRenderers.Length == 0)
            {
                return;
            }

            var finalColor = Color.Lerp(Color.white, color, ownerTintStrength);

            for (int i = 0; i < ownerTintRenderers.Length; i++)
            {
                var renderer = ownerTintRenderers[i];
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
                _allRenderers = GetComponentsInChildren<Renderer>(true);
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
                return false;
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
            EnsureAllRenderers();
        }
#endif
    }
}
