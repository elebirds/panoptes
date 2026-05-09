using System;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.ViewModels;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class RecipeSynthesisUiToolkitBinder : ManagementPanelUiToolkitBinderBase<RecipeSynthesisViewModel>
    {
        private RecipeSynthesisContextStore _contextStore;
        private GameStateStore _gameStateStore;
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
            PlanningDraftStore planningDraftStore,
            GameStateStore gameStateStore)
        {
            _planningIntentService = planningIntentService;
            _contextStore = contextStore;
            _planningDraftStore = planningDraftStore;
            _gameStateStore = gameStateStore;
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

            var game = _gameStateStore?.Snapshot;
            if (game == null || game.IsGameOver || !GamePhases.IsPlanning(game.Phase))
            {
                return;
            }

            RecipeSelectionRequested?.Invoke(nodeId, recipeId);
            var selectedRecipeId = RecipeSynthesisSelectionResolver.ResolveSelectedRecipeId(
                _planningDraftStore?.Snapshot,
                game,
                nodeId);
            if (string.Equals(recipeId.Trim(), selectedRecipeId, StringComparison.Ordinal))
            {
                if (_planningIntentService?.CancelBuildingRecipe(nodeId) == true)
                {
                    _planningDraftStore?.ApplyRecipeSelection(nodeId, string.Empty);
                }
                return;
            }

            var normalizedRecipeId = recipeId.Trim();
            if (_planningIntentService?.SetBuildingRecipe(nodeId, normalizedRecipeId) == true)
            {
                _planningDraftStore?.ApplyRecipeSelection(nodeId, normalizedRecipeId);
            }
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
