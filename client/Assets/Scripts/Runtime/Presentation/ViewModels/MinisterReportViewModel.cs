using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class MinisterReportViewModel : ManagementPanelViewModelBase
    {
        private readonly PlanningDraftStore _planningDraftStore;

        public MinisterReportViewModel(PlanningDraftStore planningDraftStore)
        {
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            AddSubscription(_planningDraftStore.State.Subscribe(this, static (_, self) => self.Publish()));
            Publish();
        }

        protected override ManagementPanelState Project()
        {
            var drafts = _planningDraftStore.Snapshot.MinisterDrafts;
            if (drafts == null || drafts.Count == 0)
            {
                return new ManagementPanelState("Minister Report");
            }

            var groups = new Dictionary<string, List<ManagementPanelRowState>>(StringComparer.Ordinal);
            for (var i = 0; i < drafts.Count; i++)
            {
                var draft = drafts[i];
                if (draft == null)
                {
                    continue;
                }

                var role = string.IsNullOrWhiteSpace(draft.MinisterRole) ? "general" : Normalize(draft.MinisterRole);
                if (!groups.TryGetValue(role, out var rows))
                {
                    rows = new List<ManagementPanelRowState>();
                    groups[role] = rows;
                }

                rows.Add(new ManagementPanelRowState(
                    draft.DraftId,
                    draft.Title,
                    draft.Summary,
                    draft.Rationale,
                    draft.DisplayStatus,
                    draft.IsInteractive ? "Review" : string.Empty));
            }

            return new ManagementPanelState("Minister Report", BuildGroups(groups));
        }

        private static IReadOnlyList<ManagementPanelGroupState> BuildGroups(Dictionary<string, List<ManagementPanelRowState>> groups)
        {
            var keys = new List<string>(groups.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            var result = new List<ManagementPanelGroupState>(keys.Count);
            for (var i = 0; i < keys.Count; i++)
            {
                result.Add(new ManagementPanelGroupState(keys[i], ToTitle(keys[i]), groups[keys[i]]));
            }

            return result;
        }

        private static string ToTitle(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "General" : value.Trim();
        }
    }
}
