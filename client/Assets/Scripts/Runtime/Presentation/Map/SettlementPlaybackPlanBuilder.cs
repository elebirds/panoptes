using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;

namespace Panoptes.Presentation.Map
{
    public sealed class SettlementPlaybackStep
    {
        public string ActorUnitId = string.Empty;
        public TurnEventDto MoveEvent;
        public readonly List<TurnEventDto> ImpactEvents = new();
        public readonly List<SettlementPlaybackImpact> Impacts = new();

        public bool HasActor => !string.IsNullOrWhiteSpace(ActorUnitId);
        public bool HasMove => MoveEvent != null;
        public bool HasImpacts => Impacts.Count > 0;

        public void AddImpactEvent(TurnEventDto evt)
        {
            if (evt == null)
            {
                return;
            }

            ImpactEvents.Add(evt);
            if (TryMergeUnitDamageAndDeath(evt))
            {
                return;
            }

            Impacts.Add(SettlementPlaybackImpact.FromEvent(evt));
        }

        private bool TryMergeUnitDamageAndDeath(TurnEventDto evt)
        {
            if (!IsUnitDamageOrDeath(evt) || string.IsNullOrWhiteSpace(evt.UnitId))
            {
                return false;
            }

            var unitId = evt.UnitId.Trim();
            for (var i = Impacts.Count - 1; i >= 0; i--)
            {
                var impact = Impacts[i];
                if (impact == null ||
                    !impact.IsUnitImpact ||
                    !string.Equals(impact.UnitId, unitId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(evt.Type, "unit_damaged", StringComparison.Ordinal) &&
                    impact.DamageEvent == null &&
                    impact.DeathEvent != null)
                {
                    impact.DamageEvent = evt;
                    return true;
                }

                if (string.Equals(evt.Type, "unit_died", StringComparison.Ordinal) &&
                    impact.DeathEvent == null &&
                    impact.DamageEvent != null)
                {
                    impact.DeathEvent = evt;
                    return true;
                }
            }

            return false;
        }

        private static bool IsUnitDamageOrDeath(TurnEventDto evt)
        {
            return evt != null &&
                   (string.Equals(evt.Type, "unit_damaged", StringComparison.Ordinal) ||
                    string.Equals(evt.Type, "unit_died", StringComparison.Ordinal));
        }
    }

    public sealed class SettlementPlaybackImpact
    {
        public TurnEventDto DamageEvent;
        public TurnEventDto DeathEvent;
        public TurnEventDto BuildingEvent;

        public bool IsUnitImpact => DamageEvent != null || DeathEvent != null;
        public bool IsBuildingImpact => BuildingEvent != null;
        public string UnitId => DamageEvent?.UnitId ?? DeathEvent?.UnitId ?? string.Empty;

        public static SettlementPlaybackImpact FromEvent(TurnEventDto evt)
        {
            var impact = new SettlementPlaybackImpact();
            if (evt == null)
            {
                return impact;
            }

            switch (evt.Type)
            {
                case "unit_damaged":
                    impact.DamageEvent = evt;
                    break;
                case "unit_died":
                    impact.DeathEvent = evt;
                    break;
                default:
                    impact.BuildingEvent = evt;
                    break;
            }

            return impact;
        }
    }

    public static class SettlementPlaybackPlanBuilder
    {
        public static List<SettlementPlaybackStep> Build(TurnSettlementDto settlement)
        {
            var steps = new List<SettlementPlaybackStep>();
            if (settlement?.Sections == null)
            {
                return steps;
            }

            var latestStepByActor = new Dictionary<string, SettlementPlaybackStep>(StringComparer.Ordinal);
            for (var sectionIndex = 0; sectionIndex < settlement.Sections.Count; sectionIndex++)
            {
                var section = settlement.Sections[sectionIndex];
                if (section?.Events == null)
                {
                    continue;
                }

                for (var eventIndex = 0; eventIndex < section.Events.Count; eventIndex++)
                {
                    var evt = section.Events[eventIndex];
                    if (evt == null)
                    {
                        continue;
                    }

                    if (IsMoveEvent(evt))
                    {
                        AddMoveStep(steps, latestStepByActor, evt);
                        continue;
                    }

                    if (IsImpactEvent(evt))
                    {
                        AddImpactStep(steps, latestStepByActor, evt);
                    }
                }
            }

            return steps;
        }

        private static void AddMoveStep(
            List<SettlementPlaybackStep> steps,
            Dictionary<string, SettlementPlaybackStep> latestStepByActor,
            TurnEventDto evt)
        {
            if (string.IsNullOrWhiteSpace(evt.UnitId))
            {
                return;
            }

            var actorId = evt.UnitId.Trim();
            var step = new SettlementPlaybackStep
            {
                ActorUnitId = actorId,
                MoveEvent = evt
            };
            steps.Add(step);
            latestStepByActor[actorId] = step;
        }

        private static void AddImpactStep(
            List<SettlementPlaybackStep> steps,
            Dictionary<string, SettlementPlaybackStep> latestStepByActor,
            TurnEventDto evt)
        {
            var actorId = ResolveAttackerUnitId(evt);
            if (string.IsNullOrWhiteSpace(actorId))
            {
                var orphanStep = new SettlementPlaybackStep();
                orphanStep.AddImpactEvent(evt);
                steps.Add(orphanStep);
                return;
            }

            actorId = actorId.Trim();
            if (!latestStepByActor.TryGetValue(actorId, out var step) || step == null)
            {
                step = new SettlementPlaybackStep
                {
                    ActorUnitId = actorId
                };
                steps.Add(step);
                latestStepByActor[actorId] = step;
            }

            step.AddImpactEvent(evt);
        }

        private static bool IsMoveEvent(TurnEventDto evt)
        {
            return string.Equals(evt.Type, "unit_moved", StringComparison.Ordinal);
        }

        private static bool IsImpactEvent(TurnEventDto evt)
        {
            var type = evt.Type ?? string.Empty;
            return string.Equals(type, "unit_damaged", StringComparison.Ordinal) ||
                   string.Equals(type, "unit_died", StringComparison.Ordinal) ||
                   string.Equals(type, "building_damaged", StringComparison.Ordinal) ||
                   string.Equals(type, "city_core_damaged", StringComparison.Ordinal) ||
                   string.Equals(type, "city_core_destroyed", StringComparison.Ordinal);
        }

        private static string ResolveAttackerUnitId(TurnEventDto evt)
        {
            if (evt == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(evt.AttackerUnitId))
            {
                return evt.AttackerUnitId;
            }

            if (!string.IsNullOrWhiteSpace(evt.KillerId))
            {
                return evt.KillerId;
            }

            return ReadEventString(evt, "attacker", "attacker_unit_id", "killer_id");
        }

        private static string ReadEventString(TurnEventDto evt, params string[] keys)
        {
            if (evt?.Data == null || keys == null)
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

                if (evt.Data.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
                {
                    return raw.Trim();
                }
            }

            return string.Empty;
        }
    }
}
