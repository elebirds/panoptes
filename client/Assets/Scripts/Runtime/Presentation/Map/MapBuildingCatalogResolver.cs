using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.Planning.Feedback;

namespace Panoptes.Presentation.Map
{
    public sealed class MapBuildingCatalogResolver
    {
        private StaticCatalogCache _staticCatalogCache;

        public string ResolveBackendBuildingType(string buildingType)
        {
            var normalized = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (TryGetBuildingConfig(normalized, out var entry, out var resolvedId) && entry != null)
            {
                return NormalizeToken(string.IsNullOrWhiteSpace(entry.id) ? resolvedId : entry.id);
            }

            return normalized;
        }

        public bool TryGetServerPlacementRule(
            string buildingType,
            out string placementRule,
            out string requiredResourceType)
        {
            placementRule = string.Empty;
            requiredResourceType = string.Empty;

            if (!TryGetBuildingConfig(buildingType, out var entry, out _) || entry == null)
            {
                return false;
            }

            placementRule = NormalizeToken(entry.placement_kind);
            requiredResourceType = NormalizeToken(entry.required_resource_type);
            return !string.IsNullOrEmpty(placementRule);
        }

        public bool TryGetBuildingConfig(
            string buildingType,
            out StaticCatalogCache.BuildingEntryJson entry,
            out string resolvedId)
        {
            entry = null;
            resolvedId = string.Empty;

            var key = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            var cache = ResolveStaticCatalogCache();
            if (cache == null)
            {
                return false;
            }

            if (cache.TryGetBuilding(key, out entry) && entry != null)
            {
                resolvedId = key;
                return true;
            }

            var aliases = GetBuildingAliasKeys(key);
            for (var i = 0; i < aliases.Length; i++)
            {
                var alias = aliases[i];
                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                if (cache.TryGetBuilding(alias, out entry) && entry != null)
                {
                    resolvedId = alias;
                    return true;
                }
            }

            return false;
        }

        private StaticCatalogCache ResolveStaticCatalogCache()
        {
            if (_staticCatalogCache != null)
            {
                return _staticCatalogCache;
            }

            _staticCatalogCache = StaticCatalogCache.Instance;
            if (_staticCatalogCache == null)
            {
                _staticCatalogCache = StaticCatalogCache.EnsureInstance();
            }

            return _staticCatalogCache;
        }

        private static string[] GetBuildingAliasKeys(string key)
        {
            switch (NormalizeToken(key))
            {
                case "lumberyard":
                    return new[] { "lumber" };
                case "lumber":
                    return new[] { "lumberyard" };
                case "engineer":
                    return new[] { "engineer_camp" };
                case "engineer_camp":
                    return new[] { "engineer" };
                case "archery":
                    return new[] { "barracks" };
                case "barracks":
                    return new[] { "archery" };
                case "blacksmith":
                case "backsmith":
                    return new[] { "workshop" };
                default:
                    return Array.Empty<string>();
            }
        }

        private static string NormalizeToken(string value)
        {
            return MapInputTokens.Normalize(value);
        }
    }
}
