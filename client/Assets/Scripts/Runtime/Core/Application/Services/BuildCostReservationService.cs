using System;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Services
{
    public sealed class BuildCostReservationService
    {
        private readonly GameStateStore _gameStateStore;

        public BuildCostReservationService(GameStateStore gameStateStore)
        {
            _gameStateStore = gameStateStore ?? throw new ArgumentNullException(nameof(gameStateStore));
        }

        public bool TryReserveBuildCost(string reservationId, CatalogBuildingDto building, out string errorCode)
        {
            return _gameStateStore.TryReserveBuildCost(reservationId, building, out errorCode);
        }

        public bool RefundReservedBuildCost(string reservationId)
        {
            return _gameStateStore.RefundReservedBuildCost(reservationId);
        }
    }
}
