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

        private readonly Dictionary<string, List<CastleBuiltBuildingDto>> _castleBuiltBuildings = new();
        private readonly Dictionary<string, ResourceDto> _castleResources = new();

        public PlayerView MyPlayer { get; private set; }

        private readonly List<MinisterView> _ministers = new();
        public IReadOnlyList<MinisterView> Ministers => _ministers;

        public int TokensLeft { get; private set; }
        public int EnemyCastleHP { get; private set; }
        public int EnemyMaxCastleHP { get; private set; }

        public event Action OnStateChanged;
        public event Action<PhaseChangedEvent> OnPhaseChanged;
        public event Action<ResourcesChangedEvent> OnResourcesChanged;
        public event Action<TokensChangedEvent> OnTokensChanged;
        public event Action<NodeChangedEvent> OnNodeChanged;
        public event Action<UnitsChangedEvent> OnUnitsChanged;
        public event Action<CastleHPChangedEvent> OnCastleHPChanged;
        public event Action<TurnSettledEvent> OnTurnSettled;
        public event Action<MinisterChunkEvent> OnMinisterChunk;
        public event Action<MinisterMetricsEvent> OnMinisterMetrics;
        public event Action<TokenResultEvent> OnTokenResult;
        public event Action<RevealResultEvent> OnRevealResult;
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

        public void ApplyGameInit(MsgGameInit msg)
        {
            if (msg == null)
            {
                return;
            }

            GameID = msg.GameId ?? string.Empty;
            MyPlayerID = msg.YourPlayerId ?? string.Empty;
            Turn = msg.Turn;
            Phase = NormalizePhase(msg.Phase, GamePhases.Planning);
            MapWidth = msg.MapWidth;
            MapHeight = msg.MapHeight;
            IsGameOver = false;

            ReplaceNodes(msg.Nodes);
            ReplaceUnits(msg.Units, publishChanges: false);
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

            _castleBuiltBuildings.Clear();
            SeedCastleResourcesFromCurrentState();
            SynchronizeCastleState();

            PublishPhaseState(Turn, Phase, 0, TokensLeft, string.Empty);
            Fire(OnResourcesChanged, new ResourcesChangedEvent
            {
                Resources = SnapshotResources(MyPlayer?.Resources),
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
                Moved = new List<UnitDto>()
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

            Fire(OnCastleHPChanged, new CastleHPChangedEvent
            {
                MyHP = MyPlayer != null ? MyPlayer.MainCastleHp : 0,
                MyMaxHP = MyPlayer != null ? MyPlayer.MaxCastleHp : 0,
                EnemyHP = EnemyCastleHP,
                EnemyMaxHP = EnemyMaxCastleHP,
                MyDelta = 0,
                EnemyDelta = 0
            }, nameof(OnCastleHPChanged));

            OnStateChanged?.Invoke();
        }

        public void ApplyPlanningStart(MsgPlanningStart msg)
        {
            if (msg == null)
            {
                return;
            }

            Turn = msg.Turn;
            Phase = NormalizePhase(msg.Phase, GamePhases.Planning);
            UpdateTokens(msg.Tokens);
            var draftCache = PlanningDraftCache.EnsureInstance();
            draftCache?.ClearAll();
            if (msg.Snapshot != null)
            {
                draftCache?.ApplyPlanningSnapshot(msg.Snapshot);
            }

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
            PlanningDraftCache.EnsureInstance()?.ApplyPlanningSnapshot(msg);
            PublishPhaseState(Turn, Phase, 0, TokensLeft, string.Empty);
            OnStateChanged?.Invoke();
        }

        public void ApplyTurnSettlement(MsgTurnSettlement msg)
        {
            if (msg == null)
            {
                return;
            }

            var resourcesBefore = SnapshotResources(MyPlayer?.Resources);
            var myHpBefore = MyPlayer != null ? MyPlayer.MainCastleHp : 0;
            var enemyHpBefore = EnemyCastleHP;
            var oldUnits = CloneUnitMap(_units);

            Turn = msg.Turn > 0 ? msg.Turn : Turn;
            Phase = NormalizePhase(msg.Phase, GamePhases.Resolving);

            ReplaceNodes(msg.Nodes);
            var unitChanges = ReplaceUnits(msg.Units, publishChanges: true, oldUnits);

            if (msg.MyPlayerAfter != null)
            {
                MyPlayer = msg.MyPlayerAfter.Clone();
            }

            TokensLeft = MyPlayer != null ? MyPlayer.TokensLeft : TokensLeft;
            SeedCastleResourcesFromCurrentState();
            SynchronizeCastleState();

            var resourcesAfter = SnapshotResources(MyPlayer?.Resources);
            Fire(OnResourcesChanged, new ResourcesChangedEvent
            {
                Resources = resourcesAfter,
                Delta = ComputeResourceDelta(resourcesBefore, resourcesAfter)
            }, nameof(OnResourcesChanged));

            if (myHpBefore != (MyPlayer != null ? MyPlayer.MainCastleHp : 0) || enemyHpBefore != EnemyCastleHP)
            {
                Fire(OnCastleHPChanged, new CastleHPChangedEvent
                {
                    MyHP = MyPlayer != null ? MyPlayer.MainCastleHp : 0,
                    MyMaxHP = MyPlayer != null ? MyPlayer.MaxCastleHp : 0,
                    EnemyHP = EnemyCastleHP,
                    EnemyMaxHP = EnemyMaxCastleHP,
                    MyDelta = (MyPlayer != null ? MyPlayer.MainCastleHp : 0) - myHpBefore,
                    EnemyDelta = EnemyCastleHP - enemyHpBefore
                }, nameof(OnCastleHPChanged));
            }

            var settlement = SettlementMapper.ToDto(msg);
            TrackCastleBuiltBuildings(settlement);
            PlanningDraftCache.Instance?.ClearAll();

            Fire(OnTurnSettled, new TurnSettledEvent
            {
                Settlement = settlement,
                ResourcesAfter = resourcesAfter,
                BuiltNodeIDs = settlement?.BuiltNodeIDs ?? new List<string>(),
                MovedUnitIDs = settlement?.MovedUnitIDs ?? unitChanges.Moved.Select(unit => unit.Id).ToList(),
                DeadUnitIDs = settlement?.DeadUnitIDs ?? unitChanges.RemovedIDs,
                CastleDamaged = settlement != null && settlement.CastleDamaged
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

            _nodes[node.Id] = node;
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

        public IReadOnlyList<CastleBuiltBuildingDto> GetBuildingsBuiltByCastle(string castleId)
        {
            if (string.IsNullOrWhiteSpace(castleId))
            {
                return Array.Empty<CastleBuiltBuildingDto>();
            }

            return _castleBuiltBuildings.TryGetValue(castleId.Trim(), out var buildings)
                ? buildings
                : Array.Empty<CastleBuiltBuildingDto>();
        }

        public ResourceDto GetCastleResources(string castleId)
        {
            if (string.IsNullOrWhiteSpace(castleId))
            {
                return new ResourceDto();
            }

            return _castleResources.TryGetValue(castleId.Trim(), out var resources) && resources != null
                ? CloneResources(resources)
                : new ResourceDto();
        }

        public ResourceDto GetMyResources()
        {
            return SnapshotResources(MyPlayer?.Resources);
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
            MyPlayerID = string.Empty;
            Turn = 0;
            Phase = string.Empty;
            MapWidth = 0;
            MapHeight = 0;
            IsGameOver = false;
            _nodes.Clear();
            _units.Clear();
            _castleBuiltBuildings.Clear();
            _castleResources.Clear();
            _ministers.Clear();
            MyPlayer = null;
            TokensLeft = 0;
            EnemyCastleHP = 0;
            EnemyMaxCastleHP = 0;
            PlanningDraftCache.Instance?.ClearAll();
            OnStateChanged?.Invoke();
        }

        public void PublishPhaseChanged(PhaseChangedEvent evtArgs) => Fire(OnPhaseChanged, evtArgs, nameof(OnPhaseChanged));
        public void PublishMinisterChunk(MinisterChunkEvent evtArgs) => Fire(OnMinisterChunk, evtArgs, nameof(OnMinisterChunk));
        public void PublishMinisterMetrics(MinisterMetricsEvent evtArgs) => Fire(OnMinisterMetrics, evtArgs, nameof(OnMinisterMetrics));
        public void PublishTokenResult(TokenResultEvent evtArgs) => Fire(OnTokenResult, evtArgs, nameof(OnTokenResult));
        public void PublishRevealResult(RevealResultEvent evtArgs) => Fire(OnRevealResult, evtArgs, nameof(OnRevealResult));
        public void PublishGameOver(GameOverEvent evtArgs) => Fire(OnGameOver, evtArgs, nameof(OnGameOver));
        public void PublishGameError(GameErrorEvent evtArgs) => Fire(OnGameError, evtArgs, nameof(OnGameError));

        private UnitsChangedEvent ReplaceUnits(System.Collections.Generic.IEnumerable<UnitView> units, bool publishChanges, IDictionary<string, UnitDto> previousUnits = null)
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

                    nextUnits[unit.Id] = UnitMapper.ToDto(unit);
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

                if (oldUnit.X != pair.Value.X || oldUnit.Y != pair.Value.Y || oldUnit.Hp != pair.Value.Hp)
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
                Moved = moved
            };

            if (publishChanges)
            {
                Fire(OnUnitsChanged, evt, nameof(OnUnitsChanged));
            }

            return evt;
        }

        private void ReplaceNodes(System.Collections.Generic.IEnumerable<NodeView> nodes)
        {
            _nodes.Clear();
            if (nodes == null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id))
                {
                    continue;
                }

                var dto = NodeMapper.ToDto(node);
                _nodes[node.Id] = dto;
                Fire(OnNodeChanged, new NodeChangedEvent
                {
                    NodeID = dto.Id,
                    Node = dto,
                    ChangeType = "settlement"
                }, nameof(OnNodeChanged));
            }
        }

        private void TrackCastleBuiltBuildings(TurnSettlementDto settlement)
        {
            if (settlement?.BuiltBuildings == null || settlement.BuiltBuildings.Count == 0)
            {
                return;
            }

            for (var i = 0; i < settlement.BuiltBuildings.Count; i++)
            {
                var built = settlement.BuiltBuildings[i];
                if (built == null || string.IsNullOrWhiteSpace(built.CastleId) || string.IsNullOrWhiteSpace(built.BuildingType))
                {
                    continue;
                }

                var castleId = built.CastleId.Trim();
                if (!_castleBuiltBuildings.TryGetValue(castleId, out var buildings))
                {
                    buildings = new List<CastleBuiltBuildingDto>();
                    _castleBuiltBuildings[castleId] = buildings;
                }

                var record = new CastleBuiltBuildingDto
                {
                    NodeId = built.NodeId?.Trim() ?? string.Empty,
                    BuildingType = built.BuildingType.Trim(),
                    X = 0,
                    Y = 0,
                    HasCoordinates = false
                };

                if (!string.IsNullOrWhiteSpace(record.NodeId) && _nodes.TryGetValue(record.NodeId, out var node) && node != null)
                {
                    record.X = node.X;
                    record.Y = node.Y;
                    record.HasCoordinates = true;
                }

                buildings.Add(record);
            }
        }

        private void SeedCastleResourcesFromCurrentState()
        {
            _castleResources.Clear();
            var playerId = NormalizePlayerId(MyPlayerID, MyPlayer != null ? MyPlayer.Id : string.Empty);
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            var ownedCastleIds = _nodes.Values
                .Where(node => node != null &&
                               string.Equals((node.BuildingType ?? string.Empty).Trim(), "castle", StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(NormalizePlayerId(node.Owner, node.TerritoryOwner), playerId, StringComparison.Ordinal))
                .Select(node => node.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            for (var i = 0; i < ownedCastleIds.Count; i++)
            {
                _castleResources[ownedCastleIds[i]] = i == 0
                    ? SnapshotResources(MyPlayer?.Resources)
                    : new ResourceDto();
            }
        }

        private void SynchronizeCastleState()
        {
            var playerId = NormalizePlayerId(MyPlayerID, MyPlayer != null ? MyPlayer.Id : string.Empty);
            EnemyCastleHP = 0;
            EnemyMaxCastleHP = 0;

            foreach (var node in _nodes.Values)
            {
                if (node == null || !string.Equals((node.BuildingType ?? string.Empty).Trim(), "castle", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var owner = NormalizePlayerId(node.Owner, node.TerritoryOwner);
                if (string.Equals(owner, playerId, StringComparison.Ordinal))
                {
                    if (MyPlayer != null)
                    {
                        MyPlayer.MainCastleHp = node.BuildingHp;
                        if (MyPlayer.MaxCastleHp < node.BuildingHp)
                        {
                            MyPlayer.MaxCastleHp = node.BuildingHp;
                        }
                    }

                    continue;
                }

                EnemyCastleHP = Math.Max(EnemyCastleHP, node.BuildingHp);
                EnemyMaxCastleHP = Math.Max(EnemyMaxCastleHP, node.BuildingHp);
            }
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
                X = source.X,
                Y = source.Y,
                Hp = source.Hp,
                MaxHp = source.MaxHp
            };
        }

        private static ResourceDto ComputeResourceDelta(ResourceDto before, ResourceDto after)
        {
            return new ResourceDto
            {
                Ore = after.Ore - before.Ore,
                Wood = after.Wood - before.Wood,
                Food = after.Food - before.Food,
                RefinedOre = after.RefinedOre - before.RefinedOre,
                EngineerMaterial = after.EngineerMaterial - before.EngineerMaterial,
                BuildPoints = after.BuildPoints - before.BuildPoints
            };
        }

        private static ResourceDto SnapshotResources(ResourceBag bag)
        {
            var resources = new ResourceDto();
            if (bag == null || bag.Items == null)
            {
                return resources;
            }

            for (var i = 0; i < bag.Items.Count; i++)
            {
                var item = bag.Items[i];
                if (item == null)
                {
                    continue;
                }

                switch (item.Key)
                {
                    case ResourceKeys.ResourceOre:
                        resources.Ore = item.Amount;
                        break;
                    case ResourceKeys.ResourceWood:
                        resources.Wood = item.Amount;
                        break;
                    case ResourceKeys.ResourceFood:
                        resources.Food = item.Amount;
                        break;
                    case ResourceKeys.ResourceRefinedOre:
                        resources.RefinedOre = item.Amount;
                        break;
                    case ResourceKeys.ResourceEngineerMaterial:
                        resources.EngineerMaterial = item.Amount;
                        break;
                    case ResourceKeys.ResourceBuildPoints:
                        resources.BuildPoints = item.Amount;
                        break;
                }
            }

            return resources;
        }

        private static ResourceDto CloneResources(ResourceDto source)
        {
            if (source == null)
            {
                return new ResourceDto();
            }

            return new ResourceDto
            {
                Ore = source.Ore,
                Wood = source.Wood,
                Food = source.Food,
                RefinedOre = source.RefinedOre,
                EngineerMaterial = source.EngineerMaterial,
                BuildPoints = source.BuildPoints
            };
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
    }
}
