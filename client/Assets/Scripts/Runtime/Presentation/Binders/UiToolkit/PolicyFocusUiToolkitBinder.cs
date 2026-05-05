using System;
using Panoptes.Core.Application.Services;
using Panoptes.Presentation.ViewModels;
using UnityEngine;
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
                if (_gameIntentService?.SetInstitutionLoadout(policyId) != true)
                {
                    return;
                }

                MarkPolicySelected(policyId, institutionPolicy: true);
                _closeAllowedAfterSelection = true;
                ApplyCloseButtonVisibility();
                return;
            }

            if (_gameIntentService?.SetPolicy(policyId) != true)
            {
                return;
            }

            MarkPolicySelected(policyId, institutionPolicy: false);
            _closeAllowedAfterSelection = true;
            ApplyCloseButtonVisibility();
        }

        private void MarkPolicySelected(string policyId, bool institutionPolicy)
        {
            var root = GetComponent<UIDocument>()?.rootVisualElement;
            if (root == null)
            {
                return;
            }

            var rows = institutionPolicy ? GetInstitutionPolicyRows() : GetNationalPolicyRows();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = root.Q<VisualElement>("management-panel-row-" + SafeName(rows[i]?.Id));
                if (row != null)
                {
                    ApplyNeutralPolicyPalette(row);
                }
            }

            var selected = root.Q<VisualElement>("management-panel-row-" + SafeName(policyId));
            if (selected != null)
            {
                ApplySelectedPolicyPalette(selected);
            }
        }

        private static void ApplySelectedPolicyPalette(VisualElement row)
        {
            row.style.backgroundColor = new Color(0.055f, 0.12f, 0.28f, 0.98f);
            row.style.borderBottomColor = new Color(0.24f, 0.56f, 1f, 0.95f);
            row.style.borderLeftColor = new Color(0.24f, 0.56f, 1f, 0.95f);
            row.style.borderRightColor = new Color(0.24f, 0.56f, 1f, 0.95f);
            row.style.borderTopColor = new Color(0.24f, 0.56f, 1f, 0.95f);
            row.style.borderBottomWidth = 2f;
            row.style.borderLeftWidth = 2f;
            row.style.borderRightWidth = 2f;
            row.style.borderTopWidth = 2f;
        }

        private static void ApplyNeutralPolicyPalette(VisualElement row)
        {
            row.style.backgroundColor = new Color(0.10f, 0.07f, 0.055f, 0.96f);
            row.style.borderBottomColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            row.style.borderLeftColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            row.style.borderRightColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            row.style.borderTopColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            row.style.borderBottomWidth = 1f;
            row.style.borderLeftWidth = 1f;
            row.style.borderRightWidth = 1f;
            row.style.borderTopWidth = 1f;
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

        private System.Collections.Generic.IReadOnlyList<ManagementPanelRowState> GetNationalPolicyRows()
        {
            return GetPolicyRows("national");
        }

        private System.Collections.Generic.IReadOnlyList<ManagementPanelRowState> GetInstitutionPolicyRows()
        {
            return GetPolicyRows("institution");
        }

        private System.Collections.Generic.IReadOnlyList<ManagementPanelRowState> GetPolicyRows(string groupId)
        {
            var groups = _viewModel?.Current?.Groups;
            if (groups == null)
            {
                return Array.Empty<ManagementPanelRowState>();
            }

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                if (group?.Rows != null &&
                    string.Equals(group.Id, groupId, StringComparison.OrdinalIgnoreCase))
                {
                    return group.Rows;
                }
            }

            return Array.Empty<ManagementPanelRowState>();
        }

        private static string SafeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant().Replace(' ', '-');
        }
    }
}
