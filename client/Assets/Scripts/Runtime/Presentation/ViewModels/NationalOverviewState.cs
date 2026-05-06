using System.Collections.Generic;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class NationalOverviewState
    {
        public NationalOverviewState(
            string title = "国家概览",
            string turnText = "--",
            string phaseText = "--",
            string tokensText = "--",
            IReadOnlyList<NationalOverviewMetricState> metrics = null,
            IReadOnlyList<NationalOverviewResourceState> resources = null,
            IReadOnlyList<NationalOverviewEventState> events = null,
            string plannedResearchText = "无",
            string plannedPolicyText = "无")
        {
            Events = events != null
                ? new List<NationalOverviewEventState>(events)
                : new List<NationalOverviewEventState>();
            Metrics = metrics != null
                ? new List<NationalOverviewMetricState>(metrics)
                : new List<NationalOverviewMetricState>();
            PhaseText = string.IsNullOrWhiteSpace(phaseText) ? "--" : phaseText.Trim();
            PlannedPolicyText = string.IsNullOrWhiteSpace(plannedPolicyText) ? "无" : plannedPolicyText.Trim();
            PlannedResearchText = string.IsNullOrWhiteSpace(plannedResearchText) ? "无" : plannedResearchText.Trim();
            Resources = resources != null
                ? new List<NationalOverviewResourceState>(resources)
                : new List<NationalOverviewResourceState>();
            Title = string.IsNullOrWhiteSpace(title) ? "国家概览" : title.Trim();
            TokensText = string.IsNullOrWhiteSpace(tokensText) ? "--" : tokensText.Trim();
            TurnText = string.IsNullOrWhiteSpace(turnText) ? "--" : turnText.Trim();
        }

        public IReadOnlyList<NationalOverviewEventState> Events { get; }
        public bool HasEvents => Events.Count > 0;
        public bool HasMetrics => Metrics.Count > 0;
        public bool HasResources => Resources.Count > 0;
        public IReadOnlyList<NationalOverviewMetricState> Metrics { get; }
        public string PhaseText { get; }
        public string PlannedPolicyText { get; }
        public string PlannedResearchText { get; }
        public IReadOnlyList<NationalOverviewResourceState> Resources { get; }
        public string Title { get; }
        public string TokensText { get; }
        public string TurnText { get; }
    }

    public sealed class NationalOverviewMetricState
    {
        public NationalOverviewMetricState(string id = "", string label = "", string value = "")
        {
            Id = id ?? string.Empty;
            Label = string.IsNullOrWhiteSpace(label) ? Id : label.Trim();
            Value = string.IsNullOrWhiteSpace(value) ? "--" : value.Trim();
        }

        public string Id { get; }
        public string Label { get; }
        public string Value { get; }
    }

    public sealed class NationalOverviewResourceState
    {
        public NationalOverviewResourceState(string id = "", string label = "", int amount = 0)
        {
            Amount = amount;
            Id = id ?? string.Empty;
            Label = string.IsNullOrWhiteSpace(label) ? Id : label.Trim();
        }

        public int Amount { get; }
        public string AmountText => Amount.ToString();
        public string Id { get; }
        public string Label { get; }
    }

    public sealed class NationalOverviewEventState
    {
        public NationalOverviewEventState(string title = "", string detail = "")
        {
            Detail = detail ?? string.Empty;
            Title = string.IsNullOrWhiteSpace(title) ? "事件" : title.Trim();
        }

        public string Detail { get; }
        public string Title { get; }
    }
}
