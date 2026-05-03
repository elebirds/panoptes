using Panoptes.Presentation.ViewModels;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class MinisterReportUiToolkitBinder : ManagementPanelUiToolkitBinderBase<MinisterReportViewModel>
    {
        protected override string DefaultTitle => "Minister Report";

        [Inject]
        private void ConstructVisibility(ManagementPanelVisibilityStore visibilityStore)
        {
            BindVisibility(visibilityStore, ManagementPanelId.MinisterReport);
        }
    }
}
