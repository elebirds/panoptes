using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    internal static class MapRenderTokens
    {
        public static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        public static int ResolveBuildingMaxHp(
            string buildingType,
            int fallbackHp,
            IReadOnlyDictionary<string, CatalogBuildingDto> buildings = null)
        {
            var normalized = Normalize(buildingType);
            if (string.IsNullOrEmpty(normalized))
            {
                return 0;
            }

            if (buildings != null &&
                buildings.TryGetValue(normalized, out var entry) &&
                entry != null &&
                entry.MaxHp > 0)
            {
                return entry.MaxHp;
            }

            return Mathf.Max(0, fallbackHp);
        }
    }
}
