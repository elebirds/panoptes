using System;
using Panoptes.Core.Application.Services;
using Panoptes.Presentation.ViewModels;
using UnityEngine.UIElements;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class PolicyFocusUiToolkitBinder : ManagementPanelUiToolkitBinderBase<PolicyFocusViewModel>
    {
        private GameIntentService _gameIntentService;
        private PolicyFocusViewModel _viewModel;
        private bool _closeAllowedAfterSelection;

        protected override string DefaultTitle => "国策";
        protected override bool UseFallbackVisualTree => true;

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
            _closeAllowedAfterSelection = HasSelectedPolicy();
            ApplyCloseButtonVisibility();
        }

        private void LateUpdate()
        {
            ApplyCloseButtonVisibility();
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
                _closeAllowedAfterSelection = true;
                ApplyCloseButtonVisibility();
                return;
            }

            _gameIntentService?.SetPolicy(policyId);
            _closeAllowedAfterSelection = true;
            ApplyCloseButtonVisibility();
        }

        private void ApplyCloseButtonVisibility()
        {
            var root = GetComponent<UIDocument>()?.rootVisualElement;
            var close = root?.Q<Button>(ManagementPanelUiToolkitRenderer.CloseButtonName);
            if (close == null)
            {
                return;
            }

            close.style.display = (_closeAllowedAfterSelection || HasSelectedPolicy())
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private bool HasSelectedPolicy()
        {
            var groups = _viewModel?.Current?.Groups;
            if (groups == null)
            {
                return false;
            }

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var rows = groups[groupIndex]?.Rows;
                if (rows == null)
                {
                    continue;
                }

                for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    if (!string.IsNullOrWhiteSpace(rows[rowIndex]?.Status))
                    {
                        return true;
                    }
                }
            }

            return false;
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
