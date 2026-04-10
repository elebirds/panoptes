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
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class BuildTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform tooltipRoot;
        [SerializeField] private TMP_Text tooltipText;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Vector2 screenOffset = new Vector2(16f, -16f);
        [SerializeField] private float aboveTargetPixels = 12f;
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
            RefreshTooltipLayout();
            Move(screenPosition);
        }

        public void ShowAbove(string text, RectTransform target, Camera targetEventCamera = null)
        {
            if (tooltipRoot == null || tooltipText == null || target == null)
            {
                return;
            }

            var normalizedText = normalizeFullWidthPunctuation
                ? NormalizePunctuation(text)
                : (text ?? string.Empty);
            tooltipText.text = normalizedText;
            tooltipRoot.gameObject.SetActive(true);
            RefreshTooltipLayout();
            MoveAbove(target, targetEventCamera);
        }

        public void Move(Vector2 screenPosition)
        {
            SetScreenPosition(screenPosition, screenOffset);
        }

        public void MoveAbove(RectTransform target, Camera targetEventCamera = null)
        {
            if (target == null)
            {
                return;
            }

            var worldTopCenter = target.TransformPoint(
                new Vector3(target.rect.center.x, target.rect.yMax, 0f));
            Camera cameraForScreenPoint = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cameraForScreenPoint = canvas.worldCamera != null ? canvas.worldCamera : targetEventCamera;
            }

            var screenTopCenter = RectTransformUtility.WorldToScreenPoint(cameraForScreenPoint, worldTopCenter);
            var yOffset = aboveTargetPixels;
            if (tooltipRoot != null)
            {
                yOffset += tooltipRoot.rect.height * tooltipRoot.pivot.y;
            }

            SetScreenPosition(screenTopCenter, new Vector2(0f, yOffset));
        }

        private void SetScreenPosition(Vector2 screenPosition, Vector2 extraOffset)
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
                screenPosition + extraOffset,
                cameraForCanvas,
                out localPoint);

            var worldPosition = canvasRect.TransformPoint(new Vector3(localPoint.x, localPoint.y, 0f));
            tooltipRoot.position = worldPosition;

            if (!clampToScreen)
            {
                return;
            }

            var rootSize = tooltipRoot.rect.size;
            var min = canvasRect.rect.min + Vector2.Scale(rootSize, tooltipRoot.pivot);
            var max = canvasRect.rect.max - Vector2.Scale(rootSize, Vector2.one - tooltipRoot.pivot);
            var p = localPoint;
            p.x = Mathf.Clamp(p.x, min.x, max.x);
            p.y = Mathf.Clamp(p.y, min.y, max.y);
            var clampedWorldPosition = canvasRect.TransformPoint(new Vector3(p.x, p.y, 0f));
            tooltipRoot.position = clampedWorldPosition;
        }

        private void RefreshTooltipLayout()
        {
            if (tooltipRoot == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRoot);
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
