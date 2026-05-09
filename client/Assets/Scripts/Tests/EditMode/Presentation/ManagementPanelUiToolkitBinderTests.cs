using System.Collections.Generic;
using System.Reflection;
using Google.Protobuf;
using NUnit.Framework;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Binders.UiToolkit;
using Panoptes.Presentation.ViewModels;
using Panoptes.Protocol.V1;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using UguiButton = UnityEngine.UI.Button;
using UguiImage = UnityEngine.UI.Image;
using UguiLayoutElement = UnityEngine.UI.LayoutElement;

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
            Assert.That(rootElement.Q<Label>("management-panel-row-title").ClassListContains("management-panel-row-title"), Is.True);
            Assert.That(rootElement.Q<Label>("management-panel-row-summary").ClassListContains("management-panel-row-summary"), Is.True);
            Assert.That(rootElement.Q<Label>("management-panel-row-status").ClassListContains("management-panel-row-status"), Is.True);
        }

        [Test]
        public void TechTreeBinder_ShouldFollowManagementPanelVisibilityStore()
        {
            _root = new GameObject("TechTreeVisibilityTest");
            var binder = _root.AddComponent<TechTreeUiToolkitBinder>();
            var sender = new RecordingMessageSender();
            var visibilityStore = new ManagementPanelVisibilityStore();
            InjectTechTreeFlow(binder, new GameIntentService(sender), visibilityStore);

            var document = _root.GetComponent<UIDocument>();
            var rootElement = document.rootVisualElement;

            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.None));

            visibilityStore.Toggle(ManagementPanelId.TechTree);
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(rootElement.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(rootElement.Q<VisualElement>(ManagementPanelUiToolkitRenderer.RootName).pickingMode, Is.EqualTo(PickingMode.Position));

            visibilityStore.Toggle(ManagementPanelId.TechTree);
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.None));

            visibilityStore.Dispose();
        }

        [Test]
        public void TechTreeBinder_ShouldSubmitResearchTargetThroughGameIntentService()
        {
            ActionLock.Release();
            _root = new GameObject("TechTreeCommandTest");
            var binder = _root.AddComponent<TechTreeUiToolkitBinder>();
            var sender = new RecordingMessageSender();
            var visibilityStore = new ManagementPanelVisibilityStore();
            InjectTechTreeFlow(binder, new GameIntentService(sender), visibilityStore);

            RequestManagementRowAction(binder, "agrarian_foundations");

            Assert.That(sender.LastMessage, Is.TypeOf<MsgSetResearchTarget>());
            Assert.That(((MsgSetResearchTarget)sender.LastMessage).TechnologyId, Is.EqualTo("agrarian_foundations"));
            visibilityStore.Dispose();
        }

        [Test]
        public void PolicyFocusBinder_ShouldRouteNationalPolicyCommand()
        {
            ActionLock.Release();
            _root = new GameObject("PolicyFocusCommandTest");
            var binder = _root.AddComponent<PolicyFocusUiToolkitBinder>();
            var sender = new RecordingMessageSender();
            var visibilityStore = new ManagementPanelVisibilityStore();
            var staticCatalogStore = new StaticCatalogStore();
            var planningDraftStore = new PlanningDraftStore();
            var viewModel = new PolicyFocusViewModel(staticCatalogStore, planningDraftStore);

            staticCatalogStore.Replace(new StaticCatalogState(policies: new System.Collections.Generic.Dictionary<string, CatalogPolicyDto>
            {
                ["recovery"] = new CatalogPolicyDto { Id = "recovery", Name = "Recovery", Layer = "national" }
            }));
            InjectPolicyFlow(binder, new GameIntentService(sender), viewModel, visibilityStore);

            RequestManagementRowAction(binder, "recovery");
            Assert.That(sender.LastMessage, Is.TypeOf<MsgSetPolicy>());
            Assert.That(((MsgSetPolicy)sender.LastMessage).NationalPolicyId, Is.EqualTo("recovery"));

            visibilityStore.Show(ManagementPanelId.PolicyFocus);
            Assert.That(_root.GetComponent<UIDocument>().rootVisualElement.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            visibilityStore.Dispose();
            viewModel.Dispose();
        }

        [Test]
        public void InstitutionBinder_ShouldSubmitFullCategoryPreservingLoadout()
        {
            ActionLock.Release();
            _root = new GameObject("InstitutionCommandTest");
            var binder = _root.AddComponent<InstitutionUiToolkitBinder>();
            var sender = new RecordingMessageSender();
            var visibilityStore = new ManagementPanelVisibilityStore();
            var staticCatalogStore = new StaticCatalogStore();
            var planningDraftStore = new PlanningDraftStore();
            var viewModel = new InstitutionViewModel(staticCatalogStore, planningDraftStore);

            staticCatalogStore.Replace(new StaticCatalogState(
                institutionCategories: new Dictionary<string, CatalogInstitutionCategoryDto>
                {
                    ["power"] = new CatalogInstitutionCategoryDto { Id = "power", Name = "Power", SortOrder = 10 },
                    ["economy"] = new CatalogInstitutionCategoryDto { Id = "economy", Name = "Economy", SortOrder = 20 }
                },
                institutions: new Dictionary<string, CatalogInstitutionDto>
                {
                    ["royal_prerogative"] = new CatalogInstitutionDto { Id = "royal_prerogative", Name = "Royal Prerogative", Category = "power" },
                    ["estate_economy"] = new CatalogInstitutionDto { Id = "estate_economy", Name = "Estate Economy", Category = "economy" },
                    ["free_market"] = new CatalogInstitutionDto { Id = "free_market", Name = "Free Market", Category = "economy" }
                }));
            planningDraftStore.Replace(new PlanningDraftState(
                plannedInstitutionIds: new[] { "royal_prerogative", "estate_economy" }));
            InjectInstitutionFlow(binder, new GameIntentService(sender), viewModel, visibilityStore);

            RequestManagementRowAction(binder, "free_market");

            Assert.That(sender.LastMessage, Is.TypeOf<MsgSetInstitutionLoadout>());
            Assert.That(((MsgSetInstitutionLoadout)sender.LastMessage).InstitutionIds, Is.EquivalentTo(new[] { "royal_prerogative", "free_market" }));

            visibilityStore.Show(ManagementPanelId.Institutions);
            Assert.That(_root.GetComponent<UIDocument>().rootVisualElement.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            visibilityStore.Dispose();
            viewModel.Dispose();
        }

        [Test]
        public void NationalLedgerBinder_ShouldFollowManagementPanelVisibilityStore()
        {
            _root = new GameObject("NationalLedgerVisibilityTest");
            var binder = _root.AddComponent<NationalLedgerUiToolkitBinder>();
            var visibilityStore = new ManagementPanelVisibilityStore();
            InjectNationalLedgerVisibility(binder, visibilityStore);

            var rootElement = _root.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.None));

            visibilityStore.Show(ManagementPanelId.NationalLedger);
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            visibilityStore.Show(ManagementPanelId.TechTree);
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.None));
            visibilityStore.Dispose();
        }

        [Test]
        public void MinisterReportBinder_ShouldRenderUguiMinisterConversationWhenVisible()
        {
            ActionLock.Release();
            _root = new GameObject("MinisterReportPaintTest");
            var binder = _root.AddComponent<MinisterReportUiToolkitBinder>();
            var draftStore = new PlanningDraftStore();
            var sender = new RecordingMessageSender();
            var viewModel = new MinisterReportViewModel(
                draftStore,
                null,
                null,
                new MinisterCommandService(sender));
            var visibilityStore = new ManagementPanelVisibilityStore();
            InjectMinisterReport(binder, viewModel, visibilityStore);
            binder.Render(new MinisterReportState(
                "大臣汇报",
                "domestic",
                new[]
                {
                    new MinisterTabState(
                        "domestic",
                        "minister-domestic",
                        "内政大臣",
                        "内政大臣",
                        string.Empty,
                        "内",
                        selected: true,
                        vacant: false,
                        attributes: new[]
                        {
                            new MinisterAttributeState("ability", "能力", 7),
                            new MinisterAttributeState("loyalty", "忠诚", 8),
                            new MinisterAttributeState("ambition", "野心", 4)
                        }),
                    new MinisterTabState(
                        "command",
                        "minister-command",
                        "军令大臣",
                        "军令大臣",
                        string.Empty,
                        "令",
                        selected: false,
                        vacant: false,
                        attributes: new[]
                        {
                            new MinisterAttributeState("ability", "能力", 8),
                            new MinisterAttributeState("loyalty", "忠诚", 7)
                        })
                },
                new[]
                {
                    new MinisterTabState(
                        "defense",
                        "cand-defense-1",
                        "候选军备大臣",
                        "军备大臣",
                        string.Empty,
                        "备",
                        selected: false,
                        vacant: false,
                        roleVacant: true,
                        attributes: new[]
                        {
                            new MinisterAttributeState("ability", "能力", 9),
                            new MinisterAttributeState("loyalty", "忠诚", 6)
                        })
                },
                new[]
                {
                    new MinisterChatMessageState("m1", "domestic", "内政大臣", "内政大臣", string.Empty, "内", "建议扩张粮食产出。", false, false),
                    new MinisterChatMessageState("m2", "domestic", string.Empty, string.Empty, string.Empty, string.Empty, "接受。", true, false)
                },
                new[]
                {
                    new MinisterReplyOptionState("accept", string.Empty, "domestic", "采纳全部", "采纳", true),
                    new MinisterReplyOptionState("reject", string.Empty, "domestic", "暂不采纳", "暂不采纳", false)
                },
                new[]
                {
                    new MinisterSkillCardState(
                        "stargazing",
                        "domestic",
                        "观星",
                        "下一回合展开全图视野，仅持续一回合。",
                        "下一回合生效，持续 1 回合",
                        true)
                }));

            visibilityStore.Show(ManagementPanelId.MinisterReport);

            var canvas = _root.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.enabled, Is.True);
            Assert.That(canvas.sortingOrder, Is.EqualTo(5000));
            Assert.That(_root.GetComponent<UIDocument>() == null || !_root.GetComponent<UIDocument>().enabled, Is.True);

            var texts = _root.GetComponentsInChildren<TextMeshProUGUI>(true);
            Assert.That(ContainsText(texts, "内政大臣"), Is.True);
            Assert.That(ContainsText(texts, "军令大臣"), Is.True);
            Assert.That(ContainsText(texts, "候选大臣"), Is.True);
            Assert.That(ContainsText(texts, "能力 7"), Is.True);
            Assert.That(ContainsText(texts, "忠诚 8"), Is.True);
            Assert.That(ContainsText(texts, "野心 4"), Is.True);
            Assert.That(ContainsText(texts, "雇佣"), Is.True);
            Assert.That(ContainsText(texts, "好感 12"), Is.False);
            Assert.That(ContainsText(texts, "建议扩张粮食产出。"), Is.True);
            Assert.That(ContainsText(texts, "采纳全部"), Is.True);
            Assert.That(ContainsText(texts, "暂不采纳"), Is.True);
            Assert.That(ContainsText(texts, "部长技能"), Is.True);

            var skillButton = FindButton(MinisterReportUiToolkitBinder.SkillButtonName);
            Assert.That(skillButton, Is.Not.Null);
            skillButton!.onClick.Invoke();

            texts = _root.GetComponentsInChildren<TextMeshProUGUI>(true);
            Assert.That(ContainsText(texts, "观星"), Is.True);
            Assert.That(ContainsText(texts, "下一回合展开全图视野，仅持续一回合。"), Is.True);
            Assert.That(ContainsText(texts, "释放技能"), Is.True);

            var activateButton = FindButton("Activate");
            Assert.That(activateButton, Is.Not.Null);
            activateButton!.onClick.Invoke();
            var directive = sender.LastMessage as MsgSetMinisterDirective;
            Assert.That(directive, Is.Not.Null);
            Assert.That(directive!.MinisterRole, Is.EqualTo("domestic"));
            StringAssert.Contains("\"directive_type\":\"activate_skill\"", directive.Content);
            StringAssert.Contains("\"skill_card_id\":\"stargazing\"", directive.Content);

            var images = _root.GetComponentsInChildren<UguiImage>(true);
            Assert.That(images, Has.Some.Matches<UguiImage>(image =>
                image.gameObject.name == MinisterReportUiToolkitBinder.RootName &&
                image.color.r < 0.1f &&
                image.color.g < 0.1f &&
                image.color.b < 0.1f));

            visibilityStore.Dispose();
            viewModel.Dispose();
            draftStore.Dispose();
        }

        [Test]
        public void MinisterReportBinder_ShouldSizeChatBubblesToMessageContent()
        {
            _root = new GameObject("MinisterReportBubbleSizingTest");
            var binder = _root.AddComponent<MinisterReportUiToolkitBinder>();
            var draftStore = new PlanningDraftStore();
            var viewModel = new MinisterReportViewModel(draftStore);
            var visibilityStore = new ManagementPanelVisibilityStore();
            InjectMinisterReport(binder, viewModel, visibilityStore);
            binder.Render(new MinisterReportState(
                "大臣汇报",
                "domestic",
                new[] { new MinisterTabState("domestic", "minister-domestic", "内政大臣", "内政大臣", string.Empty, "内", selected: true, vacant: false, affection: 12) },
                System.Array.Empty<MinisterTabState>(),
                new[]
                {
                    new MinisterChatMessageState("m1", "domestic", "内政大臣", "内政大臣", string.Empty, "内", "好。", false, false),
                    new MinisterChatMessageState("m2", "domestic", string.Empty, string.Empty, string.Empty, string.Empty, "收到。", true, false)
                },
                null,
                null));

            visibilityStore.Show(ManagementPanelId.MinisterReport);

            var bubbleLayouts = FindBubbleLayoutElements();
            Assert.That(bubbleLayouts, Has.Count.EqualTo(2));
            foreach (var bubble in bubbleLayouts)
            {
                Assert.That(bubble.preferredWidth, Is.GreaterThanOrEqualTo(96f));
                Assert.That(bubble.preferredWidth, Is.LessThan(360f));
                Assert.That(bubble.preferredHeight, Is.GreaterThan(0f));
                Assert.That(bubble.preferredHeight, Is.LessThan(180f));
            }

            visibilityStore.Dispose();
            viewModel.Dispose();
            draftStore.Dispose();
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
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(rootElement.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(rootElement.Q<VisualElement>(ManagementHostUiToolkitBinder.RootName).pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(rootElement.Q<Label>(ManagementHostUiToolkitBinder.TitleName).text, Is.EqualTo("管理"));
            Assert.That(rootElement.Q<VisualElement>(ManagementHostUiToolkitBinder.OverviewPanelName).style.display.value, Is.EqualTo(DisplayStyle.None));

            ShowNationalOverview(binder);
            Assert.That(visibilityStore.Current.ActivePanel, Is.EqualTo(ManagementPanelId.NationalOverview));
            Assert.That(rootElement.Q<Label>(ManagementHostUiToolkitBinder.TitleName).text, Is.EqualTo("国家概览"));
            Assert.That(rootElement.Q<Label>(ManagementHostUiToolkitBinder.TurnValueName).text, Is.EqualTo("6"));
            Assert.That(rootElement.Q<Label>(ManagementHostUiToolkitBinder.ResearchValueName).text, Is.EqualTo("Irrigation"));
            Assert.That(rootElement.Q<VisualElement>(ManagementHostUiToolkitBinder.MetricsName).childCount, Is.EqualTo(1));
            Assert.That(rootElement.Q<VisualElement>(ManagementHostUiToolkitBinder.ResourcesName).childCount, Is.EqualTo(1));
            Assert.That(rootElement.Q<VisualElement>(ManagementHostUiToolkitBinder.EventsName).childCount, Is.EqualTo(1));
            Assert.That(rootElement.Q<Label>("national-overview-metric-units-label").ClassListContains("national-overview-label"), Is.True);
            Assert.That(rootElement.Q<Label>("national-overview-resource-food-value").ClassListContains("national-overview-value"), Is.True);
            Assert.That(rootElement.Q<Label>("national-overview-event-title").ClassListContains("national-overview-event-title"), Is.True);

            ShowTechTree(binder);
            Assert.That(visibilityStore.Current.ActivePanel, Is.EqualTo(ManagementPanelId.TechTree));
            Assert.That(rootElement.Q<VisualElement>(ManagementHostUiToolkitBinder.OverviewPanelName).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(rootElement.Q<Label>(ManagementHostUiToolkitBinder.TitleName).text, Is.EqualTo("科技树"));

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
            var draftStore = new PlanningDraftStore();
            var gameStateStore = new GameStateStore();
            InjectRecipeFlow(binder, new PlanningIntentService(sender), visibilityStore, contextStore, draftStore, gameStateStore);

            var document = _root.GetComponent<UIDocument>();
            var rootElement = document.rootVisualElement;

            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.None));

            visibilityStore.Show(ManagementPanelId.RecipeSynthesis);
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            visibilityStore.Show(ManagementPanelId.BuildCatalog);
            Assert.That(rootElement.style.display.value, Is.EqualTo(DisplayStyle.None));

            visibilityStore.Dispose();
            contextStore.Dispose();
            gameStateStore.Dispose();
            draftStore.Dispose();
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
            var draftStore = new PlanningDraftStore();
            var gameStateStore = new GameStateStore();
            gameStateStore.Replace(new GameStateStoreState(phase: GamePhases.Planning));
            contextStore.SetContext("node-a", "mill", "player-1");
            InjectRecipeFlow(binder, new PlanningIntentService(sender), visibilityStore, contextStore, draftStore, gameStateStore);

            RequestRecipeSelection(binder, "grain");

            Assert.That(sender.LastMessage, Is.TypeOf<MsgSetBuildingRecipe>());
            var message = (MsgSetBuildingRecipe)sender.LastMessage;
            Assert.That(message.NodeId, Is.EqualTo("node-a"));
            Assert.That(message.RecipeId, Is.EqualTo("grain"));
            Assert.That(draftStore.Snapshot.RecipeSelections, Has.Count.EqualTo(1));
            Assert.That(draftStore.Snapshot.RecipeSelections[0].NodeId, Is.EqualTo("node-a"));
            Assert.That(draftStore.Snapshot.RecipeSelections[0].RecipeId, Is.EqualTo("grain"));

            visibilityStore.Dispose();
            contextStore.Dispose();
            draftStore.Dispose();
            gameStateStore.Dispose();
        }

        [Test]
        public void RecipeSynthesisBinder_ShouldIgnoreSelectionOutsidePlanning()
        {
            ActionLock.Release();
            _root = new GameObject("RecipeSynthesisPhaseGuardTest");
            var binder = _root.AddComponent<RecipeSynthesisUiToolkitBinder>();
            var sender = new RecordingMessageSender();
            var visibilityStore = new ManagementPanelVisibilityStore();
            var contextStore = new RecipeSynthesisContextStore();
            var draftStore = new PlanningDraftStore();
            var gameStateStore = new GameStateStore();
            gameStateStore.Replace(new GameStateStoreState(phase: GamePhases.TurnReport));
            contextStore.SetContext("node-a", "mill", "player-1");
            InjectRecipeFlow(binder, new PlanningIntentService(sender), visibilityStore, contextStore, draftStore, gameStateStore);

            RequestRecipeSelection(binder, "grain");

            Assert.That(sender.LastMessage, Is.Null);
            Assert.That(draftStore.Snapshot.RecipeSelections, Is.Empty);

            visibilityStore.Dispose();
            contextStore.Dispose();
            draftStore.Dispose();
            gameStateStore.Dispose();
        }

        [Test]
        public void RecipeSynthesisBinder_ShouldCancelPlannedRecipeWhenClickingCurrentSelection()
        {
            ActionLock.Release();
            _root = new GameObject("RecipeSynthesisCancelTest");
            var binder = _root.AddComponent<RecipeSynthesisUiToolkitBinder>();
            var sender = new RecordingMessageSender();
            var visibilityStore = new ManagementPanelVisibilityStore();
            var contextStore = new RecipeSynthesisContextStore();
            var draftStore = new PlanningDraftStore();
            var gameStateStore = new GameStateStore();
            gameStateStore.Replace(new GameStateStoreState(phase: GamePhases.Planning));
            contextStore.SetContext("node-a", "mill", "player-1");
            draftStore.Replace(new PlanningDraftState(recipeSelections: new[]
            {
                new QueuedRecipeSelectionDto { NodeId = "node-a", RecipeId = "grain" }
            }));
            InjectRecipeFlow(binder, new PlanningIntentService(sender), visibilityStore, contextStore, draftStore, gameStateStore);

            RequestRecipeSelection(binder, "grain");

            Assert.That(sender.LastMessage, Is.TypeOf<MsgCancelBuildingRecipe>());
            var message = (MsgCancelBuildingRecipe)sender.LastMessage;
            Assert.That(message.NodeId, Is.EqualTo("node-a"));
            Assert.That(draftStore.Snapshot.RecipeSelections, Has.Count.EqualTo(1));
            Assert.That(draftStore.Snapshot.RecipeSelections[0].NodeId, Is.EqualTo("node-a"));
            Assert.That(draftStore.Snapshot.RecipeSelections[0].RecipeId, Is.Empty);

            visibilityStore.Dispose();
            contextStore.Dispose();
            draftStore.Dispose();
            gameStateStore.Dispose();
        }

        [Test]
        public void RecipeSynthesisBinder_ShouldCancelActiveRecipeWhenClickingCurrentSelection()
        {
            ActionLock.Release();
            _root = new GameObject("RecipeSynthesisActiveCancelTest");
            var binder = _root.AddComponent<RecipeSynthesisUiToolkitBinder>();
            var sender = new RecordingMessageSender();
            var visibilityStore = new ManagementPanelVisibilityStore();
            var contextStore = new RecipeSynthesisContextStore();
            var draftStore = new PlanningDraftStore();
            var gameStateStore = new GameStateStore();
            contextStore.SetContext("node-a", "mill", "player-1");
            gameStateStore.Replace(new GameStateStoreState(phase: GamePhases.Planning, nodes: new Dictionary<string, NodeDto>
            {
                ["node-a"] = new NodeDto { Id = "node-a", OperationSelectedRecipeId = "grain" }
            }));
            InjectRecipeFlow(binder, new PlanningIntentService(sender), visibilityStore, contextStore, draftStore, gameStateStore);

            RequestRecipeSelection(binder, "grain");

            Assert.That(sender.LastMessage, Is.TypeOf<MsgCancelBuildingRecipe>());
            var message = (MsgCancelBuildingRecipe)sender.LastMessage;
            Assert.That(message.NodeId, Is.EqualTo("node-a"));
            Assert.That(draftStore.Snapshot.RecipeSelections, Has.Count.EqualTo(1));
            Assert.That(draftStore.Snapshot.RecipeSelections[0].NodeId, Is.EqualTo("node-a"));
            Assert.That(draftStore.Snapshot.RecipeSelections[0].RecipeId, Is.Empty);

            visibilityStore.Dispose();
            contextStore.Dispose();
            draftStore.Dispose();
            gameStateStore.Dispose();
        }

        private static void InjectTechTreeFlow(
            TechTreeUiToolkitBinder binder,
            GameIntentService gameIntentService,
            ManagementPanelVisibilityStore visibilityStore)
        {
            var method = typeof(TechTreeUiToolkitBinder).GetMethod(
                "ConstructTechFlow",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[] { gameIntentService, visibilityStore });
        }

        private static void InjectPolicyFlow(
            PolicyFocusUiToolkitBinder binder,
            GameIntentService gameIntentService,
            PolicyFocusViewModel viewModel,
            ManagementPanelVisibilityStore visibilityStore)
        {
            var method = typeof(PolicyFocusUiToolkitBinder).GetMethod(
                "ConstructPolicyFlow",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[] { gameIntentService, viewModel, visibilityStore });
        }

        private static void InjectInstitutionFlow(
            InstitutionUiToolkitBinder binder,
            GameIntentService gameIntentService,
            InstitutionViewModel viewModel,
            ManagementPanelVisibilityStore visibilityStore)
        {
            var method = typeof(InstitutionUiToolkitBinder).GetMethod(
                "ConstructInstitutionFlow",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[] { gameIntentService, viewModel, visibilityStore });
        }

        private static void InjectNationalLedgerVisibility(
            NationalLedgerUiToolkitBinder binder,
            ManagementPanelVisibilityStore visibilityStore)
        {
            var method = typeof(NationalLedgerUiToolkitBinder).GetMethod(
                "ConstructVisibility",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[] { visibilityStore });
        }

        private static void InjectMinisterReport(
            MinisterReportUiToolkitBinder binder,
            MinisterReportViewModel viewModel,
            ManagementPanelVisibilityStore visibilityStore)
        {
            var method = typeof(MinisterReportUiToolkitBinder).GetMethod(
                "Construct",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[] { viewModel, visibilityStore });
        }

        private static bool ContainsText(IEnumerable<TextMeshProUGUI> texts, string expected)
        {
            foreach (var text in texts)
            {
                if (text != null && text.text == expected)
                {
                    return true;
                }
            }

            return false;
        }

        private UguiButton FindButton(string name)
        {
            var buttons = _root.GetComponentsInChildren<UguiButton>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.name == name)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private List<UguiLayoutElement> FindBubbleLayoutElements()
        {
            var result = new List<UguiLayoutElement>();
            var layouts = _root.GetComponentsInChildren<UguiLayoutElement>(true);
            for (var i = 0; i < layouts.Length; i++)
            {
                if (layouts[i] != null && layouts[i].gameObject.name == "Bubble")
                {
                    result.Add(layouts[i]);
                }
            }

            return result;
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

        private static void ShowNationalOverview(ManagementHostUiToolkitBinder binder)
        {
            var method = typeof(ManagementHostUiToolkitBinder).GetMethod(
                "ShowNationalOverview",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, null);
        }

        private static void InjectRecipeFlow(
            RecipeSynthesisUiToolkitBinder binder,
            PlanningIntentService planningIntentService,
            ManagementPanelVisibilityStore visibilityStore,
            RecipeSynthesisContextStore contextStore,
            PlanningDraftStore draftStore,
            GameStateStore gameStateStore)
        {
            var method = typeof(RecipeSynthesisUiToolkitBinder).GetMethod(
                "ConstructRecipeFlow",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[] { planningIntentService, visibilityStore, contextStore, draftStore, gameStateStore });
        }

        private static void RequestRecipeSelection(RecipeSynthesisUiToolkitBinder binder, string recipeId)
        {
            var method = typeof(RecipeSynthesisUiToolkitBinder).GetMethod(
                "RequestRecipeSelection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(binder, new object[] { recipeId });
        }

        private static void RequestManagementRowAction(object binder, string rowId)
        {
            var eventField = typeof(ManagementPanelUiToolkitBinderBase<>)
                .MakeGenericType(binder.GetType().BaseType!.GetGenericArguments()[0])
                .GetField("RowActionRequested", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(eventField, Is.Not.Null);
            var handler = eventField!.GetValue(binder) as System.Action<string>;
            Assert.That(handler, Is.Not.Null);
            handler!.Invoke(rowId);
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
