using System;
using Panoptes.Core.Application.Cache;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    internal static class MapRenderTokens
    {
        public static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        public static int ResolveBuildingMaxHp(string buildingType, int fallbackHp)
        {
            var normalized = Normalize(buildingType);
            if (string.IsNullOrEmpty(normalized))
            {
                return 0;
            }

            var catalog = StaticCatalogCache.EnsureInstance();
            if (catalog != null)
            {
                if (string.Equals(normalized, "city_core", StringComparison.OrdinalIgnoreCase) &&
                    catalog.Rules != null &&
                    catalog.Rules.city_core_max_hp > 0)
                {
                    return catalog.Rules.city_core_max_hp;
                }

                if (catalog.TryGetBuilding(normalized, out var buildingEntry) &&
                    buildingEntry != null &&
                    buildingEntry.max_hp > 0)
                {
                    return buildingEntry.max_hp;
                }
            }

            return Mathf.Max(0, fallbackHp);
        }
    }
}
