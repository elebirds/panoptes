using System;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Planning.Feedback;

namespace Panoptes.Presentation.Map
{
    public sealed class MapBuildingCatalogResolver
    {
        private StaticCatalogStore _staticCatalogStore;

        public void Configure(StaticCatalogStore staticCatalogStore)
        {
            _staticCatalogStore = staticCatalogStore;
        }

        public string ResolveBackendBuildingType(string buildingType)
        {
            var normalized = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (TryGetBuildingConfig(normalized, out var entry, out var resolvedId) && entry != null)
            {
                return NormalizeToken(string.IsNullOrWhiteSpace(entry.Id) ? resolvedId : entry.Id);
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

            placementRule = NormalizeToken(entry.PlacementKind);
            requiredResourceType = NormalizeToken(entry.RequiredResourceType);
            return !string.IsNullOrEmpty(placementRule);
        }

        public bool TryGetBuildingConfig(
            string buildingType,
            out CatalogBuildingDto entry,
            out string resolvedId)
        {
            entry = null;
            resolvedId = string.Empty;

            var key = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            var buildings = _staticCatalogStore?.Snapshot?.Buildings;
            if (buildings == null)
            {
                return false;
            }

            if (buildings.TryGetValue(key, out entry) && entry != null)
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

                if (buildings.TryGetValue(alias, out entry) && entry != null)
                {
                    resolvedId = alias;
                    return true;
                }
            }

            return false;
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
