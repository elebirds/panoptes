using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;

namespace Panoptes.Presentation.UI.HUD
{
    public readonly struct CityCoreRecipeBuildingContext
    {
        public CityCoreRecipeBuildingContext(string nodeId, string buildingTypeId, string ownerId)
        {
            NodeId = nodeId ?? string.Empty;
            BuildingTypeId = buildingTypeId ?? string.Empty;
            OwnerId = ownerId ?? string.Empty;
        }

        public string NodeId { get; }
        public string BuildingTypeId { get; }
        public string OwnerId { get; }
    }

    public static class CityCoreBuildingActionResolver
    {
        private const string CityCoreBuildingType = "city_core";

        public static bool TryResolveCityCoreNodeId(UnitView unit, out string nodeId)
        {
            nodeId = string.Empty;
            if (!IsOwnedCityCoreBuildingProxy(unit))
            {
                return false;
            }

            nodeId = unit.UnitId;
            return !string.IsNullOrWhiteSpace(nodeId);
        }

        public static bool TryResolveRecipeBuilding(UnitView unit, out CityCoreRecipeBuildingContext context)
        {
            context = default;
            if (!IsOwnedRecipeBuildingProxy(unit))
            {
                return false;
            }

            var cache = GameStateCache.Instance;
            if (cache == null)
            {
                return false;
            }

            var nodeId = unit.UnitId;
            if (!cache.TryGetBuilding(nodeId, out var building) || building == null)
            {
                return false;
            }

            var buildingType = NormalizeToken(building.BuildingTypeId);
            var ownerId = ResolveAuthoritativeBuildingOwner(cache, building);
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(buildingType))
            {
                return false;
            }

            context = new CityCoreRecipeBuildingContext(nodeId, buildingType, ownerId);
            return true;
        }

        public static bool IsOwnedCityCoreBuildingProxy(UnitView unit)
        {
            if (unit == null)
            {
                return false;
            }

            var cache = GameStateCache.Instance;
            if (cache == null)
            {
                return false;
            }

            var localOwner = NormalizeToken(cache.MyPlayerID);
            if (string.IsNullOrWhiteSpace(localOwner))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(unit.UnitId))
            {
                return false;
            }

            if (!cache.TryGetBuilding(unit.UnitId, out var building) || building == null)
            {
                return false;
            }

            var buildingType = NormalizeToken(building.BuildingTypeId);
            if (!building.IsCityCore && !IsCityCoreBuildingType(buildingType))
            {
                return false;
            }

            var owner = NormalizeToken(ResolveAuthoritativeBuildingOwner(cache, building));
            return string.Equals(owner, localOwner, StringComparison.Ordinal);
        }

        public static bool IsOwnedRecipeBuildingProxy(UnitView unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.UnitId))
            {
                return false;
            }

            var cache = GameStateCache.Instance;
            if (cache == null)
            {
                return false;
            }

            if (!cache.TryGetBuilding(unit.UnitId, out var building) || building == null)
            {
                return false;
            }

            var localOwner = NormalizeToken(cache.MyPlayerID);
            if (string.IsNullOrWhiteSpace(localOwner))
            {
                return false;
            }

            var owner = NormalizeToken(ResolveAuthoritativeBuildingOwner(cache, building));
            if (!string.Equals(owner, localOwner, StringComparison.Ordinal))
            {
                return false;
            }

            var buildingType = NormalizeToken(building.BuildingTypeId);
            if (string.IsNullOrWhiteSpace(buildingType))
            {
                return false;
            }

            var catalog = StaticCatalogCache.EnsureInstance();
            if (catalog == null)
            {
                return false;
            }

            if (!catalog.TryGetBuilding(buildingType, out var catalogBuilding) || catalogBuilding == null)
            {
                return false;
            }

            var recipeIds = catalogBuilding.recipe_ids;
            var hasRecipeIds = recipeIds != null && recipeIds.Length > 0;
            var hasDefaultRecipe = !string.IsNullOrWhiteSpace(catalogBuilding.default_recipe_id);
            return hasRecipeIds || hasDefaultRecipe;
        }

        private static string ResolveAuthoritativeBuildingOwner(GameStateCache cache, BuildingDto building)
        {
            if (building == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(building.OwnerId))
            {
                return building.OwnerId.Trim();
            }

            var cityId = !string.IsNullOrWhiteSpace(building.CityId)
                ? building.CityId
                : building.ServiceCityId;
            if (cache != null &&
                !string.IsNullOrWhiteSpace(cityId) &&
                cache.TryGetCity(cityId, out var city) &&
                city != null &&
                !string.IsNullOrWhiteSpace(city.OwnerId))
            {
                return city.OwnerId.Trim();
            }

            return string.Empty;
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static bool IsCityCoreBuildingType(string value)
        {
            var normalized = NormalizeToken(value);
            return string.Equals(normalized, CityCoreBuildingType, StringComparison.Ordinal);
        }
    }
}
