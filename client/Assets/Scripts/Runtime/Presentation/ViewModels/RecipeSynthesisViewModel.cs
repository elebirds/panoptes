using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class RecipeSynthesisViewModel : ManagementPanelViewModelBase
    {
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly StaticCatalogStore _staticCatalogStore;

        public RecipeSynthesisViewModel(StaticCatalogStore staticCatalogStore, PlanningDraftStore planningDraftStore)
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
            if (catalog?.Recipes == null || catalog.Recipes.Count == 0)
            {
                return new ManagementPanelState("Recipe Synthesis");
            }

            var selectedRecipes = BuildSelectedRecipes(_planningDraftStore.Snapshot);
            var recipes = new List<CatalogRecipeDto>(catalog.Recipes.Values);
            recipes.Sort(CompareRecipes);

            var groups = new Dictionary<string, List<ManagementPanelRowState>>(StringComparer.Ordinal);
            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id))
                {
                    continue;
                }

                var buildingId = Normalize(recipe.BuildingId);
                if (string.IsNullOrEmpty(buildingId))
                {
                    buildingId = "general";
                }

                if (!groups.TryGetValue(buildingId, out var rows))
                {
                    rows = new List<ManagementPanelRowState>();
                    groups[buildingId] = rows;
                }

                var recipeId = Normalize(recipe.Id);
                rows.Add(new ManagementPanelRowState(
                    recipeId,
                    recipe.Name,
                    recipe.Description,
                    $"Work {recipe.WorkAmount}, base {recipe.BaseProgress}",
                    selectedRecipes.Contains(recipeId) ? "Selected" : string.Empty,
                    "Select"));
            }

            return new ManagementPanelState("Recipe Synthesis", BuildGroups(groups));
        }

        private static HashSet<string> BuildSelectedRecipes(PlanningDraftState draft)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var previewRecipeId = Normalize(draft?.CurrentRecipePreview?.RecipeId);
            if (!string.IsNullOrEmpty(previewRecipeId))
            {
                result.Add(previewRecipeId);
            }

            var selections = draft?.RecipeSelections;
            if (selections == null)
            {
                return result;
            }

            for (var i = 0; i < selections.Count; i++)
            {
                var recipeId = Normalize(selections[i]?.RecipeId);
                if (!string.IsNullOrEmpty(recipeId))
                {
                    result.Add(recipeId);
                }
            }

            return result;
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

        private static int CompareRecipes(CatalogRecipeDto left, CatalogRecipeDto right)
        {
            var buildingCompare = string.Compare(left?.BuildingId, right?.BuildingId, StringComparison.OrdinalIgnoreCase);
            if (buildingCompare != 0)
            {
                return buildingCompare;
            }

            var sortCompare = (left?.SortOrder ?? 0).CompareTo(right?.SortOrder ?? 0);
            return sortCompare != 0
                ? sortCompare
                : string.Compare(left?.Id, right?.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
}
