using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.HUD
{
    public static class UnitInfoPanelLayoutBuilder
    {
        public static void BuildDefaultSliderVisual(Slider slider, RectTransform sliderRoot)
        {
            if (slider == null || sliderRoot == null)
            {
                return;
            }

            var background = EnsureSliderGraphic(sliderRoot, "Background", new Color(0.15f, 0.15f, 0.18f, 0.95f));
            var fillArea = EnsureRect(sliderRoot, "Fill Area");
            StretchToParent(fillArea, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            var fill = EnsureSliderGraphic(fillArea, "Fill", new Color(0.28f, 0.86f, 0.3f, 1f));
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = fill;
            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.ColorTint;
            slider.interactable = false;
            slider.handleRect = null;
            slider.value = 0f;
        }

        public static Image EnsureSliderGraphic(Transform parent, string name, Color color)
        {
            var rect = EnsureRect(parent, name);
            var image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }

            image.color = color;
            StretchToParent(rect, Vector2.zero, Vector2.zero);
            return image;
        }

        public static RectTransform EnsureRect(Transform parent, string name)
        {
            var existing = parent != null ? parent.Find(name) as RectTransform : null;
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static TMP_Text CreateTmpText(RectTransform root, string initialText)
        {
            var text = root.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = root.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.text = initialText ?? string.Empty;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
            text.raycastTarget = false;
            return text;
        }

        public static void StretchToParent(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
