/*************************************************
 * Project: Panoptes
 * File: DamageNumberPopupController.cs
 * Author: Panoptes Team
 * Date: 2026-04-17
 * Description: World-space floating damage number popup.
 *************************************************/

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.Map
{
    public sealed class DamageNumberPopupController : MonoBehaviour
    {
        [Header("Popup Motion")]
        [SerializeField] private float popupLifetimeSeconds = 0.78f;
        [SerializeField] private float popupRiseDistance = 1.05f;
        [SerializeField] private float baseWorldOffsetY = 0.36f;
        [SerializeField] private float horizontalJitter = 0.2f;

        [Header("Popup Text")]
        [SerializeField] private float textScreenScale = 1f;
        [SerializeField] private float textFontSize = 30f;
        [SerializeField] private FontStyles textStyle = FontStyles.Bold;
        [SerializeField] private Color unitDamageColor = new Color(1f, 0.35f, 0.3f, 1f);
        [SerializeField] private Color buildingDamageColor = new Color(1f, 0.55f, 0.2f, 1f);

        private sealed class PopupState
        {
            public RectTransform Root;
            public TMP_Text Text;
            public Vector2 StartScreenPosition;
            public Vector2 TravelOffset;
            public float BaseScale;
            public float Elapsed;
            public Color BaseColor;
        }

        private readonly List<PopupState> _active = new List<PopupState>(16);
        private Canvas _canvas;
        private RectTransform _canvasRect;

        public void ShowDamage(Transform target, int damage, bool isBuilding)
        {
            if (target == null || damage <= 0)
            {
                return;
            }

            ShowDamageAt(ResolvePopupStartPosition(target), damage, isBuilding);
        }

        public void ShowDamage(Vector3 worldPosition, int damage, bool isBuilding)
        {
            if (damage <= 0)
            {
                return;
            }

            ShowDamageAt(worldPosition + Vector3.up * Mathf.Max(0.05f, baseWorldOffsetY), damage, isBuilding);
        }

        private void ShowDamageAt(Vector3 start, int damage, bool isBuilding)
        {
            if (!TryResolveScreenPosition(start, out var screenPosition) || !EnsureCanvas())
            {
                return;
            }

            var popupGo = new GameObject($"DamagePopup_{damage}", typeof(RectTransform), typeof(TextMeshProUGUI));
            popupGo.transform.SetParent(_canvasRect, worldPositionStays: false);

            var rect = popupGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(140f, 56f);
            rect.anchoredPosition = ScreenToCanvasPosition(screenPosition);

            var text = popupGo.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                Destroy(popupGo);
                return;
            }

            text.text = $"-{damage}";
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = Mathf.Max(1f, textFontSize);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.fontStyle = textStyle;
            text.color = isBuilding ? buildingDamageColor : unitDamageColor;
            text.enableVertexGradient = true;
            text.outlineWidth = 0.16f;
            text.outlineColor = new Color(0f, 0f, 0f, 0.85f);
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            text.horizontalAlignment = HorizontalAlignmentOptions.Center;
            rect.localScale = Vector3.one * Mathf.Max(0.1f, textScreenScale);

            var jitterPixels = Mathf.Max(0f, horizontalJitter) * 90f;
            var travel = new Vector2(
                Random.Range(-jitterPixels, jitterPixels),
                Mathf.Max(28f, popupRiseDistance * 72f));

            _active.Add(new PopupState
            {
                Root = rect,
                Text = text,
                StartScreenPosition = screenPosition,
                TravelOffset = travel,
                BaseScale = rect.localScale.x,
                Elapsed = 0f,
                BaseColor = text.color
            });
        }

        private void LateUpdate()
        {
            if (_active.Count == 0)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            var life = Mathf.Max(0.05f, popupLifetimeSeconds);
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var item = _active[i];
                if (item == null || item.Root == null || item.Text == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                item.Elapsed += dt;
                var t = Mathf.Clamp01(item.Elapsed / life);
                var eased = 1f - (1f - t) * (1f - t);

                item.Root.anchoredPosition = ScreenToCanvasPosition(item.StartScreenPosition + item.TravelOffset * eased);
                item.Root.localScale = Vector3.one * (item.BaseScale * ResolveScaleMultiplier(t));

                var color = item.BaseColor;
                color.a = 1f - t;
                item.Text.color = color;

                if (t >= 1f)
                {
                    Destroy(item.Root.gameObject);
                    _active.RemoveAt(i);
                }
            }
        }

        private Vector3 ResolvePopupStartPosition(Transform target)
        {
            if (target == null)
            {
                return transform.position;
            }

            var fallback = target.position + Vector3.up * Mathf.Max(0.05f, baseWorldOffsetY);
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return fallback;
            }

            var hasBounds = false;
            var bounds = default(Bounds);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return fallback;
            }

            var yOffset = Mathf.Max(0.05f, baseWorldOffsetY);
            return new Vector3(bounds.center.x, bounds.max.y + yOffset, bounds.center.z);
        }

        private bool EnsureCanvas()
        {
            if (_canvas != null && _canvasRect != null)
            {
                return true;
            }

            var canvasGo = new GameObject("DamageNumberPopupCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRect = canvasGo.GetComponent<RectTransform>();
            _canvasRect.anchorMin = Vector2.zero;
            _canvasRect.anchorMax = Vector2.one;
            _canvasRect.offsetMin = Vector2.zero;
            _canvasRect.offsetMax = Vector2.zero;
            return true;
        }

        private static bool TryResolveScreenPosition(Vector3 worldPosition, out Vector2 screenPosition)
        {
            screenPosition = default;
            var camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            var projected = camera.WorldToScreenPoint(worldPosition);
            if (projected.z <= 0f)
            {
                return false;
            }

            screenPosition = new Vector2(projected.x, projected.y);
            return true;
        }

        private Vector2 ScreenToCanvasPosition(Vector2 screenPosition)
        {
            if (_canvasRect == null)
            {
                return screenPosition;
            }

            return new Vector2(
                screenPosition.x - Screen.width * 0.5f,
                screenPosition.y - Screen.height * 0.5f);
        }

        private static float ResolveScaleMultiplier(float t)
        {
            var popIn = Mathf.Lerp(0.86f, 1.18f, Mathf.Clamp01(t / 0.18f));
            var settle = Mathf.Lerp(1.18f, 1f, Mathf.Clamp01((t - 0.18f) / 0.5f));
            return t < 0.18f ? popIn : settle;
        }
    }
}
