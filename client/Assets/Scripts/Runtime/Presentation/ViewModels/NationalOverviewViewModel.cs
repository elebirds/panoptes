using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class NationalOverviewViewModel : IViewModel<NationalOverviewState>, IDisposable
    {
        private const int MaxEvents = 5;

        private readonly GameStateStore _gameStateStore;
        private readonly BehaviorSubject<NationalOverviewState> _state;
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly SettlementStore _settlementStore;
        private readonly StaticCatalogStore _staticCatalogStore;
        private readonly List<IDisposable> _subscriptions = new();
        private readonly TurnStore _turnStore;
        private bool _disposed;
        private NationalOverviewState _current;

        public NationalOverviewViewModel(
            GameStateStore gameStateStore,
            TurnStore turnStore,
            PlanningDraftStore planningDraftStore,
            StaticCatalogStore staticCatalogStore,
            SettlementStore settlementStore)
        {
            _gameStateStore = gameStateStore ?? throw new ArgumentNullException(nameof(gameStateStore));
            _turnStore = turnStore ?? throw new ArgumentNullException(nameof(turnStore));
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            _staticCatalogStore = staticCatalogStore ?? throw new ArgumentNullException(nameof(staticCatalogStore));
            _settlementStore = settlementStore ?? throw new ArgumentNullException(nameof(settlementStore));
            _current = Project();
            _state = new BehaviorSubject<NationalOverviewState>(_current);
            _subscriptions.Add(_gameStateStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_turnStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_planningDraftStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_staticCatalogStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_settlementStore.State.Subscribe(this, static (_, self) => self.Publish()));
        }

        public NationalOverviewState Current => _current;
        public Observable<NationalOverviewState> State => _state;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (var i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i]?.Dispose();
            }

            _subscriptions.Clear();
            _state.Dispose();
        }

        private void Publish()
        {
            if (_disposed)
            {
                return;
            }

            _current = Project();
            _state.OnNext(_current);
        }

        private NationalOverviewState Project()
        {
            var game = _gameStateStore.Snapshot;
            var turn = _turnStore.Snapshot;
            var planning = _planningDraftStore.Snapshot;
            var catalog = _staticCatalogStore.Snapshot;
            var phase = !string.IsNullOrWhiteSpace(turn.Phase) ? turn.Phase : game.Phase;
            var turnNumber = turn.Turn > 0 ? turn.Turn : game.Turn;
            var tokensLeft = turn.TokensLeft > 0 ? turn.TokensLeft : game.TokensLeft;
            return new NationalOverviewState(
                turnText: turnNumber > 0 ? turnNumber.ToString() : "--",
                phaseText: GamePhases.ToDisplayText(phase),
                tokensText: tokensLeft >= 0 ? tokensLeft.ToString() : "--",
                metrics: BuildMetrics(game),
                resources: BuildResources(game.MyResources),
                events: BuildEvents(turn.PlanningStartEvents, _settlementStore.Snapshot?.Settlement),
                plannedResearchText: ResolveTechnologyName(planning.PlannedResearchTargetTechnologyId, catalog),
                plannedPolicyText: ResolvePolicyName(planning.PlannedNationalPolicyId, catalog));
        }

        private static IReadOnlyList<NationalOverviewMetricState> BuildMetrics(GameStateStoreState game)
        {
            return new List<NationalOverviewMetricState>
            {
                new("visible_nodes", "Visible Nodes", CountVisibleNodes(game).ToString()),
                new("known_units", "Known Units", (game.Units?.Count ?? 0).ToString()),
                new("cities", "Cities", CountCities(game).ToString()),
                new("map", "Map", game.MapWidth > 0 && game.MapHeight > 0 ? $"{game.MapWidth} x {game.MapHeight}" : "--")
            };
        }

        private static int CountVisibleNodes(GameStateStoreState game)
        {
            if (game?.Nodes == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var pair in game.Nodes)
            {
                if (pair.Value != null && pair.Value.IsVisible)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountCities(GameStateStoreState game)
        {
            if (game?.Nodes == null)
            {
                return 0;
            }

            var cityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cityCoreCount = 0;
            foreach (var pair in game.Nodes)
            {
                var node = pair.Value;
                if (node == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(node.CityId))
                {
                    cityIds.Add(node.CityId.Trim());
                }
                else if (node.IsCityCore)
                {
                    cityCoreCount++;
                }
            }

            return cityIds.Count + cityCoreCount;
        }

        private static IReadOnlyList<NationalOverviewResourceState> BuildResources(ResourceDto resources)
        {
            var rows = new List<NationalOverviewResourceState>
            {
                new(ResourceKeys.ResourceFood, "Food", resources?.Food ?? 0),
                new(ResourceKeys.ResourceWood, "Wood", resources?.Wood ?? 0),
                new(ResourceKeys.ResourceOre, "Ore", resources?.Ore ?? 0),
                new("industry_output", "Industry", resources?.IndustryOutput ?? 0)
            };

            AddExtraAmounts(rows, resources?.ResourceAmounts);
            AddExtraAmounts(rows, resources?.PointAmounts);
            return rows;
        }

        private static void AddExtraAmounts(List<NationalOverviewResourceState> rows, IReadOnlyDictionary<string, int> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var pair in source)
            {
                var id = Normalize(pair.Key);
                if (string.IsNullOrEmpty(id) || ContainsResource(rows, id))
                {
                    continue;
                }

                rows.Add(new NationalOverviewResourceState(id, ToTitle(id), pair.Value));
            }
        }

        private static bool ContainsResource(IReadOnlyList<NationalOverviewResourceState> rows, string id)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<NationalOverviewEventState> BuildEvents(
            IReadOnlyList<TurnEventDto> planningEvents,
            TurnSettlementDto settlement)
        {
            var rows = new List<NationalOverviewEventState>();
            AddEvents(rows, planningEvents);
            if (rows.Count < MaxEvents && settlement?.Sections != null)
            {
                for (var i = 0; i < settlement.Sections.Count && rows.Count < MaxEvents; i++)
                {
                    AddEvents(rows, settlement.Sections[i]?.Events);
                }
            }

            return rows;
        }

        private static void AddEvents(List<NationalOverviewEventState> rows, IReadOnlyList<TurnEventDto> events)
        {
            if (events == null)
            {
                return;
            }

            for (var i = 0; i < events.Count && rows.Count < MaxEvents; i++)
            {
                var evt = events[i];
                if (evt == null)
                {
                    continue;
                }

                rows.Add(new NationalOverviewEventState(BuildEventTitle(evt), BuildEventDetail(evt)));
            }
        }

        private static string BuildEventTitle(TurnEventDto evt)
        {
            if (!string.IsNullOrWhiteSpace(evt.Type))
            {
                return ToTitle(evt.Type);
            }

            return !string.IsNullOrWhiteSpace(evt.Section) ? ToTitle(evt.Section) : "Event";
        }

        private static string BuildEventDetail(TurnEventDto evt)
        {
            if (!string.IsNullOrWhiteSpace(evt.ReasonMessage))
            {
                return evt.ReasonMessage.Trim();
            }

            if (!string.IsNullOrWhiteSpace(evt.BlockedReasonMessage))
            {
                return evt.BlockedReasonMessage.Trim();
            }

            if (!string.IsNullOrWhiteSpace(evt.UnitId) && !string.IsNullOrWhiteSpace(evt.NodeId))
            {
                return $"{evt.UnitId.Trim()} -> {evt.NodeId.Trim()}";
            }

            if (!string.IsNullOrWhiteSpace(evt.NodeId))
            {
                return evt.NodeId.Trim();
            }

            return string.Empty;
        }

        private static string ResolveTechnologyName(string technologyId, StaticCatalogState catalog)
        {
            var id = Normalize(technologyId);
            if (string.IsNullOrEmpty(id))
            {
                return "None";
            }

            return catalog?.Technologies != null &&
                   catalog.Technologies.TryGetValue(id, out var technology) &&
                   technology != null &&
                   !string.IsNullOrWhiteSpace(technology.Name)
                ? technology.Name.Trim()
                : id;
        }

        private static string ResolvePolicyName(string policyId, StaticCatalogState catalog)
        {
            var id = Normalize(policyId);
            if (string.IsNullOrEmpty(id))
            {
                return "None";
            }

            return catalog?.Policies != null &&
                   catalog.Policies.TryGetValue(id, out var policy) &&
                   policy != null &&
                   !string.IsNullOrWhiteSpace(policy.Name)
                ? policy.Name.Trim()
                : id;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static string ToTitle(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('_', ' ');
        }
    }
}
