using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    internal sealed class MapCameraContextBuilder
    {
        public bool TryBuild(
            IReadOnlyDictionary<string, NodeDto> nodeStates,
            IReadOnlyDictionary<string, NodeView> tileViews,
            IReadOnlyDictionary<string, UnitView> unitViews,
            float tileSize,
            float plainElevation,
            Func<string, bool> isBaseVehicleUnitType,
            out MapCameraContext context)
        {
            context = default;
            if (!TryBuildWorldRect(nodeStates, tileSize, out var worldRect))
            {
                return false;
            }

            var focusPoint = ResolvePreferredFocusPoint(
                worldRect,
                nodeStates,
                tileViews,
                unitViews,
                plainElevation,
                isBaseVehicleUnitType);
            context = new MapCameraContext(worldRect, plainElevation, focusPoint);
            return context.IsValid;
        }

        private static Vector3 ResolvePreferredFocusPoint(
            Rect worldRect,
            IReadOnlyDictionary<string, NodeDto> nodeStates,
            IReadOnlyDictionary<string, NodeView> tileViews,
            IReadOnlyDictionary<string, UnitView> unitViews,
            float plainElevation,
            Func<string, bool> isBaseVehicleUnitType)
        {
            if (TryGetOwnedCityCoreFocusPoint(nodeStates, tileViews, plainElevation, out var cityCoreFocus))
            {
                return cityCoreFocus;
            }

            if (TryGetOwnedBaseVehicleFocusPoint(unitViews, plainElevation, isBaseVehicleUnitType, out var baseVehicleFocus))
            {
                return baseVehicleFocus;
            }

            return new Vector3(worldRect.center.x, plainElevation, worldRect.center.y);
        }

        private static bool TryGetOwnedBaseVehicleFocusPoint(
            IReadOnlyDictionary<string, UnitView> unitViews,
            float plainElevation,
            Func<string, bool> isBaseVehicleUnitType,
            out Vector3 focusPoint)
        {
            focusPoint = default;
            var cache = GameStateCache.Instance;
            if (cache == null || string.IsNullOrWhiteSpace(cache.MyPlayerID) || unitViews == null)
            {
                return false;
            }

            var myPlayerId = MapRenderTokens.Normalize(cache.MyPlayerID);
            foreach (var pair in unitViews)
            {
                var unitView = pair.Value;
                if (unitView == null)
                {
                    continue;
                }

                if (!string.Equals(MapRenderTokens.Normalize(unitView.Faction), myPlayerId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (isBaseVehicleUnitType == null || !isBaseVehicleUnitType(unitView.UnitType))
                {
                    continue;
                }

                focusPoint = unitView.transform.position;
                focusPoint.y = plainElevation;
                return true;
            }

            return false;
        }

        private static bool TryGetOwnedCityCoreFocusPoint(
            IReadOnlyDictionary<string, NodeDto> nodeStates,
            IReadOnlyDictionary<string, NodeView> tileViews,
            float plainElevation,
            out Vector3 focusPoint)
        {
            focusPoint = default;
            var cache = GameStateCache.Instance;
            if (cache == null || string.IsNullOrWhiteSpace(cache.MyPlayerID) || nodeStates == null || tileViews == null)
            {
                return false;
            }

            var myPlayerId = MapRenderTokens.Normalize(cache.MyPlayerID);
            foreach (var pair in nodeStates)
            {
                var node = pair.Value;
                if (node == null || !string.Equals(MapRenderTokens.Normalize(node.BuildingType), "city_core", StringComparison.Ordinal))
                {
                    continue;
                }

                var ownerId = MapRenderTokens.Normalize(string.IsNullOrWhiteSpace(node.TerritoryOwner) ? node.Owner : node.TerritoryOwner);
                if (!string.Equals(ownerId, myPlayerId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (tileViews.TryGetValue(node.Id, out var nodeView) && nodeView != null)
                {
                    focusPoint = nodeView.BuildingInstance != null
                        ? nodeView.BuildingInstance.transform.position
                        : nodeView.transform.position;
                    focusPoint.y = plainElevation;
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildWorldRect(
            IReadOnlyDictionary<string, NodeDto> nodeStates,
            float tileSize,
            out Rect worldRect)
        {
            worldRect = default;
            if (nodeStates == null || nodeStates.Count == 0)
            {
                return false;
            }

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minZ = float.PositiveInfinity;
            var maxZ = float.NegativeInfinity;
            var halfWidth = Mathf.Sqrt(3f) * tileSize * 0.5f;
            var halfHeight = tileSize;
            var hasNode = false;

            foreach (var pair in nodeStates)
            {
                var node = pair.Value;
                if (node == null)
                {
                    continue;
                }

                var world = HexGrid.AxialToWorld(node.Q, node.R, tileSize);
                minX = Mathf.Min(minX, world.x - halfWidth);
                maxX = Mathf.Max(maxX, world.x + halfWidth);
                minZ = Mathf.Min(minZ, world.z - halfHeight);
                maxZ = Mathf.Max(maxZ, world.z + halfHeight);
                hasNode = true;
            }

            if (!hasNode)
            {
                return false;
            }

            worldRect = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            return worldRect.width > 0f && worldRect.height > 0f;
        }
    }
}
