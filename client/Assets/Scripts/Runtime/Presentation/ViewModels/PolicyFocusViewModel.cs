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
        private readonly GameStateCache _gameStateCache;
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly StaticCatalogStore _staticCatalogStore;

        public PolicyFocusViewModel(
            StaticCatalogStore staticCatalogStore,
            PlanningDraftStore planningDraftStore,
            GameStateCache gameStateCache = null)
        {
            _staticCatalogStore = staticCatalogStore ?? throw new ArgumentNullException(nameof(staticCatalogStore));
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            _gameStateCache = gameStateCache;
            AddSubscription(_staticCatalogStore.State.Subscribe(this, static (_, self) => self.Publish()));
            AddSubscription(_planningDraftStore.State.Subscribe(this, static (_, self) => self.Publish()));
            if (_gameStateCache != null)
            {
                _gameStateCache.OnStateChanged += Publish;
                AddCleanup(() => _gameStateCache.OnStateChanged -= Publish);
            }

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
            var activeNational = Normalize(_gameStateCache?.GetActiveNationalPolicyId());

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
                if (!isNational)
                {
                    continue;
                }

                var planned = isNational
                    ? string.Equals(policyId, plannedNational, StringComparison.Ordinal)
                    : false;
                var active = isNational
                    ? string.Equals(policyId, activeNational, StringComparison.Ordinal)
                    : false;
                var status = planned ? "已规划" : active ? "已选择" : string.Empty;
                var row = new ManagementPanelRowState(
                    policyId,
                    policy.Name,
                    ManagementEffectSummaryFormatter.Build(policy.ModifierEffects, policy.Description, _staticCatalogStore.Snapshot),
                    policy.Description,
                    status,
                    "采纳",
                    policy.IconKey);
                nationalRows.Add(row);
            }

            var groups = new List<ManagementPanelGroupState>();
            if (nationalRows.Count > 0)
            {
                groups.Add(new ManagementPanelGroupState("national", "国家方针", nationalRows));
            }

            return new ManagementPanelState("国策", groups);
        }
    }
}
