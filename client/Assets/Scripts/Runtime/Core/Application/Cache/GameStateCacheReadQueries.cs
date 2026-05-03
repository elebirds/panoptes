/*************************************************
 * Project: Panoptes
 * File: GameStateCacheReadQueries.cs
 * Author: Panoptes Team
 * Date: 2026-05-01
 * Description: Read-only snapshot helpers for GameStateCache facade queries.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Application.Cache
{
    internal static class GameStateCacheReadQueries
    {
        private const string IndustryOutputPointKey = "industry_output";

        internal static ResourceDto SnapshotResources(PlayerView player)
        {
            return SnapshotResources(player?.Resources, player?.Points);
        }

        internal static ResourceDto SnapshotResources(ResourceBag bag, PointBag points)
        {
            var resources = new ResourceDto
            {
                ResourceAmounts = SnapshotResourceAmounts(bag),
                PointAmounts = SnapshotPointAmounts(points)
            };
            ApplyFixedResourceFields(resources);
            return resources;
        }

        internal static ResourceDto CloneResources(ResourceDto source)
        {
            if (source == null)
            {
                return new ResourceDto();
            }

            return new ResourceDto
            {
                Ore = source.Ore,
                Wood = source.Wood,
                Food = source.Food,
                IndustryOutput = source.IndustryOutput,
                ResourceAmounts = CloneAmounts(source.ResourceAmounts),
                PointAmounts = CloneAmounts(source.PointAmounts)
            };
        }

        internal static Dictionary<string, int> SnapshotResourceAmounts(ResourceBag bag)
        {
            var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (bag?.Items == null)
            {
                return values;
            }

            for (var i = 0; i < bag.Items.Count; i++)
            {
                AddAmount(values, bag.Items[i]?.Key, bag.Items[i]?.Amount ?? 0);
            }

            return values;
        }

        internal static Dictionary<string, int> SnapshotPointAmounts(PointBag bag)
        {
            var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (bag?.Items == null)
            {
                return values;
            }

            for (var i = 0; i < bag.Items.Count; i++)
            {
                AddAmount(values, bag.Items[i]?.Key, bag.Items[i]?.Amount ?? 0);
            }

            return values;
        }

        internal static List<string> SnapshotStringList(IEnumerable<string> values)
        {
            if (values == null)
            {
                return new List<string>();
            }

            var result = new List<string>();
            foreach (var value in values)
            {
                var normalized = TrimOrEmpty(value);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    result.Add(normalized);
                }
            }

            return result;
        }

        private static void AddAmount(Dictionary<string, int> values, string key, int amount)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            values[key.Trim()] = amount;
        }

        private static void ApplyFixedResourceFields(ResourceDto resources)
        {
            if (resources == null)
            {
                return;
            }

            resources.ResourceAmounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            resources.PointAmounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            resources.ResourceAmounts.TryGetValue(ResourceKeys.ResourceOre, out resources.Ore);
            resources.ResourceAmounts.TryGetValue(ResourceKeys.ResourceWood, out resources.Wood);
            resources.ResourceAmounts.TryGetValue(ResourceKeys.ResourceFood, out resources.Food);
            resources.PointAmounts.TryGetValue(IndustryOutputPointKey, out resources.IndustryOutput);
        }

        private static Dictionary<string, int> CloneAmounts(IReadOnlyDictionary<string, int> source)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (var pair in source)
            {
                AddAmount(result, pair.Key, pair.Value);
            }

            return result;
        }

        private static string TrimOrEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
