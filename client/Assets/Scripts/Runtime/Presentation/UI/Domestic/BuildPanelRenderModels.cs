using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Panoptes.Presentation.UI.Domestic
{
    public enum BuildItemAvailabilityState
    {
        Available = 0,
        Locked = 1,
        Pending = 2
    }

    [Serializable]
    public sealed class BuildMetricRenderModel
    {
        public string Key;
        public string Text;
        public Sprite Icon;
    }

    [Serializable]
    public sealed class BuildItemRenderModel
    {
        public string BuildingId;
        public string Title;
        public string ShortDescription;
        public Sprite Icon;
        public BuildItemAvailabilityState AvailabilityState;
        public List<BuildMetricRenderModel> SummaryMetrics = new();
        public string TooltipText;
    }

    [Serializable]
    public sealed class BuildGroupRenderModel
    {
        public string Id;
        public string Title;
        public List<BuildItemRenderModel> Items = new();
    }

    internal enum BuildRenderGroupKind
    {
        Tile = 0,
        City = 1,
        Other = 2
    }

    internal readonly struct BuildMetricSource
    {
        public readonly string Key;
        public readonly string DisplayName;
        public readonly int Amount;
        public readonly Sprite Icon;
        public readonly bool IsPoint;

        public BuildMetricSource(string key, string displayName, int amount, Sprite icon, bool isPoint)
        {
            Key = key;
            DisplayName = displayName;
            Amount = amount;
            Icon = icon;
            IsPoint = isPoint;
        }
    }

    internal sealed class BuildItemRenderSource
    {
        public string BuildingId;
        public string Title;
        public string Description;
        public string PlacementKind;
        public string RequiredResourceType;
        public Sprite Icon;
        public BuildItemAvailabilityState AvailabilityState;
        public string LockedSuffix;
        public readonly List<BuildMetricSource> Costs = new();
        public readonly List<string> RequiredTechNames = new();
    }

    internal static class BuildPanelRenderBuilder
    {
        public static List<BuildGroupRenderModel> Build(IReadOnlyList<BuildItemRenderSource> sources)
        {
            var groupsByKind = new Dictionary<BuildRenderGroupKind, BuildGroupRenderModel>();
            var orderedKinds = new[] { BuildRenderGroupKind.Tile, BuildRenderGroupKind.City, BuildRenderGroupKind.Other };

            if (sources != null)
            {
                for (var i = 0; i < sources.Count; i++)
                {
                    var source = sources[i];
                    if (source == null)
                    {
                        continue;
                    }

                    var kind = ResolveGroupKind(source.PlacementKind);
                    if (!groupsByKind.TryGetValue(kind, out var group))
                    {
                        group = new BuildGroupRenderModel
                        {
                            Id = kind.ToString().ToLowerInvariant(),
                            Title = ResolveGroupTitle(kind)
                        };
                        groupsByKind[kind] = group;
                    }

                    group.Items.Add(BuildItem(source));
                }
            }

            var orderedGroups = new List<BuildGroupRenderModel>(3);
            for (var i = 0; i < orderedKinds.Length; i++)
            {
                if (groupsByKind.TryGetValue(orderedKinds[i], out var group) &&
                    group != null &&
                    group.Items.Count > 0)
                {
                    orderedGroups.Add(group);
                }
            }

            return orderedGroups;
        }

        private static BuildItemRenderModel BuildItem(BuildItemRenderSource source)
        {
            return new BuildItemRenderModel
            {
                BuildingId = source.BuildingId ?? string.Empty,
                Title = string.IsNullOrWhiteSpace(source.Title) ? "Unknown Building" : source.Title.Trim(),
                ShortDescription = BuildShortDescription(source.Description),
                Icon = source.Icon,
                AvailabilityState = source.AvailabilityState,
                SummaryMetrics = BuildSummaryMetrics(source),
                TooltipText = BuildTooltipText(source)
            };
        }

        private static List<BuildMetricRenderModel> BuildSummaryMetrics(BuildItemRenderSource source)
        {
            var result = new List<BuildMetricRenderModel>(3);
            if (source == null || source.Costs.Count == 0)
            {
                return result;
            }

            BuildMetricSource? primaryPoint = null;
            var pointFallbacks = new List<BuildMetricSource>();
            var resources = new List<BuildMetricSource>();

            for (var i = 0; i < source.Costs.Count; i++)
            {
                var cost = source.Costs[i];
                if (cost.Amount <= 0)
                {
                    continue;
                }

                if (cost.IsPoint)
                {
                    if (string.Equals(cost.Key, "industry_output", StringComparison.OrdinalIgnoreCase))
                    {
                        primaryPoint = cost;
                    }
                    else
                    {
                        pointFallbacks.Add(cost);
                    }
                }
                else
                {
                    resources.Add(cost);
                }
            }

            if (primaryPoint.HasValue)
            {
                result.Add(ToMetric(primaryPoint.Value));
            }

            for (var i = 0; i < resources.Count && result.Count < 3; i++)
            {
                result.Add(ToMetric(resources[i]));
            }

            for (var i = 0; i < pointFallbacks.Count && result.Count < 3; i++)
            {
                result.Add(ToMetric(pointFallbacks[i]));
            }

            return result;
        }

        private static BuildMetricRenderModel ToMetric(BuildMetricSource source)
        {
            var label = string.IsNullOrWhiteSpace(source.DisplayName) ? source.Key : source.DisplayName;
            return new BuildMetricRenderModel
            {
                Key = source.Key ?? string.Empty,
                Text = $"{label} x{Mathf.Max(0, source.Amount)}",
                Icon = source.Icon
            };
        }

        private static string BuildTooltipText(BuildItemRenderSource source)
        {
            var sb = new StringBuilder(256);
            if (!string.IsNullOrWhiteSpace(source.Title))
            {
                sb.AppendLine(source.Title.Trim());
            }

            if (!string.IsNullOrWhiteSpace(source.Description))
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine(source.Description.Trim());
            }

            if (source.Costs.Count > 0)
            {
                AppendSectionTitle(sb, "消耗");
                for (var i = 0; i < source.Costs.Count; i++)
                {
                    var cost = source.Costs[i];
                    if (cost.Amount <= 0)
                    {
                        continue;
                    }

                    var label = string.IsNullOrWhiteSpace(cost.DisplayName) ? cost.Key : cost.DisplayName;
                    sb.Append("- ").Append(label).Append(": ").Append(cost.Amount).AppendLine();
                }
            }

            var placementLine = BuildPlacementLine(source);
            if (!string.IsNullOrWhiteSpace(placementLine))
            {
                AppendSectionTitle(sb, "放置要求");
                sb.AppendLine(placementLine);
            }

            if (source.AvailabilityState == BuildItemAvailabilityState.Locked)
            {
                AppendSectionTitle(sb, "解锁要求");
                if (source.RequiredTechNames.Count > 0)
                {
                    sb.AppendLine(string.Join(" / ", source.RequiredTechNames));
                }
                else if (!string.IsNullOrWhiteSpace(source.LockedSuffix))
                {
                    sb.AppendLine(source.LockedSuffix.Trim());
                }
            }

            if (source.AvailabilityState == BuildItemAvailabilityState.Pending)
            {
                AppendSectionTitle(sb, "状态");
                sb.AppendLine("已加入本回合规划");
            }

            return sb.ToString().Trim();
        }

        private static string BuildPlacementLine(BuildItemRenderSource source)
        {
            var placementKind = NormalizeToken(source != null ? source.PlacementKind : string.Empty);
            switch (placementKind)
            {
                case "resource_node":
                    if (!string.IsNullOrWhiteSpace(source?.RequiredResourceType))
                    {
                        return $"仅可放置在{source.RequiredResourceType.Trim()}资源地块";
                    }

                    return "仅可放置在资源地块";
                case "city_territory":
                    return "仅可放置在城市领土";
                case "city_foundation_center":
                    return "仅可放置在城市核心";
                default:
                    return string.Empty;
            }
        }

        private static string BuildShortDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return string.Empty;
            }

            var normalized = description.Trim().Replace('\n', ' ').Replace('\r', ' ');
            while (normalized.Contains("  ", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            }

            return normalized;
        }

        private static void AppendSectionTitle(StringBuilder sb, string title)
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine(title);
        }

        private static BuildRenderGroupKind ResolveGroupKind(string placementKind)
        {
            return NormalizeToken(placementKind) switch
            {
                "resource_node" => BuildRenderGroupKind.Tile,
                "city_territory" => BuildRenderGroupKind.City,
                _ => BuildRenderGroupKind.Other
            };
        }

        private static string ResolveGroupTitle(BuildRenderGroupKind kind)
        {
            return kind switch
            {
                BuildRenderGroupKind.Tile => "地块建筑",
                BuildRenderGroupKind.City => "城市建筑",
                _ => "其他"
            };
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
