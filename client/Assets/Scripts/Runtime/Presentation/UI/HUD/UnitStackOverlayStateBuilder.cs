using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class UnitStackOverlayStateBuilder
    {
        private readonly Dictionary<string, int> _typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public UnitStackOverlayState Build(IReadOnlyList<UnitView> units, StaticCatalogState catalog)
        {
            _typeCounts.Clear();
            if (units == null || units.Count == 0)
            {
                return default;
            }

            var count = 0;
            var totalHp = 0;
            var totalMaxHp = 0;
            var primaryType = string.Empty;
            var primaryTypeCount = 0;

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                count++;
                var maxHp = Mathf.Max(1, unit.MaxHitPoints > 0 ? unit.MaxHitPoints : unit.HitPoints);
                totalHp += Mathf.Clamp(unit.HitPoints, 0, maxHp);
                totalMaxHp += maxHp;

                var unitType = Normalize(unit.UnitType);
                if (string.IsNullOrEmpty(unitType))
                {
                    continue;
                }

                _typeCounts.TryGetValue(unitType, out var typeCount);
                typeCount++;
                _typeCounts[unitType] = typeCount;
                if (typeCount > primaryTypeCount)
                {
                    primaryType = unitType;
                    primaryTypeCount = typeCount;
                }
            }

            if (count <= 0 || totalMaxHp <= 0)
            {
                return default;
            }

            var isMixed = _typeCounts.Count > 1;
            var entry = ResolveCatalogUnit(primaryType, catalog);
            var displayName = ResolveDisplayName(primaryType, entry, isMixed);
            var iconKey = entry != null && !string.IsNullOrWhiteSpace(entry.IconKey)
                ? entry.IconKey.Trim()
                : primaryType;
            var fallbackText = ResolveFallbackText(primaryType, displayName, isMixed);

            return new UnitStackOverlayState(
                count,
                displayName,
                fallbackText,
                iconKey,
                isMixed,
                totalHp,
                totalMaxHp,
                primaryType);
        }

        private static CatalogUnitDto ResolveCatalogUnit(string unitType, StaticCatalogState catalog)
        {
            if (string.IsNullOrEmpty(unitType) || catalog?.Units == null)
            {
                return null;
            }

            return catalog.Units.TryGetValue(unitType, out var entry) ? entry : null;
        }

        private static string ResolveDisplayName(string unitType, CatalogUnitDto entry, bool isMixed)
        {
            if (isMixed)
            {
                return "Mixed";
            }

            if (entry != null && !string.IsNullOrWhiteSpace(entry.Name))
            {
                return entry.Name.Trim();
            }

            return string.IsNullOrEmpty(unitType) ? "Unit" : unitType.Replace('_', ' ');
        }

        private static string ResolveFallbackText(string unitType, string displayName, bool isMixed)
        {
            if (isMixed)
            {
                return "M";
            }

            var source = !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : unitType;
            if (string.IsNullOrWhiteSpace(source))
            {
                return "?";
            }

            return source.Substring(0, 1).ToUpperInvariant();
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }
}
