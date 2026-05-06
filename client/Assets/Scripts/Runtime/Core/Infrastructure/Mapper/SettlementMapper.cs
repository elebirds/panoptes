using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Infrastructure.Mapper
{
    public static class SettlementMapper
    {
        public static TurnSettlementDto ToDto(MsgGameSync msg)
        {
            if (msg == null)
            {
                return null;
            }

            var sections = new List<SettlementSectionDto>();
            var builtNodeIds = new List<string>();
            var builtBuildings = new List<BuiltStructureDto>();
            var movedUnitIds = new List<string>();
            var deadUnitIds = new List<string>();
            var cityCoreDamaged = false;

            var groupedEvents = msg.Events
                .Where(evt => evt != null)
                .GroupBy(evt => NormalizeToken(evt.Channel))
                .ToList();

            for (var sectionIndex = 0; sectionIndex < groupedEvents.Count; sectionIndex++)
            {
                var section = groupedEvents[sectionIndex];
                var sectionName = NormalizeToken(section.Key);
                var events = new List<TurnEventDto>();
                var sectionEvents = section.ToList();
                for (var eventIndex = 0; eventIndex < sectionEvents.Count; eventIndex++)
                {
                    var evt = MapEvent(sectionEvents[eventIndex], sectionName, eventIndex);
                    if (evt == null)
                    {
                        continue;
                    }

                    events.Add(evt);

                    switch (evt.Type)
                    {
                        case "building_built":
                        case "city_founded":
                            if (!string.IsNullOrWhiteSpace(evt.NodeId))
                            {
                                builtNodeIds.Add(evt.NodeId);
                            }

                            builtBuildings.Add(new BuiltStructureDto
                            {
                                NodeId = evt.NodeId,
                                BuildingType = ReadString(evt.Data, "building_type_id", "building_type"),
                                OwnerId = ReadString(evt.Data, "controller_player_id", "owner"),
                                CityId = ReadString(evt.Data, "city_id"),
                                BuildingHp = ReadInt(evt.Data, 100, "building_hp", "hp_after", "hp")
                            });
                            break;
                        case "unit_moved":
                            if (!string.IsNullOrWhiteSpace(evt.UnitId))
                            {
                                movedUnitIds.Add(evt.UnitId);
                            }
                            break;
                        case "unit_died":
                            if (!string.IsNullOrWhiteSpace(evt.UnitId))
                            {
                                deadUnitIds.Add(evt.UnitId);
                            }
                            break;
                        case "city_core_damaged":
                        case "city_core_destroyed":
                            cityCoreDamaged = true;
                            break;
                    }
                }

                sections.Add(new SettlementSectionDto
                {
                    Section = sectionName,
                    Events = events
                });
            }

            return new TurnSettlementDto
            {
                Phase = msg.Phase,
                NextPhase = msg.NextPhase,
                Sections = sections,
                BuiltNodeIDs = builtNodeIds.Distinct().ToList(),
                BuiltBuildings = builtBuildings,
                MovedUnitIDs = movedUnitIds.Distinct().ToList(),
                DeadUnitIDs = deadUnitIds.Distinct().ToList(),
                CityCoreDamaged = cityCoreDamaged
            };
        }

        public static List<TurnEventDto> ToPlanningStartEvents(MsgPlanningStart msg)
        {
            if (msg?.PlanningStartEvents == null || msg.PlanningStartEvents.Count == 0)
            {
                return new List<TurnEventDto>();
            }

            // planning_start_events 和 settlement 事件共用同一份 TurnEventDto 结构，
            // 这样客户端缓存、日志和后续展示都不需要再维护第二套事件 DTO。
            var events = new List<TurnEventDto>();
            for (var i = 0; i < msg.PlanningStartEvents.Count; i++)
            {
                var evt = MapEvent(msg.PlanningStartEvents[i], "planning_start", i);
                if (evt != null)
                {
                    events.Add(evt);
                }
            }

            return events;
        }

        private static TurnEventDto MapEvent(DomainEventEnvelope evt, string section, int sequence)
        {
            if (evt == null)
            {
                return null;
            }

            var data = evt.Data != null
                ? evt.Data.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, string>();
            ApplyTypedEventData(evt, data);

            return new TurnEventDto
            {
                Section = section,
                Type = ResolveEventType(evt),
                Data = data,
                ReasonMessage = ReadString(data, "reason_message"),
                BlockedReasonMessage = ReadString(data, "blocked_reason_message"),
                UnitId = ReadString(data, "unit_id", "unit_a_id"),
                AttackerUnitId = ReadString(data, "attacker", "attacker_unit_id", "killer_id"),
                TargetUnitId = ReadString(data, "target_unit_id", "unit_b_id"),
                EnemyUnitId = ReadString(data, "enemy_unit_id", "unit_b_id"),
                KillerId = ReadString(data, "killer_id"),
                NodeId = ReadString(data, "node_id"),
                Source = ReadString(data, "source", "owner", "player_id", "attacker", "faction"),
                ConflictType = ReadString(data, "conflict_type"),
                Damage = ReadInt(data, 0, "damage"),
                HpAfter = ReadInt(data, 0, "hp_after", "building_hp"),
                Sequence = sequence,
                PosQ = ReadInt(data, 0, "pos_q"),
                PosR = ReadInt(data, 0, "pos_r"),
                FromQ = ReadInt(data, 0, "from_q"),
                FromR = ReadInt(data, 0, "from_r"),
                ToQ = ReadInt(data, 0, "to_q"),
                ToR = ReadInt(data, 0, "to_r")
            };
        }

        private static string ResolveEventType(DomainEventEnvelope evt)
        {
            var kind = NormalizeToken(evt?.Kind);
            if (!string.IsNullOrWhiteSpace(kind))
            {
                return kind;
            }

            if (evt?.UnitMoved != null)
            {
                return "unit_moved";
            }

            return string.Empty;
        }

        private static void ApplyTypedEventData(DomainEventEnvelope evt, IDictionary<string, string> data)
        {
            if (evt == null || data == null)
            {
                return;
            }

            if (evt.UnitMoved != null)
            {
                SetIfMissing(data, "unit_id", evt.UnitMoved.UnitId);
                SetIfMissing(data, "from_q", evt.UnitMoved.FromQ.ToString());
                SetIfMissing(data, "from_r", evt.UnitMoved.FromR.ToString());
                SetIfMissing(data, "to_q", evt.UnitMoved.ToQ.ToString());
                SetIfMissing(data, "to_r", evt.UnitMoved.ToR.ToString());
            }
        }

        private static void SetIfMissing(IDictionary<string, string> data, string key, string value)
        {
            if (data == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!data.ContainsKey(key) || string.IsNullOrWhiteSpace(data[key]))
            {
                data[key] = value.Trim();
            }
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

                if (!data.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (int.TryParse(value, out var parsed))
                {
                    return parsed;
                }
            }

            return fallback;
        }
    }
}
