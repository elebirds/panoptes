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
