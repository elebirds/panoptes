using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class PolicyFocusViewModel : ManagementPanelViewModelBase
    {
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly StaticCatalogStore _staticCatalogStore;

        public PolicyFocusViewModel(StaticCatalogStore staticCatalogStore, PlanningDraftStore planningDraftStore)
        {
            _staticCatalogStore = staticCatalogStore ?? throw new ArgumentNullException(nameof(staticCatalogStore));
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            AddSubscription(_staticCatalogStore.State.Subscribe(this, static (_, self) => self.Publish()));
            AddSubscription(_planningDraftStore.State.Subscribe(this, static (_, self) => self.Publish()));
            Publish();
        }

        protected override ManagementPanelState Project()
        {
            var policies = _staticCatalogStore.Snapshot.Policies;
            if (policies == null || policies.Count == 0)
            {
                return new ManagementPanelState("国策");
            }

            var draft = _planningDraftStore.Snapshot;
            var plannedNational = Normalize(draft.PlannedNationalPolicyId);
            var plannedInstitutions = new HashSet<string>(StringComparer.Ordinal);
            if (draft.PlannedInstitutionPolicyIds != null)
            {
                for (var i = 0; i < draft.PlannedInstitutionPolicyIds.Count; i++)
                {
                    var id = Normalize(draft.PlannedInstitutionPolicyIds[i]);
                    if (!string.IsNullOrEmpty(id))
                    {
                        plannedInstitutions.Add(id);
                    }
                }
            }

            var nationalRows = new List<ManagementPanelRowState>();
            var institutionRows = new List<ManagementPanelRowState>();
            foreach (var pair in policies)
            {
                var policy = pair.Value;
                if (policy == null || string.IsNullOrWhiteSpace(policy.Id))
                {
                    continue;
                }

                var policyId = Normalize(policy.Id);
                var isNational = string.Equals(Normalize(policy.Layer), "national", StringComparison.Ordinal) ||
                                 string.Equals(Normalize(policy.Layer), "national_focus", StringComparison.Ordinal);
                var planned = isNational
                    ? string.Equals(policyId, plannedNational, StringComparison.Ordinal)
                    : plannedInstitutions.Contains(policyId);
                var row = new ManagementPanelRowState(
                    policyId,
                    policy.Name,
                    string.IsNullOrWhiteSpace(policy.ActivationTiming) ? policy.Description : policy.ActivationTiming,
                    policy.Description,
                    planned ? "已规划" : string.Empty,
                    isNational ? "采纳" : "设置",
                    policy.IconKey);
                if (isNational)
                {
                    nationalRows.Add(row);
                }
                else
                {
                    institutionRows.Add(row);
                }
            }

            var groups = new List<ManagementPanelGroupState>();
            if (nationalRows.Count > 0)
            {
                groups.Add(new ManagementPanelGroupState("national", "国家方针", nationalRows));
            }

            if (institutionRows.Count > 0)
            {
                groups.Add(new ManagementPanelGroupState("institution", "制度国策", institutionRows));
            }

            return new ManagementPanelState("国策", groups);
        }
    }
}
