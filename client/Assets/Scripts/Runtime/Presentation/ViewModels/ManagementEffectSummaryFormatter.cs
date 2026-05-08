using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;

namespace Panoptes.Presentation.ViewModels
{
    internal static class ManagementEffectSummaryFormatter
    {
        public static string Build(
            IReadOnlyList<CatalogPolicyModifierEffectDto> effects,
            string fallback,
            StaticCatalogState catalog)
        {
            if (effects == null || effects.Count == 0)
            {
                return fallback ?? string.Empty;
            }

            var values = new List<string>();
            for (var i = 0; i < effects.Count; i++)
            {
                var text = FormatModifierEffect(effects[i], catalog);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text);
                }
            }

            return values.Count > 0 ? string.Join("；", values) : fallback ?? string.Empty;
        }

        private static string FormatModifierEffect(CatalogPolicyModifierEffectDto effect, StaticCatalogState catalog)
        {
            if (effect == null)
            {
                return string.Empty;
            }

            var value = FormatSigned(effect.Value);
            var target = CatalogDisplayNameResolver.ResolveEffectTargetName(effect.TargetId, catalog);
            var trigger = Normalize(effect.Trigger);
            switch (trigger)
            {
                case "recipe.work_amount":
                    return string.IsNullOrWhiteSpace(target) ? $"工时 {value}" : $"{target} 工时 {value}";
                case "recipe.resource_output":
                    var resource = CatalogDisplayNameResolver.ResolveResourceName(effect.ResourceKey);
                    return string.IsNullOrWhiteSpace(target)
                        ? $"{resource}产出 {value}"
                        : $"{target} {resource}产出 {value}";
                case "recipe.base_progress":
                    return string.IsNullOrWhiteSpace(target) ? $"基础进度 {value}" : $"{target} 基础进度 {value}";
                case "point.output":
                    var point = CatalogDisplayNameResolver.ResolvePointName(effect.PointKey);
                    return string.IsNullOrWhiteSpace(point) ? $"点数产出 {value}" : $"{point} {value}";
                case "logistics.road_capacity":
                    return $"道路运力 {value}";
                default:
                    return string.IsNullOrWhiteSpace(trigger) ? value : $"{FormatTrigger(trigger)} {value}";
            }
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? "+" + value : value.ToString();
        }

        private static string FormatTrigger(string trigger)
        {
            switch (Normalize(trigger))
            {
                case "recipe.work_amount":
                    return "配方工时";
                case "recipe.resource_output":
                    return "配方资源产出";
                case "recipe.base_progress":
                    return "配方基础进度";
                case "point.output":
                    return "点数产出";
                case "logistics.road_capacity":
                    return "道路运力";
                default:
                    return CatalogDisplayNameResolver.ToReadableKey(trigger);
            }
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
