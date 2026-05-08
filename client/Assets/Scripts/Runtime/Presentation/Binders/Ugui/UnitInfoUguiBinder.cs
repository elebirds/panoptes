using System;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.UI.Common;
using Panoptes.Presentation.ViewModels;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.Binders.Ugui
{
    public sealed class UnitInfoUguiBinder : IBinder<UnitInfoViewModel>, IDisposable
    {
        private readonly References _references;
        private readonly UnitInfoDirectOrderPanelBinder _directOrderPanelBinder;
        private IDisposable _subscription;
        private UnitInfoViewModel _viewModel;

        public UnitInfoUguiBinder(References references, UnitInfoDirectOrderPanelBinder directOrderPanelBinder)
        {
            _references = references;
            _directOrderPanelBinder = directOrderPanelBinder ?? throw new ArgumentNullException(nameof(directOrderPanelBinder));
        }

        public void Bind(UnitInfoViewModel viewModel)
        {
            if (ReferenceEquals(_viewModel, viewModel))
            {
                Render(viewModel?.Current);
                return;
            }

            Unbind();
            _viewModel = viewModel;
            if (_viewModel == null)
            {
                Render(null);
                return;
            }

            _subscription = _viewModel.State.Subscribe(this, static (state, self) => self.Render(state));
            Render(_viewModel.Current);
        }

        public void Unbind()
        {
            _subscription?.Dispose();
            _subscription = null;
            _viewModel = null;
        }

        public void Dispose()
        {
            Unbind();
        }

        public void Render(UnitInfoState state)
        {
            if (state == null || !state.HasSelection)
            {
                SetText(_references.UnitNameText, string.Empty);
                SetText(_references.UnitDescriptionText, string.Empty);
                SetActive(_references.UnitDescriptionText, false);
                SetText(_references.PlanningSummaryText, string.Empty);
                SetActive(_references.PlanningSummaryText, false);
                SetIcon(_references.UnitIcon, null);
                UnitInfoHpBinder.Apply(_references.HpSlider, _references.HpValueText, new UnitInfoHpState(0, 1));
                _directOrderPanelBinder.ApplyState(
                    _references.DirectOrderButtonsRoot,
                    _references.DirectOrderButtons,
                    visible: false,
                    default,
                    actionLocked: false);
                return;
            }

            SetText(_references.UnitNameText, state.DisplayName);
            SetText(_references.UnitDescriptionText, state.Description);
            SetActive(_references.UnitDescriptionText, state.DescriptionVisible);
            SetText(_references.PlanningSummaryText, state.PlanningSummary);
            SetActive(_references.PlanningSummaryText, state.PlanningSummaryVisible);
            SetIcon(_references.UnitIcon, UiIconLoader.LoadSprite(
                state.IconKey,
                state.UnitType,
                _references.UnitIconResourcesRoot,
                _references.BuildingIconResourcesRoot,
                _references.ResourceIconResourcesRoot));
            UnitInfoHpBinder.Apply(_references.HpSlider, _references.HpValueText, new UnitInfoHpState(state.Hp, state.MaxHp));
            _directOrderPanelBinder.ApplyState(
                _references.DirectOrderButtonsRoot,
                _references.DirectOrderButtons,
                state.ShowDirectOrderButtons,
                new UnitInfoDirectOrderState(state.CanMove, state.IsMilitaryUnit, state.CanAttack, state.CanCharge),
                state.ActionLocked);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null)
            {
                component.gameObject.SetActive(active);
            }
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.color = sprite == null ? new Color(0.3f, 0.3f, 0.3f, 1f) : Color.white;
            image.enabled = true;
        }

        public readonly struct References
        {
            public readonly UnitInfoDirectOrderButtons DirectOrderButtons;
            public readonly RectTransform DirectOrderButtonsRoot;
            public readonly string BuildingIconResourcesRoot;
            public readonly Slider HpSlider;
            public readonly TMP_Text HpValueText;
            public readonly TMP_Text PlanningSummaryText;
            public readonly string ResourceIconResourcesRoot;
            public readonly Image UnitIcon;
            public readonly string UnitIconResourcesRoot;
            public readonly TMP_Text UnitDescriptionText;
            public readonly TMP_Text UnitNameText;

            public References(
                TMP_Text unitNameText,
                TMP_Text unitDescriptionText,
                TMP_Text planningSummaryText,
                Slider hpSlider,
                TMP_Text hpValueText,
                RectTransform directOrderButtonsRoot,
                UnitInfoDirectOrderButtons directOrderButtons,
                Image unitIcon = null,
                string unitIconResourcesRoot = "Icons/Units",
                string buildingIconResourcesRoot = "Icons/Buildings",
                string resourceIconResourcesRoot = "Icons/Resources")
            {
                BuildingIconResourcesRoot = buildingIconResourcesRoot;
                DirectOrderButtons = directOrderButtons;
                DirectOrderButtonsRoot = directOrderButtonsRoot;
                HpSlider = hpSlider;
                HpValueText = hpValueText;
                PlanningSummaryText = planningSummaryText;
                ResourceIconResourcesRoot = resourceIconResourcesRoot;
                UnitIcon = unitIcon;
                UnitIconResourcesRoot = unitIconResourcesRoot;
                UnitDescriptionText = unitDescriptionText;
                UnitNameText = unitNameText;
            }
        }
    }
}
