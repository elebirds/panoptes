using System;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    /// <summary>
    /// Registers the city-settlement action for settler-like units during planning.
    /// </summary>
    public sealed class SettlerUnitActionRegistrar : UnitInfoActionProviderBase
    {
        [SerializeField] private string actionId = "expand_territory";
        [SerializeField] private string legacyActionId = "settle_city";
        [SerializeField] private string actionLabel = "建立城堡";
        [SerializeField] private bool planningPhaseOnly = true;
        [SerializeField] private string[] supportedUnitTypes = { "settler", "pioneer", "expander", "engineer" };

        private GameStateStore _gameStateStore;
        private PlanningIntentService _planningIntentService;

        [Inject]
        private void Construct(GameStateStore gameStateStore, PlanningIntentService planningIntentService)
        {
            _gameStateStore = gameStateStore;
            _planningIntentService = planningIntentService;
        }

        protected override void RegisterActions(UnitInfoActionRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            registry.RegisterAction(
                actionId,
                OnExpandClicked,
                string.IsNullOrWhiteSpace(actionLabel) ? actionId : actionLabel,
                IsSupportedSettlerUnitType);

            if (!string.IsNullOrWhiteSpace(legacyActionId) &&
                !string.Equals(actionId, legacyActionId, StringComparison.OrdinalIgnoreCase))
            {
                registry.RegisterAction(
                    legacyActionId,
                    OnExpandClicked,
                    string.IsNullOrWhiteSpace(actionLabel) ? legacyActionId : actionLabel,
                    IsSupportedSettlerUnitType);
            }
        }

        private void OnExpandClicked(UnitView unit)
        {
            if (unit == null)
            {
                return;
            }

            if (!IsSupportedSettlerUnit(unit))
            {
                return;
            }

            if (_planningIntentService == null)
            {
                PanoptesLog.Warning("[SettlerUnitActionRegistrar] PlanningIntentService missing, cannot send expand request.");
                return;
            }

            if (!TryResolveCenterNodeId(unit, out var centerNodeId))
            {
                PanoptesLog.Warning("[SettlerUnitActionRegistrar] Could not resolve center node from GameStateStore; sending empty center for server-side unit-position resolution.");
                centerNodeId = string.Empty;
            }

            _planningIntentService.ExpandTerritory(unit.UnitId, centerNodeId);
        }

        private bool IsSupportedSettlerUnit(UnitView unit)
        {
            if (!IsSupportedSettlerUnitType(unit))
            {
                return false;
            }

            if (planningPhaseOnly)
            {
                var phase = _gameStateStore?.Snapshot?.Phase;
                if (!IsPlanningPhase(phase))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsSupportedSettlerUnitType(UnitView unit)
        {
            if (unit == null || supportedUnitTypes == null || supportedUnitTypes.Length == 0)
            {
                return false;
            }

            var unitType = ResolveUnitType(unit);
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

        private string ResolveUnitType(UnitView unit)
        {
            var unitType = NormalizeToken(unit != null ? unit.UnitType : string.Empty);
            if (!string.IsNullOrEmpty(unitType))
            {
                return unitType;
            }

            var state = _gameStateStore?.Snapshot;
            if (unit == null || state?.Units == null || string.IsNullOrWhiteSpace(unit.UnitId))
            {
                return string.Empty;
            }

            return TryGetUnit(state, unit.UnitId, out var unitState)
                ? NormalizeToken(unitState.Type)
                : string.Empty;
        }

        private bool TryResolveCenterNodeId(UnitView unit, out string centerNodeId)
        {
            centerNodeId = string.Empty;
            var state = _gameStateStore?.Snapshot;
            if (unit == null ||
                state?.Units == null ||
                state.Nodes == null ||
                !TryGetUnit(state, unit.UnitId, out var unitState))
            {
                return false;
            }

            foreach (var node in state.Nodes.Values)
            {
                if (node == null ||
                    node.Q != unitState.Q ||
                    node.R != unitState.R ||
                    string.IsNullOrWhiteSpace(node.Id))
                {
                    continue;
                }

                centerNodeId = node.Id.Trim();
                return true;
            }

            return false;
        }

        private static bool TryGetUnit(GameStateStoreState state, string unitId, out UnitDto unit)
        {
            unit = null;
            if (state?.Units == null || string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            var normalizedUnitId = unitId.Trim();
            if (state.Units.TryGetValue(normalizedUnitId, out unit) && unit != null)
            {
                return true;
            }

            foreach (var candidate in state.Units.Values)
            {
                if (candidate != null &&
                    string.Equals(candidate.Id?.Trim(), normalizedUnitId, StringComparison.OrdinalIgnoreCase))
                {
                    unit = candidate;
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
