using System;
using Panoptes.Presentation.Binders.Ugui;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class UnitInfoReactiveBridge : IDisposable
    {
        private readonly UnitInfoDirectOrderPanelBinder _directOrderPanelBinder;
        private UnitInfoUguiBinder _binder;
        private UnitInfoViewModel _viewModel;

        public UnitInfoReactiveBridge(UnitInfoDirectOrderPanelBinder directOrderPanelBinder)
        {
            _directOrderPanelBinder = directOrderPanelBinder ?? throw new ArgumentNullException(nameof(directOrderPanelBinder));
        }

        public void SetViewModel(UnitInfoViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void Bind(
            TMP_Text unitNameText,
            TMP_Text unitDescriptionText,
            TMP_Text planningSummaryText,
            Slider hpSlider,
            TMP_Text hpValueText,
            RectTransform directOrderButtonsRoot,
            UnitInfoDirectOrderButtons directOrderButtons)
        {
            if (_viewModel == null)
            {
                return;
            }

            _binder ??= new UnitInfoUguiBinder(
                new UnitInfoUguiBinder.References(
                    unitNameText,
                    unitDescriptionText,
                    planningSummaryText,
                    hpSlider,
                    hpValueText,
                    directOrderButtonsRoot,
                    directOrderButtons),
                _directOrderPanelBinder);
            _binder.Bind(_viewModel);
        }

        public void Clear()
        {
            _viewModel?.ClearSelection();
            _binder?.Render(_viewModel?.Current);
        }

        public void SelectUnit(UnitView unit, bool actionLocked)
        {
            if (_viewModel == null || unit == null)
            {
                return;
            }

            _viewModel.SelectUnit(unit.UnitId);
            _viewModel.SetActionLocked(actionLocked);
        }

        public bool TryRender(UnitView unit, bool actionLocked)
        {
            if (_viewModel == null || unit == null)
            {
                return false;
            }

            SelectUnit(unit, actionLocked);
            var state = _viewModel.Current;
            if (state == null ||
                !state.HasSelection ||
                !string.Equals(state.UnitId, unit.UnitId, StringComparison.Ordinal))
            {
                return false;
            }

            _binder?.Render(state);
            return true;
        }

        public void Unbind()
        {
            _binder?.Unbind();
        }

        public void Dispose()
        {
            _binder?.Dispose();
            _binder = null;
        }
    }
}
