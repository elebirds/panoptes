using NUnit.Framework;
using Panoptes.Presentation.UI.Domestic;
using UnityEngine;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class BuildPanelScrollStateTests
    {
        private GameObject _rootObject;

        [TearDown]
        public void TearDown()
        {
            if (_rootObject != null)
            {
                Object.DestroyImmediate(_rootObject);
            }
        }

        [Test]
        public void Reset_ShouldRebaseToCurrentXAndZeroY_WhenForced()
        {
            var rect = CreateRect(new Vector2(24f, -120f));
            var state = new BuildPanelScrollState();

            state.Reset(rect, forceReset: true);

            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(24f, 0f)));
        }

        [Test]
        public void Reset_ShouldPreserveBasePosition_WhenNotForced()
        {
            var rect = CreateRect(new Vector2(24f, -120f));
            var state = new BuildPanelScrollState();

            state.Reset(rect, forceReset: true);
            rect.anchoredPosition = new Vector2(99f, -300f);
            state.Reset(rect, forceReset: false);

            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(24f, 0f)));
        }

        private RectTransform CreateRect(Vector2 anchoredPosition)
        {
            _rootObject = new GameObject("BuildListRoot", typeof(RectTransform));
            var rect = _rootObject.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }
    }
}
