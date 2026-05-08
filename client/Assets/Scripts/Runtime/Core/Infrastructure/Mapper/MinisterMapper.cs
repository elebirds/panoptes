using System;
using System.Collections.Generic;
using System.Globalization;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Infrastructure.Mapper
{
    public static class MinisterMapper
    {
        public static MinisterMetricDto ToDto(MetricItem item)
        {
            if (item == null)
            {
                return null;
            }

            return new MinisterMetricDto
            {
                // Current protocol exposes label/value as strings.
                // Keep Key aligned with label for downstream UI grouping.
                Key = item.Label,
                Value = ParseMetricValue(item.Value),
                Label = item.Label,
            };
        }

        private static float ParseMetricValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0f;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return 0f;
        }

        public static List<MinisterMetricDto> ToDtoList(IEnumerable<MetricItem> items)
        {
            var result = new List<MinisterMetricDto>();
            if (items == null)
            {
                return result;
            }

            foreach (var item in items)
            {
                var dto = ToDto(item);
                if (dto != null)
                {
                    result.Add(dto);
                }
            }

            return result;
        }

        public static MinisterDraftDto ToDto(MinisterDraftView view)
        {
            if (view == null || string.IsNullOrWhiteSpace(view.JsonPayload))
            {
                return null;
            }

            MinisterDraftPayload payload;
            try
            {
                payload = JsonUtility.FromJson<MinisterDraftPayload>(view.JsonPayload);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.draft_id))
            {
                return null;
            }

            return new MinisterDraftDto
            {
                DraftId = payload.draft_id ?? string.Empty,
                PlayerId = payload.player_id ?? string.Empty,
                MinisterRole = !string.IsNullOrWhiteSpace(payload.minister_role)
                    ? payload.minister_role
                    : (view.MinisterRole ?? string.Empty),
                Kind = payload.kind ?? string.Empty,
                TargetId = payload.target_id ?? string.Empty,
                TargetLabel = payload.target_label ?? string.Empty,
                Title = payload.title ?? string.Empty,
                Summary = payload.summary ?? string.Empty,
                Rationale = payload.rationale ?? string.Empty,
                RiskNote = payload.risk_note ?? string.Empty,
                Status = payload.status ?? string.Empty,
                Available = view.Available && payload.available,
                Turn = payload.turn,
                Source = payload.source ?? string.Empty,
                InstitutionIds = payload.institution_ids ?? Array.Empty<string>(),
                NodeId = payload.node_id ?? string.Empty,
                BuildingTypeId = payload.building_type_id ?? string.Empty,
                CityId = payload.city_id ?? string.Empty,
                RecipeId = payload.recipe_id ?? string.Empty,
                UnitId = payload.unit_id ?? string.Empty,
                Action = payload.action ?? string.Empty,
                TargetNodeId = payload.target_node_id ?? string.Empty,
                TargetUnitId = payload.target_unit_id ?? string.Empty,
                SecondaryNodeId = payload.secondary_node_id ?? string.Empty
            };
        }

        [Serializable]
        private sealed class MinisterDraftPayload
        {
            public string draft_id;
            public string player_id;
            public string minister_role;
            public string kind;
            public string target_id;
            public string target_label;
            public string title;
            public string summary;
            public string rationale;
            public string risk_note;
            public string status;
            public bool available;
            public int turn;
            public string source;
            public string[] institution_ids;
            public string node_id;
            public string building_type_id;
            public string city_id;
            public string recipe_id;
            public string unit_id;
            public string action;
            public string target_node_id;
            public string target_unit_id;
            public string secondary_node_id;
        }
    }
}
