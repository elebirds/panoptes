/*************************************************
 * Project: Panoptes
 * File: MapRenderer.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Map + unit runtime rendering manager.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Core.Application.App;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.UI.Common;
using R3;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.Map
{
    public sealed class MapRenderer : MonoBehaviour
    {
        [Header("Map Source")]
        [SerializeField] private bool useJsonMapOnStart = false;
        [SerializeField] private TextAsset startupMapJson;
        [SerializeField] private bool autoFillMissingJsonTiles = false;
        [SerializeField] private bool preferLocalMapWhenBackendHasNoTerritory = false;
        [SerializeField] private string localFallbackMapResourcePath = "Data/maps/default.runtime";
        [SerializeField] private bool preferServerPushedMapConfig = true;
        [SerializeField] private string serverMapConfigKey = "mapconfig";
        [SerializeField] private bool listenServerMapConfigUpdates = true;
        [Header("Prefab")]
        [SerializeField] private NodeView nodeTilePrefab;
        [SerializeField] private Transform tilesRoot;
        [SerializeField] private float tileSize = 1f;
        
        [Header("Terrain Elevation")]
        [SerializeField] private bool applyTerrainElevation = false;
        [SerializeField] private float plainElevation = 0f;
        [SerializeField] private float forestElevation = 0.06f;
        [SerializeField] private float mountainElevation = 0.28f;
        [SerializeField] private float riverElevation = -0.1f;
        [SerializeField] private float snowElevation = 0.1f;
        [SerializeField] private float forbiddenElevation = 0.2f;
        [SerializeField] private bool applyElevationNoise = true;
        [SerializeField] private float elevationNoiseAmplitude = 0.02f;
        [SerializeField] private float elevationNoiseScale = 0.15f;

        [Header("Units")]
        [SerializeField] private UnitView unitPrefab;
        [SerializeField] private Transform unitsRoot;
        [SerializeField] private string unitPrefabResourcesRoot = "Prefabs/Units";
        [SerializeField] private bool useDedicatedBaseVehiclePrefab = true;
        [SerializeField] private string baseVehiclePrefabResourcePath = "Prefabs/Units/BaseVehicle";
        [SerializeField] private string[] baseVehicleUnitTypes = { "settler", "pioneer", "expander", "engineer" };
        [SerializeField] private bool spawnDebugUnitsWhenNoUnits = false;
        [SerializeField] private string[] debugFactions = { "blue", "red", "green", "yellow" };

        [Header("Debug Generation (Local Only)")]
        [SerializeField] private bool generateDebugMapOnStart = false;
        [SerializeField] private bool generateDebugTerritoriesOnStart = false;
        [SerializeField] private bool generateDebugBuildingsOnStart = false;
        [SerializeField] private int debugMapWidth = 30;
        [SerializeField] private int debugMapHeight = 30;
        [SerializeField] private int debugBuildingsPerType = 2;
        [Range(0f, 1f)] [SerializeField] private float debugExtraBuildingSpawnRate = 0.04f;
        [SerializeField] private int randomSeed = 20260405;

        [Header("Resource Points")]
        [SerializeField] private bool autoInjectResourcePointsWhenSparse = true;
        [SerializeField] private int minimumResourcePoints = 18;

        [Header("Terrain Decorations")]
        [SerializeField] private bool autoSpawnTerrainDecorations = true;

        [Header("Map Backdrop")]
        [SerializeField] private bool autoSpawnMapBackdrop = true;

        [Header("Observation Fog")]
        [SerializeField] private bool useGlobalObservationFog = true;
        [SerializeField] private bool hideUnknownNodeDetails = true;

        private readonly Dictionary<string, NodeView> _tileViews = new();
        private readonly Dictionary<Vector2Int, NodeView> _tileViewsByGrid = new();
        private readonly Dictionary<string, NodeDto> _nodeStates = new();

        private readonly Dictionary<string, UnitView> _unitViews = new();
        private readonly Dictionary<string, string> _unitNodeById = new();
        private readonly Dictionary<string, HashSet<string>> _unitsByNodeId = new();
        private readonly Dictionary<string, UnitDto> _settlementPlaybackUnitSnapshots = new();
        private readonly List<string> _scratchRemovedUnitIds = new();
        private readonly List<NodeDto> _scratchChangedNodes = new();
        private UnitView _baseVehiclePrefabCache;
        private readonly Dictionary<string, GameObject> _catalogUnitPrefabCache = new(StringComparer.OrdinalIgnoreCase);
        private TerrainDecorationSpawner _terrainDecorationSpawner;
        private MapBackdropSpawner _mapBackdropSpawner;
        private MapFogOverlayController _mapFogOverlayController;
        private MapCameraContext _currentCameraContext;
        private bool _hasCameraContext;

        private readonly List<UnitDto> _jsonUnits = new();
        private readonly MapCameraContextBuilder _cameraContextBuilder = new();
        private AppManager _appManager;
        private ConfigCache _configCache;
        private StaticCatalogStore _staticCatalogStore;
        private GameStateStore _gameStateStore;
        private UnitCache _unitCache;
        private ErrorToast _errorToast;
        private GameStateStoreState _latestGameState = new();
        private IDisposable _gameStateSubscription;
        private IDisposable _staticCatalogSubscription;
        private bool _hasRenderedBackendMap;

        public IReadOnlyDictionary<string, NodeView> TileViews => _tileViews;
        public IReadOnlyDictionary<string, UnitView> UnitViews => _unitViews;
        public float TileSize => tileSize;
        public bool IsResolvingAuthoritativeState => GamePhases.IsResolving(GetGameStateSnapshot().Phase);
        public event Action<MapCameraContext> CameraContextReady;
        public event Action StatePresentationRefreshed;

        public bool TryGetCameraContext(out MapCameraContext context)
        {
            context = _currentCameraContext;
            return _hasCameraContext && context.IsValid;
        }

        [Inject]
        private void Construct(
            GameStateStore gameStateStore,
            StaticCatalogStore staticCatalogStore,
            AppManager appManager,
            ConfigCache configCache,
            UnitCache unitCache,
            ErrorToast errorToast)
        {
            _gameStateStore = gameStateStore;
            _staticCatalogStore = staticCatalogStore;
            _appManager = appManager;
            _configCache = configCache;
            _unitCache = unitCache;
            _errorToast = errorToast;
            _latestGameState = _gameStateStore?.Snapshot ?? new GameStateStoreState();
            if (isActiveAndEnabled)
            {
                SubscribeGameState();
                if (!IsGameRuntime())
                {
                    SubscribeServerMapConfig();
                }
            }
        }

        private void OnEnable()
        {
            SubscribeGameState();

            if (IsGameRuntime())
            {
                return;
            }

            SubscribeServerMapConfig();
        }

        private void OnDisable()
        {
            UnsubscribeGameState();
            UnsubscribeServerMapConfig();
        }

        private void Start()
        {
            if (!ShouldBuildOnStart())
            {
                return;
            }

            if (generateDebugMapOnStart)
            {
                BuildDebugMap();
                return;
            }

            if (IsGameRuntime())
            {
                if (!BuildBackendGameMap())
                {
                    return;
                }
                return;
            }

            if (useJsonMapOnStart && startupMapJson != null && LoadMapFromJsonAsset(startupMapJson))
            {
                return;
            }

            RebuildMap();
        }

        private bool ShouldBuildOnStart()
        {
            var state = GetGameStateSnapshot();
            if (HasBackendNodes(state))
            {
                return true;
            }

            var app = _appManager;
            if (app != null && app.State != AppState.Game)
            {
                return false;
            }

            return true;
        }

        public void RebuildMap()
        {
            if (IsGameRuntime())
            {
                BuildBackendGameMap();
                return;
            }

            var state = GetGameStateSnapshot();
            if (HasBackendNodes(state))
            {
                var backendNodes = SnapshotBackendNodes(state);
                if (!preferLocalMapWhenBackendHasNoTerritory || MapSourceResolver.HasTerritorySnapshot(backendNodes))
                {
                    PanoptesLog.Log($"[MapRenderer] Rebuild from backend nodes: {backendNodes.Count}");
                    PrepareRuntimeRoots();
                    BuildFromBackendNodes(backendNodes);
                    return;
                }
            }

            if (TryLoadToolSceneConfiguredMap())
            {
                return;
            }

            if (HasBackendNodes(state))
            {
                var backendNodes = SnapshotBackendNodes(state);
                PanoptesLog.Warning($"[MapRenderer] Using backend nodes despite incomplete territory snapshot: {backendNodes.Count}");
                PrepareRuntimeRoots();
                BuildFromBackendNodes(backendNodes);
                return;
            }

            PanoptesLog.Error("[MapRenderer] Cannot build map: no backend nodes or local tool-scene map source available.");
        }

        private bool BuildBackendGameMap()
        {
            if (_gameStateStore == null)
            {
                ReportBackendGameMapFailure("GameStateStore is not injected; cannot render server map.");
                return false;
            }

            var state = GetGameStateSnapshot();
            if (!HasBackendNodes(state))
            {
                PanoptesLog.Warning("[MapRenderer] Waiting for server map nodes before rendering game map.");
                return false;
            }

            var backendNodes = SnapshotBackendNodes(state);
            PanoptesLog.Log($"[MapRenderer] Rebuild game map from backend nodes: {backendNodes.Count}");
            BuildFromBackendNodes(backendNodes);
            _hasRenderedBackendMap = true;
            return true;
        }

        private void SubscribeGameState()
        {
            _gameStateSubscription?.Dispose();
            _gameStateSubscription = _gameStateStore?.State.Subscribe(this, static (state, self) => self.OnGameStateChanged(state));
            OnGameStateChanged(_gameStateStore?.Snapshot);
        }

        private void UnsubscribeGameState()
        {
            _gameStateSubscription?.Dispose();
            _gameStateSubscription = null;
        }

        private void OnGameStateChanged(GameStateStoreState state)
        {
            var incomingState = state ?? new GameStateStoreState();
            var wasResolving = GamePhases.IsResolving(_latestGameState?.Phase);
            var isResolving = GamePhases.IsResolving(incomingState.Phase);
            if (isResolving && !wasResolving)
            {
                CaptureSettlementPlaybackUnitSnapshots();
            }

            _latestGameState = incomingState;
            if (!isActiveAndEnabled || !HasBackendNodes(_latestGameState))
            {
                return;
            }

            RefreshRenderedGameState(_latestGameState);
            if (!isResolving)
            {
                CaptureSettlementPlaybackUnitSnapshots();
            }
        }

        private GameStateStoreState GetGameStateSnapshot()
        {
            return _latestGameState ?? _gameStateStore?.Snapshot ?? new GameStateStoreState();
        }

        private IReadOnlyDictionary<string, CatalogBuildingDto> GetBuildingCatalog()
        {
            return _staticCatalogStore?.Snapshot?.Buildings;
        }

        private string GetLocalPlayerId()
        {
            return GetGameStateSnapshot().MyPlayerId;
        }

        private static bool HasBackendNodes(GameStateStoreState state)
        {
            return state?.Nodes != null && state.Nodes.Count > 0;
        }

        private static bool HasBackendUnits(GameStateStoreState state)
        {
            return state?.Units != null && state.Units.Count > 0;
        }

        private static List<NodeDto> SnapshotBackendNodes(GameStateStoreState state)
        {
            var result = new List<NodeDto>();
            if (state?.Nodes == null)
            {
                return result;
            }

            foreach (var node in state.Nodes.Values)
            {
                if (node != null)
                {
                    result.Add(node);
                }
            }

            return result;
        }

        private static List<UnitDto> SnapshotBackendUnits(GameStateStoreState state)
        {
            var result = new List<UnitDto>();
            if (state?.Units == null)
            {
                return result;
            }

            foreach (var unit in state.Units.Values)
            {
                if (unit != null)
                {
                    result.Add(unit);
                }
            }

            return result;
        }

        private void RefreshRenderedGameState(GameStateStoreState state)
        {
            if (!HasBackendNodes(state))
            {
                return;
            }

            if (!_hasRenderedBackendMap || _tileViews.Count != state.Nodes.Count || HasMissingRenderedNode(state))
            {
                BuildFromBackendNodes(SnapshotBackendNodes(state));
                _hasRenderedBackendMap = true;
                return;
            }

            _scratchChangedNodes.Clear();
            foreach (var node in state.Nodes.Values)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id))
                {
                    continue;
                }

                _nodeStates.TryGetValue(node.Id, out var previousNode);
                if (NodeSnapshotsEqual(previousNode, node))
                {
                    continue;
                }

                var isResolving = GamePhases.IsResolving(state.Phase);
                var visualNode = isResolving
                    ? CreateResolvingVisualNode(node, previousNode)
                    : node;
                _nodeStates[node.Id] = node;
                if (_tileViews.TryGetValue(node.Id, out var view) && view != null)
                {
                    view.SetLocalPlayerId(GetLocalPlayerId());
                    view.SetBuildingCatalog(GetBuildingCatalog());
                    view.Bind(visualNode);
                }

                _scratchChangedNodes.Add(node);
            }

            if (_scratchChangedNodes.Count > 0)
            {
                RefreshObservationPresentationForChangedNodes(_scratchChangedNodes);
            }

            var unitsChanged = GamePhases.IsResolving(state.Phase)
                ? false
                : RefreshUnitsForCurrentSource();
            if (_scratchChangedNodes.Count > 0 || unitsChanged)
            {
                PublishCameraContext();
                StatePresentationRefreshed?.Invoke();
            }
        }

        public void ReconcileUnitsToCurrentState()
        {
            var unitsChanged = RefreshUnitsForCurrentSource();
            if (!unitsChanged)
            {
                return;
            }

            PublishCameraContext();
            StatePresentationRefreshed?.Invoke();
        }

        public void PrepareSettlementPlaybackUnits(IReadOnlyList<SettlementPlaybackStep> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                return;
            }

            if (_settlementPlaybackUnitSnapshots.Count == 0)
            {
                CaptureSettlementPlaybackUnitSnapshots();
            }

            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null)
                {
                    continue;
                }

                if (step.MoveEvent != null && !string.IsNullOrWhiteSpace(step.MoveEvent.UnitId))
                {
                    PlaceSettlementPlaybackUnitAtMoveStart(step.MoveEvent);
                }

                for (var impactIndex = 0; impactIndex < step.Impacts.Count; impactIndex++)
                {
                    var impact = step.Impacts[impactIndex];
                    EnsureSettlementPlaybackImpactUnit(impact?.DamageEvent);
                    EnsureSettlementPlaybackImpactUnit(impact?.DeathEvent);
                }
            }
        }

        public bool PlaceSettlementPlaybackUnitAtMoveStart(TurnEventDto moveEvent)
        {
            if (moveEvent == null || string.IsNullOrWhiteSpace(moveEvent.UnitId))
            {
                return false;
            }

            var startGrid = new Vector2Int(moveEvent.FromQ, moveEvent.FromR);
            if (!TryGetNodeIdByGrid(startGrid, out var startNodeId))
            {
                PanoptesLog.Warning($"[MapRenderer] Cannot place settlement unit '{moveEvent.UnitId}' at move start ({moveEvent.FromQ},{moveEvent.FromR}); node not found.");
                return false;
            }

            EnsureSettlementPlaybackUnit(moveEvent.UnitId, hasGrid: true, grid: startGrid, forceGrid: true);
            if (!_unitViews.TryGetValue(moveEvent.UnitId.Trim(), out var unitView) || unitView == null)
            {
                PanoptesLog.Warning($"[MapRenderer] Cannot place settlement unit '{moveEvent.UnitId}' at move start; unit view not found.");
                return false;
            }

            SetUnitNode(moveEvent.UnitId.Trim(), startNodeId);
            return true;
        }

        public bool ApplySettlementUnitHitPoints(string unitId, int hpAfter, int maxHp = 0)
        {
            if (string.IsNullOrWhiteSpace(unitId) || !_unitViews.TryGetValue(unitId.Trim(), out var unitView) || unitView == null)
            {
                return false;
            }

            unitView.SetHitPoints(hpAfter, maxHp);
            StatePresentationRefreshed?.Invoke();
            return true;
        }

        public bool ApplySettlementBuildingHitPoints(TurnEventDto evt, int hpAfter)
        {
            if (!TryResolveNodeForSettlementEvent(evt, out var nodeView) ||
                nodeView == null ||
                nodeView.BuildingInstance == null)
            {
                return false;
            }

            var maxHp = nodeView.BuildingInstance.MaxHitPoints;
            if (evt != null && evt.Data != null && evt.Data.TryGetValue("building_max_hp", out var rawMaxHp) && int.TryParse(rawMaxHp, out var parsedMaxHp))
            {
                maxHp = parsedMaxHp;
            }

            nodeView.BuildingInstance.SetHitPoints(hpAfter, maxHp);
            StatePresentationRefreshed?.Invoke();
            return true;
        }

        private bool TryResolveNodeForSettlementEvent(TurnEventDto evt, out NodeView nodeView)
        {
            nodeView = null;
            if (evt == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(evt.NodeId) && TryGetNodeView(evt.NodeId, out nodeView))
            {
                return nodeView != null;
            }

            return TryGetNodeIdByGrid(new Vector2Int(evt.PosQ, evt.PosR), out var nodeId) &&
                   TryGetNodeView(nodeId, out nodeView) &&
                   nodeView != null;
        }

        private void EnsureSettlementPlaybackImpactUnit(TurnEventDto evt)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.UnitId))
            {
                return;
            }

            var hasGrid = TryGetEventGrid(evt, out var grid);
            EnsureSettlementPlaybackUnit(evt.UnitId, hasGrid, grid, forceGrid: false);
        }

        private void EnsureSettlementPlaybackUnit(string unitId, bool hasGrid, Vector2Int grid, bool forceGrid)
        {
            var normalizedUnitId = string.IsNullOrWhiteSpace(unitId) ? string.Empty : unitId.Trim();
            if (string.IsNullOrEmpty(normalizedUnitId))
            {
                return;
            }

            var unit = ResolveSettlementPlaybackUnitSnapshot(normalizedUnitId);
            if (unit == null)
            {
                return;
            }

            if ((forceGrid || !_unitViews.ContainsKey(normalizedUnitId)) && hasGrid)
            {
                unit.Q = grid.x;
                unit.R = grid.y;
            }

            if (_unitViews.TryGetValue(normalizedUnitId, out var existingView) && existingView != null)
            {
                if (forceGrid)
                {
                    RebindRuntimeUnit(existingView, unit);
                }
                return;
            }

            TrySpawnUnitInternal(unit, false, false, _unitCache);
        }

        private UnitDto ResolveSettlementPlaybackUnitSnapshot(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return null;
            }

            var normalizedUnitId = unitId.Trim();
            if (_settlementPlaybackUnitSnapshots.TryGetValue(normalizedUnitId, out var snapshot) && snapshot != null)
            {
                return CloneUnitDto(snapshot);
            }

            var state = GetGameStateSnapshot();
            if (state?.Units != null && state.Units.TryGetValue(normalizedUnitId, out var currentUnit) && currentUnit != null)
            {
                return CloneUnitDto(currentUnit);
            }

            if (_unitViews.TryGetValue(normalizedUnitId, out var view) && view != null)
            {
                return CreateUnitSnapshot(view);
            }

            return null;
        }

        private void CaptureSettlementPlaybackUnitSnapshots()
        {
            _settlementPlaybackUnitSnapshots.Clear();
            foreach (var pair in _unitViews)
            {
                var view = pair.Value;
                if (view == null || string.IsNullOrWhiteSpace(view.UnitId))
                {
                    continue;
                }

                _settlementPlaybackUnitSnapshots[view.UnitId.Trim()] = CreateUnitSnapshot(view);
            }
        }

        private static UnitDto CreateUnitSnapshot(UnitView view)
        {
            if (view == null)
            {
                return null;
            }

            return new UnitDto
            {
                Id = view.UnitId,
                Type = view.UnitType,
                Owner = view.Faction,
                Q = view.GridPos.x,
                R = view.GridPos.y,
                Hp = view.HitPoints,
                MaxHp = view.MaxHitPoints
            };
        }

        private static UnitDto CloneUnitDto(UnitDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new UnitDto
            {
                Id = source.Id,
                Type = source.Type,
                Owner = source.Owner,
                Q = source.Q,
                R = source.R,
                Hp = source.Hp,
                MaxHp = source.MaxHp
            };
        }

        private static bool TryGetEventGrid(TurnEventDto evt, out Vector2Int grid)
        {
            grid = default;
            if (evt == null)
            {
                return false;
            }

            if (evt.Data != null &&
                (evt.Data.ContainsKey("pos_q") ||
                 evt.Data.ContainsKey("pos_r") ||
                 evt.Data.ContainsKey("q") ||
                 evt.Data.ContainsKey("r")))
            {
                grid = new Vector2Int(evt.PosQ, evt.PosR);
                return true;
            }

            return false;
        }

        private static NodeDto CreateResolvingVisualNode(NodeDto current, NodeDto previous)
        {
            if (current == null)
            {
                return null;
            }

            var visual = CloneNodeDto(current);
            if (previous != null &&
                string.Equals(previous.BuildingType ?? string.Empty, current.BuildingType ?? string.Empty, StringComparison.Ordinal))
            {
                visual.BuildingHp = previous.BuildingHp;
                visual.BuildingMaxHp = previous.BuildingMaxHp;
            }

            return visual;
        }

        private static NodeDto CloneNodeDto(NodeDto source)
        {
            if (source == null)
            {
                return null;
            }

            return new NodeDto
            {
                Id = source.Id,
                Q = source.Q,
                R = source.R,
                Type = source.Type,
                Owner = source.Owner,
                TerritoryOwner = source.TerritoryOwner,
                BuildingType = source.BuildingType,
                BuildingHp = source.BuildingHp,
                BuildingMaxHp = source.BuildingMaxHp,
                BuildingStatus = source.BuildingStatus,
                OperationSelectedRecipeId = source.OperationSelectedRecipeId,
                OperationCurrentProgress = source.OperationCurrentProgress,
                OperationRequiredProgress = source.OperationRequiredProgress,
                OperationBaseProgress = source.OperationBaseProgress,
                OperationBlockedReason = source.OperationBlockedReason,
                OperationBlockedMessage = source.OperationBlockedMessage,
                CityId = source.CityId,
                ServiceCityId = source.ServiceCityId,
                TakeoverProgress = source.TakeoverProgress,
                TakeoverRequired = source.TakeoverRequired,
                IsCityCore = source.IsCityCore,
                IsVisible = source.IsVisible,
                IsMemory = source.IsMemory,
                LastObservedTurn = source.LastObservedTurn,
                HasRoad = source.HasRoad,
                Terrain = source.Terrain,
                IsResourcePoint = source.IsResourcePoint,
                ResourceType = source.ResourceType,
                IsSafeZone = source.IsSafeZone
            };
        }

        private bool HasMissingRenderedNode(GameStateStoreState state)
        {
            if (state?.Nodes == null)
            {
                return false;
            }

            foreach (var nodeId in state.Nodes.Keys)
            {
                if (!_tileViews.ContainsKey(nodeId))
                {
                    return true;
                }
            }

            return false;
        }

        private void SubscribeServerMapConfig()
        {
            if (!listenServerMapConfigUpdates)
            {
                return;
            }

            _staticCatalogSubscription?.Dispose();
            _staticCatalogSubscription = _staticCatalogStore?.State.Subscribe(this, static (state, self) => self.OnStaticCatalogChanged(state));

            if (_configCache != null)
            {
                _configCache.ConfigUpdated -= OnServerMapConfigUpdated;
                _configCache.ConfigUpdated += OnServerMapConfigUpdated;
            }
        }

        private void UnsubscribeServerMapConfig()
        {
            _staticCatalogSubscription?.Dispose();
            _staticCatalogSubscription = null;

            if (_configCache != null)
            {
                _configCache.ConfigUpdated -= OnServerMapConfigUpdated;
            }
        }

        private void OnStaticCatalogChanged(StaticCatalogState state)
        {
            if (IsGameRuntime())
            {
                return;
            }

            if (HasBackendNodes(GetGameStateSnapshot()))
            {
                return;
            }

            RebuildMap();
        }

        private void OnServerMapConfigUpdated(string key)
        {
            if (IsGameRuntime())
            {
                return;
            }

            if (!listenServerMapConfigUpdates ||
                !string.Equals(MapRenderTokens.Normalize(key), MapRenderTokens.Normalize(serverMapConfigKey), System.StringComparison.Ordinal))
            {
                return;
            }

            if (HasBackendNodes(GetGameStateSnapshot()))
            {
                return;
            }

            RebuildMap();
        }

        private bool TryLoadToolSceneConfiguredMap()
        {
            if (preferServerPushedMapConfig && TryLoadToolSceneMapFromServerConfig())
            {
                return true;
            }

            if (TryLoadToolSceneMapFromStaticCatalog())
            {
                return true;
            }

            return TryLoadToolSceneMapFromLocalFallback();
        }

        private bool TryLoadToolSceneMapFromServerConfig()
        {
            return CreateSourceResolver().TryResolveServerConfig(_configCache, out var snapshot) &&
                   BuildFromSourceSnapshot(snapshot);
        }

        private bool TryLoadToolSceneMapFromStaticCatalog()
        {
            return CreateSourceResolver().TryResolveStaticCatalog(_staticCatalogStore?.Snapshot?.DefaultMap, out var snapshot) &&
                   BuildFromSourceSnapshot(snapshot);
        }

        private bool TryLoadToolSceneMapFromLocalFallback()
        {
            return CreateSourceResolver().TryResolveLocalFallback(out var snapshot) &&
                   BuildFromSourceSnapshot(snapshot);
        }

        private void ReportBackendGameMapFailure(string message)
        {
            ClearMap();
            ClearUnits();

            var resolvedMessage = string.IsNullOrWhiteSpace(message)
                ? "Server map failed to load."
                : message.Trim();
            PanoptesLog.Error($"[MapRenderer] {resolvedMessage}");

            if (_errorToast != null)
            {
                _errorToast.Show(resolvedMessage, false);
            }
        }

        private bool IsGameRuntime()
        {
            return _appManager != null && _appManager.State == AppState.Game;
        }

        public bool LoadMapFromJsonString(string json)
        {
            return CreateSourceResolver().TryParseJsonString(json, out var snapshot) &&
                   BuildFromSourceSnapshot(snapshot);
        }

        public bool LoadMapFromJsonAsset(TextAsset jsonAsset)
        {
            return CreateSourceResolver().TryParseJsonAsset(jsonAsset, out var snapshot) &&
                   BuildFromSourceSnapshot(snapshot);
        }

        private MapSourceResolver CreateSourceResolver()
        {
            return new MapSourceResolver(new MapSourceResolver.Options
            {
                AutoFillMissingJsonTiles = autoFillMissingJsonTiles,
                LocalFallbackMapResourcePath = localFallbackMapResourcePath,
                ServerMapConfigKey = serverMapConfigKey,
                BuildingCatalog = GetBuildingCatalog()
            });
        }

        private bool BuildFromSourceSnapshot(MapSourceSnapshot snapshot)
        {
            if (!snapshot.HasNodes)
            {
                return false;
            }

            _jsonUnits.Clear();
            if (snapshot.Units != null && snapshot.Units.Count > 0)
            {
                _jsonUnits.AddRange(snapshot.Units);
            }

            BuildFromNodes(snapshot.Nodes);
            return true;
        }

        private void BuildFromBackendNodes(List<NodeDto> nodes)
        {
            _jsonUnits.Clear();
            BuildFromNodes(nodes);
        }

        public void RefreshNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            if (!_tileViews.TryGetValue(nodeId, out var view) || view == null)
            {
                return;
            }

            if (_nodeStates.TryGetValue(nodeId, out var node))
            {
                view.Bind(node);
            }
        }

        public bool ApplyNodeSnapshot(NodeDto node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id))
            {
                return false;
            }

            _nodeStates[node.Id] = node;

            if (_tileViews.TryGetValue(node.Id, out var view) && view != null)
            {
                view.Bind(node);
            }

            RefreshObservationPresentation(fullRebuildFog: false, snapshotNode: node);

            return true;
        }

        public bool TryGetNodeView(string nodeId, out NodeView nodeView)
        {
            return _tileViews.TryGetValue(nodeId, out nodeView) && nodeView != null;
        }

        public bool TryGetNodeViewByGrid(Vector2Int gridPos, out NodeView nodeView)
        {
            return _tileViewsByGrid.TryGetValue(gridPos, out nodeView) && nodeView != null;
        }

        public bool TryGetNodeState(string nodeId, out NodeDto nodeState)
        {
            return _nodeStates.TryGetValue(nodeId, out nodeState) && nodeState != null;
        }

        public bool TryGetNodeTerritoryOwner(string nodeId, out string territoryOwner)
        {
            territoryOwner = string.Empty;
            if (!TryGetNodeState(nodeId, out var nodeState))
            {
                return false;
            }

            territoryOwner = MapRenderTokens.Normalize(nodeState.TerritoryOwner);
            if (string.IsNullOrEmpty(territoryOwner))
            {
                // Backward compatibility: old protocol may not send territory_owner.
                territoryOwner = MapRenderTokens.Normalize(nodeState.Owner);
            }
            return !string.IsNullOrEmpty(territoryOwner);
        }

        public bool IsNodeInTerritory(string nodeId, string ownerId)
        {
            if (!TryGetNodeTerritoryOwner(nodeId, out var territoryOwner))
            {
                return false;
            }

            return string.Equals(territoryOwner, MapRenderTokens.Normalize(ownerId), System.StringComparison.OrdinalIgnoreCase);
        }

        public bool IsNodeInSafeZone(string nodeId)
        {
            return TryGetNodeState(nodeId, out var nodeState) && nodeState.IsSafeZone;
        }

        public bool TryGetNodeIdByGrid(Vector2Int gridPos, out string nodeId)
        {
            nodeId = string.Empty;
            if (!_tileViewsByGrid.TryGetValue(gridPos, out var nodeView) || nodeView == null)
            {
                return false;
            }

            nodeId = nodeView.NodeId;
            return !string.IsNullOrEmpty(nodeId);
        }

        public bool TryGetUnitView(string unitId, out UnitView unitView)
        {
            return _unitViews.TryGetValue(unitId, out unitView) && unitView != null;
        }

        public bool TryGetUnitsOnNode(string nodeId, List<UnitView> units)
        {
            if (units == null)
            {
                return false;
            }

            units.Clear();
            if (string.IsNullOrWhiteSpace(nodeId) || !_unitsByNodeId.TryGetValue(nodeId, out var unitIds) || unitIds == null)
            {
                return false;
            }

            foreach (var unitId in unitIds)
            {
                if (string.IsNullOrWhiteSpace(unitId))
                {
                    continue;
                }

                if (_unitViews.TryGetValue(unitId, out var unitView) && unitView != null)
                {
                    units.Add(unitView);
                }
            }

            return units.Count > 0;
        }

        public bool TrySpawnRuntimeUnit(UnitDto unit, bool replaceIfExists = false, bool updateCache = true)
        {
            return TrySpawnUnitInternal(unit, replaceIfExists, updateCache);
        }

        public bool RemoveRuntimeUnit(string unitId, bool updateCache = true)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return false;
            }

            if (!_unitViews.TryGetValue(unitId, out var unitView) || unitView == null)
            {
                return false;
            }

            if (_unitNodeById.TryGetValue(unitId, out var oldNodeId) &&
                _unitsByNodeId.TryGetValue(oldNodeId, out var oldSet))
            {
                oldSet.Remove(unitId);
            }

            if (_unitCache != null)
            {
                _unitCache.Unregister(unitView);
            }

            Destroy(unitView.gameObject);
            _unitViews.Remove(unitId);
            _unitNodeById.Remove(unitId);

            return true;
        }

        public bool IsNodeResourcePoint(string nodeId)
        {
            return TryGetNodeState(nodeId, out var state) && state.IsResourcePoint;
        }

        public bool IsNodePassableForMove(string nodeId)
        {
            if (!TryGetNodeState(nodeId, out var state))
            {
                return false;
            }

            var terrain = MapRenderTokens.Normalize(state.Terrain);
            return terrain != "river" && terrain != "mountain";
        }

        public bool IsNodeBuildBaseAvailable(string nodeId)
        {
            if (!TryGetNodeState(nodeId, out var state))
            {
                return false;
            }

            return string.IsNullOrEmpty(MapRenderTokens.Normalize(state.BuildingType));
        }

        public bool ApplyBuildingPlacement(string nodeId, string buildingType, string ownerId, bool isGhost, int hp = 100, Color? ghostColor = null)
        {
            if (!TryGetNodeView(nodeId, out var nodeView))
            {
                return false;
            }

            if (isGhost)
            {
                if (TryGetNodeState(nodeId, out var existingState) &&
                    existingState != null &&
                    !string.IsNullOrWhiteSpace(existingState.BuildingType))
                {
                    return false;
                }

                nodeView.SetLocalPlayerId(GetLocalPlayerId());
                nodeView.SetBuildingGhost(buildingType, ownerId, ghostColor ?? new Color(0.6f, 1f, 0.6f, 0.9f));
                return true;
            }

            var maxHp = MapRenderTokens.ResolveBuildingMaxHp(buildingType, hp, GetBuildingCatalog());
            nodeView.SetLocalPlayerId(GetLocalPlayerId());
            nodeView.SetBuilding(buildingType, ownerId, hp, maxHp, false);

            if (_nodeStates.TryGetValue(nodeId, out var state) && state != null)
            {
                state.BuildingType = buildingType ?? string.Empty;
                state.Owner = ownerId ?? string.Empty;
                state.BuildingHp = hp;
                state.BuildingMaxHp = maxHp;
            }

            return true;
        }

        public void SetUnitNode(string unitId, string targetNodeId)
        {
            if (string.IsNullOrEmpty(unitId) || string.IsNullOrEmpty(targetNodeId))
            {
                return;
            }

            if (!_unitViews.TryGetValue(unitId, out var unitView) || unitView == null)
            {
                return;
            }

            if (!TryGetNodeView(targetNodeId, out var targetNode))
            {
                return;
            }

            var previousNodeId = string.Empty;
            if (_unitNodeById.TryGetValue(unitId, out var oldNodeId))
            {
                previousNodeId = oldNodeId;
                if (_unitsByNodeId.TryGetValue(oldNodeId, out var unitsAtOld))
                {
                    unitsAtOld.Remove(unitId);
                }
            }

            if (!_unitsByNodeId.TryGetValue(targetNodeId, out var unitsAtNew))
            {
                unitsAtNew = new HashSet<string>();
                _unitsByNodeId[targetNodeId] = unitsAtNew;
            }
            unitsAtNew.Add(unitId);
            _unitNodeById[unitId] = targetNodeId;

            unitView.SetGridPosition(targetNode.GridPos);
            unitView.transform.position = targetNode.ResolveUnitAnchorWorldPosition();

            if (!string.Equals(previousNodeId, targetNodeId, StringComparison.Ordinal))
            {
                StatePresentationRefreshed?.Invoke();
            }
        }

        public bool TryGetGridBounds(out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            minY = int.MaxValue;
            maxY = int.MinValue;

            if (_tileViewsByGrid.Count == 0)
            {
                return false;
            }

            foreach (var pair in _tileViewsByGrid)
            {
                var pos = pair.Key;
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
            }

            return true;
        }

        public Vector3 GridToWorld(int q, int r)
        {
            return HexGrid.AxialToWorld(q, r, tileSize);
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            var localPosition = tilesRoot != null
                ? tilesRoot.InverseTransformPoint(worldPosition)
                : transform.InverseTransformPoint(worldPosition);
            return HexGrid.WorldToAxial(localPosition, tileSize);
        }

        public bool TryGetNodeViewByWorld(Vector3 worldPosition, out NodeView nodeView)
        {
            return TryGetNodeViewByGrid(WorldToGrid(worldPosition), out nodeView);
        }

        private Vector3 GridToWorldWithTerrain(int q, int r, string terrain)
        {
            if (!applyTerrainElevation)
            {
                return GridToWorld(q, r);
            }

            var basePosition = GridToWorld(q, r);
            var elevation = GetTerrainElevation(terrain, q, r);
            return new Vector3(basePosition.x, elevation, basePosition.z);
        }

        private float GetTerrainElevation(string terrain, int x, int y)
        {
            float value;
            switch (MapRenderTokens.Normalize(terrain))
            {
                case "forest":
                    value = forestElevation;
                    break;
                case "mountain":
                    value = mountainElevation;
                    break;
                case "river":
                case "water":
                    value = riverElevation;
                    break;
                case "snow":
                    value = snowElevation;
                    break;
                case "forbidden":
                case "blocked":
                    value = forbiddenElevation;
                    break;
                default:
                    value = plainElevation;
                    break;
            }

            if (!applyElevationNoise || elevationNoiseAmplitude <= 0.0001f)
            {
                return value;
            }

            var nx = (x + randomSeed * 0.137f) * Mathf.Max(0.0001f, elevationNoiseScale);
            var ny = (y - randomSeed * 0.173f) * Mathf.Max(0.0001f, elevationNoiseScale);
            var noise = Mathf.PerlinNoise(nx, ny) * 2f - 1f;
            return value + noise * elevationNoiseAmplitude;
        }

        private void BuildDebugMap()
        {
            _jsonUnits.Clear();
            var nodes = DebugMapFactory.CreateNodes(new DebugMapFactory.Options
            {
                Width = debugMapWidth,
                Height = debugMapHeight,
                Seed = randomSeed,
                GenerateTerritories = generateDebugTerritoriesOnStart,
                GenerateBuildings = generateDebugBuildingsOnStart,
                BuildingsPerType = debugBuildingsPerType,
                ExtraBuildingSpawnRate = debugExtraBuildingSpawnRate
            });
            BuildFromNodes(nodes);
        }

        private void BuildFromNodes(IEnumerable<NodeDto> nodes)
        {
            if (nodeTilePrefab == null)
            {
                PanoptesLog.Error("[MapRenderer] NodeTile prefab is not assigned.");
                return;
            }

            PrepareRuntimeRoots();
            ClearMap();

            var nodeList = new List<NodeDto>();
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    if (node != null)
                    {
                        nodeList.Add(node);
                    }
                }
            }

            EnsureMinimumResourcePoints(nodeList);

            foreach (var node in nodeList)
            {
                if (string.IsNullOrEmpty(node.Id))
                {
                    continue;
                }

                var tile = Instantiate(nodeTilePrefab, EnsureTilesRoot(), false);
                tile.transform.localPosition = GridToWorldWithTerrain(node.Q, node.R, node.Terrain);
                tile.SetLocalPlayerId(GetLocalPlayerId());
                tile.SetBuildingCatalog(GetBuildingCatalog());
                tile.Bind(node);

                _tileViews[node.Id] = tile;
                _tileViewsByGrid[tile.GridPos] = tile;
                _nodeStates[node.Id] = node;
            }

            RebuildTerrainDecorations(nodeList);
            RebuildMapBackdrop();
            RefreshObservationPresentation(fullRebuildFog: true, snapshotNode: null);

            if (!GamePhases.IsResolving(GetGameStateSnapshot().Phase))
            {
                RebuildUnitsForCurrentSource();
            }
            PublishCameraContext();
            StatePresentationRefreshed?.Invoke();
        }

        private void PublishCameraContext()
        {
            if (!TryBuildCameraContext(out var context))
            {
                _hasCameraContext = false;
                _currentCameraContext = default;
                return;
            }

            _currentCameraContext = context;
            _hasCameraContext = true;
            CameraContextReady?.Invoke(context);
        }

        private bool TryBuildCameraContext(out MapCameraContext context)
        {
            return _cameraContextBuilder.TryBuild(
                _nodeStates,
                _tileViews,
                _unitViews,
                tileSize,
                plainElevation,
                GetGameStateSnapshot().MyPlayerId,
                IsBaseVehicleUnitType,
                out context);
        }

        private void RebuildTerrainDecorations(List<NodeDto> nodeList)
        {
            if (!autoSpawnTerrainDecorations)
            {
                return;
            }

            if (_terrainDecorationSpawner == null)
            {
                _terrainDecorationSpawner = GetComponent<TerrainDecorationSpawner>();
            }

            if (_terrainDecorationSpawner == null)
            {
                _terrainDecorationSpawner = gameObject.AddComponent<TerrainDecorationSpawner>();
            }

            _terrainDecorationSpawner.RebuildDecorations(nodeList, _tileViews);
        }

        private void RefreshObservationPresentation(bool fullRebuildFog, NodeDto snapshotNode)
        {
            var hasObservationData = HasObservationData(_nodeStates.Values);
            var hideUnknownNodeDetailsEffective = hideUnknownNodeDetails && hasObservationData;
            var hideUnknownGroundEffective = false;

            if (useGlobalObservationFog)
            {
                if (_mapFogOverlayController == null)
                {
                    _mapFogOverlayController = GetComponent<MapFogOverlayController>();
                }

                if (_mapFogOverlayController == null)
                {
                    _mapFogOverlayController = gameObject.AddComponent<MapFogOverlayController>();
                }

                _mapFogOverlayController.ConfigureUnknownCulling(
                    hideUnknownNodeDetailsEffective,
                    hideUnknownGroundEffective);

                if (fullRebuildFog)
                {
                    _mapFogOverlayController.Rebuild(_tileViews, _nodeStates, tileSize);
                }
                else if (snapshotNode != null)
                {
                    _mapFogOverlayController.ApplyNodeSnapshot(snapshotNode);
                }
            }
            else
            {
                if (_mapFogOverlayController == null)
                {
                    _mapFogOverlayController = GetComponent<MapFogOverlayController>();
                }

                if (_mapFogOverlayController != null)
                {
                    _mapFogOverlayController.ClearOverlay();
                }

                foreach (var pair in _tileViews)
                {
                    var tile = pair.Value;
                    if (tile == null)
                    {
                        continue;
                    }

                    tile.SetPerTileObservationFogEnabled(true);
                    tile.SetUnknownDetailCulling(false, false);
                }
            }

            if (_terrainDecorationSpawner != null)
            {
                _terrainDecorationSpawner.ApplyObservationState(_nodeStates, hideUnknownNodeDetailsEffective);
            }
        }

        private void RefreshObservationPresentationForChangedNodes(IReadOnlyList<NodeDto> changedNodes)
        {
            if (changedNodes == null || changedNodes.Count == 0)
            {
                return;
            }

            if (changedNodes.Count > 32)
            {
                RefreshObservationPresentation(fullRebuildFog: true, snapshotNode: null);
                return;
            }

            var hasObservationData = HasObservationData(_nodeStates.Values);
            var hideUnknownNodeDetailsEffective = hideUnknownNodeDetails && hasObservationData;
            var hideUnknownGroundEffective = false;

            if (useGlobalObservationFog)
            {
                if (_mapFogOverlayController == null)
                {
                    _mapFogOverlayController = GetComponent<MapFogOverlayController>();
                }

                if (_mapFogOverlayController == null)
                {
                    _mapFogOverlayController = gameObject.AddComponent<MapFogOverlayController>();
                }

                _mapFogOverlayController.ConfigureUnknownCulling(
                    hideUnknownNodeDetailsEffective,
                    hideUnknownGroundEffective);

                for (var i = 0; i < changedNodes.Count; i++)
                {
                    if (changedNodes[i] != null)
                    {
                        _mapFogOverlayController.ApplyNodeSnapshot(changedNodes[i]);
                    }
                }
            }
            else if (_mapFogOverlayController != null)
            {
                _mapFogOverlayController.ClearOverlay();
            }

            if (_terrainDecorationSpawner != null)
            {
                _terrainDecorationSpawner.ApplyObservationState(_nodeStates, hideUnknownNodeDetailsEffective);
            }
        }

        private static bool HasObservationData(IEnumerable<NodeDto> nodes)
        {
            if (nodes == null)
            {
                return false;
            }

            foreach (var node in nodes)
            {
                if (node != null && (node.IsVisible || node.IsMemory || node.LastObservedTurn > 0))
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildMapBackdrop()
        {
            if (!autoSpawnMapBackdrop)
            {
                return;
            }

            if (_mapBackdropSpawner == null)
            {
                _mapBackdropSpawner = GetComponent<MapBackdropSpawner>();
            }

            if (_mapBackdropSpawner == null)
            {
                _mapBackdropSpawner = gameObject.AddComponent<MapBackdropSpawner>();
            }

            _mapBackdropSpawner.RebuildBackdrop(_tileViews);
        }

        private void PrepareRuntimeRoots()
        {
            var tileRoot = EnsureTilesRoot();
            if (tileRoot != null)
            {
                tileRoot.position = Vector3.zero;
                tileRoot.rotation = Quaternion.identity;
                tileRoot.localScale = Vector3.one;
            }

            var unitRoot = EnsureUnitsRoot();
            if (unitRoot != null)
            {
                unitRoot.position = Vector3.zero;
                unitRoot.rotation = Quaternion.identity;
                unitRoot.localScale = Vector3.one;
            }
        }

        private void RebuildUnitsForCurrentSource()
        {
            ClearUnits();

            if (_jsonUnits.Count > 0)
            {
                BuildUnitsFromState(_jsonUnits);
                return;
            }

            var state = GetGameStateSnapshot();
            if (!generateDebugMapOnStart && HasBackendUnits(state))
            {
                BuildUnitsFromState(SnapshotBackendUnits(state));
                return;
            }

            if (spawnDebugUnitsWhenNoUnits)
            {
                BuildUnitsFromState(CreateDebugUnits());
            }
        }

        private bool RefreshUnitsForCurrentSource()
        {
            if (_jsonUnits.Count > 0)
            {
                RebuildUnitsForCurrentSource();
                return true;
            }

            var state = GetGameStateSnapshot();
            if (!generateDebugMapOnStart && HasBackendUnits(state))
            {
                return RefreshUnitsFromState(SnapshotBackendUnits(state));
            }

            if (spawnDebugUnitsWhenNoUnits)
            {
                RebuildUnitsForCurrentSource();
                return true;
            }

            if (_unitViews.Count == 0)
            {
                return false;
            }

            ClearUnits();
            return true;
        }

        private bool RefreshUnitsFromState(IReadOnlyList<UnitDto> units)
        {
            _scratchRemovedUnitIds.Clear();
            foreach (var pair in _unitViews)
            {
                _scratchRemovedUnitIds.Add(pair.Key);
            }

            var changed = false;
            if (units != null)
            {
                for (var i = 0; i < units.Count; i++)
                {
                    var unit = units[i];
                    if (unit == null || string.IsNullOrWhiteSpace(unit.Id))
                    {
                        continue;
                    }

                    var unitId = unit.Id;
                    _scratchRemovedUnitIds.Remove(unitId);

                    if (!_unitViews.TryGetValue(unitId, out var view) || view == null)
                    {
                        changed |= TrySpawnUnitInternal(unit, false, false, _unitCache);
                        continue;
                    }

                    if (UnitViewMatches(view, unit))
                    {
                        continue;
                    }

                    changed |= RebindRuntimeUnit(view, unit);
                }
            }

            for (var i = 0; i < _scratchRemovedUnitIds.Count; i++)
            {
                changed |= RemoveRuntimeUnit(_scratchRemovedUnitIds[i], false);
            }

            _scratchRemovedUnitIds.Clear();
            return changed;
        }

        private bool RebindRuntimeUnit(UnitView view, UnitDto unit)
        {
            if (view == null || unit == null || string.IsNullOrWhiteSpace(unit.Id))
            {
                return false;
            }

            if (!string.Equals(view.UnitType, unit.Type ?? string.Empty, StringComparison.Ordinal))
            {
                RemoveRuntimeUnit(unit.Id, false);
                return TrySpawnUnitInternal(unit, false, false, _unitCache);
            }

            var gridPos = new Vector2Int(unit.Q, unit.R);
            if (!TryGetNodeViewByGrid(gridPos, out var nodeView) || nodeView == null)
            {
                return false;
            }

            var previousNodeId = _unitNodeById.TryGetValue(unit.Id, out var oldNodeId)
                ? oldNodeId
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(previousNodeId) &&
                !string.Equals(previousNodeId, nodeView.NodeId, StringComparison.Ordinal) &&
                _unitsByNodeId.TryGetValue(previousNodeId, out var oldSet))
            {
                oldSet.Remove(unit.Id);
            }

            if (!_unitsByNodeId.TryGetValue(nodeView.NodeId, out var newSet))
            {
                newSet = new HashSet<string>();
                _unitsByNodeId[nodeView.NodeId] = newSet;
            }
            newSet.Add(unit.Id);
            _unitNodeById[unit.Id] = nodeView.NodeId;

            var worldPos = nodeView.ResolveUnitAnchorWorldPosition();
            view.SetLocalPlayerId(GetLocalPlayerId());
            view.Bind(unit, worldPos);
            return true;
        }

        private List<UnitDto> CreateDebugUnits()
        {
            var result = new List<UnitDto>();
            if (!TryGetGridBounds(out var minX, out var maxX, out var minY, out var maxY))
            {
                return result;
            }

            var corners = new[]
            {
                new Vector2Int(minX + 1, minY + 1),
                new Vector2Int(maxX - 1, minY + 1),
                new Vector2Int(minX + 1, maxY - 1),
                new Vector2Int(maxX - 1, maxY - 1)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                var p = corners[i];
                if (!TryGetNodeViewByGrid(p, out _))
                {
                    continue;
                }

                var faction = (debugFactions != null && i < debugFactions.Length)
                    ? debugFactions[i]
                    : $"faction_{i + 1}";

                result.Add(new UnitDto
                {
                    Id = $"U_DEBUG_{i + 1}",
                    Owner = faction,
                    Type = "infantry",
                    Hp = 100,
                    MaxHp = 100,
                    Q = p.x,
                    R = p.y,
                });
            }

            return result;
        }

        private void BuildUnitsFromState(IEnumerable<UnitDto> units)
        {
            if (units == null)
            {
                return;
            }

            foreach (var unit in units)
            {
                if (unit == null)
                {
                    continue;
                }

                TrySpawnUnitInternal(unit, false, false, _unitCache);
            }
        }

        private void EnsureMinimumResourcePoints(List<NodeDto> nodes)
        {
            if (!autoInjectResourcePointsWhenSparse || nodes == null || nodes.Count == 0)
            {
                return;
            }

            var target = Mathf.Max(0, minimumResourcePoints);
            if (target <= 0)
            {
                return;
            }

            var current = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node != null && node.IsResourcePoint)
                {
                    current++;
                }
            }

            if (current >= target)
            {
                return;
            }

            var outsideTerritory = new List<NodeDto>();
            var insideTerritory = new List<NodeDto>();

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!CanInjectResourcePoint(node))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.TerritoryOwner))
                {
                    outsideTerritory.Add(node);
                }
                else
                {
                    insideTerritory.Add(node);
                }
            }

            var rng = new System.Random(randomSeed ^ (nodes.Count * 31 + current * 131));
            var injected = 0;
            while (current + injected < target)
            {
                var selected = TakeRandomNode(outsideTerritory, rng);
                if (selected == null)
                {
                    selected = TakeRandomNode(insideTerritory, rng);
                }

                if (selected == null)
                {
                    break;
                }

                selected.IsResourcePoint = true;
                selected.ResourceType = ResolveInjectedResourceType(current + injected);
                injected++;
            }

            if (injected > 0)
            {
                PanoptesLog.Log($"[MapRenderer] Injected {injected} resource points (current={current + injected}, target={target}).");
            }
        }

        private static bool CanInjectResourcePoint(NodeDto node)
        {
            if (node == null)
            {
                return false;
            }

            if (node.IsResourcePoint)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(node.BuildingType))
            {
                return false;
            }

            var terrain = MapRenderTokens.Normalize(node.Terrain);
            return terrain != "river"
                   && terrain != "water"
                   && terrain != "forbidden"
                   && terrain != "blocked";
        }

        private static NodeDto TakeRandomNode(List<NodeDto> list, System.Random rng)
        {
            if (list == null || list.Count == 0 || rng == null)
            {
                return null;
            }

            var index = rng.Next(list.Count);
            var node = list[index];
            list.RemoveAt(index);
            return node;
        }

        private static string ResolveInjectedResourceType(int index)
        {
            switch (index % 3)
            {
                case 1:
                    return "wood";
                case 2:
                    return "ore";
                default:
                    return "food";
            }
        }

        private bool TrySpawnUnitInternal(UnitDto unit, bool replaceIfExists, bool updateCache, UnitCache unitCacheOverride = null)
        {
            if (unit == null || string.IsNullOrEmpty(unit.Id))
            {
                return false;
            }

            if (_unitViews.ContainsKey(unit.Id))
            {
                if (!replaceIfExists)
                {
                    return false;
                }

                RemoveRuntimeUnit(unit.Id, updateCache);
            }

            var gridPos = new Vector2Int(unit.Q, unit.R);
            if (!TryGetNodeViewByGrid(gridPos, out var nodeView) || nodeView == null)
            {
                return false;
            }

            var instance = CreateUnitInstance(unit);
            instance.transform.SetParent(EnsureUnitsRoot(), false);
            var worldPos = nodeView.ResolveUnitAnchorWorldPosition();
            instance.SetLocalPlayerId(GetLocalPlayerId());
            instance.Bind(unit, worldPos);

            _unitViews[unit.Id] = instance;
            _unitNodeById[unit.Id] = nodeView.NodeId;

            if (!_unitsByNodeId.TryGetValue(nodeView.NodeId, out var set))
            {
                set = new HashSet<string>();
                _unitsByNodeId[nodeView.NodeId] = set;
            }
            set.Add(unit.Id);

            var unitCache = unitCacheOverride != null ? unitCacheOverride : _unitCache;
            unitCache?.Register(instance);

            return true;
        }

        private static bool UnitViewMatches(UnitView view, UnitDto unit)
        {
            if (view == null || unit == null)
            {
                return false;
            }

            return string.Equals(view.UnitId, unit.Id ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(view.Faction, unit.Owner ?? string.Empty, StringComparison.Ordinal) &&
                   string.Equals(view.UnitType, unit.Type ?? string.Empty, StringComparison.Ordinal) &&
                   view.HitPoints == unit.Hp &&
                   view.MaxHitPoints == unit.MaxHp &&
                   view.GridPos.x == unit.Q &&
                   view.GridPos.y == unit.R;
        }

        private static bool NodeSnapshotsEqual(NodeDto left, NodeDto right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
                   left.Q == right.Q &&
                   left.R == right.R &&
                   string.Equals(left.Type, right.Type, StringComparison.Ordinal) &&
                   string.Equals(left.Owner, right.Owner, StringComparison.Ordinal) &&
                   string.Equals(left.TerritoryOwner, right.TerritoryOwner, StringComparison.Ordinal) &&
                   string.Equals(left.BuildingType, right.BuildingType, StringComparison.Ordinal) &&
                   left.BuildingHp == right.BuildingHp &&
                   left.BuildingMaxHp == right.BuildingMaxHp &&
                   string.Equals(left.BuildingStatus, right.BuildingStatus, StringComparison.Ordinal) &&
                   string.Equals(left.OperationSelectedRecipeId, right.OperationSelectedRecipeId, StringComparison.Ordinal) &&
                   left.OperationCurrentProgress == right.OperationCurrentProgress &&
                   left.OperationRequiredProgress == right.OperationRequiredProgress &&
                   left.OperationBaseProgress == right.OperationBaseProgress &&
                   string.Equals(left.OperationBlockedReason, right.OperationBlockedReason, StringComparison.Ordinal) &&
                   string.Equals(left.OperationBlockedMessage, right.OperationBlockedMessage, StringComparison.Ordinal) &&
                   string.Equals(left.CityId, right.CityId, StringComparison.Ordinal) &&
                   string.Equals(left.ServiceCityId, right.ServiceCityId, StringComparison.Ordinal) &&
                   left.TakeoverProgress == right.TakeoverProgress &&
                   left.TakeoverRequired == right.TakeoverRequired &&
                   left.IsCityCore == right.IsCityCore &&
                   left.IsVisible == right.IsVisible &&
                   left.IsMemory == right.IsMemory &&
                   left.LastObservedTurn == right.LastObservedTurn &&
                   left.HasRoad == right.HasRoad &&
                   string.Equals(left.Terrain, right.Terrain, StringComparison.Ordinal) &&
                   left.IsResourcePoint == right.IsResourcePoint &&
                   string.Equals(left.ResourceType, right.ResourceType, StringComparison.Ordinal) &&
                   left.IsSafeZone == right.IsSafeZone;
        }

        private UnitView CreateUnitInstance(UnitDto unit)
        {
            var dedicatedPrefab = ResolveDedicatedUnitPrefab(unit);
            if (dedicatedPrefab != null)
            {
                return Instantiate(dedicatedPrefab);
            }

            var catalogPrefab = ResolveCatalogUnitPrefab(unit);
            if (catalogPrefab != null)
            {
                return InstantiateCatalogUnitPrefab(catalogPrefab);
            }

            if (unitPrefab != null)
            {
                return Instantiate(unitPrefab);
            }

            // Fallback: build a runtime capsule unit if prefab is not assigned yet.
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Unit_Runtime";
            go.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            var view = go.GetComponent<UnitView>();
            if (view == null)
            {
                view = go.AddComponent<UnitView>();
            }
            return view;
        }

        private UnitView InstantiateCatalogUnitPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            var instance = Instantiate(prefab);
            var view = instance.GetComponent<UnitView>();
            if (view == null)
            {
                view = instance.AddComponent<UnitView>();
            }

            return view;
        }

        private GameObject ResolveCatalogUnitPrefab(UnitDto unit)
        {
            if (unit == null)
            {
                return null;
            }

            var unitType = MapRenderTokens.Normalize(unit.Type);
            if (string.IsNullOrEmpty(unitType) ||
                _staticCatalogStore?.Snapshot?.Units == null ||
                !_staticCatalogStore.Snapshot.Units.TryGetValue(unitType, out var entry) ||
                entry == null ||
                string.IsNullOrWhiteSpace(entry.PrefabKey))
            {
                return null;
            }

            return LoadCatalogUnitPrefab(entry.PrefabKey);
        }

        private GameObject LoadCatalogUnitPrefab(string prefabKey)
        {
            var trimmedKey = prefabKey?.Trim();
            if (string.IsNullOrEmpty(trimmedKey))
            {
                return null;
            }

            if (trimmedKey.Contains("/") || trimmedKey.Contains("\\"))
            {
                return LoadCatalogUnitPrefabByPath(trimmedKey.Replace('\\', '/'));
            }

            if (!string.IsNullOrWhiteSpace(unitPrefabResourcesRoot))
            {
                var rootedPath = $"{unitPrefabResourcesRoot.Trim().TrimEnd('/')}/{trimmedKey}";
                var rootedPrefab = LoadCatalogUnitPrefabByPath(rootedPath);
                if (rootedPrefab != null)
                {
                    return rootedPrefab;
                }
            }

            return LoadCatalogUnitPrefabByPath(trimmedKey);
        }

        private GameObject LoadCatalogUnitPrefabByPath(string resourcePath)
        {
            var path = resourcePath?.Trim();
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (_catalogUnitPrefabCache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var prefab = Resources.Load<GameObject>(path);
            _catalogUnitPrefabCache[path] = prefab;
            return prefab;
        }

        private UnitView ResolveDedicatedUnitPrefab(UnitDto unit)
        {
            if (!useDedicatedBaseVehiclePrefab || unit == null)
            {
                return null;
            }

            var unitType = MapRenderTokens.Normalize(unit.Type);
            if (!IsBaseVehicleUnitType(unitType))
            {
                return null;
            }

            if (_baseVehiclePrefabCache == null && !string.IsNullOrWhiteSpace(baseVehiclePrefabResourcePath))
            {
                _baseVehiclePrefabCache = Resources.Load<UnitView>(baseVehiclePrefabResourcePath.Trim());
            }

            return _baseVehiclePrefabCache;
        }

        private bool IsBaseVehicleUnitType(string unitType)
        {
            if (string.IsNullOrWhiteSpace(unitType) || baseVehicleUnitTypes == null || baseVehicleUnitTypes.Length == 0)
            {
                return false;
            }

            var normalized = MapRenderTokens.Normalize(unitType);
            for (var i = 0; i < baseVehicleUnitTypes.Length; i++)
            {
                if (string.Equals(normalized, MapRenderTokens.Normalize(baseVehicleUnitTypes[i]), System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private Transform EnsureTilesRoot()
        {
            if (tilesRoot != null)
            {
                return tilesRoot;
            }

            var root = new GameObject("TilesRoot");
            root.transform.SetParent(transform, false);
            tilesRoot = root.transform;
            return tilesRoot;
        }

        private Transform EnsureUnitsRoot()
        {
            if (unitsRoot != null)
            {
                return unitsRoot;
            }

            var root = new GameObject("UnitsRoot");
            root.transform.SetParent(transform, false);
            unitsRoot = root.transform;
            return unitsRoot;
        }

        private void ClearMap()
        {
            foreach (var pair in _tileViews)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            _tileViews.Clear();
            _tileViewsByGrid.Clear();
            _nodeStates.Clear();
            _hasRenderedBackendMap = false;
            if (_mapFogOverlayController != null)
            {
                _mapFogOverlayController.ClearOverlay();
            }
        }

        private void ClearUnits()
        {
            foreach (var pair in _unitViews)
            {
                if (pair.Value != null)
                {
                    if (_unitCache != null)
                    {
                        _unitCache.Unregister(pair.Value);
                    }

                    Destroy(pair.Value.gameObject);
                }
            }

            _unitViews.Clear();
            _unitNodeById.Clear();
            _unitsByNodeId.Clear();
        }
    }
}
