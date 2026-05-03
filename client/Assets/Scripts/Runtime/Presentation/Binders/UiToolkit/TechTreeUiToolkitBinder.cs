using Panoptes.Presentation.ViewModels;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class TechTreeUiToolkitBinder : ManagementPanelUiToolkitBinderBase<TechTreeViewModel>
    {
        protected override string DefaultTitle => "Tech Tree";

        [Inject]
        private void ConstructVisibility(ManagementPanelVisibilityStore visibilityStore)
        {
            BindVisibility(visibilityStore, ManagementPanelId.TechTree);
        }
    }
}
