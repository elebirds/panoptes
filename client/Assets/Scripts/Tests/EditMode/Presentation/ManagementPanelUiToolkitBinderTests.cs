using NUnit.Framework;
using Panoptes.Presentation.Binders.UiToolkit;
using Panoptes.Presentation.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class ManagementPanelUiToolkitBinderTests
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
        public void Render_ShouldPopulateSharedManagementPanelElements()
        {
            _root = new GameObject("TechTreeBinderTest");
            var binder = _root.AddComponent<TechTreeUiToolkitBinder>();

            binder.Render(new ManagementPanelState("Tech Tree", new[]
            {
                new ManagementPanelGroupState(
                    "economy",
                    "Economy",
                    new[]
                    {
                        new ManagementPanelRowState(
                            "irrigation",
                            "Irrigation",
                            "Water control",
                            "Cost 4",
                            "Planned research",
                            "Research")
                    })
            }));

            var document = _root.GetComponent<UIDocument>();
            var rootElement = document.rootVisualElement;
            Assert.That(rootElement.Q<VisualElement>(ManagementPanelUiToolkitRenderer.RootName), Is.Not.Null);
            Assert.That(rootElement.Q<Label>(ManagementPanelUiToolkitRenderer.TitleName).text, Is.EqualTo("Tech Tree"));
            Assert.That(rootElement.Q<VisualElement>(ManagementPanelUiToolkitRenderer.GroupsName).childCount, Is.EqualTo(1));
            Assert.That(rootElement.Q<Button>("management-panel-row-irrigation"), Is.Not.Null);
            Assert.That(rootElement.Q<Label>("management-panel-row-status").text, Is.EqualTo("Planned research"));
        }
    }
}
