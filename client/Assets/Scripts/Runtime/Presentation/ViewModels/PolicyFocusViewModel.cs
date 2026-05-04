using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
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
            if (StaticCatalogCache.Instance != null)
            {
                StaticCatalogCache.Instance.CatalogChanged += Publish;
            }

            Publish();
        }

        protected override ManagementPanelState Project()
        {
            var policies = _staticCatalogStore.Snapshot.Policies;
            if ((policies == null || policies.Count == 0) && StaticCatalogCache.Instance != null)
            {
                return ProjectFromRuntimeCache();
            }

            if (policies == null || policies.Count == 0)
            {
                return new ManagementPanelState("Policy Focus");
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
                if (!isNational)
                {
                    continue;
                }

                var row = new ManagementPanelRowState(
                    policyId,
                    policy.Name,
                    FirstText(policy.BenefitDescription, policy.Description),
                    FirstText(policy.NextActionDescription, policy.ActivationTiming),
                    planned ? "Planned" : string.Empty,
                    "Adopt",
                    policy.IconKey,
                    FirstText(policy.BenefitDescription, policy.Description),
                    FirstText(policy.NextActionDescription, policy.ActivationTiming));
                nationalRows.Add(row);
            }

            var groups = new List<ManagementPanelGroupState>();
            if (nationalRows.Count > 0)
            {
                groups.Add(new ManagementPanelGroupState("national", "National Focus", nationalRows));
            }

            return new ManagementPanelState("Policy Focus", groups);
        }

        private ManagementPanelState ProjectFromRuntimeCache()
        {
            var cache = StaticCatalogCache.Instance;
            var draft = _planningDraftStore.Snapshot;
            var plannedNational = Normalize(draft.PlannedNationalPolicyId);
            var rows = new List<ManagementPanelRowState>();

            foreach (var pair in cache.Policies)
            {
                var policy = pair.Value;
                if (policy == null || string.IsNullOrWhiteSpace(policy.id))
                {
                    continue;
                }

                var policyId = Normalize(policy.id);
                var isNational = string.Equals(Normalize(policy.layer), "national", StringComparison.Ordinal) ||
                                 string.Equals(Normalize(policy.layer), "national_focus", StringComparison.Ordinal);
                if (!isNational)
                {
                    continue;
                }

                var benefit = FirstText(policy.benefit_description, policy.description);
                var nextAction = FirstText(policy.next_action_description, policy.activation_timing);
                rows.Add(new ManagementPanelRowState(
                    policyId,
                    policy.name,
                    benefit,
                    nextAction,
                    string.Equals(policyId, plannedNational, StringComparison.Ordinal) ? "Planned" : string.Empty,
                    "Adopt",
                    policy.icon_key,
                    benefit,
                    nextAction));
            }

            return rows.Count == 0
                ? new ManagementPanelState("Policy Focus")
                : new ManagementPanelState("Policy Focus", new[] { new ManagementPanelGroupState("national", "National Focus", rows) });
        }

        private static string FirstText(string preferred, string fallback)
        {
            return string.IsNullOrWhiteSpace(preferred) ? fallback ?? string.Empty : preferred;
        }
    }
}
