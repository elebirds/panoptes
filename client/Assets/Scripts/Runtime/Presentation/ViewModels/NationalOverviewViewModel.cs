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
                new("visible_nodes", "可见地块", CountVisibleNodes(game).ToString()),
                new("known_units", "已知单位", (game.Units?.Count ?? 0).ToString()),
                new("cities", "城市", CountCities(game).ToString()),
                new("map", "地图", game.MapWidth > 0 && game.MapHeight > 0 ? $"{game.MapWidth} x {game.MapHeight}" : "--")
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
                new(ResourceKeys.ResourceFood, CatalogDisplayNameResolver.ResolveResourceName(ResourceKeys.ResourceFood), resources?.Food ?? 0),
                new(ResourceKeys.ResourceWood, CatalogDisplayNameResolver.ResolveResourceName(ResourceKeys.ResourceWood), resources?.Wood ?? 0),
                new(ResourceKeys.ResourceOre, CatalogDisplayNameResolver.ResolveResourceName(ResourceKeys.ResourceOre), resources?.Ore ?? 0),
                new("industry_output", CatalogDisplayNameResolver.ResolvePointName("industry_output"), resources?.IndustryOutput ?? 0)
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

                rows.Add(new NationalOverviewResourceState(id, CatalogDisplayNameResolver.ResolveKnownName(id), pair.Value));
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

            return !string.IsNullOrWhiteSpace(evt.Section) ? ToTitle(evt.Section) : "事件";
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
                return "无";
            }

            return CatalogDisplayNameResolver.ResolveTechnologyName(id, catalog, "无");
        }

        private static string ResolvePolicyName(string policyId, StaticCatalogState catalog)
        {
            var id = Normalize(policyId);
            if (string.IsNullOrEmpty(id))
            {
                return "无";
            }

            return CatalogDisplayNameResolver.ResolvePolicyName(id, catalog, "无");
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static string ToTitle(string value)
        {
            var id = Normalize(value);
            return id switch
            {
                "" => string.Empty,
                "unit_moved" => "单位移动",
                "technology_activated" => "科技生效",
                "technology_completed" => "科技完成",
                "technology_grant_applied" => "科技奖励生效",
                "building_built" => "建筑完工",
                "building_demolished" => "建筑拆除",
                "building_skipped" => "建筑跳过",
                "building_demolish_skipped" => "拆除跳过",
                "building_status_changed" => "建筑状态变化",
                "building_damaged" => "建筑受损",
                "building_ruined" => "建筑毁坏",
                "city_core_damaged" => "城市核心受损",
                "recipe_progressed" => "配方推进",
                "recipe_skipped" => "配方未推进",
                "recipe_completed" => "配方完成",
                "road_built" => "道路建成",
                "facility_takeover_progressed" => "设施接管推进",
                "national_policy_changed" => "国策变更",
                "institution_loadout_activated" => "制度配置生效",
                _ => CatalogDisplayNameResolver.ResolveKnownName(id)
            };
        }
    }
}
