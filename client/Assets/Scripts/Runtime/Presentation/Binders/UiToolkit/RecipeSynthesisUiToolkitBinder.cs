using System;
using Panoptes.Core.Application.Services;
using Panoptes.Presentation.ViewModels;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class RecipeSynthesisUiToolkitBinder : ManagementPanelUiToolkitBinderBase<RecipeSynthesisViewModel>
    {
        private RecipeSynthesisContextStore _contextStore;
        private PlanningIntentService _planningIntentService;
        private bool _rowActionBound;

        public event Action<string, string> RecipeSelectionRequested;

        protected override string DefaultTitle => "配方";
        protected override bool UseFallbackVisualTree => true;

        [Inject]
        private void ConstructRecipeFlow(
            PlanningIntentService planningIntentService,
            ManagementPanelVisibilityStore visibilityStore,
            RecipeSynthesisContextStore contextStore)
        {
            _planningIntentService = planningIntentService;
            _contextStore = contextStore;
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
