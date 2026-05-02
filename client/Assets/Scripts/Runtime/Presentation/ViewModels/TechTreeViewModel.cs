using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class TechTreeViewModel : ManagementPanelViewModelBase
    {
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly StaticCatalogStore _staticCatalogStore;

        public TechTreeViewModel(StaticCatalogStore staticCatalogStore, PlanningDraftStore planningDraftStore)
        {
            _staticCatalogStore = staticCatalogStore ?? throw new ArgumentNullException(nameof(staticCatalogStore));
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            AddSubscription(_staticCatalogStore.State.Subscribe(this, static (_, self) => self.Publish()));
            AddSubscription(_planningDraftStore.State.Subscribe(this, static (_, self) => self.Publish()));
            Publish();
        }

        protected override ManagementPanelState Project()
        {
            var catalog = _staticCatalogStore.Snapshot;
            if (catalog?.Technologies == null || catalog.Technologies.Count == 0)
            {
                return new ManagementPanelState("Tech Tree");
            }

            var plannedTechId = Normalize(_planningDraftStore.Snapshot.PlannedResearchTargetTechnologyId);
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

                var branch = string.IsNullOrWhiteSpace(technology.Branch) ? "General" : technology.Branch.Trim();
                if (!groups.TryGetValue(branch, out var rows))
                {
                    rows = new List<ManagementPanelRowState>();
                    groups[branch] = rows;
                }

                var techId = Normalize(technology.Id);
                var status = string.Equals(techId, plannedTechId, StringComparison.Ordinal)
                    ? "Planned research"
                    : $"Tier {technology.Tier}";
                rows.Add(new ManagementPanelRowState(
                    techId,
                    technology.Name,
                    technology.Description,
                    $"Cost {technology.ResearchCost}",
                    status,
                    "Research"));
            }

            return new ManagementPanelState("Tech Tree", BuildGroups(groups));
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
    }
}
