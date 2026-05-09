using System.Collections.Generic;
using System.Globalization;
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

            return values.Count > 0 ? string.Join("，", values) : fallback ?? string.Empty;
        }

        private static string FormatModifierEffect(CatalogPolicyModifierEffectDto effect, StaticCatalogState catalog)
        {
            if (effect == null)
            {
                return string.Empty;
            }

            var value = FormatValue(effect.Value, effect.ModifierType);
            var target = CatalogDisplayNameResolver.ResolveEffectTargetName(effect.TargetId, catalog);
            var trigger = Normalize(effect.Trigger);
            switch (trigger)
            {
                case "building.max_hp":
                    return string.IsNullOrWhiteSpace(target) ? $"建筑耐久 {value}" : $"{target}耐久 {value}";
                case "building.point_cost":
                    var costPoint = CatalogDisplayNameResolver.ResolvePointName(effect.PointKey);
                    return string.IsNullOrWhiteSpace(costPoint)
                        ? $"建筑点数消耗 {value}"
                        : $"建筑{costPoint}消耗 {value}";
                case "recipe.resource_input":
                    var inputResource = CatalogDisplayNameResolver.ResolveResourceName(effect.ResourceKey);
                    var inputLabel = string.IsNullOrWhiteSpace(inputResource) ? "资源" : inputResource;
                    return string.IsNullOrWhiteSpace(target)
                        ? $"配方{inputLabel}消耗 {value}"
                        : $"{target}{inputLabel}消耗 {value}";
                case "recipe.work_amount":
                    return string.IsNullOrWhiteSpace(target) ? $"配方工时 {value}" : $"{target}工时 {value}";
                case "recipe.resource_output":
                    var outputResource = CatalogDisplayNameResolver.ResolveResourceName(effect.ResourceKey);
                    var outputLabel = string.IsNullOrWhiteSpace(outputResource) ? "资源" : outputResource;
                    return string.IsNullOrWhiteSpace(target)
                        ? $"{outputLabel}产出 {value}"
                        : $"{target}{outputLabel}产出 {value}";
                case "recipe.base_progress":
                    return string.IsNullOrWhiteSpace(target) ? $"配方基础进度 {value}" : $"{target}基础进度 {value}";
                case "unit.attack":
                    return string.IsNullOrWhiteSpace(target) ? $"单位攻击 {value}" : $"{target}攻击 {value}";
                case "unit.move_range":
                    return string.IsNullOrWhiteSpace(target) ? $"单位移动力 {value}" : $"{target}移动力 {value}";
                case "unit.siege_multiplier":
                    return string.IsNullOrWhiteSpace(target) ? $"单位攻城倍率 {value}" : $"{target}攻城倍率 {value}";
                case "point.output":
                    var point = CatalogDisplayNameResolver.ResolvePointName(effect.PointKey);
                    return string.IsNullOrWhiteSpace(point) ? $"点数产出 {value}" : $"{point} {value}";
                case "logistics.road_capacity":
                    return $"道路运力 {value}";
                default:
                    return string.IsNullOrWhiteSpace(trigger) ? value : $"{FormatTrigger(trigger)} {value}";
            }
        }

        private static string FormatValue(float value, string modifierType)
        {
            var normalizedType = Normalize(modifierType);
            if (normalizedType == "percent")
            {
                return FormatSignedNumber(value * 100f) + "%";
            }

            if (normalizedType == "multiplier")
            {
                return "x" + FormatNumber(value);
            }

            return FormatSignedNumber(value);
        }

        private static string FormatSignedNumber(float value)
        {
            return value > 0f ? "+" + FormatNumber(value) : FormatNumber(value);
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatTrigger(string trigger)
        {
            switch (Normalize(trigger))
            {
                case "building.max_hp":
                    return "建筑耐久";
                case "building.point_cost":
                    return "建筑点数消耗";
                case "recipe.resource_input":
                    return "配方资源消耗";
                case "recipe.work_amount":
                    return "配方工时";
                case "recipe.resource_output":
                    return "配方资源产出";
                case "recipe.base_progress":
                    return "配方基础进度";
                case "unit.attack":
                    return "单位攻击";
                case "unit.move_range":
                    return "单位移动力";
                case "unit.siege_multiplier":
                    return "单位攻城倍率";
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
