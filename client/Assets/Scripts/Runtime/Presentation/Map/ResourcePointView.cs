/*************************************************
 * Project: Panoptes
 * File: ResourcePointView.cs
 * Author: Panoptes Team
 * Date: 2026-04-05
 * Description: Resource point visual controller.
 *************************************************/

using UnityEngine;

namespace Panoptes.Presentation.Map
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

        [Header("Footprint Decorations")]
        [SerializeField] private bool useProceduralFootprintDecorations = true;
        [SerializeField] private float decorationYOffset = 0.02f;
        [SerializeField] private Color treeTrunkColor = new Color(0.34f, 0.2f, 0.1f, 1f);
        [SerializeField] private Color treeCrownColor = new Color(0.12f, 0.42f, 0.18f, 1f);
        [SerializeField] private Color oreRockColor = new Color(0.42f, 0.44f, 0.46f, 1f);
        [SerializeField] private Color oreHighlightColor = new Color(0.62f, 0.64f, 0.66f, 1f);

        public string ResourceType { get; private set; } = string.Empty;
        public bool IsHighValue { get; private set; }
        public string OwnerId { get; private set; } = string.Empty;

        private Transform _decorationRoot;
        private Material _treeTrunkMaterial;
        private Material _treeCrownMaterial;
        private Material _oreRockMaterial;
        private Material _oreHighlightMaterial;

        public void SetType(string resourceType)
        {
            ResourceType = NormalizeToken(resourceType);

            SetActiveSafe(oreIcon, ResourceType == "ore" || ResourceType == "stone" || ResourceType == "rock");
            SetActiveSafe(woodIcon, ResourceType == "wood");
            SetActiveSafe(foodIcon, ResourceType == "food");

            RebuildFootprintDecorations();
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

        private void RebuildFootprintDecorations()
        {
            ClearFootprintDecorations();
            if (!useProceduralFootprintDecorations)
            {
                return;
            }

            switch (ResourceType)
            {
                case "wood":
                    EnsureDecorationRoot();
                    CreateTree(new Vector3(-0.34f, decorationYOffset, 0.36f), 0.95f);
                    CreateTree(new Vector3(0.34f, decorationYOffset, 0.36f), 0.88f);
                    break;
                case "ore":
                case "stone":
                case "rock":
                    EnsureDecorationRoot();
                    CreateRockRing();
                    break;
            }
        }

        private void ClearFootprintDecorations()
        {
            if (_decorationRoot == null)
            {
                return;
            }

            for (var i = _decorationRoot.childCount - 1; i >= 0; i--)
            {
                var child = _decorationRoot.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void EnsureDecorationRoot()
        {
            if (_decorationRoot != null)
            {
                return;
            }

            var go = new GameObject("FootprintDecorations");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            _decorationRoot = go.transform;
        }

        private void CreateTree(Vector3 localPosition, float scale)
        {
            var root = new GameObject("TreeDecoration");
            root.transform.SetParent(_decorationRoot, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.Euler(0f, localPosition.x < 0f ? -14f : 14f, 0f);
            root.transform.localScale = Vector3.one * Mathf.Max(0.2f, scale);

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            trunk.transform.localScale = new Vector3(0.055f, 0.14f, 0.055f);
            AssignMaterial(trunk, EnsureMaterial(ref _treeTrunkMaterial, "ResourceTreeTrunk_Runtime", treeTrunkColor));
            DestroyCollider(trunk);

            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown";
            crown.transform.SetParent(root.transform, false);
            crown.transform.localPosition = new Vector3(0f, 0.34f, 0f);
            crown.transform.localScale = new Vector3(0.26f, 0.22f, 0.26f);
            AssignMaterial(crown, EnsureMaterial(ref _treeCrownMaterial, "ResourceTreeCrown_Runtime", treeCrownColor));
            DestroyCollider(crown);

            var crownTop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crownTop.name = "CrownTop";
            crownTop.transform.SetParent(root.transform, false);
            crownTop.transform.localPosition = new Vector3(0.02f, 0.48f, -0.01f);
            crownTop.transform.localScale = new Vector3(0.18f, 0.16f, 0.18f);
            AssignMaterial(crownTop, EnsureMaterial(ref _treeCrownMaterial, "ResourceTreeCrown_Runtime", treeCrownColor));
            DestroyCollider(crownTop);
        }

        private void CreateRockRing()
        {
            var positions = new[]
            {
                new Vector3(-0.45f, decorationYOffset, 0.28f),
                new Vector3(-0.28f, decorationYOffset, 0.46f),
                new Vector3(0f, decorationYOffset, 0.52f),
                new Vector3(0.28f, decorationYOffset, 0.46f),
                new Vector3(0.45f, decorationYOffset, 0.28f),
                new Vector3(0.48f, decorationYOffset, -0.02f),
                new Vector3(0.35f, decorationYOffset, -0.34f),
                new Vector3(0.02f, decorationYOffset, -0.48f),
                new Vector3(-0.34f, decorationYOffset, -0.34f),
                new Vector3(-0.48f, decorationYOffset, -0.02f)
            };

            for (var i = 0; i < positions.Length; i++)
            {
                CreateRock(positions[i], i);
            }
        }

        private void CreateRock(Vector3 localPosition, int index)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = $"Rock_{index:00}";
            rock.transform.SetParent(_decorationRoot, false);
            rock.transform.localPosition = localPosition;
            rock.transform.localRotation = Quaternion.Euler(
                0f,
                index * 31f,
                index % 2 == 0 ? 8f : -7f);
            var width = 0.13f + (index % 3) * 0.025f;
            var height = 0.07f + (index % 2) * 0.025f;
            var depth = 0.11f + ((index + 1) % 3) * 0.02f;
            rock.transform.localScale = new Vector3(width, height, depth);
            var material = index % 4 == 0
                ? EnsureMaterial(ref _oreHighlightMaterial, "ResourceOreHighlight_Runtime", oreHighlightColor)
                : EnsureMaterial(ref _oreRockMaterial, "ResourceOreRock_Runtime", oreRockColor);
            AssignMaterial(rock, material);
            DestroyCollider(rock);
        }

        private static void AssignMaterial(GameObject target, Material material)
        {
            if (target == null || material == null)
            {
                return;
            }

            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Material EnsureMaterial(ref Material material, string materialName, Color color)
        {
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader)
            {
                name = materialName,
                color = color
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private static void DestroyCollider(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            var collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
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
