using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
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

    public sealed class CityCoreBuildingActionResolver
    {
        private const string CityCoreBuildingType = "city_core";

        private readonly GameStateStore _gameStateStore;
        private readonly StaticCatalogStore _staticCatalogStore;

        public CityCoreBuildingActionResolver(GameStateStore gameStateStore, StaticCatalogStore staticCatalogStore)
        {
            _gameStateStore = gameStateStore;
            _staticCatalogStore = staticCatalogStore;
        }

        public bool TryResolveCityCoreNodeId(UnitView unit, out string nodeId)
        {
            nodeId = string.Empty;
            var state = _gameStateStore?.Snapshot;
            if (!TryGetNode(unit, state, out var node) || !IsOwnedCityCoreBuildingNode(node, state))
            {
                return false;
            }

            nodeId = node.Id;
            return !string.IsNullOrWhiteSpace(nodeId);
        }

        public bool TryResolveRecipeBuilding(UnitView unit, out CityCoreRecipeBuildingContext context)
        {
            context = default;
            var state = _gameStateStore?.Snapshot;
            if (!TryGetNode(unit, state, out var node) || !IsOwnedBuildingNode(node, state))
            {
                return false;
            }

            var buildingType = NormalizeToken(node.BuildingType);
            if (string.IsNullOrWhiteSpace(buildingType) ||
                string.Equals(buildingType, CityCoreBuildingType, StringComparison.Ordinal) ||
                !CatalogBuildingHasRecipes(buildingType))
            {
                return false;
            }

            context = new CityCoreRecipeBuildingContext(node.Id, buildingType, ResolveAuthoritativeNodeOwner(node, state));
            return true;
        }

        public bool IsOwnedCityCoreBuildingProxy(UnitView unit)
        {
            var state = _gameStateStore?.Snapshot;
            return TryGetNode(unit, state, out var node) && IsOwnedCityCoreBuildingNode(node, state);
        }

        public bool TryResolveCityCoreNodeIdAnyOwner(UnitView unit, out string nodeId)
        {
            nodeId = string.Empty;
            var state = _gameStateStore?.Snapshot;
            if (!TryGetCityCoreNodeAnyOwner(unit, state, out var node))
            {
                return false;
            }

            nodeId = node.Id;
            return !string.IsNullOrWhiteSpace(nodeId);
        }

        public bool IsCityCoreBuildingProxy(UnitView unit)
        {
            var state = _gameStateStore?.Snapshot;
            return TryGetCityCoreNodeAnyOwner(unit, state, out _);
        }

        public bool IsOwnedRecipeBuildingProxy(UnitView unit)
        {
            var state = _gameStateStore?.Snapshot;
            if (!TryGetNode(unit, state, out var node) || !IsOwnedBuildingNode(node, state))
            {
                return false;
            }

            var buildingType = NormalizeToken(node.BuildingType);
            return !string.IsNullOrWhiteSpace(buildingType) &&
                   !string.Equals(buildingType, CityCoreBuildingType, StringComparison.Ordinal) &&
                   CatalogBuildingHasRecipes(buildingType);
        }

        public bool TryResolveDemolishableBuildingNodeId(UnitView unit, out string nodeId)
        {
            nodeId = string.Empty;
            var state = _gameStateStore?.Snapshot;
            if (!TryGetNode(unit, state, out var node) || !IsOwnedDemolishableBuildingNode(node, state))
            {
                return false;
            }

            nodeId = node.Id;
            return !string.IsNullOrWhiteSpace(nodeId);
        }

        public bool IsOwnedDemolishableBuildingProxy(UnitView unit)
        {
            var state = _gameStateStore?.Snapshot;
            return TryGetNode(unit, state, out var node) && IsOwnedDemolishableBuildingNode(node, state);
        }

        private static bool TryGetNode(UnitView unit, GameStateStoreState state, out NodeDto node)
        {
            node = null;
            var nodeId = unit != null ? unit.UnitId : string.Empty;
            var nodes = state?.Nodes;
            return !string.IsNullOrWhiteSpace(nodeId) &&
                   nodes != null &&
                   nodes.TryGetValue(nodeId, out node) &&
                   node != null;
        }

        private static bool TryGetCityCoreNodeAnyOwner(UnitView unit, GameStateStoreState state, out NodeDto node)
        {
            node = null;
            if (unit == null)
            {
                return false;
            }

            if (TryGetNode(unit, state, out node) && IsCityCore(node))
            {
                return true;
            }

            var nodes = state?.Nodes;
            if (nodes != null)
            {
                foreach (var candidate in nodes.Values)
                {
                    if (candidate == null || !IsCityCore(candidate))
                    {
                        continue;
                    }

                    if (candidate.Q == unit.GridPos.x && candidate.R == unit.GridPos.y)
                    {
                        node = candidate;
                        return true;
                    }
                }
            }

            if (string.Equals(NormalizeToken(unit.UnitType), CityCoreBuildingType, StringComparison.Ordinal))
            {
                node = new NodeDto
                {
                    Id = unit.UnitId,
                    BuildingType = CityCoreBuildingType,
                    IsCityCore = true
                };
                return !string.IsNullOrWhiteSpace(node.Id);
            }

            return false;
        }

        private bool IsOwnedCityCoreBuildingNode(NodeDto node, GameStateStoreState state)
        {
            if (!IsOwnedBuildingNode(node, state))
            {
                return false;
            }

            return node.IsCityCore || string.Equals(NormalizeToken(node.BuildingType), CityCoreBuildingType, StringComparison.Ordinal);
        }

        private bool IsOwnedBuildingNode(NodeDto node, GameStateStoreState state)
        {
            var localOwner = NormalizeToken(state?.MyPlayerId);
            if (node == null || string.IsNullOrWhiteSpace(localOwner))
            {
                return false;
            }

            var buildingType = NormalizeToken(node.BuildingType);
            if (string.IsNullOrWhiteSpace(buildingType))
            {
                return false;
            }

            return string.Equals(NormalizeToken(ResolveAuthoritativeNodeOwner(node, state)), localOwner, StringComparison.Ordinal);
        }

        private bool IsOwnedDemolishableBuildingNode(NodeDto node, GameStateStoreState state)
        {
            return node != null &&
                   IsOwnedBuildingNode(node, state) &&
                   !IsCityCore(node);
        }

        private bool CatalogBuildingHasRecipes(string buildingType)
        {
            var buildings = _staticCatalogStore?.Snapshot?.Buildings;
            if (buildings == null || !buildings.TryGetValue(buildingType, out var building) || building == null)
            {
                return false;
            }

            return HasEntries(building.RecipeIds) || !string.IsNullOrWhiteSpace(building.DefaultRecipeId);
        }

        private static bool HasEntries(System.Collections.Generic.IReadOnlyCollection<string> values)
        {
            return values != null && values.Count > 0;
        }

        private static string ResolveAuthoritativeNodeOwner(NodeDto node, GameStateStoreState state)
        {
            if (node == null)
            {
                return string.Empty;
            }

            var directOwner = ResolveDirectNodeOwner(node);
            if (!string.IsNullOrWhiteSpace(directOwner))
            {
                return directOwner;
            }

            var cityId = ResolveCityId(node);
            if (string.IsNullOrWhiteSpace(cityId))
            {
                return string.Empty;
            }

            return ResolveCityOwner(cityId, state?.Nodes);
        }

        private static string ResolveCityOwner(string cityId, IReadOnlyDictionary<string, NodeDto> nodes)
        {
            if (string.IsNullOrWhiteSpace(cityId) || nodes == null)
            {
                return string.Empty;
            }

            if (nodes.TryGetValue(cityId, out var cityNode))
            {
                var owner = ResolveDirectNodeOwner(cityNode);
                if (!string.IsNullOrWhiteSpace(owner))
                {
                    return owner;
                }
            }

            foreach (var candidate in nodes.Values)
            {
                if (!IsSameCity(candidate, cityId) || !IsCityCore(candidate))
                {
                    continue;
                }

                var owner = ResolveDirectNodeOwner(candidate);
                if (!string.IsNullOrWhiteSpace(owner))
                {
                    return owner;
                }
            }

            foreach (var candidate in nodes.Values)
            {
                if (!IsSameCity(candidate, cityId))
                {
                    continue;
                }

                var owner = ResolveDirectNodeOwner(candidate);
                if (!string.IsNullOrWhiteSpace(owner))
                {
                    return owner;
                }
            }

            return string.Empty;
        }

        private static string ResolveDirectNodeOwner(NodeDto node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(node.Owner) ? node.Owner.Trim() : node.TerritoryOwner?.Trim() ?? string.Empty;
        }

        private static string ResolveCityId(NodeDto node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(node.CityId) ? node.CityId.Trim() : node.ServiceCityId?.Trim() ?? string.Empty;
        }

        private static bool IsSameCity(NodeDto node, string cityId)
        {
            return node != null &&
                   !string.IsNullOrWhiteSpace(cityId) &&
                   string.Equals(ResolveCityId(node), cityId.Trim(), StringComparison.Ordinal);
        }

        private static bool IsCityCore(NodeDto node)
        {
            return node != null &&
                   (node.IsCityCore ||
                    string.Equals(NormalizeToken(node.BuildingType), CityCoreBuildingType, StringComparison.Ordinal));
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
