using NUnit.Framework;
using Panoptes.Presentation.Binders.UiToolkit;
using Panoptes.Presentation.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class BuildCatalogUiToolkitBinderTests
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
            _root = new GameObject("BuildCatalogBinderTest");
            var binder = _root.AddComponent<BuildCatalogUiToolkitBinder>();

            binder.Render(new BuildCatalogState(new[]
            {
                new BuildCatalogGroupState(
                    "tile",
                    "地块建筑",
                    new[]
                    {
                        new BuildCatalogItemState("farm", "Farm", "Grow food", "resource_node", isPending: true)
                    })
            }));

            var document = _root.GetComponent<UIDocument>();
            var rootElement = document.rootVisualElement;
            Assert.That(rootElement.Q<VisualElement>(BuildCatalogUiToolkitBinder.RootName), Is.Not.Null);
            Assert.That(rootElement.Q<Label>(BuildCatalogUiToolkitBinder.TitleName).text, Is.EqualTo("Build Catalog"));
            Assert.That(rootElement.Q<VisualElement>(BuildCatalogUiToolkitBinder.GroupsName).childCount, Is.EqualTo(1));
            Assert.That(rootElement.Q<Button>("build-catalog-item-farm"), Is.Not.Null);
            Assert.That(rootElement.Q<Label>("build-catalog-item-pending").text, Is.EqualTo("Pending"));
        }
    }
}
