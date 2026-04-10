using System.Collections.Generic;
using System.Globalization;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

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

        public static MinisterActionItemDto ToDto(MinisterActionItem item)
        {
            if (item == null)
            {
                return null;
            }

            return new MinisterActionItemDto
            {
                ActionId = GetParam(item, "action_id"),
                ActionType = item.Type,
                Description = GetActionDescription(item),
                TargetNodeId = GetParam(item, "node_id", "target_node"),
                TargetUnitId = GetParam(item, "unit_id", "target_unit"),
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

        private static string GetActionDescription(MinisterActionItem item)
        {
            var desc = GetParam(item, "description", "desc", "reason");
            return string.IsNullOrWhiteSpace(desc) ? item.Type : desc;
        }

        private static string GetParam(MinisterActionItem item, params string[] keys)
        {
            if (item == null || item.Params == null || keys == null)
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

                if (item.Params.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
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

        public static List<MinisterActionItemDto> ToDtoList(IEnumerable<MinisterActionItem> items)
        {
            var result = new List<MinisterActionItemDto>();
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
    }
}
