using TMPro;
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
            }

            if (valueText != null)
            {
                valueText.text = state.DisplayText;
            }
        }
    }
}
