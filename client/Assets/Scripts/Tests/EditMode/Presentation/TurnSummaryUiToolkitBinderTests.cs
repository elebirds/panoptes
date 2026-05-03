using NUnit.Framework;
using Panoptes.Presentation.Binders.UiToolkit;
using Panoptes.Presentation.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class TurnSummaryUiToolkitBinderTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        [Test]
        public void Render_ShouldPopulateNamedUiToolkitElements()
        {
            _root = new GameObject("TurnSummaryBinderTest");
            var binder = _root.AddComponent<TurnSummaryUiToolkitBinder>();

            binder.Render(new TurnSummaryState(
                turn: 5,
                phase: "planning",
                phaseLabel: "Planning",
                tokensLeft: 2,
                visibleNodeCount: 12,
                unitCount: 4,
                events: new[]
                {
                    new TurnSummaryEventState("unit moved", "u1 -> n2")
                }));

            var document = _root.GetComponent<UIDocument>();
            var rootElement = document.rootVisualElement;
            Assert.That(rootElement.Q<VisualElement>(TurnSummaryUiToolkitBinder.RootName), Is.Not.Null);
            Assert.That(rootElement.Q<Label>(TurnSummaryUiToolkitBinder.TitleName).text, Is.EqualTo("Planning"));
            Assert.That(rootElement.Q<Label>(TurnSummaryUiToolkitBinder.TurnValueName).text, Is.EqualTo("5"));
            Assert.That(rootElement.Q<Label>(TurnSummaryUiToolkitBinder.PhaseValueName).text, Is.EqualTo("Planning"));
            Assert.That(rootElement.Q<Label>(TurnSummaryUiToolkitBinder.TokensValueName).text, Is.EqualTo("2"));
            Assert.That(rootElement.Q<Label>(TurnSummaryUiToolkitBinder.NodesValueName).text, Is.EqualTo("12"));
            Assert.That(rootElement.Q<Label>(TurnSummaryUiToolkitBinder.UnitsValueName).text, Is.EqualTo("4"));
            Assert.That(rootElement.Q<VisualElement>(TurnSummaryUiToolkitBinder.EventsListName).childCount, Is.EqualTo(1));
        }
    }
}
