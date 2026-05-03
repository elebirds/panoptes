using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class RecipeSynthesisViewModel : ManagementPanelViewModelBase
    {
        private readonly RecipeSynthesisContextStore _contextStore;
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly StaticCatalogStore _staticCatalogStore;

        public RecipeSynthesisViewModel(
            StaticCatalogStore staticCatalogStore,
            PlanningDraftStore planningDraftStore,
            RecipeSynthesisContextStore contextStore)
        {
            _staticCatalogStore = staticCatalogStore ?? throw new ArgumentNullException(nameof(staticCatalogStore));
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            _contextStore = contextStore ?? throw new ArgumentNullException(nameof(contextStore));
            AddSubscription(_staticCatalogStore.State.Subscribe(this, static (_, self) => self.Publish()));
            AddSubscription(_planningDraftStore.State.Subscribe(this, static (_, self) => self.Publish()));
            AddSubscription(_contextStore.State.Subscribe(this, static (_, self) => self.Publish()));
            Publish();
        }

        protected override ManagementPanelState Project()
        {
            var context = _contextStore.Current;
            if (context == null || !context.HasContext)
            {
                return new ManagementPanelState("Recipe Synthesis");
            }

            var catalog = _staticCatalogStore.Snapshot;
            if (catalog?.Recipes == null || catalog.Recipes.Count == 0)
            {
                return new ManagementPanelState("Recipe Synthesis");
            }

            var contextNodeId = Normalize(context.NodeId);
            var contextBuildingTypeId = Normalize(context.BuildingTypeId);
            var draft = _planningDraftStore.Snapshot;
            var selectedRecipeId = ResolveSelectedRecipeId(draft, contextNodeId);
            var preview = ResolvePreview(draft, contextNodeId);
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
                if (!string.Equals(buildingId, contextBuildingTypeId, StringComparison.Ordinal))
                {
                    continue;
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
                    ResolveStatus(recipeId, selectedRecipeId, preview),
                    "Select"));
            }

            return new ManagementPanelState("Recipe Synthesis", BuildGroups(groups));
        }

        private static RecipePreviewDto ResolvePreview(PlanningDraftState draft, string contextNodeId)
        {
            var preview = draft?.CurrentRecipePreview;
            if (preview == null || !string.Equals(Normalize(preview.NodeId), contextNodeId, StringComparison.Ordinal))
            {
                return null;
            }

            return preview;
        }

        private static string ResolveSelectedRecipeId(PlanningDraftState draft, string contextNodeId)
        {
            var selections = draft?.RecipeSelections;
            if (selections == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < selections.Count; i++)
            {
                if (!string.Equals(Normalize(selections[i]?.NodeId), contextNodeId, StringComparison.Ordinal))
                {
                    continue;
                }

                var recipeId = Normalize(selections[i]?.RecipeId);
                if (!string.IsNullOrEmpty(recipeId))
                {
                    return recipeId;
                }
            }

            return string.Empty;
        }

        private static string ResolveStatus(string recipeId, string selectedRecipeId, RecipePreviewDto preview)
        {
            if (string.Equals(recipeId, selectedRecipeId, StringComparison.Ordinal))
            {
                return "Selected";
            }

            if (preview == null || !string.Equals(recipeId, Normalize(preview.RecipeId), StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(preview.ErrorCode))
            {
                return $"Preview: {preview.ErrorCode.Trim()}";
            }

            return preview.Valid ? "Preview valid" : "Preview";
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
