/*************************************************
 * Project: Panoptes
 * File: BuildingView.cs
 * Author: Panoptes Team
 * Date: 2026-04-05
 * Description: Building visual controller.
 *************************************************/

using UnityEngine;

namespace Panoptes.Runtime.Map
{
    public sealed class BuildingView : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string buildingType = string.Empty;

        [Header("Visuals")]
        [SerializeField] private Renderer[] ownerTintRenderers;
        [SerializeField] private GameObject damagedMark;
        [SerializeField] private GameObject selectedRing;
        [SerializeField] private Color neutralOwnerColor = Color.white;

        [Header("Damage Threshold")]
        [SerializeField] private int lowHitPointThreshold = 30;

        public string BuildingType => buildingType;
        public string OwnerId { get; private set; } = string.Empty;
        public int HitPoints { get; private set; }
        public int MaxHitPoints { get; private set; }

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
            var color = string.IsNullOrEmpty(OwnerId)
                ? neutralOwnerColor
                : GetColorFromOwnerId(OwnerId);

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
            if (ownerTintRenderers == null)
            {
                return;
            }

            for (int i = 0; i < ownerTintRenderers.Length; i++)
            {
                var renderer = ownerTintRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
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
            if ((ownerTintRenderers == null || ownerTintRenderers.Length == 0))
            {
                ownerTintRenderers = GetComponentsInChildren<Renderer>(true);
            }
        }
#endif
    }
}
