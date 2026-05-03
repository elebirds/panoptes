using Panoptes.Core.Domain;
using Panoptes.Core.Application.Cache;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Infrastructure.Mapper
{
    public static class UnitMapper
    {
        public static UnitDto ToDto(UnitView view, StaticCatalogCache catalog = null)
        {
            if (view == null)
            {
                return null;
            }

            var maxHp = view.MaxHp;
            if (catalog != null && catalog.TryGetUnit(view.UnitType, out var unitEntry) && unitEntry != null)
            {
                maxHp = unitEntry.max_hp > 0 ? unitEntry.max_hp : maxHp;
            }

            if (maxHp <= 0)
            {
                maxHp = view.Hp;
            }

            return new UnitDto
            {
                Id = view.Id,
                Type = view.UnitType,
                Owner = view.Faction,
                Q = view.Pos?.Q ?? 0,
                R = view.Pos?.R ?? 0,
                Hp = view.Hp,
                MaxHp = maxHp,
            };
        }
    }
}
