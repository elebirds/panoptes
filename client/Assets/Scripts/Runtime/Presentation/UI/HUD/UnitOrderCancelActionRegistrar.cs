using System;
using System.Linq;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class UnitOrderCancelActionRegistrar : UnitInfoActionProviderBase
    {
        [SerializeField] private string actionId = "cancel_unit_order";
        [SerializeField] private string actionLabel = "Cancel";
        [SerializeField] private bool planningPhaseOnly = true;

        private PlanningIntentService _planningIntentService;
        private PlanningDraftStore _planningDraftStore;
        private GameStateStore _gameStateStore;

        [Inject]
        private void Construct(
            PlanningIntentService planningIntentService,
            PlanningDraftStore planningDraftStore,
            GameStateStore gameStateStore)
        {
            _planningIntentService = planningIntentService;
            _planningDraftStore = planningDraftStore;
            _gameStateStore = gameStateStore;
        }

        protected override void RegisterActions(UnitInfoActionRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            registry.RegisterAction(
                actionId,
                OnCancelClicked,
                string.IsNullOrWhiteSpace(actionLabel) ? actionId : actionLabel,
                CanCancelOrder);
        }

        private void OnCancelClicked(UnitView unit)
        {
            if (unit == null)
            {
                return;
            }

            if (_planningIntentService == null)
            {
                PanoptesLog.Warning("[UnitOrderCancelActionRegistrar] PlanningIntentService missing, cannot cancel unit order.");
                return;
            }

            _planningIntentService.CancelUnitOrder(unit.UnitId);
        }

        private bool CanCancelOrder(UnitView unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.UnitId))
            {
                return false;
            }

            if (planningPhaseOnly)
            {
                var game = _gameStateStore?.Snapshot;
                if (game == null || !GamePhases.IsPlanning(game.Phase) || game.IsGameOver)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(game.MyPlayerId) &&
                    !string.Equals(game.MyPlayerId, unit.Faction, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            var orders = _planningDraftStore?.Snapshot.UnitOrders;
            return orders != null && orders.Any(order =>
                order != null &&
                string.Equals(order.UnitId, unit.UnitId, StringComparison.Ordinal));
        }
    }
}
