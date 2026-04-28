using TMPro;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.HUD
{
    public static class UnitInfoActionButtonBinder
    {
        public static void ApplyState(Button button, string label, bool visible, bool interactable, bool actionLocked)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(visible);
            button.interactable = visible && interactable && !actionLocked;

            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = label;
            }
        }
    }
}
