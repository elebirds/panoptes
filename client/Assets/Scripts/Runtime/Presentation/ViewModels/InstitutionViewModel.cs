using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class InstitutionViewModel : ManagementPanelViewModelBase
    {
        private readonly GameStateCache _gameStateCache;
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly StaticCatalogStore _staticCatalogStore;

        public InstitutionViewModel(
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

        public IReadOnlyList<string> BuildLoadoutForSelection(string institutionId)
        {
            var snapshot = _staticCatalogStore.Snapshot;
            var current = GetCurrentLoadout();
            var institution = snapshot.Institutions != null && snapshot.Institutions.TryGetValue(Normalize(institutionId), out var value)
                ? value
                : null;
            if (institution == null || string.IsNullOrWhiteSpace(institution.Id))
            {
                return current;
            }

            var result = new List<string>();
            var seenCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < current.Count; i++)
            {
                var currentId = Normalize(current[i]);
                if (string.IsNullOrEmpty(currentId) || !snapshot.Institutions.TryGetValue(currentId, out var currentInstitution))
                {
                    continue;
                }

                if (string.Equals(currentInstitution.Category, institution.Category, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (seenCategories.Add(Normalize(currentInstitution.Category)))
                {
                    result.Add(currentInstitution.Id);
                }
            }

            result.Add(institution.Id);
            return result;
        }

        protected override ManagementPanelState Project()
        {
            var catalog = _staticCatalogStore.Snapshot;
            if (catalog.Institutions == null || catalog.Institutions.Count == 0)
            {
                return new ManagementPanelState("制度");
            }

            var categoryMap = catalog.InstitutionCategories ?? new Dictionary<string, CatalogInstitutionCategoryDto>();
            var categories = categoryMap.Values.OrderBy(category => category.SortOrder).ThenBy(category => category.Id, StringComparer.OrdinalIgnoreCase).ToList();
            var draft = _planningDraftStore.Snapshot;
            var plannedLoadout = BuildIdSet(draft.PlannedInstitutionIds);
            var activeLoadout = BuildIdSet(_gameStateCache?.GetInstitutionState()?.ActiveInstitutionIds);
            var candidateLoadout = BuildIdSet(_gameStateCache?.GetInstitutionState()?.CandidateInstitutionIds);

            var groups = new List<ManagementPanelGroupState>();
            for (var i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                var rows = BuildCategoryRows(category.Id, catalog, plannedLoadout, activeLoadout, candidateLoadout);
                if (rows.Count == 0)
                {
                    continue;
                }

                groups.Add(new ManagementPanelGroupState(
                    category.Id,
                    category.Name,
                    rows));
            }

            return new ManagementPanelState("制度", groups);
        }

        private static List<ManagementPanelRowState> BuildCategoryRows(
            string categoryId,
            StaticCatalogState catalog,
            HashSet<string> plannedLoadout,
            HashSet<string> activeLoadout,
            HashSet<string> candidateLoadout)
        {
            var rows = new List<ManagementPanelRowState>();
            foreach (var pair in catalog.Institutions)
            {
                var institution = pair.Value;
                if (institution == null || !string.Equals(institution.Category, categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var institutionId = Normalize(institution.Id);
                var visible = candidateLoadout.Count == 0 || candidateLoadout.Contains(institutionId) || plannedLoadout.Contains(institutionId) || activeLoadout.Contains(institutionId);
                if (!visible)
                {
                    continue;
                }

                var planned = plannedLoadout.Contains(institutionId);
                var active = activeLoadout.Contains(institutionId);
                rows.Add(new ManagementPanelRowState(
                    institutionId,
                    institution.Name,
                    ManagementEffectSummaryFormatter.Build(institution.ModifierEffects, institution.Description, catalog),
                    institution.Description,
                    planned ? "已规划" : active ? "已生效" : string.Empty,
                    "设置",
                    institution.IconKey));
            }

            rows.Sort((left, right) => string.Compare(left?.Id, right?.Id, StringComparison.OrdinalIgnoreCase));
            return rows;
        }

        private IReadOnlyList<string> GetCurrentLoadout()
        {
            var planned = _planningDraftStore.Snapshot.PlannedInstitutionIds;
            if (planned != null && planned.Count > 0)
            {
                return planned;
            }

            var active = _gameStateCache?.GetInstitutionState()?.ActiveInstitutionIds;
            return active != null ? active : Array.Empty<string>();
        }

        private static HashSet<string> BuildIdSet(IReadOnlyList<string> ids)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
