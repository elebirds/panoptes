using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    public readonly struct UnitInfoHpState
    {
        public readonly int Hp;
        public readonly int MaxHp;
        public readonly int ClampedHp;

        public UnitInfoHpState(int hp, int maxHp)
        {
            MaxHp = Mathf.Max(1, maxHp);
            Hp = hp;
            ClampedHp = Mathf.Clamp(hp, 0, MaxHp);
        }

        public string DisplayText => $"{ClampedHp}/{MaxHp}";
    }

    public static class UnitInfoHpStateResolver
    {
        public static UnitInfoHpState Resolve(UnitView currentUnit, GameStateCache cache)
        {
            if (currentUnit == null)
            {
                return new UnitInfoHpState(0, 1);
            }

            return Resolve(
                currentUnit.HitPoints,
                currentUnit.MaxHitPoints,
                currentUnit.UnitId,
                currentUnit.UnitType,
                cache);
        }

        public static UnitInfoHpState Resolve(
            int fallbackHp,
            int fallbackMaxHp,
            string unitId,
            string unitType,
            GameStateCache cache)
        {
            var hp = fallbackHp;
            var maxHp = Mathf.Max(1, fallbackMaxHp);
            if (cache == null)
            {
                return new UnitInfoHpState(hp, maxHp);
            }

            var cachedUnit = cache.GetUnit(unitId);
            if (cachedUnit != null)
            {
                return new UnitInfoHpState(cachedUnit.Hp, Mathf.Max(1, cachedUnit.MaxHp));
            }

            var cachedNode = cache.GetNode(unitId);
            if (cachedNode == null || (string.IsNullOrWhiteSpace(cachedNode.BuildingType) && !cachedNode.IsResourcePoint))
            {
                return new UnitInfoHpState(hp, maxHp);
            }

            hp = cachedNode.BuildingHp > 0 ? cachedNode.BuildingHp : Mathf.Max(1, hp);
            maxHp = ResolveBuildingMaxHp(cachedNode, unitType, hp, StaticCatalogCache.EnsureInstance());
            return new UnitInfoHpState(hp, maxHp);
        }

        public static int ResolveBuildingMaxHp(
            NodeDto node,
            string fallbackType,
            int hp,
            StaticCatalogCache catalog = null)
        {
            if (node == null || node.IsResourcePoint)
            {
                return Mathf.Max(1, hp);
            }

            var maxHp = node.BuildingMaxHp;
            if (maxHp <= 0)
            {
                var buildingType = NormalizeToken(!string.IsNullOrWhiteSpace(node.BuildingType) ? node.BuildingType : fallbackType);
                if (catalog != null)
                {
                    if (string.Equals(buildingType, "city_core", StringComparison.OrdinalIgnoreCase) &&
                        catalog.Rules != null &&
                        catalog.Rules.city_core_max_hp > 0)
                    {
                        maxHp = catalog.Rules.city_core_max_hp;
                    }
                    else if (catalog.TryGetBuilding(buildingType, out var buildingEntry) && buildingEntry != null)
                    {
                        maxHp = buildingEntry.max_hp;
                    }
                }
            }

            return Mathf.Max(1, Mathf.Max(maxHp, hp));
        }

        private static string NormalizeToken(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
