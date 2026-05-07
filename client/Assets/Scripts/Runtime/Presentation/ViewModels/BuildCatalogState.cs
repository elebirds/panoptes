using System.Collections.Generic;
using Panoptes.Core.Application.Stores;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class BuildCatalogState
    {
        public BuildCatalogState(IReadOnlyList<BuildCatalogGroupState> groups = null)
        {
            Groups = groups != null
                ? new List<BuildCatalogGroupState>(groups)
                : new List<BuildCatalogGroupState>();
        }

        public IReadOnlyList<BuildCatalogGroupState> Groups { get; }
        public bool HasGroups => Groups.Count > 0;
    }

    public sealed class BuildCatalogGroupState
    {
        public BuildCatalogGroupState(
            string id = "",
            string title = "",
            IReadOnlyList<BuildCatalogItemState> items = null)
        {
            Id = id ?? string.Empty;
            Items = items != null
                ? new List<BuildCatalogItemState>(items)
                : new List<BuildCatalogItemState>();
            Title = string.IsNullOrWhiteSpace(title) ? "其他" : title.Trim();
        }

        public string Id { get; }
        public IReadOnlyList<BuildCatalogItemState> Items { get; }
        public string Title { get; }
    }

    public sealed class BuildCatalogItemState
    {
        public BuildCatalogItemState(
            string buildingId = "",
            string title = "",
            string description = "",
            string placementKind = "",
            PlanningBuildPlacementRule placementRule = PlanningBuildPlacementRule.AnyTerrain,
            bool isPending = false,
            string iconKey = "",
            IReadOnlyList<ManagementPanelAmountState> costs = null)
        {
            BuildingId = buildingId ?? string.Empty;
            Costs = costs != null
                ? new List<ManagementPanelAmountState>(costs)
                : new List<ManagementPanelAmountState>();
            Description = description ?? string.Empty;
            IconKey = iconKey ?? string.Empty;
            IsPending = isPending;
            PlacementKind = placementKind ?? string.Empty;
            PlacementRule = placementRule;
            Title = string.IsNullOrWhiteSpace(title) ? BuildingId : title.Trim();
        }

        public string BuildingId { get; }
        public IReadOnlyList<ManagementPanelAmountState> Costs { get; }
        public string Description { get; }
        public string IconKey { get; }
        public bool IsPending { get; }
        public string PendingText => IsPending ? "已规划" : string.Empty;
        public string PlacementKind { get; }
        public PlanningBuildPlacementRule PlacementRule { get; }
        public string Title { get; }
    }
}
