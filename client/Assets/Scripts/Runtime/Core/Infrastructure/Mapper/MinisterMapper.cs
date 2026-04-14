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
    }
}
