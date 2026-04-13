/*************************************************
 * Project: Panoptes
 * File: NodeView.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: 3D tile node view.
 *************************************************/

using UnityEngine;
using Panoptes.Core.Domain;

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

        [Header("Resource")]
        [SerializeField] private ResourcePointView resourcePointPrefab;

        [Header("Building Prefabs")]
        [SerializeField] private BuildingView defaultBuildingPrefab;
        [SerializeField] private BuildingPrefabEntry[] buildingPrefabs;

        [Header("Move Preview Marker")]
        [SerializeField] private bool autoCreateMoveMarker = true;
        [SerializeField] private float moveMarkerY = 0.13f;
        [SerializeField] private Vector3 moveArrowScale = new Vector3(0.12f, 0.01f, 0.42f);
        [SerializeField] private float moveArrowHeadScale = 0.15f;
        [SerializeField] private float moveDestinationScale = 0.24f;
        [SerializeField] private Color moveMarkerDefaultColor = new Color(0.35f, 1f, 0.45f, 0.9f);

        public string NodeId { get; private set; } = string.Empty;
        public Vector2Int GridPos { get; private set; }
        public Transform UnitAnchor => unitAnchor;
        public Transform ResourceAnchor => resourceAnchor;
        public Transform BuildingAnchor => buildingAnchor;
        public BuildingView BuildingInstance => _buildingInstance;
        public string BuildingType => _buildingType;

        private ResourcePointView _resourceInstance;
        private string _resourceType = string.Empty;
        private BuildingView _buildingInstance;
        private string _buildingType = string.Empty;
        private MaterialPropertyBlock _highlightBlock;
        private Transform _moveMarkerRoot;
        private Transform _moveArrowRoot;
        private Renderer _moveArrowShaftRenderer;
        private Renderer _moveArrowHeadLeftRenderer;
        private Renderer _moveArrowHeadRightRenderer;
        private Renderer _moveDestinationRenderer;
        private MaterialPropertyBlock _moveMarkerBlock;
        private Material _moveMarkerMaterial;

        private void Awake()
        {
            EnsureHighlightBlock();
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
            GridPos = new Vector2Int(node.X, node.Y);
            name = $"Node_{NodeId}";

            SetTerrain(string.IsNullOrWhiteSpace(node.Terrain) ? node.Type : node.Terrain);
            SetRoadVisible(node.HasRoad);
            SetResource(node.IsResourcePoint, node.ResourceType);
            SetBuilding(node.BuildingType, node.Owner, node.BuildingHp, node.BuildingMaxHp, false);
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

            var dir = new Vector3(direction.x, 0f, direction.y);
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

        private void SetMoveMarkerColor(Color color)
        {
            if (_moveMarkerBlock == null)
            {
                _moveMarkerBlock = new MaterialPropertyBlock();
            }

            ApplyMoveMarkerColor(_moveArrowShaftRenderer, color);
            ApplyMoveMarkerColor(_moveArrowHeadLeftRenderer, color);
            ApplyMoveMarkerColor(_moveArrowHeadRightRenderer, color);
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
                _buildingInstance.SetOwner(ownerId);
                _buildingInstance.SetHitPoints(buildingHp, buildingMaxHp);
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

            return defaultBuildingPrefab;
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
                case "engineer_camp":
                    return "engineer";
                default:
                    return token;
            }
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
