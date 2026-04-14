using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Infrastructure.Mapper
{
    public static class SettlementMapper
    {
        public static DomesticSettlementDto ToDto(MsgDomesticSettlement msg)
        {
            if (msg == null)
            {
                return null;
            }

            var builtBuildings = new List<DomesticBuildResultDto>();
            var builtNodeIDs = new List<string>();
            if (msg.Changes != null)
            {
                foreach (var change in msg.Changes)
                {
                    if (change == null || change.Data == null)
                    {
                        continue;
                    }

                    var type = NormalizeToken(change.Type);
                    if (type != "building_built" && type != "buildingbuiltevent")
                    {
                        continue;
                    }

                    var nodeId = ReadString(change.Data, "node_id", "nodeId");
                    var buildingType = NormalizeToken(ReadString(change.Data, "building_type", "buildingType"));
                    if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(buildingType))
                    {
                        continue;
                    }

                    var ownerId = ReadString(change.Data, "owner", "owner_id", "ownerId");
                    var castleId = ReadString(change.Data, "castle_id", "castleId");
                    var hp = ReadInt(change.Data, 100, "building_hp", "hp_after", "hp");

                    builtBuildings.Add(new DomesticBuildResultDto
                    {
                        NodeId = nodeId,
                        BuildingType = buildingType,
                        OwnerId = ownerId,
                        CastleId = castleId,
                        BuildingHp = hp
                    });
                    builtNodeIDs.Add(nodeId);
                }
            }

            var changedNodeIDs = msg.Changes
                .Where(c => c != null && c.Data != null)
                .Select(c => c.Data.TryGetValue("node_id", out var id) ? id : string.Empty)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            return new DomesticSettlementDto
            {
                BuiltNodeIDs = builtNodeIDs,
                ChangedNodeIDs = changedNodeIDs,
                BuiltBuildings = builtBuildings,
            };
        }

        public static CombatSettlementDto ToDto(MsgCombatSettlement msg)
        {
            if (msg == null)
            {
                return null;
            }

            var events = new List<CombatEventDto>();
            for (var i = 0; i < msg.Events.Count; i++)
            {
                var evt = msg.Events[i];
                if (evt == null)
                {
                    continue;
                }

                switch (evt.DataCase)
                {
                    case CombatEvent.DataOneofCase.UnitMove when evt.UnitMove != null:
                        events.Add(new CombatEventDto
                        {
                            Type = "unit_move",
                            Sequence = i,
                            UnitId = evt.UnitMove.UnitId,
                            FromX = evt.UnitMove.From?.X ?? 0,
                            FromY = evt.UnitMove.From?.Y ?? 0,
                            ToX = evt.UnitMove.To?.X ?? 0,
                            ToY = evt.UnitMove.To?.Y ?? 0,
                        });
                        break;
                    case CombatEvent.DataOneofCase.UnitDamaged when evt.UnitDamaged != null:
                        events.Add(new CombatEventDto
                        {
                            Type = "unit_damaged",
                            Sequence = i,
                            UnitId = evt.UnitDamaged.UnitId,
                            Damage = evt.UnitDamaged.Damage,
                            HpAfter = evt.UnitDamaged.HpAfter,
                            Source = evt.UnitDamaged.Source,
                        });
                        break;
                    case CombatEvent.DataOneofCase.UnitDied when evt.UnitDied != null:
                        events.Add(new CombatEventDto
                        {
                            Type = "unit_died",
                            Sequence = i,
                            UnitId = evt.UnitDied.UnitId,
                            KillerId = evt.UnitDied.KillerId,
                            PosX = evt.UnitDied.Pos?.X ?? 0,
                            PosY = evt.UnitDied.Pos?.Y ?? 0,
                        });
                        break;
                    case CombatEvent.DataOneofCase.CastleDamaged when evt.CastleDamaged != null:
                        events.Add(new CombatEventDto
                        {
                            Type = "castle_damaged",
                            Sequence = i,
                            NodeId = evt.CastleDamaged.NodeId,
                            UnitId = evt.CastleDamaged.AttackerId,
                            Damage = evt.CastleDamaged.Damage,
                            HpAfter = evt.CastleDamaged.HpAfter,
                        });
                        break;
                    case CombatEvent.DataOneofCase.CastleDestroyed when evt.CastleDestroyed != null:
                        events.Add(new CombatEventDto
                        {
                            Type = "castle_destroyed",
                            Sequence = i,
                            NodeId = evt.CastleDestroyed.NodeId,
                            Source = evt.CastleDestroyed.ConquerorFaction,
                        });
                        break;
                    case CombatEvent.DataOneofCase.Conflict when evt.Conflict != null:
                        events.Add(new CombatEventDto
                        {
                            Type = "conflict",
                            Sequence = i,
                            UnitId = evt.Conflict.UnitAId,
                            EnemyUnitId = evt.Conflict.UnitBId,
                            ConflictType = evt.Conflict.ConflictType,
                            PosX = evt.Conflict.Location?.X ?? 0,
                            PosY = evt.Conflict.Location?.Y ?? 0,
                        });
                        break;
                    case CombatEvent.DataOneofCase.RoadDestroyed when evt.RoadDestroyed != null:
                        events.Add(new CombatEventDto
                        {
                            Type = "road_destroyed",
                            Sequence = i,
                            UnitId = evt.RoadDestroyed.DestroyerId,
                            NodeId = $"{evt.RoadDestroyed.FromNode}->{evt.RoadDestroyed.ToNode}",
                        });
                        break;
                    case CombatEvent.DataOneofCase.BuildingDamaged when evt.BuildingDamaged != null:
                        events.Add(new CombatEventDto
                        {
                            Type = "building_damaged",
                            Sequence = i,
                            NodeId = evt.BuildingDamaged.NodeId,
                            Damage = evt.BuildingDamaged.Damage,
                            HpAfter = evt.BuildingDamaged.HpAfter,
                        });
                        break;
                }
            }

            return new CombatSettlementDto
            {
                MovedUnitIDs = events.Where(e => e.Type == "unit_move").Select(e => e.UnitId).ToList(),
                DeadUnitIDs = events.Where(e => e.Type == "unit_died").Select(e => e.UnitId).ToList(),
                CastleDamaged = events.Any(e => e.Type == "castle_damaged"),
                Events = events,
            };
        }

        public static TurnSettlementDto ToDto(MsgTurnSettlement msg)
        {
            if (msg == null)
            {
                return null;
            }

            var sections = new List<TurnSettlementSectionDto>();
            var flatEvents = new List<CombatEventDto>();
            var builtBuildings = new List<DomesticBuildResultDto>();
            var builtNodeIds = new List<string>();

            if (msg.Sections != null)
            {
                foreach (var section in msg.Sections)
                {
                    if (section == null)
                    {
                        continue;
                    }

                    var sectionEvents = new List<CombatEventDto>();
                    if (section.Events != null)
                    {
                        foreach (var evt in section.Events)
                        {
                            if (evt == null)
                            {
                                continue;
                            }

                            var dto = MapTurnEvent(evt);
                            sectionEvents.Add(dto);
                            flatEvents.Add(dto);

                            if (!TryMapBuiltBuilding(evt, out var built))
                            {
                                continue;
                            }

                            builtBuildings.Add(built);
                            if (!string.IsNullOrWhiteSpace(built.NodeId))
                            {
                                builtNodeIds.Add(built.NodeId);
                            }
                        }
                    }

                    sections.Add(new TurnSettlementSectionDto
                    {
                        Section = section.Section,
                        Events = sectionEvents
                    });
                }
            }

            return new TurnSettlementDto
            {
                Sections = sections,
                Events = flatEvents,
                BuiltNodeIDs = builtNodeIds,
                BuiltBuildings = builtBuildings,
                MovedUnitIDs = flatEvents.Where(e => e.Type == "unit_moved" || e.Type == "unit_move").Select(e => e.UnitId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList(),
                DeadUnitIDs = flatEvents.Where(e => e.Type == "unit_died").Select(e => e.UnitId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList(),
                CastleDamaged = flatEvents.Any(e => e.Type == "castle_damaged")
            };
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static CombatEventDto MapTurnEvent(TurnEvent evt)
        {
            var data = evt != null ? evt.Data : null;
            return new CombatEventDto
            {
                Type = NormalizeToken(evt != null ? evt.Type : string.Empty),
                UnitId = ReadString(data, "unit_id", "unitId", "attacker_id", "attackerId"),
                TargetUnitId = ReadString(data, "target_unit_id", "targetUnitId"),
                EnemyUnitId = ReadString(data, "enemy_unit_id", "enemyUnitId"),
                KillerId = ReadString(data, "killer_id", "killerId"),
                NodeId = ReadString(data, "node_id", "nodeId", "center_node_id", "centerNodeId"),
                Source = ReadString(data, "source", "conqueror_faction", "conquerorFaction"),
                ConflictType = ReadString(data, "conflict_type", "conflictType"),
                Damage = ReadInt(data, 0, "damage"),
                HpAfter = ReadInt(data, 0, "hp_after", "hpAfter"),
                PosX = ReadInt(data, 0, "pos_x", "posX", "x"),
                PosY = ReadInt(data, 0, "pos_y", "posY", "y"),
                FromX = ReadInt(data, 0, "from_x", "fromX"),
                FromY = ReadInt(data, 0, "from_y", "fromY"),
                ToX = ReadInt(data, 0, "to_x", "toX"),
                ToY = ReadInt(data, 0, "to_y", "toY")
            };
        }

        private static bool TryMapBuiltBuilding(TurnEvent evt, out DomesticBuildResultDto result)
        {
            result = null;
            if (evt == null || evt.Data == null)
            {
                return false;
            }

            var type = NormalizeToken(evt.Type);
            if (type != "building_built" && type != "settle_city")
            {
                return false;
            }

            var nodeId = ReadString(evt.Data, "node_id", "nodeId", "center_node_id", "centerNodeId");
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            var buildingType = type == "settle_city"
                ? "castle"
                : NormalizeToken(ReadString(evt.Data, "building_type", "buildingType"));
            if (string.IsNullOrWhiteSpace(buildingType))
            {
                return false;
            }

            result = new DomesticBuildResultDto
            {
                NodeId = nodeId,
                BuildingType = buildingType,
                OwnerId = ReadString(evt.Data, "player_id", "playerId", "owner_id", "ownerId"),
                CastleId = ReadString(evt.Data, "castle_id", "castleId", "center_node_id", "centerNodeId"),
                BuildingHp = ReadInt(evt.Data, 100, "building_hp", "hp_after", "hp")
            };
            return true;
        }

        private static string ReadString(IDictionary<string, string> data, params string[] keys)
        {
            if (data == null || keys == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static int ReadInt(IDictionary<string, string> data, int fallback, params string[] keys)
        {
            if (data == null || keys == null)
            {
                return fallback;
            }

            for (var i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!data.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                if (int.TryParse(raw.Trim(), out var value))
                {
                    return value;
                }
            }

            return fallback;
        }
    }
}
