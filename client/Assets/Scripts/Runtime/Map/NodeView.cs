/*************************************************
 * Project: Panoptes
 * File: NodeView.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: 3D tile node view.
 *************************************************/

using UnityEngine;
using ProtoNodeView = Panoptes.Protocol.V1.NodeView;

namespace Panoptes.Runtime.Map
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
        [SerializeField] private Transform resourceAnchor;
        [SerializeField] private Transform buildingAnchor;
        [SerializeField] private Transform unitAnchor;

        [Header("Terrain Materials")]
        [SerializeField] private Material plainMaterial;
        [SerializeField] private Material mountainMaterial;
        [SerializeField] private Material forestMaterial;
        [SerializeField] private Material riverMaterial;

        [Header("Resource")]
        [SerializeField] private ResourcePointView resourcePointPrefab;

        [Header("Building Prefabs")]
        [SerializeField] private BuildingView defaultBuildingPrefab;
        [SerializeField] private BuildingPrefabEntry[] buildingPrefabs;

        public string NodeId { get; private set; } = string.Empty;
        public Vector2Int GridPos { get; private set; }
        public Transform UnitAnchor => unitAnchor;
        public Transform ResourceAnchor => resourceAnchor;
        public Transform BuildingAnchor => buildingAnchor;

        private ResourcePointView _resourceInstance;
        private string _resourceType = string.Empty;
        private BuildingView _buildingInstance;
        private string _buildingType = string.Empty;
        private readonly MaterialPropertyBlock _highlightBlock = new();

        /// <summary>
        /// Bind visual from protocol node data.
        /// </summary>
        public void Bind(ProtoNodeView node)
        {
            if (node == null)
            {
                Debug.LogWarning("[NodeView] Bind called with null node.");
                return;
            }

            NodeId = node.Id ?? string.Empty;
            GridPos = new Vector2Int(node.Pos?.X ?? 0, node.Pos?.Y ?? 0);
            name = $"Node_{NodeId}";

            SetTerrain(node.Terrain);
            SetRoadVisible(node.HasRoad);
            SetResource(node.IsResourcePoint, node.ResourceType);
            SetBuilding(node.BuildingType, node.Owner, node.BuildingHp, false);
            SetHighlightVisible(false);
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
        }

        public void SetRoadVisible(bool isVisible)
        {
            if (roadOverlay != null)
            {
                roadOverlay.SetActive(isVisible);
            }
        }

        public void SetHighlightVisible(bool isVisible)
        {
            if (highlight != null)
            {
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
            if (highlightRenderer == null && highlight != null)
            {
                highlightRenderer = highlight.GetComponentInChildren<Renderer>();
            }

            if (highlightRenderer == null)
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

        /// <summary>
        /// Update/clear resource visual according to resource point state.
        /// </summary>
        public void SetResource(bool isResourcePoint, string resourceType, bool isHighValue = false)
        {
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
        }

        /// <summary>
        /// Update/clear building visual according to building_type.
        /// </summary>
        public void SetBuilding(string buildingType, string ownerId, int buildingHp, bool isGhost)
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
                _buildingInstance.SetOwner(ownerId);
                _buildingInstance.SetHitPoints(buildingHp);
                _buildingInstance.SetPlacementGhost(isGhost);
            }

            _buildingType = normalized;
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
                _buildingInstance.SetOwner(ownerId);
                _buildingInstance.SetPlacementGhost(true, ghostColor);
            }

            _buildingType = normalized;
        }

        public void ClearResource()
        {
            if (_resourceInstance != null)
            {
                Destroy(_resourceInstance.gameObject);
                _resourceInstance = null;
            }
            _resourceType = string.Empty;
        }

        public void ClearBuilding()
        {
            if (_buildingInstance != null)
            {
                Destroy(_buildingInstance.gameObject);
                _buildingInstance = null;
            }
            _buildingType = string.Empty;
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
        }

        private Material GetTerrainMaterial(string terrain)
        {
            switch (NormalizeToken(terrain))
            {
                case "plain":
                    return plainMaterial;
                case "mountain":
                    return mountainMaterial;
                case "forest":
                    return forestMaterial;
                case "river":
                    return riverMaterial;
                default:
                    return plainMaterial;
            }
        }

        private BuildingView GetBuildingPrefab(string buildingType)
        {
            if (buildingPrefabs != null)
            {
                for (int i = 0; i < buildingPrefabs.Length; i++)
                {
                    if (NormalizeToken(buildingPrefabs[i].buildingType) == buildingType)
                    {
                        return buildingPrefabs[i].prefab;
                    }
                }
            }

            return defaultBuildingPrefab;
        }

        public BuildingView ResolveBuildingPrefab(string buildingType)
        {
            return GetBuildingPrefab(NormalizeToken(buildingType));
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
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
