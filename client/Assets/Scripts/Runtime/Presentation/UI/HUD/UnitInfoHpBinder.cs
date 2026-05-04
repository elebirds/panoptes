using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.HUD
{
    public static class UnitInfoHpBinder
    {
        public static void Apply(Slider slider, TMP_Text valueText, UnitInfoHpState state)
        {
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = state.MaxHp;
                slider.value = state.ClampedHp;

                var fillImage = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
                if (fillImage != null)
                {
                    fillImage.color = ResolveHpColor(state.ClampedHp / (float)state.MaxHp);
                }
            }

            if (valueText != null)
            {
                valueText.text = state.DisplayText;
            }
        }

        private static Color ResolveHpColor(float ratio01)
        {
            var ratio = Mathf.Clamp01(ratio01);
            if (ratio < 0.2f)
            {
                return new Color(1f, 0.24f, 0.18f, 1f);
            }

            if (ratio < 0.5f)
            {
                return new Color(1f, 0.82f, 0.18f, 1f);
            }

            return new Color(0.2f, 0.95f, 0.35f, 1f);
        }
    }
}
