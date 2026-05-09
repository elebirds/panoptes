using System;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.ViewModels;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class RecipeSynthesisUiToolkitBinder : ManagementPanelUiToolkitBinderBase<RecipeSynthesisViewModel>
    {
        private RecipeSynthesisContextStore _contextStore;
        private PlanningDraftStore _planningDraftStore;
        private PlanningIntentService _planningIntentService;
        private bool _rowActionBound;

        public event Action<string, string> RecipeSelectionRequested;

        protected override string DefaultTitle => "配方";
        protected override bool UseFallbackVisualTree => true;

        [Inject]
        private void ConstructRecipeFlow(
            PlanningIntentService planningIntentService,
            ManagementPanelVisibilityStore visibilityStore,
            RecipeSynthesisContextStore contextStore,
            PlanningDraftStore planningDraftStore)
        {
            _planningIntentService = planningIntentService;
            _contextStore = contextStore;
            _planningDraftStore = planningDraftStore;
            BindVisibility(visibilityStore, ManagementPanelId.RecipeSynthesis);
            BindRowAction();
        }

        internal void RequestRecipeSelection(string recipeId)
        {
            var nodeId = _contextStore?.Current?.NodeId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(recipeId))
            {
                return;
            }

            RecipeSelectionRequested?.Invoke(nodeId, recipeId);
            var plannedRecipeId = RecipeSynthesisSelectionResolver.ResolvePlannedRecipeId(_planningDraftStore?.Snapshot, nodeId);
            if (string.Equals(recipeId.Trim(), plannedRecipeId, StringComparison.Ordinal))
            {
                _planningIntentService?.CancelBuildingRecipe(nodeId);
                return;
            }

            _planningIntentService?.SetBuildingRecipe(nodeId, recipeId);
        }

        private void BindRowAction()
        {
            if (_rowActionBound)
            {
                return;
            }

            RowActionRequested += RequestRecipeSelection;
            _rowActionBound = true;
        }
    }
}
