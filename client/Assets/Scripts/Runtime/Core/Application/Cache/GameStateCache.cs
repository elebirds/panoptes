/*************************************************
 * Project: Panoptes
 * File: GameStateCache.cs
 * Author: Panoptes Team
 * Date: 2026-04-04
 * Description: Server-state mirror cache placeholder.
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
        public int Turn { get; set; }
        public string Phase { get; private set; }
        public int MapWidth { get; private set; }
        public int MapHeight { get; private set; }
        public bool IsGameOver { get; private set; }

        private readonly Dictionary<string, NodeDto> _nodes = new();
        public IReadOnlyDictionary<string, NodeDto> Nodes => _nodes;

        private readonly Dictionary<string, UnitDto> _units = new();
        public IReadOnlyDictionary<string, UnitDto> Units => _units;

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
        public event Action<DomesticSettledEvent> OnDomesticSettled;
        public event Action<CombatSettledEvent> OnCombatSettled;
        public event Action<MinisterChunkEvent> OnMinisterChunk;
        public event Action<MinisterMetricsEvent> OnMinisterMetrics;
        public event Action<MinisterActionEvent> OnMinisterAction;
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

            GameID = msg.GameId;
            MyPlayerID = msg.YourPlayerId;
            Turn = msg.Turn;
            Phase = NormalizePhase(msg.Phase, GamePhases.DomesticPlanning);
            MapWidth = msg.MapWidth;
            MapHeight = msg.MapHeight;
            IsGameOver = false;

            _nodes.Clear();
            foreach (var node in msg.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id))
                {
                    continue;
                }

                _nodes[node.Id] = NodeMapper.ToDto(node);
            }

            _units.Clear();
            foreach (var unit in msg.Units)
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.Id))
                {
                    continue;
                }

                _units[unit.Id] = UnitMapper.ToDto(unit);
            }

            MyPlayer = msg.MyPlayer;
            TokensLeft = msg.MyPlayer != null ? msg.MyPlayer.TokensLeft : 0;

            _ministers.Clear();
            _ministers.AddRange(msg.Ministers);
            EnemyCastleHP = 0;
            EnemyMaxCastleHP = 0;

            var currentResources = SnapshotResources(MyPlayer?.Resources);

            PublishPhaseState(Turn, Phase, 0, TokensLeft, string.Empty);

            Fire(OnResourcesChanged, new ResourcesChangedEvent
            {
                Resources = currentResources,
                Delta = new ResourceDto()
            }, nameof(OnResourcesChanged));

            Fire(OnTokensChanged, new TokensChangedEvent
            {
                TokensLeft = TokensLeft,
                Action = "recharge"
            }, nameof(OnTokensChanged));

            Fire(OnUnitsChanged, new UnitsChangedEvent
            {
                Added = _units.Values.ToList(),
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

        public void ApplyDomesticPhaseStart(MsgDomesticPhaseStart msg)
        {
            if (msg == null)
            {
                return;
            }

            Turn = msg.Turn;
            Phase = NormalizePhase(msg.Phase, GamePhases.DomesticPlanning);
            UpdateTokens(msg.Tokens);
            PublishPhaseState(msg.Turn, Phase, msg.Timeout, msg.Tokens, string.Empty);

            OnStateChanged?.Invoke();
        }

        public void ApplyCombatPhaseStart(MsgCombatPhaseStart msg)
        {
            if (msg == null)
            {
                return;
            }

            Turn = msg.Turn > 0 ? msg.Turn : Turn;
            Phase = NormalizePhase(msg.Phase, GamePhases.CombatPlanning);
            UpdateTokens(msg.Tokens);
            PublishPhaseState(Turn, Phase, msg.Timeout, msg.Tokens, string.Empty);

            OnStateChanged?.Invoke();
        }

        public void ApplyDomesticSettlement(MsgDomesticSettlement msg)
        {
            if (msg == null)
            {
                return;
            }

            Turn = msg.Turn > 0 ? msg.Turn : Turn;
            Phase = NormalizePhase(msg.Phase, GamePhases.DomesticResolving);

            var resourcesBefore = SnapshotResources(MyPlayer?.Resources);

            if (MyPlayer != null && msg.MyResourcesAfter != null)
            {
                MyPlayer.Resources = msg.MyResourcesAfter.Clone();
            }

            var settlement = SettlementMapper.ToDto(msg);

            if (settlement != null && settlement.ChangedNodeIDs != null)
            {
                foreach (var nodeId in settlement.ChangedNodeIDs)
                {
                    if (string.IsNullOrWhiteSpace(nodeId) || !_nodes.TryGetValue(nodeId, out var node))
                    {
                        continue;
                    }

                    var changeType = "changed";
                    var matchedChange = msg.Changes.FirstOrDefault(c => c != null && c.Data != null && c.Data.TryGetValue("node_id", out var id) && id == nodeId);
                    if (matchedChange != null && !string.IsNullOrWhiteSpace(matchedChange.Type))
                    {
                        changeType = matchedChange.Type;

                        if (matchedChange.Data != null)
                        {
                            if (matchedChange.Data.TryGetValue("building_type", out var buildingType) && !string.IsNullOrWhiteSpace(buildingType))
                            {
                                node.BuildingType = buildingType;
                            }

                            if (matchedChange.Data.TryGetValue("owner", out var owner) && !string.IsNullOrWhiteSpace(owner))
                            {
                                node.Owner = owner;
                            }

                            if (matchedChange.Data.TryGetValue("building_hp", out var hpText) && int.TryParse(hpText, out var hp))
                            {
                                node.BuildingHp = hp;
                            }
                            else if (matchedChange.Data.TryGetValue("hp_after", out hpText) && int.TryParse(hpText, out hp))
                            {
                                node.BuildingHp = hp;
                            }
                            else if (matchedChange.Data.TryGetValue("hp", out hpText) && int.TryParse(hpText, out hp))
                            {
                                node.BuildingHp = hp;
                            }
                        }
                    }

                    Fire(OnNodeChanged, new NodeChangedEvent
                    {
                        NodeID = nodeId,
                        Node = node,
                        ChangeType = changeType
                    }, nameof(OnNodeChanged));
                }
            }

            var resourcesAfter = SnapshotResources(MyPlayer?.Resources);
            Fire(OnResourcesChanged, new ResourcesChangedEvent
            {
                Resources = resourcesAfter,
                Delta = ComputeResourceDelta(resourcesBefore, resourcesAfter)
            }, nameof(OnResourcesChanged));

            Fire(OnDomesticSettled, new DomesticSettledEvent
            {
                Settlement = settlement,
                ResourcesAfter = resourcesAfter,
                BuiltNodeIDs = settlement?.BuiltNodeIDs ?? new List<string>()
            }, nameof(OnDomesticSettled));

            PublishPhaseState(Turn, Phase, 0, TokensLeft, msg.NextPhase ?? string.Empty);

            OnStateChanged?.Invoke();
        }

        public void ApplyCombatSettlement(MsgCombatSettlement msg)
        {
            if (msg == null || msg.Events == null)
            {
                return;
            }

            Turn = msg.Turn > 0 ? msg.Turn : Turn;
            Phase = NormalizePhase(msg.Phase, GamePhases.CombatResolving);

            var myHpBefore = MyPlayer != null ? MyPlayer.MainCastleHp : 0;
            var enemyHpBefore = EnemyCastleHP;

            for (var i = 0; i < msg.Events.Count; i++)
            {
                var evt = msg.Events[i];
                if (evt == null)
                {
                    continue;
                }

                switch (evt.DataCase)
                {
                    case CombatEvent.DataOneofCase.UnitMove:
                        ApplyUnitMove(evt.UnitMove);
                        break;
                    case CombatEvent.DataOneofCase.UnitDamaged:
                        ApplyUnitDamaged(evt.UnitDamaged);
                        break;
                    case CombatEvent.DataOneofCase.UnitDied:
                        ApplyUnitDied(evt.UnitDied);
                        break;
                    case CombatEvent.DataOneofCase.CastleDamaged:
                        ApplyCastleDamaged(evt.CastleDamaged);
                        break;
                    case CombatEvent.DataOneofCase.CastleDestroyed:
                        ApplyCastleDestroyed(evt.CastleDestroyed);
                        break;
                    case CombatEvent.DataOneofCase.BuildingDamaged:
                        ApplyBuildingDamaged(evt.BuildingDamaged);
                        break;
                }
            }

            var settlement = SettlementMapper.ToDto(msg);
            var movedIDs = settlement?.MovedUnitIDs ?? new List<string>();
            var deadIDs = settlement?.DeadUnitIDs ?? new List<string>();
            var castleDamaged = settlement != null && settlement.CastleDamaged;

            if (castleDamaged)
            {
                var myHpAfter = MyPlayer != null ? MyPlayer.MainCastleHp : 0;
                var enemyHpAfter = EnemyCastleHP;
                Fire(OnCastleHPChanged, new CastleHPChangedEvent
                {
                    MyHP = myHpAfter,
                    MyMaxHP = MyPlayer != null ? MyPlayer.MaxCastleHp : 0,
                    EnemyHP = enemyHpAfter,
                    EnemyMaxHP = EnemyMaxCastleHP,
                    MyDelta = myHpAfter - myHpBefore,
                    EnemyDelta = enemyHpAfter - enemyHpBefore
                }, nameof(OnCastleHPChanged));
            }

            var movedUnits = new List<UnitDto>();
            foreach (var movedID in movedIDs)
            {
                if (_units.TryGetValue(movedID, out var moved) && moved != null)
                {
                    movedUnits.Add(moved);
                }
            }

            Fire(OnUnitsChanged, new UnitsChangedEvent
            {
                Added = new List<UnitDto>(),
                RemovedIDs = deadIDs,
                Moved = movedUnits
            }, nameof(OnUnitsChanged));

            Fire(OnCombatSettled, new CombatSettledEvent
            {
                Settlement = settlement,
                MovedUnitIDs = movedIDs,
                DeadUnitIDs = deadIDs,
                CastleDamaged = castleDamaged
            }, nameof(OnCombatSettled));

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
                Reason = msg.Reason,
                Narrative = msg.Narrative,
                IsWinner = isWinner
            }, nameof(OnGameOver));

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

        private void ApplyUnitMove(UnitMoveEvent evt)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.UnitId))
            {
                return;
            }

            if (!_units.TryGetValue(evt.UnitId, out var unit) || unit == null || evt.To == null)
            {
                return;
            }

            unit.X = evt.To.X;
            unit.Y = evt.To.Y;
        }

        private void ApplyUnitDamaged(UnitDamagedEvent evt)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.UnitId))
            {
                return;
            }

            if (!_units.TryGetValue(evt.UnitId, out var unit) || unit == null)
            {
                return;
            }

            unit.Hp = evt.HpAfter;
        }

        private void ApplyUnitDied(UnitDiedEvent evt)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.UnitId))
            {
                return;
            }

            _units.Remove(evt.UnitId);
        }

        private void ApplyCastleDamaged(CastleDamagedEvent evt)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.NodeId))
            {
                return;
            }

            var isMyCastle = false;

            if (_nodes.TryGetValue(evt.NodeId, out var node) && node != null)
            {
                node.BuildingHp = evt.HpAfter;
                var owner = node.Owner;
                if (!string.IsNullOrWhiteSpace(owner))
                {
                    var selfID = MyPlayerID;
                    var selfPlayerID = MyPlayer != null ? MyPlayer.Id : string.Empty;
                    isMyCastle = string.Equals(owner, selfID, StringComparison.Ordinal) ||
                                 string.Equals(owner, selfPlayerID, StringComparison.Ordinal);
                }
            }

            if (isMyCastle)
            {
                if (MyPlayer != null)
                {
                    MyPlayer.MainCastleHp = Math.Max(0, evt.HpAfter);
                }
            }
            else
            {
                EnemyCastleHP = Math.Max(0, evt.HpAfter);
                EnemyMaxCastleHP = Math.Max(EnemyMaxCastleHP, EnemyCastleHP);
            }
        }

        private void ApplyCastleDestroyed(CastleDestroyedEvent evt)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.NodeId))
            {
                return;
            }

            if (_nodes.TryGetValue(evt.NodeId, out var node) && node != null)
            {
                var selfID = MyPlayerID;
                var selfPlayerID = MyPlayer != null ? MyPlayer.Id : string.Empty;
                var isMyCastle = string.Equals(node.Owner, selfID, StringComparison.Ordinal) ||
                                 string.Equals(node.Owner, selfPlayerID, StringComparison.Ordinal);

                node.BuildingHp = 0;
                node.Owner = evt.ConquerorFaction;
                if (isMyCastle)
                {
                    if (MyPlayer != null)
                    {
                        MyPlayer.MainCastleHp = 0;
                    }
                }
                else
                {
                    EnemyCastleHP = 0;
                }
            }
        }

        private void ApplyBuildingDamaged(BuildingDamagedEvent evt)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.NodeId))
            {
                return;
            }

            if (_nodes.TryGetValue(evt.NodeId, out var node) && node != null)
            {
                node.BuildingHp = evt.HpAfter;
            }
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
            MyPlayer = null;
            _ministers.Clear();
            TokensLeft = 0;
            EnemyCastleHP = 0;
            EnemyMaxCastleHP = 0;
            OnStateChanged?.Invoke();
        }

        public void PublishPhaseChanged(PhaseChangedEvent evtArgs)
        {
            Fire(OnPhaseChanged, evtArgs, nameof(OnPhaseChanged));
        }

        public void PublishMinisterChunk(MinisterChunkEvent evtArgs)
        {
            Fire(OnMinisterChunk, evtArgs, nameof(OnMinisterChunk));
        }

        public void PublishMinisterMetrics(MinisterMetricsEvent evtArgs)
        {
            Fire(OnMinisterMetrics, evtArgs, nameof(OnMinisterMetrics));
        }

        public void PublishMinisterAction(MinisterActionEvent evtArgs)
        {
            Fire(OnMinisterAction, evtArgs, nameof(OnMinisterAction));
        }

        public void PublishTokenResult(TokenResultEvent evtArgs)
        {
            Fire(OnTokenResult, evtArgs, nameof(OnTokenResult));
        }

        public void PublishRevealResult(RevealResultEvent evtArgs)
        {
            Fire(OnRevealResult, evtArgs, nameof(OnRevealResult));
        }

        public void PublishGameOver(GameOverEvent evtArgs)
        {
            Fire(OnGameOver, evtArgs, nameof(OnGameOver));
        }

        public void PublishGameError(GameErrorEvent evtArgs)
        {
            Fire(OnGameError, evtArgs, nameof(OnGameError));
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

        private static string NormalizePhase(string phase, string fallback)
        {
            return string.IsNullOrWhiteSpace(phase) ? fallback : phase;
        }
    }
}
