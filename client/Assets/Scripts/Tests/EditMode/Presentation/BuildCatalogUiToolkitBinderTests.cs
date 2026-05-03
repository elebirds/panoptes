using System.Reflection;
using NUnit.Framework;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
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

        [Test]
        public void Binder_ShouldFollowManagementPanelVisibilityStore()
        {
            _root = new GameObject("BuildCatalogVisibilityTest");
            var binder = _root.AddComponent<BuildCatalogUiToolkitBinder>();
            var visibilityStore = new ManagementPanelVisibilityStore();
            InjectServices(binder, visibilityStore: visibilityStore);

            var rootElement = _root.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.None));

            visibilityStore.Show(ManagementPanelId.BuildCatalog);
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            visibilityStore.Show(ManagementPanelId.TechTree);
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.None));

            visibilityStore.Dispose();
        }

        [Test]
        public void RequestBuild_ShouldEnterBuildPlanningToolWithCityCoreContext()
        {
            _root = new GameObject("BuildCatalogClickTest");
            var binder = _root.AddComponent<BuildCatalogUiToolkitBinder>();
            var planningToolStore = new PlanningToolStore();
            var planningToolService = new PlanningToolService(planningToolStore);
            var contextStore = new BuildCatalogContextStore();
            var visibilityStore = new ManagementPanelVisibilityStore();
            contextStore.SetCityCoreNode("capital");
            InjectServices(binder, planningToolService, visibilityStore, contextStore);
            visibilityStore.Show(ManagementPanelId.BuildCatalog);

            binder.Render(new BuildCatalogState(new[]
            {
                new BuildCatalogGroupState(
                    "city",
                    "城市建筑",
                    new[]
                    {
                        new BuildCatalogItemState(
                            "workshop",
                            "Workshop",
                            "Craft tools",
                            "city_territory",
                            PlanningBuildPlacementRule.CityOnly)
                    })
            }));

            var button = _root.GetComponent<UIDocument>().rootVisualElement.Q<Button>("build-catalog-item-workshop");
            Assert.That(button, Is.Not.Null);
            InvokeRequestBuild(binder, "workshop", PlanningBuildPlacementRule.CityOnly);

            var state = planningToolStore.Snapshot;
            Assert.That(state.Mode, Is.EqualTo(PlanningToolMode.Build));
            Assert.That(state.BuildTypeId, Is.EqualTo("workshop"));
            Assert.That(state.BuildCityId, Is.EqualTo("capital"));
            Assert.That(state.BuildRule, Is.EqualTo(PlanningBuildPlacementRule.CityOnly));

            contextStore.Dispose();
            visibilityStore.Dispose();
        }

        private static void InjectServices(
            BuildCatalogUiToolkitBinder binder,
            PlanningToolService planningToolService = null,
            ManagementPanelVisibilityStore visibilityStore = null,
            BuildCatalogContextStore contextStore = null)
        {
            var method = typeof(BuildCatalogUiToolkitBinder).GetMethod(
                "Construct",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[]
            {
                new BuildCatalogViewModel(new StaticCatalogStore(), new PlanningDraftStore()),
                planningToolService ?? new PlanningToolService(new PlanningToolStore()),
                visibilityStore ?? new ManagementPanelVisibilityStore(),
                contextStore ?? new BuildCatalogContextStore()
            });
        }

        private static void InvokeRequestBuild(
            BuildCatalogUiToolkitBinder binder,
            string buildingId,
            PlanningBuildPlacementRule placementRule)
        {
            var method = typeof(BuildCatalogUiToolkitBinder).GetMethod(
                "RequestBuild",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[] { buildingId, placementRule });
        }
    }
}
