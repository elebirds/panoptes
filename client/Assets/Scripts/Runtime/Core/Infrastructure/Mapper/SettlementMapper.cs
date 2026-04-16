using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Infrastructure.Mapper
{
    public static class SettlementMapper
    {
        public static TurnSettlementDto ToDto(MsgTurnSettlement msg)
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

            for (var sectionIndex = 0; sectionIndex < msg.Sections.Count; sectionIndex++)
            {
                var section = msg.Sections[sectionIndex];
                if (section == null)
                {
                    continue;
                }

                var sectionName = NormalizeToken(section.Section);
                var events = new List<TurnEventDto>();
                for (var eventIndex = 0; eventIndex < section.Events.Count; eventIndex++)
                {
                    var evt = MapEvent(section.Events[eventIndex], sectionName, eventIndex);
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

        private static TurnEventDto MapEvent(TurnEvent evt, string section, int sequence)
        {
            if (evt == null)
            {
                return null;
            }

            var data = evt.Data != null
                ? evt.Data.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, string>();

            return new TurnEventDto
            {
                Section = section,
                Type = NormalizeToken(evt.Type),
                Data = data,
                UnitId = ReadString(data, "unit_id", "attacker"),
                TargetUnitId = ReadString(data, "target_unit_id"),
                EnemyUnitId = ReadString(data, "enemy_unit_id", "unit_b_id"),
                KillerId = ReadString(data, "killer_id"),
                NodeId = ReadString(data, "node_id"),
                Source = ReadString(data, "source", "owner", "player_id", "attacker", "faction"),
                ConflictType = ReadString(data, "conflict_type"),
                Damage = ReadInt(data, 0, "damage"),
                HpAfter = ReadInt(data, 0, "hp_after", "building_hp"),
                Sequence = sequence,
                PosX = ReadInt(data, 0, "pos_x"),
                PosY = ReadInt(data, 0, "pos_y"),
                FromX = ReadInt(data, 0, "from_x"),
                FromY = ReadInt(data, 0, "from_y"),
                ToX = ReadInt(data, 0, "to_x"),
                ToY = ReadInt(data, 0, "to_y")
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
