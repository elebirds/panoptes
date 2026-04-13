using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    /// <summary>
    /// Registers the territory-expansion action for base vehicle unit types.
    /// </summary>
    public sealed class SettlerUnitActionRegistrar : UnitInfoActionProviderBase
    {
        [SerializeField] private MapInputHandler mapInputHandler;
        [SerializeField] private string actionId = "expand_territory";
        [SerializeField] private string actionLabel = "Deploy";
        [SerializeField] private bool combatPhaseOnly = true;
        [SerializeField] private string[] supportedUnitTypes = { "settler", "pioneer", "expander", "engineer" };

        protected override void RegisterActions(UnitInfoActionRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            if (mapInputHandler == null)
            {
                mapInputHandler = MapInputHandler.Instance;
                if (mapInputHandler == null)
                {
                    mapInputHandler = UnityEngine.Object.FindAnyObjectByType<MapInputHandler>();
                }
            }

            registry.RegisterAction(
                actionId,
                OnExpandClicked,
                string.IsNullOrWhiteSpace(actionLabel) ? actionId : actionLabel,
                IsSupportedSettlerUnit);
        }

        private void OnExpandClicked(UnitView unit)
        {
            if (unit == null)
            {
                return;
            }

            if (mapInputHandler == null)
            {
                mapInputHandler = MapInputHandler.Instance;
                if (mapInputHandler == null)
                {
                    mapInputHandler = UnityEngine.Object.FindAnyObjectByType<MapInputHandler>();
                }
            }

            if (mapInputHandler == null)
            {
                Debug.LogWarning("[SettlerUnitActionRegistrar] MapInputHandler missing, cannot send expand request.");
                return;
            }

            mapInputHandler.RequestExpandTerritory(unit.UnitId);
        }

        private bool IsSupportedSettlerUnit(UnitView unit)
        {
            if (unit == null || supportedUnitTypes == null || supportedUnitTypes.Length == 0)
            {
                return false;
            }

            if (combatPhaseOnly)
            {
                var cache = GameStateCache.Instance;
                if (cache == null || !IsCombatPhase(cache.Phase))
                {
                    return false;
                }
            }

            var unitType = NormalizeToken(unit.UnitType);
            if (string.IsNullOrEmpty(unitType))
            {
                return false;
            }

            for (var i = 0; i < supportedUnitTypes.Length; i++)
            {
                if (string.Equals(unitType, NormalizeToken(supportedUnitTypes[i]), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static bool IsCombatPhase(string phase)
        {
            var normalized = NormalizeToken(phase);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            return string.Equals(normalized, "combat", StringComparison.Ordinal)
                   || normalized.Contains("combat");
        }
    }
}
