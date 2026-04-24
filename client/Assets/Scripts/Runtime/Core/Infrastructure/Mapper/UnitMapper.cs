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
                Q = view.Pos?.Q ?? 0,
                R = view.Pos?.R ?? 0,
                Hp = view.Hp,
                MaxHp = view.MaxHp,
            };
        }
    }
}
