/*************************************************
 * Project: Panoptes
 * File: BuildTooltipView.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Tooltip panel controller for build UI.
 *************************************************/

using TMPro;
using System.Text;
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
        [SerializeField] private bool normalizeFullWidthPunctuation = true;

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

            var normalizedText = normalizeFullWidthPunctuation
                ? NormalizePunctuation(text)
                : (text ?? string.Empty);
            tooltipText.text = normalizedText;
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

        private static string NormalizePunctuation(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                sb.Append(MapFullWidth(value[i]));
            }
            return sb.ToString();
        }

        private static char MapFullWidth(char ch)
        {
            switch (ch)
            {
                case '\u3002': return '.';
                case '\uFF0C': return ',';
                case '\uFF1A': return ':';
                case '\uFF1B': return ';';
                case '\uFF01': return '!';
                case '\uFF1F': return '?';
                case '\uFF08': return '(';
                case '\uFF09': return ')';
                case '\u3010': return '[';
                case '\u3011': return ']';
                case '\u201C': return '"';
                case '\u201D': return '"';
                case '\u2018': return '\'';
                case '\u2019': return '\'';
                case '\u3001': return ',';
                case '\u3000': return ' ';
                default: return ch;
            }
        }
    }
}
