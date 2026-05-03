using NUnit.Framework;
using Panoptes.Presentation.UI.Domestic;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class BuildPanelSlideToggleTests
    {
        private GameObject _panel;
        private GameObject _toggle;

        [TearDown]
        public void TearDown()
        {
            if (_toggle != null)
            {
                Object.DestroyImmediate(_toggle);
                _toggle = null;
            }

            if (_panel != null)
            {
                Object.DestroyImmediate(_panel);
                _panel = null;
            }
        }

        [Test]
        public void BuildPanelSlideToggle_ShouldControlGenericParentPanel()
        {
            _panel = new GameObject("RecipeSynthesisPanelRoot", typeof(RectTransform));
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(360f, 480f);

            _toggle = new GameObject("RecipeSynthesisToggle", typeof(RectTransform), typeof(Button));
            _toggle.transform.SetParent(_panel.transform, false);
            var toggle = _toggle.AddComponent<BuildPanelSlideToggle>();

            toggle.ForceRecalculatePositions();
            toggle.SetCollapsed(true, true);

            Assert.That(toggle.ControlsPanel(panelRect), Is.True);
            Assert.That(toggle.IsCollapsed, Is.True);

            toggle.Expand();

            Assert.That(toggle.IsCollapsed, Is.False);
        }
    }
}
