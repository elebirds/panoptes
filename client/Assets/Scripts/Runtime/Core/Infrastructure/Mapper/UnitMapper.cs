using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Infrastructure.Mapper
{
    public static class UnitMapper
    {
        public static UnitDto ToDto(UnitView view)
        {
            if (view == null)
            {
                return null;
            }

            return new UnitDto
            {
                Id = view.Id,
                Type = view.UnitType,
                Owner = view.Faction,
                X = view.Pos?.X ?? 0,
                Y = view.Pos?.Y ?? 0,
                Hp = view.Hp,
                MaxHp = view.MaxHp,
            };
        }
    }
}
