using System;
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
                X = view.Pos?.X ?? 0,
                Y = view.Pos?.Y ?? 0,
                Type = view.Terrain,
                Owner = view.ControllerPlayerId,
                TerritoryOwner = string.IsNullOrWhiteSpace(view.TerritoryOwnerPlayerId) ? view.ControllerPlayerId : view.TerritoryOwnerPlayerId,
                BuildingType = view.BuildingTypeId,
                BuildingHp = view.BuildingHp,
                BuildingMaxHp = view.BuildingHp,
                BuildingStatus = view.BuildingStatus,
                CityId = view.CityId,
                ServiceCityId = view.ServiceCityId,
                TakeoverProgress = view.TakeoverProgress,
                TakeoverRequired = view.TakeoverRequired,
                IsCityCore = view.IsCityCore,
                IsVisible = true,
                HasRoad = view.HasRoad,
                Terrain = view.Terrain,
                IsResourcePoint = view.IsResourcePoint,
                ResourceType = view.ResourceType,
                IsSafeZone = view.IsSafeZone,
            };
        }
    }
}
