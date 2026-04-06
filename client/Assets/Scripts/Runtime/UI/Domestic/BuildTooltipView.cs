/*************************************************
 * Project: Panoptes
 * File: BuildTooltipView.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Tooltip panel controller for build UI.
 *************************************************/

using TMPro;
using UnityEngine;

namespace Panoptes.Runtime.UI.Domestic
{
    public sealed class BuildTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private TMP_Text tooltipText;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Vector2 screenOffset = new Vector2(16f, -16f);
        [SerializeField] private bool clampToScreen = true;

        private void Awake()
        {
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            Hide();
        }

        public void Show(string text, Vector2 screenPosition)
        {
            if (tooltipRoot == null || tooltipText == null)
            {
                return;
            }

            tooltipText.text = text ?? string.Empty;
            tooltipRoot.gameObject.SetActive(true);
            Move(screenPosition);
        }

        public void Move(Vector2 screenPosition)
        {
            if (tooltipRoot == null)
            {
                return;
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
                if (canvas == null)
                {
                    return;
                }
            }

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            Vector2 localPoint;
            var cameraForCanvas = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition + screenOffset,
                cameraForCanvas,
                out localPoint);

            tooltipRoot.anchoredPosition = localPoint;

            if (!clampToScreen)
            {
                return;
            }

            var rootSize = tooltipRoot.rect.size;
            var half = rootSize * 0.5f;
            var min = canvasRect.rect.min + half;
            var max = canvasRect.rect.max - half;
            var p = tooltipRoot.anchoredPosition;
            p.x = Mathf.Clamp(p.x, min.x, max.x);
            p.y = Mathf.Clamp(p.y, min.y, max.y);
            tooltipRoot.anchoredPosition = p;
        }

        public void Hide()
        {
            if (tooltipRoot != null)
            {
                tooltipRoot.gameObject.SetActive(false);
            }
        }
    }
}
