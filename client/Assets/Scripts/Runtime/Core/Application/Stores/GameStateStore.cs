using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    public sealed class GameStateStore : ReactiveStore<GameStateStoreState>
    {
        private const string IndustryOutputPointKey = "industry_output";

        private readonly System.Collections.Generic.Dictionary<string, BuildCostReservation> _localBuildCostReservations = new(System.StringComparer.OrdinalIgnoreCase);

        public GameStateStore()
            : base(new GameStateStoreState())
        {
        }

        internal void Replace(GameStateStoreState state)
        {
            var next = state ?? new GameStateStoreState();
            if (Snapshot == null || Snapshot.Turn != next.Turn || !string.Equals(Snapshot.Phase, next.Phase, System.StringComparison.Ordinal))
            {
                _localBuildCostReservations.Clear();
            }
            Publish(next);
        }

        internal bool TryReserveBuildCost(string reservationId, CatalogBuildingDto building, out string errorCode)
        {
            errorCode = string.Empty;
            reservationId = NormalizeKey(reservationId);
            if (string.IsNullOrEmpty(reservationId) || building == null)
            {
                errorCode = "invalid_request";
                return false;
            }

            var reservation = BuildCostReservation.From(building);
            var resources = StoreSnapshotCloner.CloneResource(Snapshot?.MyResources);
            if (_localBuildCostReservations.TryGetValue(reservationId, out var existing))
            {
                ApplyReservation(resources, existing, 1);
            }

            if (!CanAfford(resources, reservation, out errorCode))
            {
                return false;
            }

            ApplyReservation(resources, reservation, -1);
            _localBuildCostReservations[reservationId] = reservation;
            Publish(WithResources(Snapshot, resources));
            return true;
        }

        internal bool RefundReservedBuildCost(string reservationId)
        {
            reservationId = NormalizeKey(reservationId);
            if (string.IsNullOrEmpty(reservationId) || !_localBuildCostReservations.TryGetValue(reservationId, out var reservation))
            {
                return false;
            }

            _localBuildCostReservations.Remove(reservationId);
            var resources = StoreSnapshotCloner.CloneResource(Snapshot?.MyResources);
            ApplyReservation(resources, reservation, 1);
            Publish(WithResources(Snapshot, resources));
            return true;
        }

        protected override GameStateStoreState CloneState(GameStateStoreState state)
        {
            return state == null ? new GameStateStoreState() : state.Clone();
        }

        private static GameStateStoreState WithResources(GameStateStoreState current, ResourceDto resources)
        {
            current ??= new GameStateStoreState();
            return new GameStateStoreState(
                current.GameId,
                current.ActiveGameSessionId,
                current.MyPlayerId,
                current.Turn,
                current.Phase,
                current.MapWidth,
                current.MapHeight,
                current.IsGameOver,
                current.TokensLeft,
                current.Nodes,
                current.Units,
                resources,
                current.ResearchState);
        }

        private static bool CanAfford(ResourceDto resources, BuildCostReservation reservation, out string errorCode)
        {
            errorCode = string.Empty;
            resources ??= new ResourceDto();
            foreach (var pair in reservation.ResourceCosts)
            {
                if (GetAmount(resources, pair.Key, false) < pair.Value)
                {
                    errorCode = "insufficient_resources";
                    return false;
                }
            }

            foreach (var pair in reservation.PointCosts)
            {
                if (GetAmount(resources, pair.Key, true) < pair.Value)
                {
                    errorCode = "insufficient_points";
                    return false;
                }
            }

            return true;
        }

        private static void ApplyReservation(ResourceDto resources, BuildCostReservation reservation, int sign)
        {
            resources ??= new ResourceDto();
            foreach (var pair in reservation.ResourceCosts)
            {
                SetAmount(resources, pair.Key, false, GetAmount(resources, pair.Key, false) + pair.Value * sign);
            }

            foreach (var pair in reservation.PointCosts)
            {
                SetAmount(resources, pair.Key, true, GetAmount(resources, pair.Key, true) + pair.Value * sign);
            }
        }

        private static int GetAmount(ResourceDto resources, string key, bool point)
        {
            key = NormalizeKey(key);
            if (string.IsNullOrEmpty(key) || resources == null)
            {
                return 0;
            }

            var amounts = point ? resources.PointAmounts : resources.ResourceAmounts;
            if (amounts != null && amounts.TryGetValue(key, out var amount))
            {
                return amount;
            }

            return point
                ? string.Equals(key, IndustryOutputPointKey, System.StringComparison.OrdinalIgnoreCase) ? resources.IndustryOutput : 0
                : FixedResourceAmount(resources, key);
        }

        private static void SetAmount(ResourceDto resources, string key, bool point, int amount)
        {
            key = NormalizeKey(key);
            if (string.IsNullOrEmpty(key) || resources == null)
            {
                return;
            }

            var amounts = point ? resources.PointAmounts : resources.ResourceAmounts;
            if (amounts != null)
            {
                amounts[key] = amount;
            }

            if (point)
            {
                if (string.Equals(key, IndustryOutputPointKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    resources.IndustryOutput = amount;
                }
                return;
            }

            if (string.Equals(key, ResourceKeys.ResourceFood, System.StringComparison.OrdinalIgnoreCase))
            {
                resources.Food = amount;
            }
            else if (string.Equals(key, ResourceKeys.ResourceWood, System.StringComparison.OrdinalIgnoreCase))
            {
                resources.Wood = amount;
            }
            else if (string.Equals(key, ResourceKeys.ResourceOre, System.StringComparison.OrdinalIgnoreCase))
            {
                resources.Ore = amount;
            }
        }

        private static int FixedResourceAmount(ResourceDto resources, string key)
        {
            if (string.Equals(key, ResourceKeys.ResourceFood, System.StringComparison.OrdinalIgnoreCase))
            {
                return resources.Food;
            }

            if (string.Equals(key, ResourceKeys.ResourceWood, System.StringComparison.OrdinalIgnoreCase))
            {
                return resources.Wood;
            }

            return string.Equals(key, ResourceKeys.ResourceOre, System.StringComparison.OrdinalIgnoreCase) ? resources.Ore : 0;
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private sealed class BuildCostReservation
        {
            public System.Collections.Generic.Dictionary<string, int> ResourceCosts { get; } = new(System.StringComparer.OrdinalIgnoreCase);
            public System.Collections.Generic.Dictionary<string, int> PointCosts { get; } = new(System.StringComparer.OrdinalIgnoreCase);

            public static BuildCostReservation From(CatalogBuildingDto building)
            {
                var reservation = new BuildCostReservation();
                AddCosts(reservation.ResourceCosts, building?.ResourceCosts);
                AddCosts(reservation.PointCosts, building?.PointCosts);
                return reservation;
            }

            private static void AddCosts(System.Collections.Generic.Dictionary<string, int> target, System.Collections.Generic.IEnumerable<CatalogAmountDto> costs)
            {
                if (target == null || costs == null)
                {
                    return;
                }

                foreach (var cost in costs)
                {
                    if (cost == null)
                    {
                        continue;
                    }

                    var key = NormalizeKey(cost.Key);
                    if (string.IsNullOrEmpty(key) || cost.Amount <= 0)
                    {
                        continue;
                    }

                    target.TryGetValue(key, out var current);
                    target[key] = current + cost.Amount;
                }
            }
        }
    }
}
