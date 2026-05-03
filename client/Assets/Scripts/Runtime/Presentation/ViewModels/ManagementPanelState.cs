using System.Collections.Generic;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class ManagementPanelState
    {
        public ManagementPanelState(string title = "", IReadOnlyList<ManagementPanelGroupState> groups = null)
        {
            Groups = groups != null
                ? new List<ManagementPanelGroupState>(groups)
                : new List<ManagementPanelGroupState>();
            Title = string.IsNullOrWhiteSpace(title) ? "Panel" : title.Trim();
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
            Title = string.IsNullOrWhiteSpace(title) ? "Other" : title.Trim();
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
            string actionLabel = "")
        {
            ActionLabel = actionLabel ?? string.Empty;
            Detail = detail ?? string.Empty;
            Id = id ?? string.Empty;
            Status = status ?? string.Empty;
            Summary = summary ?? string.Empty;
            Title = string.IsNullOrWhiteSpace(title) ? Id : title.Trim();
        }

        public string ActionLabel { get; }
        public bool HasAction => !string.IsNullOrWhiteSpace(ActionLabel);
        public string Detail { get; }
        public string Id { get; }
        public string Status { get; }
        public string Summary { get; }
        public string Title { get; }
    }
}
