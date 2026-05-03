/*************************************************
 * Project: Panoptes
 * File: BuildPlacementInputMode.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Planning input guard for build placement entry; does not validate gameplay legality.
 *************************************************/

using System;
using Panoptes.Presentation.Planning.Feedback;

namespace Panoptes.Presentation.Planning.Input.Modes
{
    /// <summary>
    /// Resolves presentation-only build placement entry guards before a server-backed build preview is requested.
    /// </summary>
    public static class BuildPlacementInputMode
    {
        public static bool IsManualPlacementBlocked(
            string buildingType,
            bool disallowCityCorePlacement,
            string[] blockedBuildingTypes)
        {
            var normalized = MapInputTokens.Normalize(buildingType);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (disallowCityCorePlacement && string.Equals(normalized, "city_core", StringComparison.Ordinal))
            {
                return true;
            }

            if (blockedBuildingTypes == null)
            {
                return false;
            }

            for (var i = 0; i < blockedBuildingTypes.Length; i++)
            {
                if (string.Equals(MapInputTokens.Normalize(blockedBuildingTypes[i]), normalized, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
