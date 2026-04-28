using System;

namespace Panoptes.Presentation.Map
{
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
