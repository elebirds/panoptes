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
        public bool IsCollapsed => _isCollapsed;

        private void Awake()
        {
            ResolveReferences();

            if (easing == null || easing.length == 0)
            {
                easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

            _isCollapsed = startCollapsed;
            RecalculatePositions();
            ApplyImmediate(_isCollapsed);
        }

        private void OnEnable()
        {
            ResolveReferences();
            RecalculatePositions();
            ApplyImmediate(_isCollapsed);

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

            if (immediate || !isActiveAndEnabled || !gameObject.activeInHierarchy)
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

        public void SetToggleButtonVisible(bool visible)
        {
            GameObject target = null;
            if (toggleButtonRect != null)
            {
                target = toggleButtonRect.gameObject;
            }
            else if (toggleButton != null)
            {
                target = toggleButton.gameObject;
            }

            if (target == null)
            {
                return;
            }

            // If this component lives on the same object as the toggle button, do not SetActive(false),
            // otherwise StartCoroutine in this component will fail while inactive.
            if (ReferenceEquals(target, gameObject))
            {
                if (toggleButton != null)
                {
                    toggleButton.interactable = visible;
                }

                var canvasGroup = target.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = target.AddComponent<CanvasGroup>();
                }
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
                return;
            }

            target.SetActive(visible);
        }

        public float GetPanelWidth()
        {
            if (buildPanelRoot == null)
            {
                return 0f;
            }
            return Mathf.Abs(buildPanelRoot.rect.width);
        }

        public float GetDuration()
        {
            return Mathf.Max(0.01f, duration);
        }

        public bool ControlsPanel(RectTransform panel)
        {
            ResolveReferences();
            return panel != null && ReferenceEquals(buildPanelRoot, panel);
        }

        public void ResetShownPositionFromCurrent()
        {
            ResolveReferences();
            if (buildPanelRoot == null)
            {
                return;
            }

            _panelShownPos = buildPanelRoot.anchoredPosition;
            if (toggleButtonRect != null)
            {
                _buttonShownPos = toggleButtonRect.anchoredPosition;
            }

            _positionsInitialized = true;
            RecalculatePositions();
        }

        public void ForceRecalculatePositions()
        {
            ResolveReferences();
            _positionsInitialized = false;
            RecalculatePositions();
        }

        private void RecalculatePositions()
        {
            if (buildPanelRoot == null)
            {
                return;
            }

            if (!_positionsInitialized)
            {
                _panelShownPos = ResolveDefaultShownPosition(buildPanelRoot);
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

        private Vector2 ResolveDefaultShownPosition(RectTransform panel)
        {
            if (panel == null)
            {
                return Vector2.zero;
            }

            var result = panel.anchoredPosition;
            const float epsilon = 0.001f;
            var anchoredRight = Mathf.Abs(panel.anchorMin.x - 1f) < epsilon &&
                                Mathf.Abs(panel.anchorMax.x - 1f) < epsilon &&
                                Mathf.Abs(panel.pivot.x - 1f) < epsilon;
            var anchoredLeft = Mathf.Abs(panel.anchorMin.x) < epsilon &&
                               Mathf.Abs(panel.anchorMax.x) < epsilon &&
                               Mathf.Abs(panel.pivot.x) < epsilon;
            var anchoredTop = Mathf.Abs(panel.anchorMin.y - 1f) < epsilon &&
                              Mathf.Abs(panel.anchorMax.y - 1f) < epsilon &&
                              Mathf.Abs(panel.pivot.y - 1f) < epsilon;
            var anchoredBottom = Mathf.Abs(panel.anchorMin.y) < epsilon &&
                                 Mathf.Abs(panel.anchorMax.y) < epsilon &&
                                 Mathf.Abs(panel.pivot.y) < epsilon;

            if ((hideDirection == SlideDirection.Right && anchoredRight) ||
                (hideDirection == SlideDirection.Left && anchoredLeft))
            {
                result.x = 0f;
            }

            if ((hideDirection == SlideDirection.Up && anchoredTop) ||
                (hideDirection == SlideDirection.Down && anchoredBottom))
            {
                result.y = 0f;
            }

            return result;
        }

        private Vector2 ComputePanelOffset(RectTransform panel)
        {
            var size = panel.rect.size;
            var width = Mathf.Abs(size.x);
            var height = Mathf.Abs(size.y);
            if (width <= 1f)
            {
                width = Mathf.Abs(LayoutUtility.GetPreferredWidth(panel));
            }
            if (height <= 1f)
            {
                height = Mathf.Abs(LayoutUtility.GetPreferredHeight(panel));
            }
            if (width <= 1f && panel.parent is RectTransform parentX)
            {
                width = Mathf.Abs(parentX.rect.width);
            }
            if (height <= 1f && panel.parent is RectTransform parentY)
            {
                height = Mathf.Abs(parentY.rect.height);
            }

            switch (hideDirection)
            {
                case SlideDirection.Left:
                    return new Vector2(-(width + extraHiddenPadding), 0f);
                case SlideDirection.Right:
                    return new Vector2(width + extraHiddenPadding, 0f);
                case SlideDirection.Up:
                    return new Vector2(0f, height + extraHiddenPadding);
                case SlideDirection.Down:
                    return new Vector2(0f, -(height + extraHiddenPadding));
                default:
                    return Vector2.zero;
            }
        }

        private void ResolveReferences()
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
