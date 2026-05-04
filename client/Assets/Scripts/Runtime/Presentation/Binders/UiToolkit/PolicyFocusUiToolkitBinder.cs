using System;
using Panoptes.Core.Application.Services;
using Panoptes.Presentation.ViewModels;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class PolicyFocusUiToolkitBinder : ManagementPanelUiToolkitBinderBase<PolicyFocusViewModel>
    {
        private GameIntentService _gameIntentService;
        private PolicyFocusViewModel _viewModel;

        protected override string DefaultTitle => "Policy Focus";
        protected override bool AllowFallbackTree => false;

        [Inject]
        private void ConstructPolicyFlow(
            GameIntentService gameIntentService,
            PolicyFocusViewModel viewModel,
            ManagementPanelVisibilityStore visibilityStore)
        {
            _gameIntentService = gameIntentService;
            _viewModel = viewModel;
            BindVisibility(visibilityStore, ManagementPanelId.PolicyFocus);
            RowActionRequested -= RequestPolicyAction;
            RowActionRequested += RequestPolicyAction;
        }

        private void RequestPolicyAction(string policyId)
        {
            if (string.IsNullOrWhiteSpace(policyId))
            {
                return;
            }

            if (IsInstitutionPolicy(policyId))
            {
                _gameIntentService?.SetInstitutionLoadout(policyId);
                return;
            }

            _gameIntentService?.SetPolicy(policyId);
        }

        private bool IsInstitutionPolicy(string policyId)
        {
            var groups = _viewModel?.Current?.Groups;
            if (groups == null)
            {
                return false;
            }

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                if (group?.Rows == null ||
                    !string.Equals(group.Id, "institution", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                for (var rowIndex = 0; rowIndex < group.Rows.Count; rowIndex++)
                {
                    if (string.Equals(group.Rows[rowIndex]?.Id, policyId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
