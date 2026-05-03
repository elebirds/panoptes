using Panoptes.Presentation.ViewModels;
using Panoptes.Core.Application.Services;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class TechTreeUiToolkitBinder : ManagementPanelUiToolkitBinderBase<TechTreeViewModel>
    {
        private GameIntentService _gameIntentService;

        protected override string DefaultTitle => "Tech Tree";

        [Inject]
        private void ConstructTechFlow(GameIntentService gameIntentService, ManagementPanelVisibilityStore visibilityStore)
        {
            _gameIntentService = gameIntentService;
            BindVisibility(visibilityStore, ManagementPanelId.TechTree);
            RowActionRequested -= RequestResearchTarget;
            RowActionRequested += RequestResearchTarget;
        }

        private void RequestResearchTarget(string technologyId)
        {
            _gameIntentService?.SetResearchTarget(technologyId);
        }
    }
}
