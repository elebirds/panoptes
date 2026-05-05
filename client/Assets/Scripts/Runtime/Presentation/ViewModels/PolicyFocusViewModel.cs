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
            var plannedInstitutions = BuildIdSet(draft.PlannedInstitutionPolicyIds);
            var activeInstitutions = BuildIdSet(_gameStateCache?.GetInstitutionState()?.ActivePolicyIds);

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
                var active = isNational
                    ? string.Equals(policyId, activeNational, StringComparison.Ordinal)
                    : activeInstitutions.Contains(policyId);
                var status = planned ? "已规划" : active ? "已选择" : string.Empty;
                var row = new ManagementPanelRowState(
                    policyId,
                    policy.Name,
                    BuildPolicyEffectSummary(policy),
                    policy.Description,
                    status,
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

        private static string BuildPolicyEffectSummary(CatalogPolicyDto policy)
        {
            var effects = policy?.ModifierEffects;
            if (effects == null || effects.Count == 0)
            {
                return policy?.Description ?? string.Empty;
            }

            var values = new List<string>();
            for (var i = 0; i < effects.Count; i++)
            {
                var text = FormatModifierEffect(effects[i]);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text);
                }
            }

            return values.Count > 0 ? string.Join("；", values) : policy?.Description ?? string.Empty;
        }

        private static string FormatModifierEffect(CatalogPolicyModifierEffectDto effect)
        {
            if (effect == null)
            {
                return string.Empty;
            }

            var value = FormatSigned(effect.Value);
            var target = FormatKey(effect.TargetId);
            var trigger = Normalize(effect.Trigger);
            switch (trigger)
            {
                case "recipe.work_amount":
                    return string.IsNullOrWhiteSpace(target) ? $"工时 {value}" : $"{target} 工时 {value}";
                case "recipe.resource_output":
                    var resource = FormatKey(effect.ResourceKey);
                    return string.IsNullOrWhiteSpace(target)
                        ? $"{resource}产出 {value}"
                        : $"{target} {resource}产出 {value}";
                case "recipe.base_progress":
                    return string.IsNullOrWhiteSpace(target) ? $"基础进度 {value}" : $"{target} 基础进度 {value}";
                case "point.output":
                    var point = FormatKey(effect.PointKey);
                    return string.IsNullOrWhiteSpace(point) ? $"点数产出 {value}" : $"{point} {value}";
                case "logistics.road_capacity":
                    return $"道路运力 {value}";
                default:
                    return string.IsNullOrWhiteSpace(trigger) ? value : $"{trigger} {value}";
            }
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? "+" + value : value.ToString();
        }

        private static string FormatKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Replace('_', ' ');
        }

        private static HashSet<string> BuildIdSet(IReadOnlyList<string> ids)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (ids == null)
            {
                return result;
            }

            for (var i = 0; i < ids.Count; i++)
            {
                var id = Normalize(ids[i]);
                if (!string.IsNullOrEmpty(id))
                {
                    result.Add(id);
                }
            }

            return result;
        }
    }
}
