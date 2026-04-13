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
            foreach (var evt in msg.Events)
            {
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
                            UnitId = evt.UnitDamaged.UnitId,
                            HpAfter = evt.UnitDamaged.HpAfter,
                        });
                        break;
                    case CombatEvent.DataOneofCase.UnitDied when evt.UnitDied != null:
                        events.Add(new CombatEventDto
                        {
                            Type = "unit_died",
                            UnitId = evt.UnitDied.UnitId,
                        });
                        break;
                    case CombatEvent.DataOneofCase.CastleDamaged when evt.CastleDamaged != null:
                        events.Add(new CombatEventDto
                        {
                            Type = "castle_damaged",
                            NodeId = evt.CastleDamaged.NodeId,
                            HpAfter = evt.CastleDamaged.HpAfter,
                        });
                        break;
                    case CombatEvent.DataOneofCase.BuildingDamaged when evt.BuildingDamaged != null:
                        events.Add(new CombatEventDto
                        {
                            Type = "building_damaged",
                            NodeId = evt.BuildingDamaged.NodeId,
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

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
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
