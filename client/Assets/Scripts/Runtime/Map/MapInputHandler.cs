/*************************************************
 * Project: Panoptes
 * File: MapInputHandler.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Map command input (move/build) + backend playback bridge.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Protocol.V1;
using Panoptes.Runtime.Animation;
using Panoptes.Runtime.Cache;
using Panoptes.Runtime.Network;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Panoptes.Runtime.Map
{
    public sealed class MapInputHandler : MonoBehaviour
    {
        public enum BuildPlacementRule
        {
            AnyTerrain = 0,
            ResourceOnly = 1,
            CityOnly = 2
        }

        [Serializable]
        public struct PendingBuildRecord
        {
            public string buildingType;
            public string nodeId;
            public string ownerId;
            public bool isGhost;
        }

        [Serializable]
        private struct CityZone
        {
            public string ownerId;
            public Vector2Int center;
            public Vector2Int size;
        }

        private enum Mode
        {
            None = 0,
            Move = 1,
            Build = 2
        }

        public static MapInputHandler Instance { get; private set; }

        [Header("Raycast")]
        [SerializeField] private Camera inputCamera;
        [SerializeField] private LayerMask raycastMask = ~0;
        [SerializeField] private float raycastDistance = 200f;

        [Header("Move")]
        [SerializeField] private int moveRange = 4;
        [SerializeField] private Color moveHighlightColor = new Color(0.35f, 1f, 0.45f, 0.9f);
        [SerializeField] private bool onlyControlOwnUnits = true;

        [Header("Build")]
        [SerializeField] private Color buildValidColor = new Color(0.35f, 1f, 0.35f, 0.92f);
        [SerializeField] private Color buildInvalidColor = new Color(1f, 0.3f, 0.3f, 0.92f);
        [SerializeField] private Color buildPlacedGhostColor = new Color(0.6f, 1f, 0.6f, 0.92f);
        [SerializeField] private bool logInvalidBuildClick = true;
        [SerializeField] private string localOwnerIdOverride = string.Empty;
        [SerializeField] private bool autoCreateCornerCityZones = true;
        [SerializeField] private int cornerInset = 2;
        [SerializeField] private CityZone[] cityZones;

        private readonly HashSet<string> _highlightNodeIds = new();
        private readonly List<PendingBuildRecord> _pendingBuilds = new();

        private Mode _mode = Mode.None;
        private UnitView _selectedUnit;
        private BuildPlacementRule _buildRule;
        private string _buildType = string.Empty;
        private NodeView _hoverNode;
        private BuildingView _hoverGhost;
        private bool _handlersRegistered;

        public IReadOnlyList<PendingBuildRecord> PendingBuilds => _pendingBuilds;

        public event Action<string, string> MoveCommandSent;
        public event Action<string, string> BuildCommandSent;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            RegisterNetworkHandlers();
        }

        private void OnDisable()
        {
            UnregisterNetworkHandlers();
        }

        private void Update()
        {
            if (!_handlersRegistered && MessageDispatcher.Instance != null)
            {
                RegisterNetworkHandlers();
            }

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }

            if (inputCamera == null || !HasMouse())
            {
                return;
            }

            if (_mode == Mode.Build)
            {
                UpdateBuildMode();
                return;
            }

            if (GetLeftMouseButtonDown())
            {
                if (IsPointerOverUI())
                {
                    return;
                }

                HandleMoveSelectionClick();
            }
        }

        public void EnterBuildPlacementAny(string buildingType)
        {
            EnterBuildPlacement(buildingType, BuildPlacementRule.AnyTerrain);
        }

        public void EnterBuildPlacementResource(string buildingType)
        {
            EnterBuildPlacement(buildingType, BuildPlacementRule.ResourceOnly);
        }

        public void EnterBuildPlacementCity(string buildingType)
        {
            EnterBuildPlacement(buildingType, BuildPlacementRule.CityOnly);
        }

        public void CancelCurrentMode()
        {
            ExitBuildMode();
            ClearMoveSelection();
            ClearNodeHighlights();
        }

        public void ApplyBackendMoveCommand(string unitId, string targetNodeId, bool enqueue = true, bool followCamera = true)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            if (enqueue)
            {
                var queue = AnimationQueue.Instance;
                if (queue == null)
                {
                    var go = new GameObject("AnimationQueue");
                    queue = go.AddComponent<AnimationQueue>();
                }

                if (queue != null)
                {
                    queue.EnqueueUnitMove(unitId, targetNodeId, followCamera);
                    return;
                }
            }

            if (MapRenderer.Instance != null)
            {
                MapRenderer.Instance.SetUnitNode(unitId, targetNodeId);
            }
        }

        public void ApplyBackendBuildCommand(string buildingType, string nodeId, bool isGhost, string ownerId, int hp = 100)
        {
            if (MapRenderer.Instance == null)
            {
                return;
            }

            MapRenderer.Instance.ApplyBuildingPlacement(nodeId, buildingType, ownerId, isGhost, hp, buildPlacedGhostColor);
            if (GameStateCache.Instance != null)
            {
                var cacheNode = GameStateCache.Instance.GetNode(nodeId);
                if (cacheNode != null)
                {
                    cacheNode.BuildingType = buildingType ?? string.Empty;
                    cacheNode.Owner = ownerId ?? string.Empty;
                    cacheNode.BuildingHp = hp;
                }
            }

            if (!isGhost)
            {
                RemovePendingBuild(nodeId);
            }
        }

        private void EnterBuildPlacement(string buildingType, BuildPlacementRule rule)
        {
            _mode = Mode.Build;
            _buildType = NormalizeToken(buildingType);
            _buildRule = rule;

            ClearMoveSelection();
            ClearNodeHighlights();
            DestroyHoverGhost();
            EnsureCityZones();
        }

        private void ExitBuildMode()
        {
            if (_hoverNode != null)
            {
                _hoverNode.SetHighlightVisible(false);
            }

            _mode = Mode.None;
            _buildType = string.Empty;
            _hoverNode = null;
            DestroyHoverGhost();
            ClearNodeHighlights();
        }

        private void HandleMoveSelectionClick()
        {
            if (TryRaycastUnit(out var unit))
            {
                SelectUnit(unit);
                return;
            }

            if (_selectedUnit != null && TryRaycastNode(out var node))
            {
                if (_highlightNodeIds.Contains(node.NodeId))
                {
                    SendMoveCommand(_selectedUnit.UnitId, node.NodeId);
                    ClearMoveSelection();
                    return;
                }
            }

            ClearMoveSelection();
        }

        private void SelectUnit(UnitView unit)
        {
            if (unit == null)
            {
                return;
            }

            if (onlyControlOwnUnits && !CanControlUnit(unit))
            {
                return;
            }

            ClearMoveSelection();
            _selectedUnit = unit;
            _selectedUnit.SetSelected(true);

            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            foreach (var pair in map.TileViews)
            {
                var node = pair.Value;
                if (node == null || node.GridPos == _selectedUnit.GridPos)
                {
                    continue;
                }

                if (!map.IsNodePassableForMove(node.NodeId))
                {
                    continue;
                }

                var dist = Mathf.Abs(node.GridPos.x - _selectedUnit.GridPos.x) + Mathf.Abs(node.GridPos.y - _selectedUnit.GridPos.y);
                if (dist > moveRange)
                {
                    continue;
                }

                node.SetHighlight(true, moveHighlightColor);
                _highlightNodeIds.Add(node.NodeId);
            }
        }

        private void ClearMoveSelection()
        {
            if (_selectedUnit != null)
            {
                _selectedUnit.SetSelected(false);
            }

            _selectedUnit = null;
            ClearNodeHighlights();
        }

        private void UpdateBuildMode()
        {
            var map = MapRenderer.Instance;
            if (map == null)
            {
                return;
            }

            if (GetRightMouseButtonDown())
            {
                ExitBuildMode();
                return;
            }

            var hasNode = TryRaycastNode(out var node);
            if (!hasNode)
            {
                if (_hoverNode != null)
                {
                    _hoverNode.SetHighlightVisible(false);
                }
                _hoverNode = null;
                DestroyHoverGhost();

                if (GetLeftMouseButtonDown() && !IsPointerOverUI() && logInvalidBuildClick)
                {
                    Debug.LogWarning("[MapInputHandler] Invalid build target: cursor is outside map tile.");
                }
                return;
            }

            var canPlace = CanPlaceBuildingAt(node.NodeId);
            var highlightColor = canPlace ? buildValidColor : buildInvalidColor;

            if (_hoverNode != node)
            {
                if (_hoverNode != null)
                {
                    _hoverNode.SetHighlightVisible(false);
                }

                _hoverNode = node;
                RecreateHoverGhost(node);
            }

            if (_hoverNode != null)
            {
                _hoverNode.SetHighlight(true, highlightColor);
            }

            if (_hoverGhost != null)
            {
                _hoverGhost.SetPlacementGhost(true, highlightColor);
            }

            if (GetLeftMouseButtonDown())
            {
                if (IsPointerOverUI())
                {
                    return;
                }

                if (!canPlace)
                {
                    if (logInvalidBuildClick)
                    {
                        Debug.LogWarning($"[MapInputHandler] Invalid build target at node '{node.NodeId}' for type '{_buildType}'.");
                    }
                    return;
                }

                var ownerId = GetLocalOwnerId();
                map.ApplyBuildingPlacement(node.NodeId, _buildType, ownerId, true, 100, buildPlacedGhostColor);
                _pendingBuilds.Add(new PendingBuildRecord
                {
                    buildingType = _buildType,
                    nodeId = node.NodeId,
                    ownerId = ownerId,
                    isGhost = true
                });

                SendBuildCommand(_buildType, node.NodeId);
                ExitBuildMode();
            }
        }

        private bool CanPlaceBuildingAt(string nodeId)
        {
            var map = MapRenderer.Instance;
            if (map == null || string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            if (!map.IsNodeBuildBaseAvailable(nodeId))
            {
                return false;
            }

            switch (_buildRule)
            {
                case BuildPlacementRule.ResourceOnly:
                    return map.IsNodeResourcePoint(nodeId);
                case BuildPlacementRule.CityOnly:
                    return IsInsideLocalCityZone(nodeId);
                default:
                    return true;
            }
        }

        private bool IsInsideLocalCityZone(string nodeId)
        {
            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetNodeView(nodeId, out var nodeView) || nodeView == null)
            {
                return false;
            }

            EnsureCityZones();
            var ownerId = GetLocalOwnerId();
            if (cityZones == null || cityZones.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < cityZones.Length; i++)
            {
                var zone = cityZones[i];
                if (!string.IsNullOrEmpty(zone.ownerId) && !string.Equals(zone.ownerId, ownerId, StringComparison.Ordinal))
                {
                    continue;
                }

                var halfX = Mathf.Max(0, zone.size.x / 2);
                var halfY = Mathf.Max(0, zone.size.y / 2);
                if (Mathf.Abs(nodeView.GridPos.x - zone.center.x) <= halfX &&
                    Mathf.Abs(nodeView.GridPos.y - zone.center.y) <= halfY)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureCityZones()
        {
            if (!autoCreateCornerCityZones)
            {
                return;
            }

            if (cityZones != null && cityZones.Length > 0)
            {
                return;
            }

            var map = MapRenderer.Instance;
            if (map == null || !map.TryGetGridBounds(out var minX, out var maxX, out var minY, out var maxY))
            {
                return;
            }

            var localOwner = GetLocalOwnerId();
            cityZones = new[]
            {
                new CityZone { ownerId = localOwner, center = new Vector2Int(minX + cornerInset, minY + cornerInset), size = new Vector2Int(3, 3) },
                new CityZone { ownerId = "red", center = new Vector2Int(maxX - cornerInset, minY + cornerInset), size = new Vector2Int(3, 3) },
                new CityZone { ownerId = "green", center = new Vector2Int(minX + cornerInset, maxY - cornerInset), size = new Vector2Int(3, 3) },
                new CityZone { ownerId = "yellow", center = new Vector2Int(maxX - cornerInset, maxY - cornerInset), size = new Vector2Int(3, 3) }
            };
        }

        private void RecreateHoverGhost(NodeView node)
        {
            DestroyHoverGhost();
            if (node == null || string.IsNullOrEmpty(_buildType))
            {
                return;
            }

            var prefab = node.ResolveBuildingPrefab(_buildType);
            if (prefab == null || node.BuildingAnchor == null)
            {
                return;
            }

            _hoverGhost = Instantiate(prefab, node.BuildingAnchor, false);
            _hoverGhost.SetBuildingType(_buildType);
            _hoverGhost.SetOwner(GetLocalOwnerId());
            _hoverGhost.SetPlacementGhost(true, buildValidColor);
        }

        private void DestroyHoverGhost()
        {
            if (_hoverGhost != null)
            {
                Destroy(_hoverGhost.gameObject);
                _hoverGhost = null;
            }
        }

        private void ClearNodeHighlights()
        {
            var map = MapRenderer.Instance;
            if (map == null)
            {
                _highlightNodeIds.Clear();
                return;
            }

            foreach (var nodeId in _highlightNodeIds)
            {
                if (map.TryGetNodeView(nodeId, out var node))
                {
                    node.SetHighlightVisible(false);
                }
            }

            _highlightNodeIds.Clear();
        }

        private void SendMoveCommand(string unitId, string targetNodeId)
        {
            var msg = new MsgTokenMicro
            {
                UnitId = unitId,
                TargetNode = targetNodeId
            };
            MessageSender.Send(msg);
            MoveCommandSent?.Invoke(unitId, targetNodeId);
        }

        private void SendBuildCommand(string buildingType, string nodeId)
        {
            var msg = new MsgTokenBuild
            {
                NodeId = nodeId,
                BuildingType = buildingType
            };
            MessageSender.Send(msg);
            BuildCommandSent?.Invoke(buildingType, nodeId);
        }

        private void RegisterNetworkHandlers()
        {
            if (_handlersRegistered || MessageDispatcher.Instance == null)
            {
                return;
            }

            MessageDispatcher.Instance.Register<MsgCombatSettlement>("MsgCombatSettlement", OnCombatSettlement);
            MessageDispatcher.Instance.Register<MsgDomesticSettlement>("MsgDomesticSettlement", OnDomesticSettlement);
            _handlersRegistered = true;
        }

        private void UnregisterNetworkHandlers()
        {
            if (!_handlersRegistered)
            {
                return;
            }

            if (MessageDispatcher.Instance != null)
            {
                MessageDispatcher.Instance.Unregister("MsgCombatSettlement");
                MessageDispatcher.Instance.Unregister("MsgDomesticSettlement");
            }

            _handlersRegistered = false;
        }

        private void OnCombatSettlement(MsgCombatSettlement msg)
        {
            if (msg == null || msg.Events == null || msg.Events.Count == 0)
            {
                return;
            }

            for (int i = 0; i < msg.Events.Count; i++)
            {
                var e = msg.Events[i];
                if (e == null || e.DataCase != CombatEvent.DataOneofCase.UnitMove || e.UnitMove == null || e.UnitMove.To == null)
                {
                    continue;
                }

                if (MapRenderer.Instance == null)
                {
                    continue;
                }

                var grid = new Vector2Int(e.UnitMove.To.X, e.UnitMove.To.Y);
                if (!MapRenderer.Instance.TryGetNodeIdByGrid(grid, out var targetNodeId))
                {
                    continue;
                }

                ApplyBackendMoveCommand(e.UnitMove.UnitId, targetNodeId, true, true);
            }
        }

        private void OnDomesticSettlement(MsgDomesticSettlement msg)
        {
            if (msg == null || msg.Changes == null)
            {
                return;
            }

            for (int i = 0; i < msg.Changes.Count; i++)
            {
                var change = msg.Changes[i];
                if (change == null)
                {
                    continue;
                }

                var type = NormalizeToken(change.Type);
                if (type != "building_built" && type != "building_preview" && type != "building_placed" && type != "build_request")
                {
                    continue;
                }

                var nodeId = GetMapValue(change.Data, "node_id", "target_node", "node");
                var buildingType = GetMapValue(change.Data, "building_type", "building", "type");
                var ownerId = GetMapValue(change.Data, "owner", "owner_id", "faction");
                var ghostRaw = GetMapValue(change.Data, "is_ghost", "ghost");
                var hpRaw = GetMapValue(change.Data, "building_hp", "hp_after", "hp");

                if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(buildingType))
                {
                    continue;
                }

                var isGhost = ParseBool(ghostRaw);
                var hp = ParseInt(hpRaw, 100);
                ApplyBackendBuildCommand(buildingType, nodeId, isGhost, ownerId, hp);
            }
        }

        private static string GetMapValue(Google.Protobuf.Collections.MapField<string, string> map, params string[] keys)
        {
            if (map == null || keys == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                if (map.TryGetValue(keys[i], out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static bool ParseBool(string value)
        {
            return value == "1" || value == "true" || value == "True";
        }

        private static int ParseInt(string value, int fallback)
        {
            if (int.TryParse(value, out var parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private void RemovePendingBuild(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            for (int i = _pendingBuilds.Count - 1; i >= 0; i--)
            {
                if (_pendingBuilds[i].nodeId == nodeId)
                {
                    _pendingBuilds.RemoveAt(i);
                }
            }
        }

        private string GetLocalOwnerId()
        {
            if (!string.IsNullOrEmpty(localOwnerIdOverride))
            {
                return localOwnerIdOverride.Trim();
            }

            if (GameStateCache.Instance != null && !string.IsNullOrEmpty(GameStateCache.Instance.MyPlayerID))
            {
                return GameStateCache.Instance.MyPlayerID;
            }

            return "blue";
        }

        private bool CanControlUnit(UnitView unit)
        {
            if (unit == null)
            {
                return false;
            }

            var localOwner = GetLocalOwnerId();
            return string.Equals(unit.Faction, localOwner, StringComparison.Ordinal);
        }

        private bool TryRaycastNode(out NodeView nodeView)
        {
            nodeView = null;
            if (!TryRaycast(out var hit))
            {
                return false;
            }

            nodeView = hit.collider.GetComponentInParent<NodeView>();
            if (nodeView == null)
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map != null && !map.IsNodeInteractable(nodeView.NodeId))
            {
                nodeView = null;
                return false;
            }

            return true;
        }

        private bool TryRaycastUnit(out UnitView unitView)
        {
            unitView = null;
            if (!TryRaycast(out var hit))
            {
                return false;
            }

            unitView = hit.collider.GetComponentInParent<UnitView>();
            return unitView != null;
        }

        private bool TryRaycast(out RaycastHit hit)
        {
            hit = default;

            var ray = inputCamera.ScreenPointToRay(GetMousePosition());
            return Physics.Raycast(ray, out hit, raycastDistance, raycastMask, QueryTriggerInteraction.Ignore);
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private bool HasMouse()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null;
#else
            return true;
#endif
        }

        private Vector3 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null ? (Vector3)mouse.position.ReadValue() : Vector3.zero;
#else
            return Input.mousePosition;
#endif
        }

        private bool GetLeftMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private bool GetRightMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(1);
#endif
        }
    }
}
