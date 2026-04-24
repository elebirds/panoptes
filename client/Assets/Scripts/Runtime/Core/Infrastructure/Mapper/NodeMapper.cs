using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Infrastructure.Mapper
{
    public static class NodeMapper
    {
        public static NodeDto ToDto(NodeView view)
        {
            if (view == null)
            {
                return null;
            }

            return new NodeDto
            {
                Id = view.Id,
                Q = view.Pos?.Q ?? 0,
                R = view.Pos?.R ?? 0,
                Type = view.Terrain,
                Owner = view.ControllerPlayerId,
                TerritoryOwner = string.IsNullOrWhiteSpace(view.TerritoryOwnerPlayerId) ? view.ControllerPlayerId : view.TerritoryOwnerPlayerId,
                BuildingType = view.BuildingTypeId,
                BuildingHp = view.BuildingHp,
                BuildingMaxHp = ResolveBuildingMaxHp(view),
                BuildingStatus = view.BuildingStatus,
                OperationSelectedRecipeId = view.Operation != null ? view.Operation.SelectedRecipeId : string.Empty,
                OperationCurrentProgress = view.Operation != null ? view.Operation.CurrentProgress : 0,
                OperationRequiredProgress = view.Operation != null ? view.Operation.RequiredProgress : 0,
                OperationBaseProgress = view.Operation != null ? view.Operation.BaseProgress : 0,
                OperationBlockedReason = view.Operation != null ? view.Operation.BlockedReason : string.Empty,
                OperationBlockedMessage = view.Operation != null ? view.Operation.BlockedMessage : string.Empty,
                CityId = view.CityId,
                ServiceCityId = view.ServiceCityId,
                TakeoverProgress = view.TakeoverProgress,
                TakeoverRequired = view.TakeoverRequired,
                IsCityCore = view.IsCityCore,
                IsVisible = view.IsCurrentlyVisible,
                IsMemory = view.IsMemory,
                LastObservedTurn = view.LastObservedTurn,
                HasRoad = view.HasRoad,
                Terrain = view.Terrain,
                IsResourcePoint = view.IsResourcePoint,
                ResourceType = view.ResourceType,
                IsSafeZone = view.IsSafeZone,
            };
        }

        private static int ResolveBuildingMaxHp(NodeView view)
        {
            if (view == null)
            {
                return 0;
            }

            var buildingType = (view.BuildingTypeId ?? string.Empty).Trim();
            if (buildingType.Length == 0)
            {
                return 0;
            }

            var cache = StaticCatalogCache.Instance;
            if (cache == null)
            {
                return 0;
            }

            if (string.Equals(buildingType, "city_core", StringComparison.OrdinalIgnoreCase))
            {
                var cityCoreMaxHp = cache.Rules != null ? cache.Rules.city_core_max_hp : 0;
                if (cityCoreMaxHp > 0)
                {
                    return cityCoreMaxHp;
                }
            }

            if (!cache.TryGetBuilding(buildingType, out var buildingEntry) || buildingEntry == null)
            {
                return 0;
            }

            var maxHp = TryReadIntMember(buildingEntry, "max_hp");
            if (maxHp <= 0)
            {
                maxHp = TryReadIntMember(buildingEntry, "maxHp");
            }

            return maxHp > 0 ? maxHp : 0;
        }

        private static int TryReadIntMember(object source, string memberName)
        {
            if (source == null || string.IsNullOrWhiteSpace(memberName))
            {
                return 0;
            }

            var type = source.GetType();
            var field = type.GetField(memberName);
            if (field != null && field.FieldType == typeof(int))
            {
                return (int)field.GetValue(source);
            }

            var property = type.GetProperty(memberName);
            if (property != null && property.CanRead && property.PropertyType == typeof(int))
            {
                return (int)property.GetValue(source, null);
            }

            return 0;
        }
    }
}
