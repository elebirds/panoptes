/*************************************************
 * Project: Panoptes
 * File: GameStateCache.cs
 * Author: Panoptes Team
 * Date: 2026-04-14
 * Description: Mirrors server-authoritative game state for planning and settlement playback.
 *************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Core.Infrastructure.Mapper;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Application.Cache
{
    public class GameStateCache : MonoBehaviour
    {
        public static GameStateCache Instance { get; private set; }

        public string GameID { get; private set; }
        public string ActiveGameSessionID { get; private set; }
        public string MyPlayerID { get; private set; }
        public int Turn { get; private set; }
        public string Phase { get; private set; }
        public int MapWidth { get; private set; }
        public int MapHeight { get; private set; }
        public bool IsGameOver { get; private set; }

        private readonly Dictionary<string, NodeDto> _nodes = new();
        public IReadOnlyDictionary<string, NodeDto> Nodes => _nodes;

        private readonly Dictionary<string, UnitDto> _units = new();
        public IReadOnlyDictionary<string, UnitDto> Units => _units;

        private readonly Dictionary<string, List<CityBuiltBuildingDto>> _cityBuiltBuildings = new();
        private readonly Dictionary<string, ResourceDto> _cityResources = new();
        private readonly Dictionary<string, CityDto> _citiesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BuildingDto> _buildingsByNodeId = new(StringComparer.OrdinalIgnoreCase);
        // 这里保留最近一次 planning start 的事件，主要给客户端调试、日志和轻量展示使用。
        // 本轮先不接动画播放，但把 activation 边界明确缓存下来，后续需要展示时可直接复用。
        private readonly List<TurnEventDto> _lastPlanningStartEvents = new();
        private TechnologyDto _researchState = new();
        private InstitutionStateDto _institutionState = new();

        public PlayerView MyPlayer { get; private set; }
        public IReadOnlyList<TurnEventDto> LastPlanningStartEvents => _lastPlanningStartEvents;

        private readonly List<MinisterView> _ministers = new();
        public IReadOnlyList<MinisterView> Ministers => _ministers;

        public int TokensLeft { get; private set; }
        public int EnemyCityCoreHP { get; private set; }
        public int EnemyMaxCityCoreHP { get; private set; }

        private StaticCatalogCache _staticCatalogCache;
        private PlanningDraftCache _planningDraftCache;
        private GameChatCache _gameChatCache;

        public event Action OnStateChanged;
        public event Action<PhaseChangedEvent> OnPhaseChanged;
        public event Action<ResourcesChangedEvent> OnResourcesChanged;
        public event Action<TokensChangedEvent> OnTokensChanged;
        public event Action<NodeChangedEvent> OnNodeChanged;
        public event Action<UnitsChangedEvent> OnUnitsChanged;
        public event Action<CityCoreHpChangedEvent> OnCityCoreHPChanged;
        public event Action<TurnSettledEvent> OnTurnSettled;
        public event Action<MinisterChunkEvent> OnMinisterChunk;
        public event Action<MinisterMetricsEvent> OnMinisterMetrics;
        public event Action<TokenResultEvent> OnTokenResult;
        public event Action<RevealResultEvent> OnRevealResult;
        public event Action<PlanningCommandResultEvent> OnPlanningCommandResult;
        public event Action<GameOverEvent> OnGameOver;
        public event Action<GameErrorEvent> OnGameError;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void UseProjectCaches(
            StaticCatalogCache staticCatalogCache,
            PlanningDraftCache planningDraftCache,
            GameChatCache gameChatCache)
        {
            _staticCatalogCache = staticCatalogCache;
            _planningDraftCache = planningDraftCache;
            _gameChatCache = gameChatCache;
        }

        public void ApplyGameInit(MsgGameInit msg)
        {
            if (msg == null)
            {
                return;
            }

            GameID = msg.GameId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ActiveGameSessionID))
            {
                ActiveGameSessionID = GameID;
            }
            MyPlayerID = msg.YourPlayerId ?? string.Empty;
            Turn = msg.Turn;
            Phase = NormalizePhase(msg.Phase, GamePhases.Planning);
            MapWidth = msg.MapWidth;
            MapHeight = msg.MapHeight;
            IsGameOver = false;

            ReplaceNodes(msg.Nodes, publishChanges: false, changeType: "init");
            ReplaceUnits(msg.Units, publishChanges: false, changeType: "init");
            if (_nodes.Count > 0)
            {
                var firstNodeId = _nodes.Values.FirstOrDefault()?.Id ?? string.Empty;
                Debug.Log($"[Game] 游戏初始化 turn={Turn} phase={Phase} nodes={_nodes.Count} first_node={firstNodeId}");
            }
            else
            {
                Debug.LogWarning($"[Game] 游戏初始化缺少地图节点 turn={Turn} phase={Phase}");
            }

            MyPlayer = msg.MyPlayer?.Clone();
            TokensLeft = MyPlayer != null ? MyPlayer.TokensLeft : 0;

            _ministers.Clear();
            if (msg.Ministers != null)
            {
                for (var i = 0; i < msg.Ministers.Count; i++)
                {
                    if (msg.Ministers[i] != null)
                    {
                        _ministers.Add(msg.Ministers[i].Clone());
                    }
                }
            }

            _cityBuiltBuildings.Clear();
            SeedCityResourcesFromCurrentState();
            SynchronizeCityCoreState();
            _lastPlanningStartEvents.Clear();
            RebuildAuthoritativeProjections();

            PublishPhaseState(Turn, Phase, 0, TokensLeft, string.Empty);
            Fire(OnResourcesChanged, new ResourcesChangedEvent
            {
                Resources = GameStateCacheReadQueries.SnapshotResources(MyPlayer),
                Delta = new ResourceDto()
            }, nameof(OnResourcesChanged));
            Fire(OnTokensChanged, new TokensChangedEvent
            {
                TokensLeft = TokensLeft,
                Action = "recharge"
            }, nameof(OnTokensChanged));
            Fire(OnUnitsChanged, new UnitsChangedEvent
            {
                Added = _units.Values.Select(CloneUnitDto).ToList(),
                RemovedIDs = new List<string>(),
                Moved = new List<UnitDto>(),
                ChangeType = "init"
            }, nameof(OnUnitsChanged));
            foreach (var node in _nodes.Values)
            {
                Fire(OnNodeChanged, new NodeChangedEvent
                {
                    NodeID = node.Id,
                    Node = node,
                    ChangeType = "init"
                }, nameof(OnNodeChanged));
            }

            Fire(OnCityCoreHPChanged, new CityCoreHpChangedEvent
            {
                MyHP = MyPlayer != null ? MyPlayer.CapitalCityCoreHp : 0,
                MyMaxHP = MyPlayer != null ? MyPlayer.CapitalCityCoreMaxHp : 0,
                EnemyHP = EnemyCityCoreHP,
                EnemyMaxHP = EnemyMaxCityCoreHP,
                MyDelta = 0,
                EnemyDelta = 0
            }, nameof(OnCityCoreHPChanged));

            OnStateChanged?.Invoke();
        }

        public void ApplyPlanningStart(MsgPlanningStart msg)
        {
            if (msg == null)
            {
                return;
            }

            ApplyPlanningStartActiveState(msg);
            ApplyPlanningStartDraft(msg.Snapshot);
            // planning start 事件与 active state 一起进缓存，
            // 这样客户端既拿到“当前已经生效后的快照”，也保留“这次为什么生效”的事件面。
            _lastPlanningStartEvents.Clear();
            _lastPlanningStartEvents.AddRange(SettlementMapper.ToPlanningStartEvents(msg));
            RebuildAuthoritativeProjections();
            PublishPhaseState(Turn, Phase, msg.Timeout, TokensLeft, string.Empty);
            OnStateChanged?.Invoke();
        }

        public void ApplyPlanningSnapshot(MsgPlanningSnapshot msg)
        {
            if (msg == null)
            {
                return;
            }

            if (msg.Turn > 0)
            {
                Turn = msg.Turn;
            }

            Phase = NormalizePhase(msg.Phase, GamePhases.Planning);
            _planningDraftCache?.ApplyPlanningSnapshot(msg);
            PublishPhaseState(Turn, Phase, 0, TokensLeft, string.Empty);
            OnStateChanged?.Invoke();
        }

        public void ApplyGameSync(MsgGameSync msg)
        {
            if (msg == null)
            {
                return;
            }

            var resourcesBefore = GameStateCacheReadQueries.SnapshotResources(MyPlayer);
            var myHpBefore = MyPlayer != null ? MyPlayer.CapitalCityCoreHp : 0;
            var enemyHpBefore = EnemyCityCoreHP;
            var oldUnits = CloneUnitMap(_units);

            Turn = msg.Turn > 0 ? msg.Turn : Turn;
            Phase = NormalizePhase(msg.Phase, GamePhases.Resolving);

            ReplaceNodes(msg.Nodes, publishChanges: true, changeType: "game_sync");
            var unitChanges = ReplaceUnits(msg.Units, publishChanges: true, oldUnits, "game_sync");

            if (msg.MyPlayer != null)
            {
                MyPlayer = msg.MyPlayer.Clone();
            }

            TokensLeft = MyPlayer != null ? MyPlayer.TokensLeft : TokensLeft;
            SeedCityResourcesFromCurrentState();
            SynchronizeCityCoreState();
            RebuildAuthoritativeProjections();

            var resourcesAfter = GameStateCacheReadQueries.SnapshotResources(MyPlayer);
            Fire(OnResourcesChanged, new ResourcesChangedEvent
            {
                Resources = resourcesAfter,
                Delta = ComputeResourceDelta(resourcesBefore, resourcesAfter)
            }, nameof(OnResourcesChanged));

            if (myHpBefore != (MyPlayer != null ? MyPlayer.CapitalCityCoreHp : 0) || enemyHpBefore != EnemyCityCoreHP)
            {
                Fire(OnCityCoreHPChanged, new CityCoreHpChangedEvent
                {
                    MyHP = MyPlayer != null ? MyPlayer.CapitalCityCoreHp : 0,
                    MyMaxHP = MyPlayer != null ? MyPlayer.CapitalCityCoreMaxHp : 0,
                    EnemyHP = EnemyCityCoreHP,
                    EnemyMaxHP = EnemyMaxCityCoreHP,
                    MyDelta = (MyPlayer != null ? MyPlayer.CapitalCityCoreHp : 0) - myHpBefore,
                    EnemyDelta = EnemyCityCoreHP - enemyHpBefore
                }, nameof(OnCityCoreHPChanged));
            }

            var settlement = SettlementMapper.ToDto(msg);
            TrackCityBuiltBuildings(settlement);
            _planningDraftCache?.ClearAll();

            Fire(OnTurnSettled, new TurnSettledEvent
            {
                Settlement = settlement,
                ResourcesAfter = resourcesAfter,
                BuiltNodeIDs = settlement?.BuiltNodeIDs ?? new List<string>(),
                MovedUnitIDs = settlement?.MovedUnitIDs ?? unitChanges.Moved.Select(unit => unit.Id).ToList(),
                DeadUnitIDs = settlement?.DeadUnitIDs ?? unitChanges.RemovedIDs,
                CityCoreDamaged = settlement != null && settlement.CityCoreDamaged
            }, nameof(OnTurnSettled));

            PublishPhaseState(Turn, Phase, 0, TokensLeft, msg.NextPhase ?? string.Empty);
            OnStateChanged?.Invoke();
        }

        public void ApplyGameOver(MsgGameOver msg)
        {
            if (msg == null)
            {
                return;
            }

            IsGameOver = true;
            var isWinner = string.Equals(msg.WinnerId, MyPlayerID, StringComparison.Ordinal) ||
                           string.Equals(msg.WinnerId, MyPlayer != null ? MyPlayer.Id : string.Empty, StringComparison.Ordinal);
            Fire(OnGameOver, new GameOverEvent
            {
                WinnerID = msg.WinnerId,
                LoserID = ResolveLikelyLoserId(msg.WinnerId, isWinner),
                Reason = msg.Reason,
                Narrative = msg.Narrative,
                IsWinner = isWinner
            }, nameof(OnGameOver));

            OnStateChanged?.Invoke();
        }

        public void UpdateTokens(int tokensLeft)
        {
            var old = TokensLeft;
            TokensLeft = tokensLeft;
            if (MyPlayer != null)
            {
                MyPlayer.TokensLeft = tokensLeft;
            }

            Fire(OnTokensChanged, new TokensChangedEvent
            {
                TokensLeft = tokensLeft,
                Action = tokensLeft > old ? "recharge" : "consume"
            }, nameof(OnTokensChanged));

            OnStateChanged?.Invoke();
        }

        public void UpdateNode(NodeDto node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id))
            {
                return;
            }

            _nodes.TryGetValue(node.Id, out var previousNode);
            StabilizeNodeTerrain(node, previousNode);
            StabilizeNodeBuildingMaxHp(node, previousNode);
            _nodes[node.Id] = node;
            RebuildAuthoritativeProjections();
            Fire(OnNodeChanged, new NodeChangedEvent
            {
                NodeID = node.Id,
                Node = node,
                ChangeType = "reveal"
            }, nameof(OnNodeChanged));
            OnStateChanged?.Invoke();
        }

        public NodeDto GetNode(string nodeId)
        {
            _nodes.TryGetValue(nodeId, out var node);
            return node;
        }

        public UnitDto GetUnit(string unitId)
        {
            _units.TryGetValue(unitId, out var unit);
            return unit;
        }

        public IReadOnlyList<CityBuiltBuildingDto> GetBuildingsBuiltByCity(string cityId)
        {
            if (string.IsNullOrWhiteSpace(cityId))
            {
                return Array.Empty<CityBuiltBuildingDto>();
            }

            return _cityBuiltBuildings.TryGetValue(cityId.Trim(), out var buildings)
                ? buildings
                : Array.Empty<CityBuiltBuildingDto>();
        }

        public ResourceDto GetCityResources(string cityId)
        {
            if (string.IsNullOrWhiteSpace(cityId))
            {
                return new ResourceDto();
            }

            return _cityResources.TryGetValue(cityId.Trim(), out var resources) && resources != null
                ? GameStateCacheReadQueries.CloneResources(resources)
                : new ResourceDto();
        }

        public ResourceDto GetMyResources()
        {
            return GameStateCacheReadQueries.SnapshotResources(MyPlayer);
        }

        public Dictionary<string, int> GetMyResourceAmounts()
        {
            return GameStateCacheReadQueries.SnapshotResourceAmounts(MyPlayer != null ? MyPlayer.Resources : null);
        }

        public Dictionary<string, int> GetMyPointAmounts()
        {
            return GameStateCacheReadQueries.SnapshotPointAmounts(MyPlayer != null ? MyPlayer.Points : null);
        }

        public IReadOnlyList<string> GetCompletedTechnologyIds()
        {
            return GameStateCacheReadQueries.SnapshotStringList(_researchState?.CompletedTechnologyIds);
        }

        public IReadOnlyList<string> GetActiveTechnologyIds()
        {
            return GameStateCacheReadQueries.SnapshotStringList(_researchState?.ActiveTechnologyIds);
        }

        public IReadOnlyList<string> GetPendingActivationTechnologyIds()
        {
            return GameStateCacheReadQueries.SnapshotStringList(_researchState?.PendingActivationTechnologyIds);
        }

        public TechnologyDto GetCurrentResearchState()
        {
            return CloneTechnologyDto(_researchState);
        }

        public InstitutionStateDto GetInstitutionState()
        {
            return CloneInstitutionStateDto(_institutionState);
        }

        public IReadOnlyList<CityDto> GetCities()
        {
            return _citiesById.Values
                .OrderBy(city => city.CityId, StringComparer.OrdinalIgnoreCase)
                .Select(CloneCityDto)
                .ToList();
        }

        public bool TryGetCity(string cityId, out CityDto city)
        {
            city = null;
            if (string.IsNullOrWhiteSpace(cityId) || !_citiesById.TryGetValue(cityId.Trim(), out var projectedCity) || projectedCity == null)
            {
                return false;
            }

            city = CloneCityDto(projectedCity);
            return true;
        }

        public bool TryGetBuilding(string nodeId, out BuildingDto building)
        {
            building = null;
            if (string.IsNullOrWhiteSpace(nodeId) || !_buildingsByNodeId.TryGetValue(nodeId.Trim(), out var projectedBuilding) || projectedBuilding == null)
            {
                return false;
            }

            building = CloneBuildingDto(projectedBuilding);
            return true;
        }

        public IReadOnlyList<TurnEventDto> GetPlanningStartEvents()
        {
            return LastPlanningStartEvents;
        }

        public void UpsertRuntimeUnit(UnitDto unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.Id))
            {
                return;
            }

            _units[unit.Id] = unit;
        }

        public void RemoveRuntimeUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            _units.Remove(unitId);
        }

        public void Clear()
        {
            GameID = string.Empty;
            ActiveGameSessionID = string.Empty;
            MyPlayerID = string.Empty;
            Turn = 0;
            Phase = string.Empty;
            MapWidth = 0;
            MapHeight = 0;
            IsGameOver = false;
            _nodes.Clear();
            _units.Clear();
            _cityBuiltBuildings.Clear();
            _cityResources.Clear();
            _citiesById.Clear();
            _buildingsByNodeId.Clear();
            _lastPlanningStartEvents.Clear();
            _ministers.Clear();
            MyPlayer = null;
            TokensLeft = 0;
            EnemyCityCoreHP = 0;
            EnemyMaxCityCoreHP = 0;
            _researchState = new TechnologyDto();
            _institutionState = new InstitutionStateDto();
            _planningDraftCache?.ClearAll();
            _gameChatCache?.Clear();
            OnStateChanged?.Invoke();
        }

        public void SetActiveGameSession(string gameSessionID)
        {
            ActiveGameSessionID = string.IsNullOrWhiteSpace(gameSessionID)
                ? string.Empty
                : gameSessionID.Trim();
        }

        public void PublishPhaseChanged(PhaseChangedEvent evtArgs) => Fire(OnPhaseChanged, evtArgs, nameof(OnPhaseChanged));
        public void PublishMinisterChunk(MinisterChunkEvent evtArgs) => Fire(OnMinisterChunk, evtArgs, nameof(OnMinisterChunk));
        public void PublishMinisterMetrics(MinisterMetricsEvent evtArgs) => Fire(OnMinisterMetrics, evtArgs, nameof(OnMinisterMetrics));
        public void PublishTokenResult(TokenResultEvent evtArgs) => Fire(OnTokenResult, evtArgs, nameof(OnTokenResult));
        public void PublishRevealResult(RevealResultEvent evtArgs) => Fire(OnRevealResult, evtArgs, nameof(OnRevealResult));
        public void PublishPlanningCommandResult(PlanningCommandResultEvent evtArgs) => Fire(OnPlanningCommandResult, evtArgs, nameof(OnPlanningCommandResult));
        public void PublishGameOver(GameOverEvent evtArgs) => Fire(OnGameOver, evtArgs, nameof(OnGameOver));
        public void PublishGameError(GameErrorEvent evtArgs) => Fire(OnGameError, evtArgs, nameof(OnGameError));

        private void ApplyPlanningStartActiveState(MsgPlanningStart msg)
        {
            Turn = msg.Turn;
            Phase = NormalizePhase(msg.Phase, GamePhases.Planning);

            var oldTokens = TokensLeft;
            var oldUnits = CloneUnitMap(_units);

            if (msg.MyPlayer != null)
            {
                MyPlayer = msg.MyPlayer.Clone();
            }

            ReplaceNodesByDiff(msg.Nodes, "planning_start");
            ReplaceUnits(msg.Units, publishChanges: true, oldUnits, "planning_start");

            TokensLeft = MyPlayer != null ? MyPlayer.TokensLeft : msg.Tokens;
            if (MyPlayer != null)
            {
                MyPlayer.TokensLeft = TokensLeft;
            }

            SeedCityResourcesFromCurrentState();
            SynchronizeCityCoreState();

            Fire(OnTokensChanged, new TokensChangedEvent
            {
                TokensLeft = TokensLeft,
                Action = TokensLeft >= oldTokens ? "recharge" : "consume"
            }, nameof(OnTokensChanged));
        }

        private void ApplyPlanningStartDraft(MsgPlanningSnapshot snapshot)
        {
            var draftCache = _planningDraftCache;
            draftCache?.ClearAll();
            if (snapshot != null)
            {
                draftCache?.ApplyPlanningSnapshot(snapshot);
            }
        }

        private UnitsChangedEvent ReplaceUnits(System.Collections.Generic.IEnumerable<UnitView> units, bool publishChanges, IDictionary<string, UnitDto> previousUnits = null, string changeType = "")
        {
            previousUnits ??= CloneUnitMap(_units);
            var nextUnits = new Dictionary<string, UnitDto>();
            if (units != null)
            {
                foreach (var unit in units)
                {
                    if (unit == null || string.IsNullOrWhiteSpace(unit.Id))
                    {
                        continue;
                    }

                    nextUnits[unit.Id] = UnitMapper.ToDto(unit, _staticCatalogCache);
                }
            }

            _units.Clear();
            foreach (var pair in nextUnits)
            {
                _units[pair.Key] = pair.Value;
            }

            var added = new List<UnitDto>();
            var moved = new List<UnitDto>();
            var removed = new List<string>();

            foreach (var pair in nextUnits)
            {
                if (!previousUnits.TryGetValue(pair.Key, out var oldUnit))
                {
                    added.Add(CloneUnitDto(pair.Value));
                    continue;
                }

                if (oldUnit.Q != pair.Value.Q || oldUnit.R != pair.Value.R || oldUnit.Hp != pair.Value.Hp)
                {
                    moved.Add(CloneUnitDto(pair.Value));
                }
            }

            foreach (var pair in previousUnits)
            {
                if (!nextUnits.ContainsKey(pair.Key))
                {
                    removed.Add(pair.Key);
                }
            }

            var evt = new UnitsChangedEvent
            {
                Added = added,
                RemovedIDs = removed,
                Moved = moved,
                ChangeType = changeType ?? string.Empty
            };

            if (publishChanges && (added.Count > 0 || moved.Count > 0 || removed.Count > 0))
            {
                Fire(OnUnitsChanged, evt, nameof(OnUnitsChanged));
            }

            return evt;
        }

        private void ReplaceNodes(System.Collections.Generic.IEnumerable<NodeView> nodes, bool publishChanges, string changeType)
        {
            var previousNodes = CloneNodeMap(_nodes);
            var nextNodes = new Dictionary<string, NodeDto>();
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.Id))
                    {
                        continue;
                    }

                    var dto = NodeMapper.ToDto(node, _staticCatalogCache);
                    previousNodes.TryGetValue(node.Id, out var previousNode);
                    StabilizeNodeTerrain(dto, previousNode);
                    StabilizeNodeBuildingMaxHp(dto, previousNode);
                    nextNodes[node.Id] = dto;
                }
            }

            _nodes.Clear();
            foreach (var pair in nextNodes)
            {
                _nodes[pair.Key] = pair.Value;
            }

            if (!publishChanges)
            {
                return;
            }

            foreach (var pair in nextNodes)
            {
                if (!previousNodes.TryGetValue(pair.Key, out var previous) ||
                    !NodeEquals(previous, pair.Value, ignoreLastObservedTurn: true))
                {
                    Fire(OnNodeChanged, new NodeChangedEvent
                    {
                        NodeID = pair.Value.Id,
                        Node = CloneNodeDto(pair.Value),
                        ChangeType = changeType ?? string.Empty
                    }, nameof(OnNodeChanged));
                }
            }
        }

        private void ReplaceNodesByDiff(System.Collections.Generic.IEnumerable<NodeView> nodes, string changeType)
        {
            var previousNodes = CloneNodeMap(_nodes);
            var nextNodes = new Dictionary<string, NodeDto>();
            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.Id))
                    {
                        continue;
                    }

                    var dto = NodeMapper.ToDto(node, _staticCatalogCache);
                    previousNodes.TryGetValue(node.Id, out var previousNode);
                    StabilizeNodeTerrain(dto, previousNode);
                    StabilizeNodeBuildingMaxHp(dto, previousNode);
                    nextNodes[node.Id] = dto;
                }
            }

            _nodes.Clear();
            foreach (var pair in nextNodes)
            {
                _nodes[pair.Key] = pair.Value;
            }

            foreach (var pair in nextNodes)
            {
                if (!previousNodes.TryGetValue(pair.Key, out var previous) ||
                    !NodeEquals(previous, pair.Value, ignoreLastObservedTurn: true))
                {
                    Fire(OnNodeChanged, new NodeChangedEvent
                    {
                        NodeID = pair.Value.Id,
                        Node = CloneNodeDto(pair.Value),
                        ChangeType = changeType ?? string.Empty
                    }, nameof(OnNodeChanged));
                }
            }
        }

        private void TrackCityBuiltBuildings(TurnSettlementDto settlement)
        {
            if (settlement?.BuiltBuildings == null || settlement.BuiltBuildings.Count == 0)
            {
                return;
            }

            for (var i = 0; i < settlement.BuiltBuildings.Count; i++)
            {
                var built = settlement.BuiltBuildings[i];
                if (built == null || string.IsNullOrWhiteSpace(built.CityId) || string.IsNullOrWhiteSpace(built.BuildingType))
                {
                    continue;
                }

                var cityId = built.CityId.Trim();
                if (!_cityBuiltBuildings.TryGetValue(cityId, out var buildings))
                {
                    buildings = new List<CityBuiltBuildingDto>();
                    _cityBuiltBuildings[cityId] = buildings;
                }

                var record = new CityBuiltBuildingDto
                {
                    NodeId = built.NodeId?.Trim() ?? string.Empty,
                    BuildingType = built.BuildingType.Trim(),
                    Q = 0,
                    R = 0,
                    HasCoordinates = false
                };

                if (!string.IsNullOrWhiteSpace(record.NodeId) && _nodes.TryGetValue(record.NodeId, out var node) && node != null)
                {
                    record.Q = node.Q;
                    record.R = node.R;
                    record.HasCoordinates = true;
                }

                buildings.Add(record);
            }
        }

        private void SeedCityResourcesFromCurrentState()
        {
            _cityResources.Clear();
            var playerId = NormalizePlayerId(MyPlayerID, MyPlayer != null ? MyPlayer.Id : string.Empty);
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            var ownedCityIds = _nodes.Values
                .Where(node => node != null &&
                               string.Equals((node.BuildingType ?? string.Empty).Trim(), "city_core", StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(NormalizePlayerId(node.Owner, node.TerritoryOwner), playerId, StringComparison.Ordinal))
                .Select(node => node.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            for (var i = 0; i < ownedCityIds.Count; i++)
            {
                _cityResources[ownedCityIds[i]] = i == 0
                    ? GameStateCacheReadQueries.SnapshotResources(MyPlayer)
                    : new ResourceDto();
            }
        }

        private void SynchronizeCityCoreState()
        {
            var playerId = NormalizePlayerId(MyPlayerID, MyPlayer != null ? MyPlayer.Id : string.Empty);
            EnemyCityCoreHP = 0;
            EnemyMaxCityCoreHP = 0;

            foreach (var node in _nodes.Values)
            {
                if (node == null || !string.Equals((node.BuildingType ?? string.Empty).Trim(), "city_core", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var owner = NormalizePlayerId(node.Owner, node.TerritoryOwner);
                if (string.Equals(owner, playerId, StringComparison.Ordinal))
                {
                    if (MyPlayer != null)
                    {
                        MyPlayer.CapitalCityCoreHp = node.BuildingHp;
                        var nodeMaxHp = node.BuildingMaxHp > 0 ? node.BuildingMaxHp : node.BuildingHp;
                        if (MyPlayer.CapitalCityCoreMaxHp < nodeMaxHp)
                        {
                            MyPlayer.CapitalCityCoreMaxHp = nodeMaxHp;
                        }
                    }

                    continue;
                }

                EnemyCityCoreHP = Math.Max(EnemyCityCoreHP, node.BuildingHp);
                EnemyMaxCityCoreHP = Math.Max(EnemyMaxCityCoreHP, node.BuildingMaxHp > 0 ? node.BuildingMaxHp : node.BuildingHp);
            }
        }

        private void RebuildAuthoritativeProjections()
        {
            _researchState = ProjectResearchState(MyPlayer?.Research);
            _institutionState = ProjectInstitutionState(MyPlayer?.Institutions);
            _citiesById.Clear();
            _buildingsByNodeId.Clear();

            foreach (var node in _nodes.Values)
            {
                if (node == null)
                {
                    continue;
                }

                var hasBuildingProjection = !string.IsNullOrWhiteSpace(node.BuildingType) ||
                                            node.IsCityCore ||
                                            !string.IsNullOrWhiteSpace(node.CityId) ||
                                            !string.IsNullOrWhiteSpace(node.ServiceCityId);
                if (!hasBuildingProjection)
                {
                    continue;
                }

                var building = new BuildingDto
                {
                    NodeId = TrimOrEmpty(node.Id),
                    BuildingTypeId = TrimOrEmpty(node.BuildingType),
                    OwnerId = NormalizePlayerId(node.Owner, node.TerritoryOwner),
                    CityId = TrimOrEmpty(node.CityId),
                    ServiceCityId = TrimOrEmpty(node.ServiceCityId),
                    Status = TrimOrEmpty(node.BuildingStatus),
                    HitPoints = node.BuildingHp,
                    MaxHitPoints = node.BuildingMaxHp,
                    OperationSelectedRecipeId = TrimOrEmpty(node.OperationSelectedRecipeId),
                    OperationCurrentProgress = node.OperationCurrentProgress,
                    OperationRequiredProgress = node.OperationRequiredProgress,
                    OperationBaseProgress = node.OperationBaseProgress,
                    OperationBlockedReason = TrimOrEmpty(node.OperationBlockedReason),
                    OperationBlockedMessage = TrimOrEmpty(node.OperationBlockedMessage),
                    TakeoverProgress = node.TakeoverProgress,
                    TakeoverRequired = node.TakeoverRequired,
                    IsCityCore = node.IsCityCore,
                    IsSafeZone = node.IsSafeZone
                };

                if (!string.IsNullOrWhiteSpace(building.NodeId))
                {
                    _buildingsByNodeId[building.NodeId] = building;
                }

                var cityId = !string.IsNullOrWhiteSpace(building.CityId)
                    ? building.CityId
                    : building.ServiceCityId;
                if (string.IsNullOrWhiteSpace(cityId))
                {
                    continue;
                }

                if (!_citiesById.TryGetValue(cityId, out var city))
                {
                    city = new CityDto
                    {
                        CityId = cityId,
                        OwnerId = building.OwnerId,
                        CoreNodeId = string.Empty
                    };
                    _citiesById[cityId] = city;
                }

                if (string.IsNullOrWhiteSpace(city.OwnerId) && !string.IsNullOrWhiteSpace(building.OwnerId))
                {
                    city.OwnerId = building.OwnerId;
                }

                if (!string.IsNullOrWhiteSpace(building.NodeId) && !city.BuildingNodeIds.Contains(building.NodeId))
                {
                    city.BuildingNodeIds.Add(building.NodeId);
                }

                if (building.IsCityCore || string.Equals(building.BuildingTypeId, "city_core", StringComparison.OrdinalIgnoreCase))
                {
                    city.CoreNodeId = building.NodeId;
                }
            }

            foreach (var city in _citiesById.Values)
            {
                city.BuildingNodeIds.Sort(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static TechnologyDto ProjectResearchState(ResearchStateView research)
        {
            var projected = new TechnologyDto();
            if (research == null)
            {
                return projected;
            }

            projected.TechnologyId = TrimOrEmpty(research.CurrentTargetTechnologyId);
            projected.CurrentProgress = research.CurrentProgress;
            projected.RequiredProgress = research.RequiredProgress;
            projected.CompletedTechnologyIds = GameStateCacheReadQueries.SnapshotStringList(research.CompletedTechnologyIds);
            projected.ActiveTechnologyIds = GameStateCacheReadQueries.SnapshotStringList(research.ActiveTechnologyIds);
            projected.PendingActivationTechnologyIds = GameStateCacheReadQueries.SnapshotStringList(research.PendingActivationTechnologyIds);
            projected.SavedProgress = SnapshotResearchProgress(research.SavedProgress);
            return projected;
        }

        private static InstitutionStateDto ProjectInstitutionState(InstitutionStateView institution)
        {
            var projected = new InstitutionStateDto();
            if (institution == null)
            {
                return projected;
            }

            projected.SlotCount = institution.SlotCount;
            projected.CandidatePolicyIds = GameStateCacheReadQueries.SnapshotStringList(institution.CandidatePolicyIds);
            projected.ActivePolicyIds = GameStateCacheReadQueries.SnapshotStringList(institution.ActivePolicyIds);
            return projected;
        }

        private string ResolveLikelyLoserId(string winnerId, bool isWinner)
        {
            var winner = NormalizePlayerId(winnerId);
            var self = NormalizePlayerId(MyPlayerID, MyPlayer != null ? MyPlayer.Id : string.Empty);
            if (!isWinner && !string.IsNullOrWhiteSpace(self))
            {
                return self;
            }

            var candidates = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(self))
            {
                candidates.Add(self);
            }

            foreach (var node in _nodes.Values)
            {
                var owner = NormalizePlayerId(node?.Owner, node?.TerritoryOwner);
                if (!string.IsNullOrWhiteSpace(owner))
                {
                    candidates.Add(owner);
                }
            }

            foreach (var unit in _units.Values)
            {
                var owner = NormalizePlayerId(unit?.Owner);
                if (!string.IsNullOrWhiteSpace(owner))
                {
                    candidates.Add(owner);
                }
            }

            if (!string.IsNullOrWhiteSpace(winner))
            {
                candidates.Remove(winner);
            }

            return candidates.FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? string.Empty;
        }

        private void PublishPhaseState(int turn, string phase, int timeoutSeconds, int tokensLeft, string nextPhase)
        {
            Fire(OnPhaseChanged, new PhaseChangedEvent
            {
                Turn = turn,
                Phase = phase,
                TimeoutSeconds = timeoutSeconds,
                TokensLeft = tokensLeft,
                NextPhase = nextPhase,
                IsInteractive = GamePhases.IsPlanning(phase) && !IsGameOver
            }, nameof(OnPhaseChanged));
        }

        private void Fire<T>(Action<T> evt, T args, string evtName)
        {
            if (evt == null)
            {
                return;
            }

            try
            {
                evt.Invoke(args);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Cache] 事件 {evtName} 触发异常: {e.Message}\n{e.StackTrace}");
            }
        }

        private static string NormalizePhase(string phase, string fallback)
        {
            return string.IsNullOrWhiteSpace(phase) ? fallback : phase.Trim();
        }

        private static IDictionary<string, UnitDto> CloneUnitMap(IDictionary<string, UnitDto> source)
        {
            var clone = new Dictionary<string, UnitDto>();
            foreach (var pair in source)
            {
                clone[pair.Key] = CloneUnitDto(pair.Value);
            }

            return clone;
        }

        private static IDictionary<string, NodeDto> CloneNodeMap(IDictionary<string, NodeDto> source)
        {
            var clone = new Dictionary<string, NodeDto>(source.Count);
            foreach (var pair in source)
            {
                clone[pair.Key] = pair.Value;
            }

            return clone;
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

        private static bool NodeEquals(NodeDto left, NodeDto right, bool ignoreLastObservedTurn = false)
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
                   (ignoreLastObservedTurn || left.LastObservedTurn == right.LastObservedTurn) &&
                   left.HasRoad == right.HasRoad &&
                   string.Equals(left.Terrain, right.Terrain, StringComparison.Ordinal) &&
                   left.IsResourcePoint == right.IsResourcePoint &&
                   string.Equals(left.ResourceType, right.ResourceType, StringComparison.Ordinal) &&
                   left.IsSafeZone == right.IsSafeZone;
        }

        private static TechnologyDto CloneTechnologyDto(TechnologyDto source)
        {
            return source == null
                ? new TechnologyDto()
                : new TechnologyDto
                {
                    TechnologyId = source.TechnologyId,
                    CurrentProgress = source.CurrentProgress,
                    RequiredProgress = source.RequiredProgress,
                    CompletedTechnologyIds = source.CompletedTechnologyIds != null ? new List<string>(source.CompletedTechnologyIds) : new List<string>(),
                    ActiveTechnologyIds = source.ActiveTechnologyIds != null ? new List<string>(source.ActiveTechnologyIds) : new List<string>(),
                    PendingActivationTechnologyIds = source.PendingActivationTechnologyIds != null ? new List<string>(source.PendingActivationTechnologyIds) : new List<string>(),
                    SavedProgress = CloneTechnologyProgressList(source.SavedProgress)
                };
        }

        private static List<TechnologyProgressDto> SnapshotResearchProgress(System.Collections.Generic.IEnumerable<ResearchProgressEntry> entries)
        {
            var result = new List<TechnologyProgressDto>();
            if (entries == null)
            {
                return result;
            }

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.TechnologyId))
                {
                    continue;
                }

                result.Add(new TechnologyProgressDto
                {
                    TechnologyId = TrimOrEmpty(entry.TechnologyId),
                    CurrentProgress = entry.CurrentProgress,
                    RequiredProgress = entry.RequiredProgress
                });
            }

            return result;
        }

        private static List<TechnologyProgressDto> CloneTechnologyProgressList(System.Collections.Generic.IEnumerable<TechnologyProgressDto> entries)
        {
            var result = new List<TechnologyProgressDto>();
            if (entries == null)
            {
                return result;
            }

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                result.Add(new TechnologyProgressDto
                {
                    TechnologyId = entry.TechnologyId,
                    CurrentProgress = entry.CurrentProgress,
                    RequiredProgress = entry.RequiredProgress
                });
            }

            return result;
        }

        private static InstitutionStateDto CloneInstitutionStateDto(InstitutionStateDto source)
        {
            return source == null
                ? new InstitutionStateDto()
                : new InstitutionStateDto
                {
                    SlotCount = source.SlotCount,
                    CandidatePolicyIds = source.CandidatePolicyIds != null ? new List<string>(source.CandidatePolicyIds) : new List<string>(),
                    ActivePolicyIds = source.ActivePolicyIds != null ? new List<string>(source.ActivePolicyIds) : new List<string>()
                };
        }

        private static CityDto CloneCityDto(CityDto source)
        {
            return source == null
                ? null
                : new CityDto
                {
                    CityId = source.CityId,
                    OwnerId = source.OwnerId,
                    CoreNodeId = source.CoreNodeId,
                    BuildingNodeIds = source.BuildingNodeIds != null ? new List<string>(source.BuildingNodeIds) : new List<string>()
                };
        }

        private static BuildingDto CloneBuildingDto(BuildingDto source)
        {
            return source == null
                ? null
                : new BuildingDto
                {
                    NodeId = source.NodeId,
                    BuildingTypeId = source.BuildingTypeId,
                    OwnerId = source.OwnerId,
                    CityId = source.CityId,
                    ServiceCityId = source.ServiceCityId,
                    Status = source.Status,
                    HitPoints = source.HitPoints,
                    MaxHitPoints = source.MaxHitPoints,
                    OperationSelectedRecipeId = source.OperationSelectedRecipeId,
                    OperationCurrentProgress = source.OperationCurrentProgress,
                    OperationRequiredProgress = source.OperationRequiredProgress,
                    OperationBaseProgress = source.OperationBaseProgress,
                    OperationBlockedReason = source.OperationBlockedReason,
                    OperationBlockedMessage = source.OperationBlockedMessage,
                    TakeoverProgress = source.TakeoverProgress,
                    TakeoverRequired = source.TakeoverRequired,
                    IsCityCore = source.IsCityCore,
                    IsSafeZone = source.IsSafeZone
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

        private static ResourceDto ComputeResourceDelta(ResourceDto before, ResourceDto after)
        {
            before ??= new ResourceDto();
            after ??= new ResourceDto();

            return new ResourceDto
            {
                Ore = after.Ore - before.Ore,
                Wood = after.Wood - before.Wood,
                Food = after.Food - before.Food,
                IndustryOutput = after.IndustryOutput - before.IndustryOutput,
                ResourceAmounts = ComputeAmountDelta(before.ResourceAmounts, after.ResourceAmounts),
                PointAmounts = ComputeAmountDelta(before.PointAmounts, after.PointAmounts)
            };
        }

        private static Dictionary<string, int> ComputeAmountDelta(
            IReadOnlyDictionary<string, int> before,
            IReadOnlyDictionary<string, int> after)
        {
            var delta = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            AddDeltaKeys(delta, before);
            AddDeltaKeys(delta, after);

            var keys = delta.Keys.ToList();
            for (var i = 0; i < keys.Count; i++)
            {
                var beforeAmount = 0;
                var afterAmount = 0;
                before?.TryGetValue(keys[i], out beforeAmount);
                after?.TryGetValue(keys[i], out afterAmount);
                delta[keys[i]] = afterAmount - beforeAmount;
            }

            return delta;
        }

        private static void AddDeltaKeys(Dictionary<string, int> delta, IReadOnlyDictionary<string, int> values)
        {
            if (values == null)
            {
                return;
            }

            foreach (var pair in values)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    delta[pair.Key.Trim()] = 0;
                }
            }
        }

        private static string NormalizePlayerId(params string[] candidates)
        {
            for (var i = 0; i < candidates.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(candidates[i]))
                {
                    return candidates[i].Trim();
                }
            }

            return string.Empty;
        }

        private static void StabilizeNodeTerrain(NodeDto incoming, NodeDto previous)
        {
            if (incoming == null)
            {
                return;
            }

            var terrain = TrimOrEmpty(incoming.Terrain);
            var type = TrimOrEmpty(incoming.Type);
            var previousTerrain = TrimOrEmpty(previous?.Terrain);
            var previousType = TrimOrEmpty(previous?.Type);

            // Debounce: if settlement/planning snapshot accidentally omits terrain fields,
            // keep previous terrain to avoid full-map visual flicker/regression.
            if (string.IsNullOrEmpty(terrain))
            {
                terrain = !string.IsNullOrEmpty(previousTerrain)
                    ? previousTerrain
                    : previousType;
            }

            if (string.IsNullOrEmpty(type))
            {
                type = !string.IsNullOrEmpty(terrain)
                    ? terrain
                    : (!string.IsNullOrEmpty(previousType) ? previousType : previousTerrain);
            }

            if (string.IsNullOrEmpty(terrain) && !string.IsNullOrEmpty(type))
            {
                terrain = type;
            }

            incoming.Terrain = terrain;
            incoming.Type = type;
        }

        private static void StabilizeNodeBuildingMaxHp(NodeDto incoming, NodeDto previous)
        {
            if (incoming == null || string.IsNullOrWhiteSpace(incoming.BuildingType))
            {
                return;
            }

            var sameBuilding = previous != null &&
                               string.Equals(TrimOrEmpty(previous.BuildingType), TrimOrEmpty(incoming.BuildingType), StringComparison.OrdinalIgnoreCase);

            if (incoming.BuildingMaxHp <= 0 && sameBuilding && previous.BuildingMaxHp > 0)
            {
                incoming.BuildingMaxHp = previous.BuildingMaxHp;
            }

            if (sameBuilding &&
                previous.BuildingMaxHp > incoming.BuildingMaxHp &&
                incoming.BuildingMaxHp <= Math.Max(0, incoming.BuildingHp))
            {
                incoming.BuildingMaxHp = previous.BuildingMaxHp;
            }
        }

        private static string TrimOrEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

    }
}
