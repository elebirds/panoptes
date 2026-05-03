using System.Reflection;
using Google.Protobuf;
using NUnit.Framework;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.Binders.UiToolkit;
using Panoptes.Presentation.ViewModels;
using Panoptes.Protocol.V1;
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

        [Test]
        public void TechTreeBinder_ShouldFollowManagementPanelVisibilityStore()
        {
            _root = new GameObject("TechTreeVisibilityTest");
            var binder = _root.AddComponent<TechTreeUiToolkitBinder>();
            var visibilityStore = new ManagementPanelVisibilityStore();
            InjectVisibilityStore(binder, visibilityStore);

            var document = _root.GetComponent<UIDocument>();
            var rootElement = document.rootVisualElement;

            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.None));

            visibilityStore.Toggle(ManagementPanelId.TechTree);
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            visibilityStore.Toggle(ManagementPanelId.TechTree);
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.None));

            visibilityStore.Dispose();
        }

        [Test]
        public void ManagementPanelVisibilityStore_ShouldToggleSingleActivePanel()
        {
            using var visibilityStore = new ManagementPanelVisibilityStore();

            Assert.That(visibilityStore.IsVisible(ManagementPanelId.TechTree), Is.False);

            visibilityStore.Toggle(ManagementPanelId.TechTree);
            Assert.That(visibilityStore.Current.ActivePanel, Is.EqualTo(ManagementPanelId.TechTree));
            Assert.That(visibilityStore.IsVisible(ManagementPanelId.TechTree), Is.True);

            visibilityStore.Show(ManagementPanelId.BuildCatalog);
            Assert.That(visibilityStore.Current.ActivePanel, Is.EqualTo(ManagementPanelId.BuildCatalog));
            Assert.That(visibilityStore.IsVisible(ManagementPanelId.TechTree), Is.False);
            Assert.That(visibilityStore.IsVisible(ManagementPanelId.BuildCatalog), Is.True);

            visibilityStore.Toggle(ManagementPanelId.TechTree);
            Assert.That(visibilityStore.Current.ActivePanel, Is.EqualTo(ManagementPanelId.TechTree));
            Assert.That(visibilityStore.IsVisible(ManagementPanelId.BuildCatalog), Is.False);

            visibilityStore.Toggle(ManagementPanelId.TechTree);
            Assert.That(visibilityStore.Current.ActivePanel, Is.EqualTo(ManagementPanelId.None));
            Assert.That(visibilityStore.IsVisible(ManagementPanelId.TechTree), Is.False);
        }

        [Test]
        public void ManagementHostBinder_ShouldRenderOverviewAndSwitchVisibility()
        {
            _root = new GameObject("ManagementHostBinderTest");
            var binder = _root.AddComponent<ManagementHostUiToolkitBinder>();
            var visibilityStore = new ManagementPanelVisibilityStore();
            var viewModel = new NationalOverviewViewModel(
                new GameStateStore(),
                new TurnStore(),
                new PlanningDraftStore(),
                new StaticCatalogStore(),
                new SettlementStore());
            InjectManagementHost(binder, viewModel, visibilityStore);

            binder.Render(new NationalOverviewState(
                turnText: "6",
                phaseText: "Planning",
                tokensText: "4",
                metrics: new[] { new NationalOverviewMetricState("units", "Known Units", "3") },
                resources: new[] { new NationalOverviewResourceState("food", "Food", 9) },
                events: new[] { new NationalOverviewEventState("unit moved", "u1 -> n2") },
                plannedResearchText: "Irrigation",
                plannedPolicyText: "Logistics"));

            var document = _root.GetComponent<UIDocument>();
            var rootElement = document.rootVisualElement;
            Assert.That(rootElement.Q<VisualElement>(ManagementHostUiToolkitBinder.RootName), Is.Not.Null);
            Assert.That(rootElement.Q<Label>(ManagementHostUiToolkitBinder.TitleName).text, Is.EqualTo("National Overview"));
            Assert.That(rootElement.Q<Label>(ManagementHostUiToolkitBinder.TurnValueName).text, Is.EqualTo("6"));
            Assert.That(rootElement.Q<Label>(ManagementHostUiToolkitBinder.ResearchValueName).text, Is.EqualTo("Irrigation"));
            Assert.That(rootElement.Q<VisualElement>(ManagementHostUiToolkitBinder.MetricsName).childCount, Is.EqualTo(1));
            Assert.That(rootElement.Q<VisualElement>(ManagementHostUiToolkitBinder.ResourcesName).childCount, Is.EqualTo(1));
            Assert.That(rootElement.Q<VisualElement>(ManagementHostUiToolkitBinder.EventsName).childCount, Is.EqualTo(1));

            ShowTechTree(binder);
            Assert.That(visibilityStore.Current.ActivePanel, Is.EqualTo(ManagementPanelId.TechTree));
            Assert.That(rootElement.Q<VisualElement>(ManagementHostUiToolkitBinder.OverviewPanelName).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(rootElement.Q<Label>(ManagementHostUiToolkitBinder.TitleName).text, Is.EqualTo("Tech Tree"));

            visibilityStore.Dispose();
            viewModel.Dispose();
        }

        [Test]
        public void RecipeSynthesisBinder_ShouldFollowRecipeVisibilityStore()
        {
            _root = new GameObject("RecipeSynthesisVisibilityTest");
            var binder = _root.AddComponent<RecipeSynthesisUiToolkitBinder>();
            var sender = new RecordingMessageSender();
            var visibilityStore = new ManagementPanelVisibilityStore();
            var contextStore = new RecipeSynthesisContextStore();
            InjectRecipeFlow(binder, new PlanningIntentService(sender), visibilityStore, contextStore);

            var document = _root.GetComponent<UIDocument>();
            var rootElement = document.rootVisualElement;

            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.None));

            visibilityStore.Show(ManagementPanelId.RecipeSynthesis);
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            visibilityStore.Show(ManagementPanelId.BuildCatalog);
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.None));

            visibilityStore.Dispose();
            contextStore.Dispose();
        }

        [Test]
        public void RecipeSynthesisBinder_ShouldSubmitSelectedRecipeThroughPlanningService()
        {
            ActionLock.Release();
            _root = new GameObject("RecipeSynthesisCommandTest");
            var binder = _root.AddComponent<RecipeSynthesisUiToolkitBinder>();
            var sender = new RecordingMessageSender();
            var visibilityStore = new ManagementPanelVisibilityStore();
            var contextStore = new RecipeSynthesisContextStore();
            contextStore.SetContext("node-a", "mill", "player-1");
            InjectRecipeFlow(binder, new PlanningIntentService(sender), visibilityStore, contextStore);

            RequestRecipeSelection(binder, "grain");

            Assert.That(sender.LastMessage, Is.TypeOf<MsgSetBuildingRecipe>());
            var message = (MsgSetBuildingRecipe)sender.LastMessage;
            Assert.That(message.NodeId, Is.EqualTo("node-a"));
            Assert.That(message.RecipeId, Is.EqualTo("grain"));

            visibilityStore.Dispose();
            contextStore.Dispose();
        }

        private static void InjectVisibilityStore(TechTreeUiToolkitBinder binder, ManagementPanelVisibilityStore visibilityStore)
        {
            var method = typeof(TechTreeUiToolkitBinder).GetMethod(
                "ConstructVisibility",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[] { visibilityStore });
        }

        private static void InjectManagementHost(
            ManagementHostUiToolkitBinder binder,
            NationalOverviewViewModel viewModel,
            ManagementPanelVisibilityStore visibilityStore)
        {
            var method = typeof(ManagementHostUiToolkitBinder).GetMethod(
                "Construct",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[] { viewModel, visibilityStore });
        }

        private static void ShowTechTree(ManagementHostUiToolkitBinder binder)
        {
            var method = typeof(ManagementHostUiToolkitBinder).GetMethod(
                "ShowTechTree",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, null);
        }

        private static void InjectRecipeFlow(
            RecipeSynthesisUiToolkitBinder binder,
            PlanningIntentService planningIntentService,
            ManagementPanelVisibilityStore visibilityStore,
            RecipeSynthesisContextStore contextStore)
        {
            var method = typeof(RecipeSynthesisUiToolkitBinder).GetMethod(
                "ConstructRecipeFlow",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[] { planningIntentService, visibilityStore, contextStore });
        }

        private static void RequestRecipeSelection(RecipeSynthesisUiToolkitBinder binder, string recipeId)
        {
            var method = typeof(RecipeSynthesisUiToolkitBinder).GetMethod(
                "RequestRecipeSelection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[] { recipeId });
        }

        private sealed class RecordingMessageSender : IClientMessageSender
        {
            public IMessage LastMessage { get; private set; }

            public bool Send(IMessage message)
            {
                LastMessage = message;
                return true;
            }
        }
    }
}
