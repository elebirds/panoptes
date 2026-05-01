using System;
using Panoptes.Core.Application.Cache;

namespace Panoptes.Presentation.UI.HUD
{
    public readonly struct UnitInfoDirectOrderState
    {
        public readonly bool CanMove;
        public readonly bool IsMilitaryUnit;
        public readonly bool CanAttack;
        public readonly bool CanCharge;

        public UnitInfoDirectOrderState(bool canMove, bool isMilitaryUnit, bool canAttack, bool canCharge)
        {
            CanMove = canMove;
            IsMilitaryUnit = isMilitaryUnit;
            CanAttack = canAttack;
            CanCharge = canCharge;
        }
    }

    public static class UnitInfoDirectOrderStateResolver
    {
        public static UnitInfoDirectOrderState Resolve(string unitType)
        {
            var normalizedType = NormalizeToken(unitType);
            if (string.IsNullOrWhiteSpace(normalizedType) || IsResourceType(normalizedType))
            {
                return default;
            }

            var catalog = StaticCatalogCache.EnsureInstance();
            if (catalog == null || catalog.TryGetBuilding(normalizedType, out _))
            {
                return default;
            }

            return catalog.TryGetUnit(normalizedType, out var unitEntry)
                ? Resolve(unitEntry, isBuildingOrResourceType: false)
                : default;
        }

        public static UnitInfoDirectOrderState Resolve(
            StaticCatalogCache.UnitEntryJson unitEntry,
            bool isBuildingOrResourceType)
        {
            if (unitEntry == null || isBuildingOrResourceType)
            {
                return default;
            }

            var isMilitaryUnit = !HasTag(unitEntry, "civilian");
            return new UnitInfoDirectOrderState(
                canMove: true,
                isMilitaryUnit: isMilitaryUnit,
                canAttack: isMilitaryUnit,
                canCharge: isMilitaryUnit && HasTag(unitEntry, "charge"));
        }

        private static bool IsResourceType(string normalizedType)
        {
            return string.Equals(normalizedType, "resource_point", StringComparison.Ordinal) ||
                   normalizedType.StartsWith("resource_", StringComparison.Ordinal);
        }

        private static bool HasTag(StaticCatalogCache.UnitEntryJson entry, string tag)
        {
            if (entry?.tags == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            for (var i = 0; i < entry.tags.Length; i++)
            {
                if (string.Equals(entry.tags[i], tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeToken(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
