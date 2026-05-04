using Panoptes.Core.Application.Services;
using Panoptes.Presentation.ViewModels;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class PolicyFocusUiToolkitBinder : ManagementPanelUiToolkitBinderBase<PolicyFocusViewModel>
    {
        private GameIntentService _gameIntentService;
        private bool _rowActionSubscribed;

        protected override string DefaultTitle => "Policy Focus";
        protected override bool AllowFallbackTree => false;

        [Inject]
        private void Construct(GameIntentService gameIntentService)
        {
            _gameIntentService = gameIntentService;
            if (!_rowActionSubscribed)
            {
                RowActionRequested += OnRowActionRequested;
                _rowActionSubscribed = true;
            }
        }

        private void OnRowActionRequested(string policyId)
        {
            _gameIntentService?.SetPolicy(policyId);
        }
    }
}
