/*************************************************
 * Project: Panoptes
 * File: BuildPanelSlideToggle.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: Slide in/out toggle for build panel root and its toggle button.
 *************************************************/

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class BuildPanelSlideToggle : MonoBehaviour
    {
        public enum SlideDirection
        {
            Left = 0,
            Right = 1,
            Up = 2,
            Down = 3
        }

        [Header("References")]
        [SerializeField] private RectTransform buildPanelRoot;
        [SerializeField] private RectTransform toggleButtonRect;
        [SerializeField] private Button toggleButton;

        [Header("Animation")]
        [SerializeField] private SlideDirection hideDirection = SlideDirection.Right;
        [SerializeField] private float duration = 0.22f;
        [SerializeField] private AnimationCurve easing = null;
        [SerializeField] private float extraHiddenPadding = 24f;

        [Header("Button Hidden Position")]
        [SerializeField] private bool keepButtonPartiallyVisible = true;
        [SerializeField] private float buttonVisiblePixelsWhenHidden = 40f;

        [Header("State")]
        [SerializeField] private bool startCollapsed = false;

        private Vector2 _panelShownPos;
        private Vector2 _panelHiddenPos;
        private Vector2 _buttonShownPos;
        private Vector2 _buttonHiddenPos;
        private bool _isCollapsed;
        private bool _positionsInitialized;
        private bool _moveButtonIndependently;
        private Coroutine _animRoutine;

        private void Awake()
        {
            if (toggleButton == null)
            {
                toggleButton = GetComponent<Button>();
            }

            if (toggleButtonRect == null && toggleButton != null)
            {
                toggleButtonRect = toggleButton.transform as RectTransform;
            }

            if (buildPanelRoot == null)
            {
                var selfRect = transform as RectTransform;
                if (selfRect != null)
                {
                    buildPanelRoot = selfRect.parent as RectTransform;
                }
            }

            if (easing == null || easing.length == 0)
            {
                easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

            RecalculatePositions();
            ApplyImmediate(startCollapsed);
            _isCollapsed = startCollapsed;
        }

        private void OnEnable()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(Toggle);
            }
        }

        private void OnDisable()
        {
            if (toggleButton != null)
            {
                toggleButton.onClick.RemoveListener(Toggle);
            }
        }

        public void Toggle()
        {
            SetCollapsed(!_isCollapsed, false);
        }

        public void Expand()
        {
            SetCollapsed(false, false);
        }

        public void Collapse()
        {
            SetCollapsed(true, false);
        }

        public void SetCollapsed(bool collapsed, bool immediate)
        {
            RecalculatePositions();
            _isCollapsed = collapsed;

            if (immediate)
            {
                ApplyImmediate(collapsed);
                return;
            }

            if (_animRoutine != null)
            {
                StopCoroutine(_animRoutine);
            }
            _animRoutine = StartCoroutine(AnimateTo(collapsed));
        }

        private void RecalculatePositions()
        {
            if (buildPanelRoot == null)
            {
                return;
            }

            if (!_positionsInitialized)
            {
                _panelShownPos = buildPanelRoot.anchoredPosition;
                if (toggleButtonRect != null)
                {
                    _buttonShownPos = toggleButtonRect.anchoredPosition;
                }
                _positionsInitialized = true;
            }

            var panelOffset = ComputePanelOffset(buildPanelRoot);
            _panelHiddenPos = _panelShownPos + panelOffset;

            _moveButtonIndependently =
                toggleButtonRect != null &&
                !toggleButtonRect.IsChildOf(buildPanelRoot);

            if (toggleButtonRect != null)
            {
                var buttonOffset = keepButtonPartiallyVisible
                    ? AdjustButtonOffset(panelOffset)
                    : panelOffset;
                _buttonHiddenPos = _buttonShownPos + buttonOffset;
            }
        }

        private Vector2 ComputePanelOffset(RectTransform panel)
        {
            var size = panel.rect.size;
            switch (hideDirection)
            {
                case SlideDirection.Left:
                    return new Vector2(-(size.x + extraHiddenPadding), 0f);
                case SlideDirection.Right:
                    return new Vector2(size.x + extraHiddenPadding, 0f);
                case SlideDirection.Up:
                    return new Vector2(0f, size.y + extraHiddenPadding);
                case SlideDirection.Down:
                    return new Vector2(0f, -(size.y + extraHiddenPadding));
                default:
                    return Vector2.zero;
            }
        }

        private Vector2 AdjustButtonOffset(Vector2 panelOffset)
        {
            var result = panelOffset;
            if (Mathf.Abs(panelOffset.x) > Mathf.Abs(panelOffset.y))
            {
                var hidden = Mathf.Max(0f, Mathf.Abs(panelOffset.x) - buttonVisiblePixelsWhenHidden);
                result.x = Mathf.Sign(panelOffset.x) * hidden;
                result.y = 0f;
            }
            else
            {
                var hidden = Mathf.Max(0f, Mathf.Abs(panelOffset.y) - buttonVisiblePixelsWhenHidden);
                result.y = Mathf.Sign(panelOffset.y) * hidden;
                result.x = 0f;
            }
            return result;
        }

        private IEnumerator AnimateTo(bool collapsed)
        {
            if (buildPanelRoot == null)
            {
                yield break;
            }

            var panelStart = buildPanelRoot.anchoredPosition;
            var panelEnd = collapsed ? _panelHiddenPos : _panelShownPos;

            Vector2 buttonStart = Vector2.zero;
            Vector2 buttonEnd = Vector2.zero;
            var hasButton = toggleButtonRect != null && _moveButtonIndependently;
            if (hasButton)
            {
                buttonStart = toggleButtonRect.anchoredPosition;
                buttonEnd = collapsed ? _buttonHiddenPos : _buttonShownPos;
            }

            var t = 0f;
            var safeDuration = Mathf.Max(0.01f, duration);
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / safeDuration;
                var k = Mathf.Clamp01(t);
                k = easing != null ? easing.Evaluate(k) : k;

                buildPanelRoot.anchoredPosition = Vector2.LerpUnclamped(panelStart, panelEnd, k);
                if (hasButton)
                {
                    toggleButtonRect.anchoredPosition = Vector2.LerpUnclamped(buttonStart, buttonEnd, k);
                }

                yield return null;
            }

            buildPanelRoot.anchoredPosition = panelEnd;
            if (hasButton)
            {
                toggleButtonRect.anchoredPosition = buttonEnd;
            }

            _animRoutine = null;
        }

        private void ApplyImmediate(bool collapsed)
        {
            if (buildPanelRoot != null)
            {
                buildPanelRoot.anchoredPosition = collapsed ? _panelHiddenPos : _panelShownPos;
            }

            if (toggleButtonRect != null)
            {
                if (_moveButtonIndependently)
                {
                    toggleButtonRect.anchoredPosition = collapsed ? _buttonHiddenPos : _buttonShownPos;
                }
            }
        }
    }
}
