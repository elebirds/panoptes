using NUnit.Framework;
using Panoptes.Presentation.UI.HUD;
using UnityEngine;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class UnitInfoPanelSlideAnimatorTests
    {
        private GameObject _panelRoot;
        private GameObject _dockRoot;

        [TearDown]
        public void TearDown()
        {
            if (_panelRoot != null)
            {
                Object.DestroyImmediate(_panelRoot);
            }

            if (_dockRoot != null)
            {
                Object.DestroyImmediate(_dockRoot);
            }
        }

        [Test]
        public void SetImmediate_ShouldPlacePanelAtDockedShownPosition()
        {
            var panel = CreatePanel(width: 420f, height: 280f);
            var dock = CreateDock(width: 96f, anchoredPosition: new Vector2(-24f, 18f));
            var animator = CreateAnimator(panel, dock);

            animator.SetImmediate(true);

            Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(-132f, 18f)));
            Assert.That(animator.IsOpen, Is.True);
        }

        [Test]
        public void SetImmediate_ShouldPlacePanelAtHiddenPosition_WhenClosed()
        {
            var panel = CreatePanel(width: 420f, height: 280f);
            var animator = CreateAnimator(panel, dockRightOfRect: null);

            animator.SetImmediate(false);

            Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(404f, -296f)));
            Assert.That(animator.IsOpen, Is.False);
        }

        [Test]
        public void SetExternalOffset_ShouldOnlyApplyOffsetToOpenTarget()
        {
            var panel = CreatePanel(width: 420f, height: 280f);
            var animator = CreateAnimator(panel, dockRightOfRect: null);

            animator.SetImmediate(true);
            animator.SetExternalOffset(new Vector2(-40f, 12f), immediate: true);
            var openPosition = panel.anchoredPosition;

            animator.SetImmediate(false);

            Assert.That(openPosition, Is.EqualTo(new Vector2(-56f, 28f)));
            Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(404f, -296f)));
        }

        private static UnitInfoPanelSlideAnimator CreateAnimator(
            RectTransform panel,
            RectTransform dockRightOfRect)
        {
            var animator = new UnitInfoPanelSlideAnimator(owner: null, stateChanged: null);
            animator.Configure(
                panel,
                hiddenOffsetX: 420f,
                hiddenBottomMargin: 16f,
                shownRightMargin: 16f,
                shownBottomMargin: 16f,
                slideDuration: 0.2f,
                slideCurve: AnimationCurve.Linear(0f, 0f, 1f, 1f),
                externalOffsetSlideDuration: 0.2f,
                externalOffsetCurve: AnimationCurve.Linear(0f, 0f, 1f, 1f),
                dockRightOfRect,
                dockSpacing: 12f);
            return animator;
        }

        private RectTransform CreatePanel(float width, float height)
        {
            _panelRoot = new GameObject("Panel", typeof(RectTransform));
            var rect = _panelRoot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private RectTransform CreateDock(float width, Vector2 anchoredPosition)
        {
            _dockRoot = new GameObject("Dock", typeof(RectTransform));
            var rect = _dockRoot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 40f);
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }
    }
}
