using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class TechTreeViewModel : ManagementPanelViewModelBase
    {
        private readonly GameStateCache _gameStateCache;
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly StaticCatalogStore _staticCatalogStore;

        public TechTreeViewModel(
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
            var catalog = _staticCatalogStore.Snapshot;
            if (catalog?.Technologies == null || catalog.Technologies.Count == 0)
            {
                return new ManagementPanelState("科技树");
            }

            var plannedTechId = Normalize(_planningDraftStore.Snapshot.PlannedResearchTargetTechnologyId);
            var research = _gameStateCache?.GetCurrentResearchState();
            var currentTechId = Normalize(research?.TechnologyId);
            var completedTechIds = BuildIdSet(research?.CompletedTechnologyIds);
            var activeTechIds = BuildIdSet(research?.ActiveTechnologyIds);
            var pendingActivationTechIds = BuildIdSet(research?.PendingActivationTechnologyIds);
            var technologies = new List<CatalogTechnologyDto>(catalog.Technologies.Values);
            technologies.Sort(CompareTechnologies);

            var groups = new Dictionary<string, List<ManagementPanelRowState>>(StringComparer.Ordinal);
            for (var i = 0; i < technologies.Count; i++)
            {
                var technology = technologies[i];
                if (technology == null || string.IsNullOrWhiteSpace(technology.Id))
                {
                    continue;
                }

                var branch = string.IsNullOrWhiteSpace(technology.Branch) ? "通用" : LocalizeBranch(technology.Branch);
                if (!groups.TryGetValue(branch, out var rows))
                {
                    rows = new List<ManagementPanelRowState>();
                    groups[branch] = rows;
                }

                var techId = Normalize(technology.Id);
                rows.Add(new ManagementPanelRowState(
                    techId,
                    technology.Name,
                    technology.Description,
                    $"研究消耗：{technology.ResearchCost}",
                    ResolveStatus(
                        techId,
                        technology.Tier,
                        plannedTechId,
                        currentTechId,
                        completedTechIds,
                        activeTechIds,
                        pendingActivationTechIds),
                    "研究",
                    technology.IconKey,
                    ResolvePrerequisiteIds(technology),
                    tier: technology.Tier));
            }

            return new ManagementPanelState("科技树", BuildGroups(groups));
        }

        private static string ResolveStatus(
            string techId,
            int tier,
            string plannedTechId,
            string currentTechId,
            ISet<string> completedTechIds,
            ISet<string> activeTechIds,
            ISet<string> pendingActivationTechIds)
        {
            if (activeTechIds.Contains(techId) ||
                completedTechIds.Contains(techId) ||
                pendingActivationTechIds.Contains(techId))
            {
                return "已研究";
            }

            if (string.Equals(techId, plannedTechId, StringComparison.Ordinal) ||
                string.Equals(techId, currentTechId, StringComparison.Ordinal))
            {
                return "研究中";
            }

            return $"第 {tier} 阶";
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

        private static string LocalizeBranch(string branch)
        {
            return Normalize(branch) switch
            {
                "economy" => "经济",
                "military" => "军事",
                "civic" => "政务",
                "industry" => "工业",
                "general" => "通用",
                _ => branch?.Trim() ?? "通用"
            };
        }

        private static IReadOnlyList<ManagementPanelGroupState> BuildGroups(Dictionary<string, List<ManagementPanelRowState>> groups)
        {
            var keys = new List<string>(groups.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            var result = new List<ManagementPanelGroupState>(keys.Count);
            for (var i = 0; i < keys.Count; i++)
            {
                result.Add(new ManagementPanelGroupState(keys[i], keys[i], groups[keys[i]]));
            }

            return result;
        }

        private static int CompareTechnologies(CatalogTechnologyDto left, CatalogTechnologyDto right)
        {
            var branchCompare = string.Compare(left?.Branch, right?.Branch, StringComparison.OrdinalIgnoreCase);
            if (branchCompare != 0)
            {
                return branchCompare;
            }

            var tierCompare = (left?.Tier ?? 0).CompareTo(right?.Tier ?? 0);
            if (tierCompare != 0)
            {
                return tierCompare;
            }

            var sortCompare = (left?.SortOrder ?? 0).CompareTo(right?.SortOrder ?? 0);
            return sortCompare != 0
                ? sortCompare
                : string.Compare(left?.Id, right?.Id, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<string> ResolvePrerequisiteIds(CatalogTechnologyDto technology)
        {
            var prerequisites = technology?.Prerequisites;
            if (prerequisites == null || prerequisites.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(prerequisites.Count);
            for (var i = 0; i < prerequisites.Count; i++)
            {
                var prerequisite = prerequisites[i];
                if (prerequisite == null || string.IsNullOrWhiteSpace(prerequisite.TargetId))
                {
                    continue;
                }

                result.Add(Normalize(prerequisite.TargetId));
            }

            return result;
        }
    }
}
