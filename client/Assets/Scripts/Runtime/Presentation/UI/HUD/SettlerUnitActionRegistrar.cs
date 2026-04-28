using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    /// <summary>
    /// Registers the city-settlement action for settler-like units during planning.
    /// </summary>
    public sealed class SettlerUnitActionRegistrar : UnitInfoActionProviderBase
    {
        [SerializeField] private MapPlanningInputController mapPlanningInputController;
        [SerializeField] private string actionId = "settle_city";
        [SerializeField] private string actionLabel = "坐城";
        [SerializeField] private bool planningPhaseOnly = true;
        [SerializeField] private string[] supportedUnitTypes = { "settler", "pioneer", "expander", "engineer" };

        protected override void RegisterActions(UnitInfoActionRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            if (mapPlanningInputController == null)
            {
                mapPlanningInputController = MapPlanningInputController.Instance;
                if (mapPlanningInputController == null)
                {
                    mapPlanningInputController = SceneObjectFinder.FindFirstSceneObject<MapPlanningInputController>();
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

            if (mapPlanningInputController == null)
            {
                mapPlanningInputController = MapPlanningInputController.Instance;
                if (mapPlanningInputController == null)
                {
                    mapPlanningInputController = SceneObjectFinder.FindFirstSceneObject<MapPlanningInputController>();
                }
            }

            if (mapPlanningInputController == null)
            {
                Debug.LogWarning("[SettlerUnitActionRegistrar] MapPlanningInputController missing, cannot send expand request.");
                return;
            }

            mapPlanningInputController.RequestExpandTerritory(unit.UnitId);
        }

        private bool IsSupportedSettlerUnit(UnitView unit)
        {
            if (unit == null || supportedUnitTypes == null || supportedUnitTypes.Length == 0)
            {
                return false;
            }

            if (planningPhaseOnly)
            {
                var cache = GameStateCache.Instance;
                if (cache == null || !IsPlanningPhase(cache.Phase))
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

        private static bool IsPlanningPhase(string phase)
        {
            var normalized = NormalizeToken(phase);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            return string.Equals(normalized, "planning", StringComparison.Ordinal);
        }
    }
}
