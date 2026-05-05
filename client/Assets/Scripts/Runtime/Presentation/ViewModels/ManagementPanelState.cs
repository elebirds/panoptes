using System.Collections.Generic;
using System.Linq;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class ManagementPanelState
    {
        public ManagementPanelState(string title = "", IReadOnlyList<ManagementPanelGroupState> groups = null)
        {
            Groups = groups != null
                ? new List<ManagementPanelGroupState>(groups)
                : new List<ManagementPanelGroupState>();
            Title = string.IsNullOrWhiteSpace(title) ? "面板" : title.Trim();
        }

        public IReadOnlyList<ManagementPanelGroupState> Groups { get; }
        public bool HasGroups => Groups.Count > 0;
        public string Title { get; }
    }

    public sealed class ManagementPanelGroupState
    {
        public ManagementPanelGroupState(
            string id = "",
            string title = "",
            IReadOnlyList<ManagementPanelRowState> rows = null)
        {
            Id = id ?? string.Empty;
            Rows = rows != null
                ? new List<ManagementPanelRowState>(rows)
                : new List<ManagementPanelRowState>();
            Title = string.IsNullOrWhiteSpace(title) ? "其他" : title.Trim();
        }

        public string Id { get; }
        public IReadOnlyList<ManagementPanelRowState> Rows { get; }
        public string Title { get; }
    }

    public sealed class ManagementPanelRowState
    {
        public ManagementPanelRowState(
            string id = "",
            string title = "",
            string summary = "",
            string detail = "",
            string status = "",
            string actionLabel = "",
            string iconKey = "",
            IReadOnlyList<string> prerequisiteIds = null,
            IReadOnlyList<ManagementPanelAmountState> costs = null,
            IReadOnlyList<ManagementPanelAmountState> outputs = null,
            string emptyCostsLabel = "")
        {
            ActionLabel = actionLabel ?? string.Empty;
            Costs = costs != null
                ? costs.Where(value => value != null && !string.IsNullOrWhiteSpace(value.Id)).ToList()
                : new List<ManagementPanelAmountState>();
            Detail = detail ?? string.Empty;
            EmptyCostsLabel = emptyCostsLabel ?? string.Empty;
            IconKey = iconKey ?? string.Empty;
            Id = id ?? string.Empty;
            Outputs = outputs != null
                ? outputs.Where(value => value != null && !string.IsNullOrWhiteSpace(value.Id)).ToList()
                : new List<ManagementPanelAmountState>();
            PrerequisiteIds = prerequisiteIds != null
                ? prerequisiteIds.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToList()
                : new List<string>();
            Status = status ?? string.Empty;
            Summary = summary ?? string.Empty;
            Title = string.IsNullOrWhiteSpace(title) ? Id : title.Trim();
        }

        public string ActionLabel { get; }
        public IReadOnlyList<ManagementPanelAmountState> Costs { get; }
        public bool HasAction => !string.IsNullOrWhiteSpace(ActionLabel);
        public string Detail { get; }
        public string EmptyCostsLabel { get; }
        public string IconKey { get; }
        public string Id { get; }
        public IReadOnlyList<ManagementPanelAmountState> Outputs { get; }
        public IReadOnlyList<string> PrerequisiteIds { get; }
        public string Status { get; }
        public string Summary { get; }
        public string Title { get; }
    }

    public sealed class ManagementPanelAmountState
    {
        public ManagementPanelAmountState(string id = "", string label = "", int amount = 0, string iconKey = "")
        {
            Id = id ?? string.Empty;
            Label = string.IsNullOrWhiteSpace(label) ? Id : label.Trim();
            Amount = amount;
            IconKey = iconKey ?? string.Empty;
        }

        public int Amount { get; }
        public string IconKey { get; }
        public string Id { get; }
        public string Label { get; }
    }
}
