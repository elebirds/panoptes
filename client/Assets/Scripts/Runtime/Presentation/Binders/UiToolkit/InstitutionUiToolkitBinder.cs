using Panoptes.Core.Application.Services;
using Panoptes.Presentation.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class InstitutionUiToolkitBinder : ManagementPanelUiToolkitBinderBase<InstitutionViewModel>
    {
        private GameIntentService _gameIntentService;
        private InstitutionViewModel _viewModel;

        protected override string DefaultTitle => "制度";
        protected override bool UseFallbackVisualTree => true;

        [Inject]
        private void ConstructInstitutionFlow(
            GameIntentService gameIntentService,
            InstitutionViewModel viewModel,
            ManagementPanelVisibilityStore visibilityStore)
        {
            _gameIntentService = gameIntentService;
            _viewModel = viewModel;
            BindVisibility(visibilityStore, ManagementPanelId.Institutions);
            RowActionRequested -= RequestInstitutionAction;
            RowActionRequested += RequestInstitutionAction;
        }

        private void RequestInstitutionAction(string institutionId)
        {
            if (string.IsNullOrWhiteSpace(institutionId))
            {
                return;
            }

            var loadout = _viewModel?.BuildLoadoutForSelection(institutionId);
            if (loadout == null || loadout.Count == 0)
            {
                return;
            }

            if (_gameIntentService?.SetInstitutionLoadout(ToArray(loadout)) != true)
            {
                return;
            }

            MarkInstitutionSelected(institutionId);
        }

        private void MarkInstitutionSelected(string institutionId)
        {
            var root = GetComponent<UIDocument>()?.rootVisualElement;
            var selected = root?.Q<VisualElement>("management-panel-row-" + SafeName(institutionId));
            if (selected == null)
            {
                return;
            }

            selected.style.borderBottomColor = new Color(0.24f, 0.56f, 1f, 0.95f);
            selected.style.borderLeftColor = new Color(0.24f, 0.56f, 1f, 0.95f);
            selected.style.borderRightColor = new Color(0.24f, 0.56f, 1f, 0.95f);
            selected.style.borderTopColor = new Color(0.24f, 0.56f, 1f, 0.95f);
            selected.style.borderBottomWidth = 2f;
            selected.style.borderLeftWidth = 2f;
            selected.style.borderRightWidth = 2f;
            selected.style.borderTopWidth = 2f;
        }

        private static string[] ToArray(System.Collections.Generic.IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                result[i] = source[i];
            }

            return result;
        }

        private static string SafeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant().Replace(' ', '-');
        }
    }
}
