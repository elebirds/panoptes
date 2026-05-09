using System.Reflection;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Binders.Ugui;
using Panoptes.Presentation.UI.HUD;
using Panoptes.Presentation.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class ResourceHudUguiBinderTests
    {
        private sealed class CoroutineHost : MonoBehaviour
        {
        }

        private GameObject _root;
        private ResourceHudUguiBinder _binder;

        [TearDown]
        public void TearDown()
        {
            _binder?.Dispose();
            _binder = null;

            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        [Test]
        public void Render_ShouldCloneTemplateAndApplyRows()
        {
            var listRoot = CreateResourceListRoot();
            _binder = CreateBinder(listRoot);

            _binder.Render(new ResourceHudState(new[]
            {
                new ResourceHudRowState("ore", 3),
                new ResourceHudRowState("coal", 12)
            }));

            Assert.That(listRoot.childCount, Is.EqualTo(2));
            Assert.That(FindText(listRoot.GetChild(0), "BaseNum").text, Is.EqualTo("3"));
            Assert.That(FindText(listRoot.GetChild(1), "BaseNum").text, Is.EqualTo("12"));
            Assert.That(FindText(listRoot.GetChild(0), "ChangeNum").gameObject.activeSelf, Is.False);
            Assert.That(FindText(listRoot.GetChild(1), "ChangeNum").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Render_ShouldShowDeltaAfterFirstSnapshotAndHideUnusedRows()
        {
            var listRoot = CreateResourceListRoot();
            _binder = CreateBinder(listRoot);
            _binder.Render(new ResourceHudState(new[]
            {
                new ResourceHudRowState("ore", 3),
                new ResourceHudRowState("coal", 12)
            }));

            _binder.Render(new ResourceHudState(new[]
            {
                new ResourceHudRowState("ore", 7)
            }));

            var changeText = FindText(listRoot.GetChild(0), "ChangeNum");
            Assert.That(changeText.gameObject.activeSelf, Is.True);
            Assert.That(changeText.text, Is.EqualTo("+4"));
            Assert.That(listRoot.GetChild(1).gameObject.activeSelf, Is.False);

            _binder.StopAllHideCoroutines();

            Assert.That(changeText.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Render_ShouldKeepDeltaHintUntilNextDeltaForSameResource()
        {
            var listRoot = CreateResourceListRoot();
            _binder = CreateBinder(listRoot);
            _binder.Render(new ResourceHudState(new[]
            {
                new ResourceHudRowState("ore", 3)
            }));

            _binder.Render(new ResourceHudState(new[]
            {
                new ResourceHudRowState("ore", 7)
            }));

            var changeText = FindText(listRoot.GetChild(0), "ChangeNum");
            Assert.That(changeText.gameObject.activeSelf, Is.True);
            Assert.That(changeText.text, Is.EqualTo("+4"));

            _binder.Render(new ResourceHudState(new[]
            {
                new ResourceHudRowState("ore", 7)
            }));

            Assert.That(changeText.gameObject.activeSelf, Is.True);
            Assert.That(changeText.text, Is.EqualTo("+4"));

            _binder.Render(new ResourceHudState(new[]
            {
                new ResourceHudRowState("ore", 5)
            }));

            Assert.That(changeText.gameObject.activeSelf, Is.True);
            Assert.That(changeText.text, Is.EqualTo("-2"));
        }

        [Test]
        public void Render_ShouldAttachTooltipEventsToAllResourceIcons()
        {
            var listRoot = CreateResourceListRoot();
            _binder = CreateBinder(listRoot);

            _binder.Render(new ResourceHudState(new[]
            {
                new ResourceHudRowState("ore", 3, displayName: "Ore", description: "Basic mineral"),
                new ResourceHudRowState("industry_output", 2, isPoint: true, displayName: "Industry", description: "Builds things")
            }));

            var resourceIcon = listRoot.GetChild(0).Find("Image").GetComponent<Image>();
            var pointIcon = listRoot.GetChild(1).Find("Image").GetComponent<Image>();
            var resourceTrigger = resourceIcon.GetComponent<EventTrigger>();
            var pointTrigger = pointIcon.GetComponent<EventTrigger>();

            Assert.That(resourceIcon.raycastTarget, Is.True);
            Assert.That(resourceTrigger, Is.Not.Null);
            Assert.That(resourceTrigger!.triggers.Count, Is.EqualTo(3));
            Assert.That(pointIcon.raycastTarget, Is.True);
            Assert.That(pointTrigger, Is.Not.Null);
            Assert.That(pointTrigger!.triggers.Count, Is.EqualTo(3));
        }

        [Test]
        public void TechButton_ShouldToggleFinalTechTreeVisibilityStore()
        {
            _root = new GameObject("ResourceHudTechButtonTest", typeof(RectTransform));
            var listObject = new GameObject("ResourceList", typeof(RectTransform));
            listObject.transform.SetParent(_root.transform, false);
            var buttonObject = new GameObject("TechBtn", typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(_root.transform, false);

            var hud = _root.AddComponent<ResourceHUD>();
            using var viewModel = new ResourceHudViewModel(new GameStateStore(), new StaticCatalogStore());
            using var visibilityStore = new ManagementPanelVisibilityStore();
            InjectDependencies(hud, viewModel, visibilityStore);

            var button = buttonObject.GetComponent<Button>();
            button.onClick.Invoke();
            Assert.That(visibilityStore.IsVisible(ManagementPanelId.TechTree), Is.True);

            button.onClick.Invoke();
            Assert.That(visibilityStore.IsVisible(ManagementPanelId.TechTree), Is.False);
        }

        [Test]
        public void MinisterButton_ShouldToggleMinisterReportVisibilityStore()
        {
            _root = new GameObject("ResourceHudMinisterButtonTest", typeof(RectTransform));
            var listObject = new GameObject("ResourceList", typeof(RectTransform));
            listObject.transform.SetParent(_root.transform, false);
            var techButtonObject = new GameObject("TechBtn", typeof(RectTransform), typeof(Button));
            techButtonObject.transform.SetParent(_root.transform, false);
            var ministerButtonObject = new GameObject("MinisterBtn", typeof(RectTransform), typeof(Button));
            ministerButtonObject.transform.SetParent(_root.transform, false);

            var hud = _root.AddComponent<ResourceHUD>();
            using var viewModel = new ResourceHudViewModel(new GameStateStore(), new StaticCatalogStore());
            using var visibilityStore = new ManagementPanelVisibilityStore();
            using var draftStore = new PlanningDraftStore();
            InjectDependencies(hud, viewModel, visibilityStore);
            InjectMinisterAttention(hud, draftStore);
            draftStore.Replace(new PlanningDraftState(ministerDrafts: new[]
            {
                new MinisterDraftDto
                {
                    DraftId = "draft-1",
                    MinisterRole = "domestic",
                    Available = true,
                    Status = "pending"
                }
            }));

            var button = ministerButtonObject.GetComponent<Button>();
            button.onClick.Invoke();
            Assert.That(visibilityStore.IsVisible(ManagementPanelId.MinisterReport), Is.True);

            button.onClick.Invoke();
            Assert.That(visibilityStore.IsVisible(ManagementPanelId.MinisterReport), Is.False);
        }

        [Test]
        public void InstitutionButton_ShouldToggleInstitutionVisibilityStore()
        {
            _root = new GameObject("ResourceHudInstitutionButtonTest", typeof(RectTransform));
            var listObject = new GameObject("ResourceList", typeof(RectTransform));
            listObject.transform.SetParent(_root.transform, false);
            var techButtonObject = new GameObject("TechBtn", typeof(RectTransform), typeof(Button));
            techButtonObject.transform.SetParent(_root.transform, false);
            var institutionButtonObject = new GameObject("InstitutionBtn", typeof(RectTransform), typeof(Button));
            institutionButtonObject.transform.SetParent(_root.transform, false);

            var hud = _root.AddComponent<ResourceHUD>();
            using var viewModel = new ResourceHudViewModel(new GameStateStore(), new StaticCatalogStore());
            using var visibilityStore = new ManagementPanelVisibilityStore();
            InjectDependencies(hud, viewModel, visibilityStore);

            var button = institutionButtonObject.GetComponent<Button>();
            button.onClick.Invoke();
            Assert.That(visibilityStore.IsVisible(ManagementPanelId.Institutions), Is.True);

            button.onClick.Invoke();
            Assert.That(visibilityStore.IsVisible(ManagementPanelId.Institutions), Is.False);
        }

        [Test]
        public void MinisterAttentionBadge_ShouldFollowInteractiveMinisterDrafts()
        {
            _root = new GameObject("ResourceHudMinisterBadgeTest", typeof(RectTransform));
            var listObject = new GameObject("ResourceList", typeof(RectTransform));
            listObject.transform.SetParent(_root.transform, false);
            var techButtonObject = new GameObject("TechBtn", typeof(RectTransform), typeof(Button));
            techButtonObject.transform.SetParent(_root.transform, false);
            var ministerButtonObject = new GameObject("MinisterBtn", typeof(RectTransform), typeof(Button));
            ministerButtonObject.transform.SetParent(_root.transform, false);

            var hud = _root.AddComponent<ResourceHUD>();
            using var viewModel = new ResourceHudViewModel(new GameStateStore(), new StaticCatalogStore());
            using var visibilityStore = new ManagementPanelVisibilityStore();
            using var draftStore = new PlanningDraftStore();
            InjectDependencies(hud, viewModel, visibilityStore);
            InjectMinisterAttention(hud, draftStore);

            var badge = ministerButtonObject.transform.Find("MinisterAttentionBadge");
            Assert.That(badge, Is.Not.Null);
            Assert.That(badge.gameObject.activeSelf, Is.False);

            draftStore.Replace(new PlanningDraftState(ministerDrafts: new[]
            {
                new MinisterDraftDto
                {
                    DraftId = "draft-1",
                    MinisterRole = "domestic",
                    Available = true,
                    Status = "pending"
                }
            }));

            Assert.That(badge.gameObject.activeSelf, Is.True);

            draftStore.Replace(new PlanningDraftState(ministerDrafts: new[]
            {
                new MinisterDraftDto
                {
                    DraftId = "draft-1",
                    MinisterRole = "domestic",
                    Available = true,
                    Status = "accepted"
                }
            }));

            Assert.That(badge.gameObject.activeSelf, Is.False);
        }

        private RectTransform CreateResourceListRoot()
        {
            _root = new GameObject("ResourceHudBinderTestRoot", typeof(RectTransform), typeof(CoroutineHost));
            var listObject = new GameObject("ResourceList", typeof(RectTransform));
            listObject.transform.SetParent(_root.transform, false);
            CreateResourceItem(listObject.transform);
            return listObject.GetComponent<RectTransform>();
        }

        private ResourceHudUguiBinder CreateBinder(RectTransform listRoot)
        {
            return new ResourceHudUguiBinder(
                listRoot,
                new[] { "Icons/Resources" },
                Color.green,
                Color.red);
        }

        private static void InjectDependencies(
            ResourceHUD hud,
            ResourceHudViewModel viewModel,
            ManagementPanelVisibilityStore visibilityStore)
        {
            var method = typeof(ResourceHUD).GetMethod("Construct", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(hud, new object[] { viewModel, visibilityStore });
        }

        private static void InjectMinisterAttention(ResourceHUD hud, PlanningDraftStore draftStore)
        {
            var method = typeof(ResourceHUD).GetMethod("ConstructMinisterAttention", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(hud, new object[] { draftStore });
        }

        private static void CreateResourceItem(Transform parent)
        {
            var item = new GameObject("ResourceItem", typeof(RectTransform));
            item.transform.SetParent(parent, false);

            var icon = new GameObject("Image", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(item.transform, false);

            var baseNum = new GameObject("BaseNum", typeof(RectTransform), typeof(TextMeshProUGUI));
            baseNum.transform.SetParent(item.transform, false);

            var changeNum = new GameObject("ChangeNum", typeof(RectTransform), typeof(TextMeshProUGUI));
            changeNum.transform.SetParent(item.transform, false);
        }

        private static TMP_Text FindText(Transform root, string childName)
        {
            return root.Find(childName).GetComponent<TMP_Text>();
        }
    }
}
