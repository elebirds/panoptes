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
            var resources = new ResourceDto();
            if (bag?.Items != null)
            {
                for (var i = 0; i < bag.Items.Count; i++)
                {
                    var item = bag.Items[i];
                    if (item == null)
                    {
                        continue;
                    }

                    switch (item.Key)
                    {
                        case ResourceKeys.ResourceOre:
                            resources.Ore = item.Amount;
                            break;
                        case ResourceKeys.ResourceWood:
                            resources.Wood = item.Amount;
                            break;
                        case ResourceKeys.ResourceFood:
                            resources.Food = item.Amount;
                            break;
                    }
                }
            }

            resources.IndustryOutput = FindPointAmount(points, IndustryOutputPointKey);
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
                IndustryOutput = source.IndustryOutput
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

        private static int FindPointAmount(PointBag bag, string key)
        {
            if (bag?.Items == null || string.IsNullOrWhiteSpace(key))
            {
                return 0;
            }

            for (var i = 0; i < bag.Items.Count; i++)
            {
                var item = bag.Items[i];
                if (item == null)
                {
                    continue;
                }

                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Amount;
                }
            }

            return 0;
        }

        private static string TrimOrEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
