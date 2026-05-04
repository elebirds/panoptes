using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class UnitInfoDefaultLayoutBuilder
    {
        public sealed class References<TSlot>
            where TSlot : UnitInfoActionButtonSlot
        {
            public RectTransform PanelRoot;
            public Image PanelBackground;
            public Image UnitIcon;
            public RawImage UnitPortraitRawImage;
            public TMP_Text UnitNameText;
            public TMP_Text UnitDescriptionText;
            public TMP_Text PlanningSummaryText;
            public Slider HpSlider;
            public TMP_Text HpValueText;
            public RectTransform ActionButtonsRoot;
            public RectTransform DirectOrderButtonsRoot;
            public TSlot[] ActionButtons;
            public Button MoveButton;
            public Button AttackButton;
            public Button HoldButton;
            public Button ChargeButton;
        }

        public References<TSlot> EnsureLayout<TSlot>(
            References<TSlot> references,
            Vector2 defaultActionButtonSize,
            Color defaultActionButtonColor,
            UnitInfoActionListBinder actionListBinder,
            UnitInfoDirectOrderPanelBinder directOrderPanelBinder,
            Func<TSlot> createActionButtonSlot)
            where TSlot : UnitInfoActionButtonSlot
        {
            references ??= new References<TSlot>();
            if (references.PanelRoot == null)
            {
                return references;
            }

            if (HasDefaultCoreLayout(references))
            {
                references.UnitPortraitRawImage = EnsurePortraitRawImage(
                    references.PanelRoot,
                    references.UnitIcon,
                    references.UnitPortraitRawImage);
                references.UnitDescriptionText = EnsureUnitDescriptionText(
                    references.PanelRoot,
                    references.UnitDescriptionText);
                references.PlanningSummaryText = EnsurePlanningSummaryText(
                    references.PanelRoot,
                    references.PlanningSummaryText);
                return references;
            }

            EnsureParentCanvas(references.PanelRoot);
            ConfigurePanelRoot(references.PanelRoot);
            references.PanelBackground = EnsureBackground(references.PanelRoot, references.PanelBackground);
            references.ActionButtonsRoot = EnsureActionButtonsRoot(references.PanelRoot, references.ActionButtonsRoot);
            references.DirectOrderButtonsRoot = EnsureDirectOrderButtonsRoot(
                references.PanelRoot,
                references.DirectOrderButtonsRoot);
            references.UnitIcon = EnsureUnitIcon(references.PanelRoot, references.UnitIcon);
            references.UnitPortraitRawImage = EnsurePortraitRawImage(
                references.PanelRoot,
                references.UnitIcon,
                references.UnitPortraitRawImage);
            references.UnitNameText = EnsureUnitNameText(references.PanelRoot, references.UnitNameText);
            references.UnitDescriptionText = EnsureUnitDescriptionText(
                references.PanelRoot,
                references.UnitDescriptionText);
            references.PlanningSummaryText = EnsurePlanningSummaryText(
                references.PanelRoot,
                references.PlanningSummaryText);
            references.HpSlider = EnsureHpSlider(references.PanelRoot, references.HpSlider);
            references.HpValueText = EnsureHpValueText(references.PanelRoot, references.HpValueText);

            if ((references.ActionButtons == null || references.ActionButtons.Length == 0) &&
                actionListBinder != null)
            {
                references.ActionButtons = actionListBinder.EnsureDefaultSlots(
                    references.ActionButtons,
                    references.ActionButtonsRoot,
                    defaultActionButtonSize,
                    defaultActionButtonColor,
                    createActionButtonSlot);
            }

            if (directOrderPanelBinder != null)
            {
                var directOrderButtons = directOrderPanelBinder.EnsureButtons(
                    references.DirectOrderButtonsRoot,
                    references.MoveButton,
                    references.AttackButton,
                    references.HoldButton,
                    references.ChargeButton,
                    defaultActionButtonColor);
                references.MoveButton = directOrderButtons.Move;
                references.AttackButton = directOrderButtons.Attack;
                references.HoldButton = directOrderButtons.Hold;
                references.ChargeButton = directOrderButtons.Charge;
            }

            if (references.UnitPortraitRawImage != null)
            {
                references.UnitPortraitRawImage.enabled = false;
            }

            return references;
        }

        public static RawImage EnsurePortraitRawImage(
            RectTransform panelRoot,
            Image unitIcon,
            RawImage unitPortraitRawImage)
        {
            if (panelRoot == null)
            {
                return unitPortraitRawImage;
            }

            RectTransform portraitRect;
            if (unitPortraitRawImage == null)
            {
                portraitRect = panelRoot.Find("UnitPortrait") as RectTransform;
                if (portraitRect == null)
                {
                    portraitRect = EnsureChild(panelRoot, "UnitPortrait");
                }

                unitPortraitRawImage = portraitRect.GetComponent<RawImage>();
                if (unitPortraitRawImage == null)
                {
                    unitPortraitRawImage = portraitRect.gameObject.AddComponent<RawImage>();
                }
            }

            if (unitPortraitRawImage == null)
            {
                return null;
            }

            portraitRect = unitPortraitRawImage.rectTransform;
            if (unitIcon != null)
            {
                var iconRect = unitIcon.rectTransform;
                portraitRect.anchorMin = iconRect.anchorMin;
                portraitRect.anchorMax = iconRect.anchorMax;
                portraitRect.pivot = iconRect.pivot;
                portraitRect.anchoredPosition = iconRect.anchoredPosition;
                portraitRect.sizeDelta = iconRect.sizeDelta;

                var maxSibling = Mathf.Max(0, panelRoot.childCount - 1);
                var targetSibling = Mathf.Clamp(iconRect.GetSiblingIndex() + 1, 0, maxSibling);
                portraitRect.SetSiblingIndex(targetSibling);
            }
            else
            {
                portraitRect.anchorMin = new Vector2(0f, 0f);
                portraitRect.anchorMax = new Vector2(0f, 0f);
                portraitRect.pivot = new Vector2(0f, 0f);
                portraitRect.anchoredPosition = new Vector2(14f, 14f);
                portraitRect.sizeDelta = new Vector2(78f, 78f);
            }

            unitPortraitRawImage.raycastTarget = false;
            unitPortraitRawImage.color = Color.white;
            return unitPortraitRawImage;
        }

        public static TMP_Text EnsureUnitDescriptionText(RectTransform panelRoot, TMP_Text unitDescriptionText)
        {
            if (panelRoot == null)
            {
                return unitDescriptionText;
            }

            RectTransform descriptionRect = null;
            var createdNow = false;
            if (unitDescriptionText == null)
            {
                descriptionRect = panelRoot.Find("UnitDescription") as RectTransform;
                if (descriptionRect == null)
                {
                    descriptionRect = EnsureChild(panelRoot, "UnitDescription");
                    createdNow = true;
                }

                unitDescriptionText = descriptionRect != null
                    ? descriptionRect.GetComponent<TextMeshProUGUI>()
                    : null;
                if (unitDescriptionText == null && descriptionRect != null)
                {
                    unitDescriptionText = descriptionRect.gameObject.AddComponent<TextMeshProUGUI>();
                    createdNow = true;
                }
            }

            if (unitDescriptionText == null)
            {
                return null;
            }

            if (unitDescriptionText.font == null && TMP_Settings.defaultFontAsset != null)
            {
                unitDescriptionText.font = TMP_Settings.defaultFontAsset;
            }

            descriptionRect = unitDescriptionText.rectTransform;
            if (createdNow && descriptionRect != null)
            {
                descriptionRect.anchorMin = new Vector2(0f, 1f);
                descriptionRect.anchorMax = new Vector2(0f, 1f);
                descriptionRect.pivot = new Vector2(0f, 1f);
                descriptionRect.anchoredPosition = new Vector2(102f, -78f);
                descriptionRect.sizeDelta = new Vector2(300f, 30f);
                unitDescriptionText.fontSize = 14f;
                unitDescriptionText.color = new Color(0.86f, 0.9f, 0.95f, 0.95f);
            }

            unitDescriptionText.alignment = TextAlignmentOptions.TopLeft;
            unitDescriptionText.textWrappingMode = TextWrappingModes.Normal;
            unitDescriptionText.overflowMode = TextOverflowModes.Ellipsis;
            if (unitDescriptionText.text == null)
            {
                unitDescriptionText.text = string.Empty;
            }

            return unitDescriptionText;
        }

        public static TMP_Text EnsurePlanningSummaryText(RectTransform panelRoot, TMP_Text planningSummaryText)
        {
            if (panelRoot == null)
            {
                return planningSummaryText;
            }

            if (planningSummaryText == null)
            {
                var summaryRect = panelRoot.Find("PlanningSummaryText") as RectTransform;
                if (summaryRect == null)
                {
                    summaryRect = EnsureChild(panelRoot, "PlanningSummaryText");
                    summaryRect.anchorMin = new Vector2(0f, 1f);
                    summaryRect.anchorMax = new Vector2(1f, 1f);
                    summaryRect.pivot = new Vector2(0f, 1f);
                    summaryRect.anchoredPosition = new Vector2(102f, -112f);
                    summaryRect.sizeDelta = new Vector2(-118f, 24f);
                }

                planningSummaryText = summaryRect.GetComponent<TextMeshProUGUI>();
                if (planningSummaryText == null)
                {
                    planningSummaryText = summaryRect.gameObject.AddComponent<TextMeshProUGUI>();
                }
            }

            if (planningSummaryText == null)
            {
                return null;
            }

            if (planningSummaryText.font == null && TMP_Settings.defaultFontAsset != null)
            {
                planningSummaryText.font = TMP_Settings.defaultFontAsset;
            }

            planningSummaryText.fontSize = 13f;
            planningSummaryText.color = new Color(0.78f, 0.9f, 1f, 0.95f);
            planningSummaryText.alignment = TextAlignmentOptions.TopLeft;
            planningSummaryText.textWrappingMode = TextWrappingModes.NoWrap;
            planningSummaryText.overflowMode = TextOverflowModes.Ellipsis;
            planningSummaryText.gameObject.SetActive(false);
            return planningSummaryText;
        }

        private static bool HasDefaultCoreLayout<TSlot>(References<TSlot> references)
            where TSlot : UnitInfoActionButtonSlot
        {
            return references.PanelBackground != null &&
                   references.UnitIcon != null &&
                   references.UnitPortraitRawImage != null &&
                   references.UnitNameText != null &&
                   references.HpSlider != null &&
                   references.HpValueText != null &&
                   references.ActionButtonsRoot != null &&
                   references.DirectOrderButtonsRoot != null &&
                   references.MoveButton != null &&
                   references.AttackButton != null &&
                   references.HoldButton != null &&
                   references.ChargeButton != null &&
                   references.ActionButtons != null &&
                   references.ActionButtons.Length > 0;
        }

        private static void EnsureParentCanvas(RectTransform panelRoot)
        {
            var canvas = panelRoot.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                return;
            }

            var canvasGO = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            panelRoot.SetParent(canvas.transform, false);
        }

        private static void ConfigurePanelRoot(RectTransform panelRoot)
        {
            panelRoot.anchorMin = new Vector2(1f, 0f);
            panelRoot.anchorMax = new Vector2(1f, 0f);
            panelRoot.pivot = new Vector2(1f, 0f);
            panelRoot.sizeDelta = new Vector2(420f, 280f);
        }

        private static Image EnsureBackground(RectTransform panelRoot, Image panelBackground)
        {
            if (panelBackground != null)
            {
                return panelBackground;
            }

            var bg = EnsureChild(panelRoot, "Background");
            panelBackground = bg.GetComponent<Image>();
            if (panelBackground == null)
            {
                panelBackground = bg.gameObject.AddComponent<Image>();
            }

            panelBackground.color = new Color(0.06f, 0.09f, 0.16f, 0.9f);
            var bgRt = bg as RectTransform;
            UnitInfoPanelLayoutBuilder.StretchToParent(bgRt, Vector2.zero, Vector2.zero);
            return panelBackground;
        }

        private static RectTransform EnsureActionButtonsRoot(RectTransform panelRoot, RectTransform actionButtonsRoot)
        {
            if (actionButtonsRoot != null)
            {
                return actionButtonsRoot;
            }

            actionButtonsRoot = EnsureChild(panelRoot, "ActionButtons");
            actionButtonsRoot.anchorMin = new Vector2(0f, 1f);
            actionButtonsRoot.anchorMax = new Vector2(0f, 1f);
            actionButtonsRoot.pivot = new Vector2(0f, 1f);
            actionButtonsRoot.anchoredPosition = new Vector2(14f, -10f);
            actionButtonsRoot.sizeDelta = new Vector2(190f, 30f);
            var layout = actionButtonsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = actionButtonsRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return actionButtonsRoot;
        }

        private static RectTransform EnsureDirectOrderButtonsRoot(
            RectTransform panelRoot,
            RectTransform directOrderButtonsRoot)
        {
            if (directOrderButtonsRoot == null)
            {
                directOrderButtonsRoot = EnsureChild(panelRoot, "DirectOrderButtons");
            }

            directOrderButtonsRoot.anchorMin = new Vector2(0f, 1f);
            directOrderButtonsRoot.anchorMax = new Vector2(0f, 1f);
            directOrderButtonsRoot.pivot = new Vector2(0f, 1f);
            directOrderButtonsRoot.anchoredPosition = new Vector2(14f, -10f);
            directOrderButtonsRoot.sizeDelta = new Vector2(386f, 30f);
            return directOrderButtonsRoot;
        }

        private static Image EnsureUnitIcon(RectTransform panelRoot, Image unitIcon)
        {
            if (unitIcon != null)
            {
                return unitIcon;
            }

            var iconRT = EnsureChild(panelRoot, "UnitIcon");
            unitIcon = iconRT.gameObject.GetComponent<Image>();
            if (unitIcon == null)
            {
                unitIcon = iconRT.gameObject.AddComponent<Image>();
            }

            iconRT.anchorMin = new Vector2(0f, 0f);
            iconRT.anchorMax = new Vector2(0f, 0f);
            iconRT.pivot = new Vector2(0f, 0f);
            iconRT.anchoredPosition = new Vector2(14f, 14f);
            iconRT.sizeDelta = new Vector2(78f, 78f);
            unitIcon.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            return unitIcon;
        }

        private static TMP_Text EnsureUnitNameText(RectTransform panelRoot, TMP_Text unitNameText)
        {
            if (unitNameText == null)
            {
                var nameRT = EnsureChild(panelRoot, "UnitName");
                unitNameText = UnitInfoPanelLayoutBuilder.CreateTmpText(nameRT, "Unit");
                nameRT.anchorMin = new Vector2(0f, 1f);
                nameRT.anchorMax = new Vector2(0f, 1f);
                nameRT.pivot = new Vector2(0f, 1f);
                nameRT.anchoredPosition = new Vector2(102f, -46f);
                nameRT.sizeDelta = new Vector2(280f, 32f);
                unitNameText.fontSize = 24f;
                unitNameText.alignment = TextAlignmentOptions.Left;
            }

            return unitNameText;
        }

        private static Slider EnsureHpSlider(RectTransform panelRoot, Slider hpSlider)
        {
            if (hpSlider != null)
            {
                return hpSlider;
            }

            var sliderRT = EnsureChild(panelRoot, "HpBar");
            hpSlider = sliderRT.gameObject.GetComponent<Slider>();
            if (hpSlider == null)
            {
                hpSlider = sliderRT.gameObject.AddComponent<Slider>();
            }

            sliderRT.anchorMin = new Vector2(0f, 0f);
            sliderRT.anchorMax = new Vector2(0f, 0f);
            sliderRT.pivot = new Vector2(0f, 0f);
            sliderRT.anchoredPosition = new Vector2(102f, 40f);
            sliderRT.sizeDelta = new Vector2(240f, 24f);
            UnitInfoPanelLayoutBuilder.BuildDefaultSliderVisual(hpSlider, sliderRT);
            return hpSlider;
        }

        private static TMP_Text EnsureHpValueText(RectTransform panelRoot, TMP_Text hpValueText)
        {
            if (hpValueText != null)
            {
                return hpValueText;
            }

            var hpTextRT = EnsureChild(panelRoot, "HpText");
            hpValueText = UnitInfoPanelLayoutBuilder.CreateTmpText(hpTextRT, "0/0");
            hpTextRT.anchorMin = new Vector2(0f, 0f);
            hpTextRT.anchorMax = new Vector2(0f, 0f);
            hpTextRT.pivot = new Vector2(0f, 0f);
            hpTextRT.anchoredPosition = new Vector2(102f, 14f);
            hpTextRT.sizeDelta = new Vector2(120f, 20f);
            hpValueText.fontSize = 16f;
            hpValueText.alignment = TextAlignmentOptions.Left;
            return hpValueText;
        }

        private static RectTransform EnsureChild(RectTransform panelRoot, string childName)
        {
            var child = panelRoot.Find(childName) as RectTransform;
            if (child != null)
            {
                return child;
            }

            var go = new GameObject(childName, typeof(RectTransform));
            child = go.GetComponent<RectTransform>();
            child.SetParent(panelRoot, false);
            return child;
        }
    }
}
