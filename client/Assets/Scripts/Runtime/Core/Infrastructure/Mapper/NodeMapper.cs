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
                // TODO: Keep Type for backward compatibility with older consumers. 这个地方后续需要重修，现在先这样搞兼容。
                Type = view.Terrain,
                Owner = view.Owner,
                TerritoryOwner = string.Empty,
                BuildingType = view.BuildingType,
                BuildingHp = view.BuildingHp,
                BuildingMaxHp = view.BuildingHp,
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
