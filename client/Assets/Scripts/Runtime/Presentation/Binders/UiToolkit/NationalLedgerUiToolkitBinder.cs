using Panoptes.Presentation.ViewModels;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class NationalLedgerUiToolkitBinder : ManagementPanelUiToolkitBinderBase<NationalLedgerViewModel>
    {
        protected override string DefaultTitle => "National Ledger";

        [Inject]
        private void ConstructVisibility(ManagementPanelVisibilityStore visibilityStore)
        {
            BindVisibility(visibilityStore, ManagementPanelId.NationalLedger);
        }
    }
}
