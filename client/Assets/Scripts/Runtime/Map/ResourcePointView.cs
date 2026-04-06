/*************************************************
 * Project: Panoptes
 * File: ResourcePointView.cs
 * Author: Panoptes Team
 * Date: 2026-04-05
 * Description: Resource point visual controller.
 *************************************************/

using UnityEngine;

namespace Panoptes.Runtime.Map
{
    public sealed class ResourcePointView : MonoBehaviour
    {
        [Header("Resource Type Icons")]
        [SerializeField] private GameObject oreIcon;
        [SerializeField] private GameObject woodIcon;
        [SerializeField] private GameObject foodIcon;

        [Header("Markers")]
        [SerializeField] private GameObject highValueMark;
        [SerializeField] private GameObject ownerRing;
        [SerializeField] private Renderer ownerRingRenderer;
        [SerializeField] private Color neutralOwnerColor = new Color(1f, 1f, 1f, 0.35f);

        public string ResourceType { get; private set; } = string.Empty;
        public bool IsHighValue { get; private set; }
        public string OwnerId { get; private set; } = string.Empty;

        public void SetType(string resourceType)
        {
            ResourceType = NormalizeToken(resourceType);

            SetActiveSafe(oreIcon, ResourceType == "ore");
            SetActiveSafe(woodIcon, ResourceType == "wood");
            SetActiveSafe(foodIcon, ResourceType == "food");
        }

        public void SetHighValue(bool isHighValue)
        {
            IsHighValue = isHighValue;
            SetActiveSafe(highValueMark, isHighValue);
        }

        public void SetOwner(string ownerId)
        {
            OwnerId = ownerId ?? string.Empty;
            if (string.IsNullOrEmpty(OwnerId))
            {
                SetOwnerVisible(false);
                return;
            }

            SetOwnerVisible(true);
            SetOwnerColor(GetColorFromOwnerId(OwnerId));
        }

        public void SetOwnerVisible(bool visible)
        {
            SetActiveSafe(ownerRing, visible);
        }

        public void SetOwnerColor(Color color)
        {
            if (ownerRingRenderer == null)
            {
                return;
            }

            var block = new MaterialPropertyBlock();
            ownerRingRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            ownerRingRenderer.SetPropertyBlock(block);
        }

        public void ResetVisual()
        {
            SetType(string.Empty);
            SetHighValue(false);
            SetOwnerVisible(false);
            SetOwnerColor(neutralOwnerColor);
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static void SetActiveSafe(GameObject target, bool isActive)
        {
            if (target != null)
            {
                target.SetActive(isActive);
            }
        }

        private static Color GetColorFromOwnerId(string ownerId)
        {
            var hash = StableHash(ownerId);
            var hue = (hash % 360u) / 360f;
            return Color.HSVToRGB(hue, 0.55f, 0.95f);
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
            if (ownerRingRenderer == null && ownerRing != null)
            {
                ownerRingRenderer = ownerRing.GetComponentInChildren<Renderer>();
            }
        }
#endif
    }
}
