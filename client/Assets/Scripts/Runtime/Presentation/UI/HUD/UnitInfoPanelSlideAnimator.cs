using System;
using System.Collections;
using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class UnitInfoPanelSlideAnimator
    {
        private readonly MonoBehaviour _owner;
        private readonly Action _stateChanged;
        private RectTransform _panelRoot;
        private RectTransform _dockRightOfRect;
        private AnimationCurve _slideCurve;
        private AnimationCurve _externalOffsetCurve;
        private Coroutine _slideRoutine;
        private Coroutine _externalOffsetRoutine;
        private Vector2 _shownAnchoredPos;
        private Vector2 _hiddenAnchoredPos;
        private Vector2 _externalOffset;
        private float _hiddenOffsetX;
        private float _hiddenBottomMargin;
        private float _shownRightMargin;
        private float _shownBottomMargin;
        private float _slideDuration;
        private float _externalOffsetSlideDuration;
        private float _dockSpacing;
        private bool _isOpen;

        public UnitInfoPanelSlideAnimator(MonoBehaviour owner, Action stateChanged)
        {
            _owner = owner;
            _stateChanged = stateChanged;
        }

        public bool IsOpen => _isOpen;

        public void Configure(
            RectTransform panelRoot,
            float hiddenOffsetX,
            float hiddenBottomMargin,
            float shownRightMargin,
            float shownBottomMargin,
            float slideDuration,
            AnimationCurve slideCurve,
            float externalOffsetSlideDuration,
            AnimationCurve externalOffsetCurve,
            RectTransform dockRightOfRect,
            float dockSpacing)
        {
            _panelRoot = panelRoot;
            _hiddenOffsetX = hiddenOffsetX;
            _hiddenBottomMargin = hiddenBottomMargin;
            _shownRightMargin = shownRightMargin;
            _shownBottomMargin = shownBottomMargin;
            _slideDuration = slideDuration;
            _slideCurve = slideCurve;
            _externalOffsetSlideDuration = externalOffsetSlideDuration;
            _externalOffsetCurve = externalOffsetCurve;
            _dockRightOfRect = dockRightOfRect;
            _dockSpacing = dockSpacing;
            ResolveAnchoredPositions();
        }

        public void ResolveAnchoredPositions()
        {
            var y = _shownBottomMargin;
            var x = -_shownRightMargin;
            if (_dockRightOfRect != null)
            {
                x = _dockRightOfRect.anchoredPosition.x -
                    Mathf.Abs(_dockRightOfRect.rect.width) -
                    Mathf.Max(0f, _dockSpacing);
                y = _dockRightOfRect.anchoredPosition.y;
            }

            var panelHeight = 0f;
            if (_panelRoot != null)
            {
                panelHeight = Mathf.Max(Mathf.Abs(_panelRoot.rect.height), Mathf.Abs(_panelRoot.sizeDelta.y));
            }
            if (panelHeight <= 0.01f)
            {
                panelHeight = 280f;
            }

            _shownAnchoredPos = new Vector2(x, y);
            _hiddenAnchoredPos = new Vector2(
                x + Mathf.Abs(_hiddenOffsetX),
                -panelHeight - Mathf.Max(0f, _hiddenBottomMargin));
        }

        public void SetExternalOffset(Vector2 offset, bool immediate)
        {
            _externalOffset = offset;
            if (_panelRoot == null)
            {
                return;
            }

            StopExternalOffsetRoutine();

            if (immediate || _owner == null)
            {
                _panelRoot.anchoredPosition = GetTargetAnchoredPosition(_isOpen);
                return;
            }

            _externalOffsetRoutine = _owner.StartCoroutine(AnimateExternalOffset());
        }

        public void AnimateVisibility(bool open)
        {
            if (_panelRoot == null)
            {
                return;
            }

            StopSlideRoutine();

            if (_owner == null)
            {
                SetImmediate(open);
                return;
            }

            _slideRoutine = _owner.StartCoroutine(SlideRoutine(open));
        }

        public void SetImmediate(bool open)
        {
            _isOpen = open;
            if (_panelRoot != null)
            {
                _panelRoot.anchoredPosition = GetTargetAnchoredPosition(open);
            }
            _stateChanged?.Invoke();
        }

        public void StopAnimations()
        {
            StopSlideRoutine();
            StopExternalOffsetRoutine();
        }

        public Vector2 GetTargetAnchoredPosition(bool open)
        {
            var basePos = open ? _shownAnchoredPos : _hiddenAnchoredPos;
            return open ? basePos + _externalOffset : basePos;
        }

        private IEnumerator SlideRoutine(bool open)
        {
            _isOpen = open;
            _stateChanged?.Invoke();
            var duration = Mathf.Max(0.01f, _slideDuration);
            var from = _panelRoot.anchoredPosition;
            var to = GetTargetAnchoredPosition(open);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var curveT = _slideCurve != null && _slideCurve.keys != null && _slideCurve.length > 0
                    ? _slideCurve.Evaluate(t)
                    : t;
                _panelRoot.anchoredPosition = Vector2.LerpUnclamped(from, to, curveT);
                yield return null;
            }

            _panelRoot.anchoredPosition = to;
            _stateChanged?.Invoke();
            _slideRoutine = null;
        }

        private IEnumerator AnimateExternalOffset()
        {
            if (_panelRoot == null)
            {
                yield break;
            }

            var duration = Mathf.Max(0.01f, _externalOffsetSlideDuration);
            var from = _panelRoot.anchoredPosition;
            var to = GetTargetAnchoredPosition(_isOpen);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var curveT = _externalOffsetCurve != null && _externalOffsetCurve.length > 0
                    ? _externalOffsetCurve.Evaluate(t)
                    : t;
                _panelRoot.anchoredPosition = Vector2.LerpUnclamped(from, to, curveT);
                yield return null;
            }

            _panelRoot.anchoredPosition = to;
            _externalOffsetRoutine = null;
        }

        private void StopSlideRoutine()
        {
            if (_slideRoutine == null || _owner == null)
            {
                _slideRoutine = null;
                return;
            }

            _owner.StopCoroutine(_slideRoutine);
            _slideRoutine = null;
        }

        private void StopExternalOffsetRoutine()
        {
            if (_externalOffsetRoutine == null || _owner == null)
            {
                _externalOffsetRoutine = null;
                return;
            }

            _owner.StopCoroutine(_externalOffsetRoutine);
            _externalOffsetRoutine = null;
        }
    }
}
