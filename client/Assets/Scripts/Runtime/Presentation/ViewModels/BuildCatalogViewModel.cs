using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class BuildCatalogViewModel : IViewModel<BuildCatalogState>, IDisposable
    {
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly BehaviorSubject<BuildCatalogState> _state;
        private readonly StaticCatalogStore _staticCatalogStore;
        private readonly List<IDisposable> _subscriptions = new();
        private BuildCatalogState _current;
        private bool _disposed;

        public BuildCatalogViewModel(
            StaticCatalogStore staticCatalogStore,
            PlanningDraftStore planningDraftStore)
        {
            _staticCatalogStore = staticCatalogStore ?? throw new ArgumentNullException(nameof(staticCatalogStore));
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            _current = Project();
            _state = new BehaviorSubject<BuildCatalogState>(_current);
            _subscriptions.Add(_staticCatalogStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_planningDraftStore.State.Subscribe(this, static (_, self) => self.Publish()));
        }

        public BuildCatalogState Current => _current;
        public Observable<BuildCatalogState> State => _state;

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

        private BuildCatalogState Project()
        {
            var catalog = _staticCatalogStore.Snapshot;
            var draft = _planningDraftStore.Snapshot;
            if (catalog?.Buildings == null || catalog.Buildings.Count == 0)
            {
                return new BuildCatalogState();
            }

            var buildings = new List<CatalogBuildingDto>(catalog.Buildings.Values);
            buildings.Sort(CompareBuildings);

            var pendingBuildingIds = BuildPendingBuildingIds(draft);
            var groupsById = new Dictionary<string, BuildCatalogGroupStateBuilder>(StringComparer.Ordinal);
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null || string.IsNullOrWhiteSpace(building.Id))
                {
                    continue;
                }

                var buildingId = Normalize(building.Id);
                if (string.IsNullOrEmpty(buildingId) || string.Equals(buildingId, "city_core", StringComparison.Ordinal))
                {
                    continue;
                }

                var groupId = ResolveGroupId(building.PlacementKind);
                if (!groupsById.TryGetValue(groupId, out var group))
                {
                    group = new BuildCatalogGroupStateBuilder(groupId, ResolveGroupTitle(groupId));
                    groupsById[groupId] = group;
                }

                group.Items.Add(new BuildCatalogItemState(
                    buildingId,
                    building.Name,
                    building.Description,
                    building.PlacementKind,
                    ResolvePlacementRule(building.PlacementKind),
                    pendingBuildingIds.Contains(buildingId)));
            }

            var groups = new List<BuildCatalogGroupState>(3);
            AddGroup(groups, groupsById, "tile");
            AddGroup(groups, groupsById, "city");
            AddGroup(groups, groupsById, "other");
            return new BuildCatalogState(groups);
        }

        private static void AddGroup(
            List<BuildCatalogGroupState> groups,
            Dictionary<string, BuildCatalogGroupStateBuilder> groupsById,
            string groupId)
        {
            if (groupsById.TryGetValue(groupId, out var group) && group.Items.Count > 0)
            {
                groups.Add(new BuildCatalogGroupState(group.Id, group.Title, group.Items));
            }
        }

        private static HashSet<string> BuildPendingBuildingIds(PlanningDraftState draft)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (draft == null)
            {
                return result;
            }

            var preview = draft.CurrentBuildPreview;
            var previewBuildingId = Normalize(preview?.BuildingTypeId);
            if (!string.IsNullOrEmpty(previewBuildingId))
            {
                result.Add(previewBuildingId);
            }

            var orders = draft.BuildOrders;
            if (orders == null)
            {
                return result;
            }

            for (var i = 0; i < orders.Count; i++)
            {
                var buildingId = Normalize(orders[i]?.BuildingTypeId);
                if (!string.IsNullOrEmpty(buildingId))
                {
                    result.Add(buildingId);
                }
            }

            return result;
        }

        private static int CompareBuildings(CatalogBuildingDto left, CatalogBuildingDto right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var sortCompare = left.SortOrder.CompareTo(right.SortOrder);
            return sortCompare != 0
                ? sortCompare
                : string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveGroupId(string placementKind)
        {
            return Normalize(placementKind) switch
            {
                "resource_node" => "tile",
                "city_territory" => "city",
                "city_foundation_center" => "city",
                _ => "other"
            };
        }

        private static string ResolveGroupTitle(string groupId)
        {
            return groupId switch
            {
                "tile" => "地块建筑",
                "city" => "城市建筑",
                _ => "其他"
            };
        }

        private static PlanningBuildPlacementRule ResolvePlacementRule(string placementKind)
        {
            return Normalize(placementKind) switch
            {
                "resource_node" => PlanningBuildPlacementRule.ResourceOnly,
                "city_territory" => PlanningBuildPlacementRule.CityOnly,
                "city_foundation_center" => PlanningBuildPlacementRule.CityOnly,
                _ => PlanningBuildPlacementRule.AnyTerrain
            };
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private sealed class BuildCatalogGroupStateBuilder
        {
            public BuildCatalogGroupStateBuilder(string id, string title)
            {
                Id = id;
                Title = title;
            }

            public string Id { get; }
            public List<BuildCatalogItemState> Items { get; } = new();
            public string Title { get; }
        }
    }
}
