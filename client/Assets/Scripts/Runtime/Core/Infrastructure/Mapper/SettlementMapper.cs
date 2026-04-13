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

            var builtNodeIDs = msg.Changes
                .Where(c => c != null && c.Type == "building_built" && c.Data != null)
                .Select(c => c.Data.TryGetValue("node_id", out var id) ? id : string.Empty)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            var changedNodeIDs = msg.Changes
                .Where(c => c != null && c.Data != null)
                .Select(c => c.Data.TryGetValue("node_id", out var id) ? id : string.Empty)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            return new DomesticSettlementDto
            {
                BuiltNodeIDs = builtNodeIDs,
                ChangedNodeIDs = changedNodeIDs,
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
    }
}
