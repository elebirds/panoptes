using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.ViewModels;
using R3;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class BuildCatalogUiToolkitBinder : MonoBehaviour, IBinder<BuildCatalogViewModel>
    {
        public const string RootName = "build-catalog-root";
        public const string TitleName = "build-catalog-title";
        public const string CloseButtonName = "build-catalog-close";
        public const string GroupsName = "build-catalog-groups";
        public const string EmptyName = "build-catalog-empty";

        private Button _closeButton;
        private Label _empty;
        private VisualElement _groups;
        private IDisposable _subscription;
        private IDisposable _visibilitySubscription;
        private BuildCatalogContextStore _contextStore;
        private GameplayFeedbackStore _feedbackStore;
        private ManagementPanelVisibilityStore _visibilityStore;
        private MapPlanningInputController _mapPlanningInputController;
        private PlanningToolService _planningToolService;
        private Label _title;
        private UIDocument _uiDocument;
        private BuildCatalogViewModel _viewModel;

        public event Action<string, PlanningBuildPlacementRule> BuildRequested;

        [Inject]
        private void Construct(
            BuildCatalogViewModel viewModel,
            PlanningToolService planningToolService,
            ManagementPanelVisibilityStore visibilityStore,
            BuildCatalogContextStore contextStore,
            GameplayFeedbackStore feedbackStore)
        {
            _planningToolService = planningToolService;
            _visibilityStore = visibilityStore;
            _contextStore = contextStore;
            _feedbackStore = feedbackStore;
            Bind(viewModel);
            EnsureVisibilitySubscription();
            ApplyVisibility();
        }

        private void Awake()
        {
            EnsureDocument();
            EnsureVisualTree();
            CacheElements();
            ApplyVisibility();
        }

        private void OnEnable()
        {
            EnsureDocument();
            EnsureVisualTree();
            CacheElements();
            EnsureVisibilitySubscription();
            ApplyVisibility();
            if (_viewModel != null)
            {
                Bind(_viewModel);
            }
        }

        private void OnDisable()
        {
            StopSubscription();
            StopVisibilitySubscription();
        }

        private void OnDestroy()
        {
            StopVisibilitySubscription();
            Unbind();
        }

        public void Bind(BuildCatalogViewModel viewModel)
        {
            if (ReferenceEquals(_viewModel, viewModel))
            {
                EnsureSubscription();
                Render(viewModel?.Current);
                return;
            }

            Unbind();
            _viewModel = viewModel;
            if (_viewModel == null)
            {
                Render(null);
                return;
            }

            EnsureSubscription();
            Render(_viewModel.Current);
        }

        public void Unbind()
        {
            StopSubscription();
            _viewModel = null;
        }

        public void Render(BuildCatalogState state)
        {
            EnsureDocument();
            EnsureVisualTree();
            CacheElements();

            state ??= new BuildCatalogState();
            SetText(_title, "建造");
            if (_groups == null)
            {
                ApplyVisibility();
                return;
            }

            _groups.Clear();
            if (!state.HasGroups)
            {
                if (_empty != null)
                {
                    _empty.style.display = DisplayStyle.Flex;
                }

                ApplyVisibility();
                return;
            }

            if (_empty != null)
            {
                _empty.style.display = DisplayStyle.None;
            }

            for (var i = 0; i < state.Groups.Count; i++)
            {
                _groups.Add(CreateGroup(state.Groups[i]));
            }

            ApplyVisibility();
        }

        private VisualElement CreateGroup(BuildCatalogGroupState group)
        {
            var groupElement = new VisualElement { name = "build-catalog-group-" + (group?.Id ?? "unknown") };
            groupElement.AddToClassList("build-catalog-group");
            groupElement.Add(new Label(group?.Title ?? "其他") { name = "build-catalog-group-title" });

            if (group?.Items == null)
            {
                return groupElement;
            }

            for (var i = 0; i < group.Items.Count; i++)
            {
                groupElement.Add(CreateItem(group.Items[i]));
            }

            return groupElement;
        }

        private Button CreateItem(BuildCatalogItemState item)
        {
            var button = new Button { name = "build-catalog-item-" + (item?.BuildingId ?? "unknown") };
            button.AddToClassList("build-catalog-item");
            button.style.backgroundColor = new Color(0.10f, 0.07f, 0.055f, 0.96f);
            button.style.borderBottomColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            button.style.borderLeftColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            button.style.borderRightColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            button.style.borderTopColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderTopWidth = 1f;
            button.style.marginBottom = 12f;
            button.style.paddingBottom = 12f;
            button.style.paddingLeft = 12f;
            button.style.paddingRight = 12f;
            button.style.paddingTop = 12f;
            if (item != null && item.IsLocked)
            {
                button.AddToClassList("build-catalog-item-locked");
                button.tooltip = "该建筑的科技未解锁";
                button.style.opacity = 0.56f;
                button.style.backgroundColor = new Color(0.045f, 0.04f, 0.038f, 0.96f);
                button.style.borderBottomColor = new Color(0.20f, 0.18f, 0.16f, 0.95f);
                button.style.borderLeftColor = new Color(0.20f, 0.18f, 0.16f, 0.95f);
                button.style.borderRightColor = new Color(0.20f, 0.18f, 0.16f, 0.95f);
                button.style.borderTopColor = new Color(0.20f, 0.18f, 0.16f, 0.95f);
            }

            var header = new VisualElement { name = "build-catalog-item-header" };
            header.AddToClassList("build-catalog-item-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8f;
            var icon = CreateIcon(item?.IconKey, item?.BuildingId);
            if (icon != null)
            {
                header.Add(icon);
            }

            var title = new Label(item?.Title ?? "未知建筑") { name = "build-catalog-item-title" };
            title.style.color = new Color(0.96f, 0.88f, 0.74f, 1f);
            title.style.whiteSpace = WhiteSpace.Normal;
            header.Add(title);
            if (item != null && item.IsLocked)
            {
                header.Add(CreateLockIcon());
            }

            button.Add(header);
            if (!string.IsNullOrWhiteSpace(item?.Description))
            {
                var description = new Label(item.Description) { name = "build-catalog-item-description" };
                description.style.color = new Color(0.86f, 0.78f, 0.66f, 1f);
                description.style.whiteSpace = WhiteSpace.Normal;
                button.Add(description);
            }

            if (item?.Costs != null && item.Costs.Count > 0)
            {
                button.Add(CreateCostStrip(item.Costs));
            }

            if (item != null && item.IsPending)
            {
                var pending = new Label(item.PendingText) { name = "build-catalog-item-pending" };
                pending.AddToClassList("build-catalog-item-pending");
                pending.style.color = new Color(1f, 0.74f, 0.42f, 1f);
                button.Add(pending);
            }

            if (item != null && item.IsLocked)
            {
                var locked = new Label(string.IsNullOrWhiteSpace(item.LockedText) ? "科技未解锁" : item.LockedText) { name = "build-catalog-item-locked" };
                locked.AddToClassList("build-catalog-item-locked");
                locked.style.color = new Color(0.72f, 0.70f, 0.66f, 1f);
                locked.style.unityFontStyleAndWeight = FontStyle.Bold;
                button.Add(locked);
            }

            var captured = item;
            button.clicked += () =>
            {
                if (captured == null || string.IsNullOrWhiteSpace(captured.BuildingId))
                {
                    return;
                }

                if (captured.IsLocked)
                {
                    PublishLockedBuildFeedback(captured);
                    return;
                }

                RequestBuild(captured.BuildingId, captured.PlacementRule);
            };
            return button;
        }

        private void PublishLockedBuildFeedback(BuildCatalogItemState item)
        {
            _feedbackStore?.PublishFeedback(
                "build_catalog",
                "building_technology_locked",
                "该建筑的科技未解锁",
                false,
                new Dictionary<string, string>
                {
                    ["building_type_id"] = item?.BuildingId ?? string.Empty,
                    ["technology_id"] = item?.UnlockTechnologyId ?? string.Empty
                });
        }

        private static VisualElement CreateLockIcon()
        {
            var sprite = Resources.Load<Sprite>("Icons/UI/icon_lock");
            var icon = new VisualElement { name = "build-catalog-item-lock-icon" };
            icon.AddToClassList("build-catalog-item-lock-icon");
            icon.style.width = 24f;
            icon.style.height = 24f;
            icon.style.flexShrink = 0f;
            icon.style.marginLeft = 8f;
            if (sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
            }
            else
            {
                icon.style.backgroundColor = new Color(0.16f, 0.15f, 0.14f, 0.98f);
                icon.style.borderBottomColor = new Color(0.68f, 0.62f, 0.50f, 0.9f);
                icon.style.borderLeftColor = new Color(0.68f, 0.62f, 0.50f, 0.9f);
                icon.style.borderRightColor = new Color(0.68f, 0.62f, 0.50f, 0.9f);
                icon.style.borderTopColor = new Color(0.68f, 0.62f, 0.50f, 0.9f);
                icon.style.borderBottomWidth = 1f;
                icon.style.borderLeftWidth = 1f;
                icon.style.borderRightWidth = 1f;
                icon.style.borderTopWidth = 1f;
            }

            return icon;
        }

        private static VisualElement CreateIcon(string iconKey, string fallbackId)
        {
            var sprite = ManagementPanelUiToolkitRenderer.LoadIconSprite(
                iconKey,
                fallbackId,
                "Icons/Buildings",
                "Icons/Resources");
            var icon = new VisualElement { name = "build-catalog-item-icon" };
            icon.AddToClassList("build-catalog-item-icon");
            icon.style.width = 46f;
            icon.style.height = 46f;
            icon.style.flexShrink = 0f;
            icon.style.marginRight = 10f;
            if (sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
            }
            else
            {
                icon.style.backgroundColor = new Color(0.22f, 0.12f, 0.08f, 0.95f);
                icon.style.borderBottomColor = new Color(0.95f, 0.72f, 0.36f, 0.9f);
                icon.style.borderLeftColor = new Color(0.95f, 0.72f, 0.36f, 0.9f);
                icon.style.borderRightColor = new Color(0.95f, 0.72f, 0.36f, 0.9f);
                icon.style.borderTopColor = new Color(0.95f, 0.72f, 0.36f, 0.9f);
                icon.style.borderBottomWidth = 1f;
                icon.style.borderLeftWidth = 1f;
                icon.style.borderRightWidth = 1f;
                icon.style.borderTopWidth = 1f;
            }

            return icon;
        }

        private static VisualElement CreateCostStrip(IReadOnlyList<ManagementPanelAmountState> costs)
        {
            var strip = new VisualElement { name = "build-catalog-item-costs" };
            strip.AddToClassList("build-catalog-item-costs");
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.flexWrap = Wrap.Wrap;
            strip.style.alignItems = Align.Center;
            strip.style.marginTop = 8f;
            strip.style.marginBottom = 2f;

            for (var i = 0; costs != null && i < costs.Count; i++)
            {
                var amount = costs[i];
                if (amount == null)
                {
                    continue;
                }

                strip.Add(CreateCostAmount(amount));
            }

            return strip;
        }

        private static VisualElement CreateCostAmount(ManagementPanelAmountState amount)
        {
            var pill = new VisualElement { name = "build-catalog-item-cost-" + SafeName(amount?.Id) };
            pill.AddToClassList("build-catalog-item-cost");
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.marginRight = 8f;
            pill.style.marginBottom = 6f;
            pill.style.paddingBottom = 2f;
            pill.style.paddingLeft = 5f;
            pill.style.paddingRight = 7f;
            pill.style.paddingTop = 2f;
            pill.style.backgroundColor = new Color(0.16f, 0.10f, 0.065f, 0.96f);
            pill.style.borderBottomColor = new Color(0.55f, 0.36f, 0.16f, 0.85f);
            pill.style.borderLeftColor = new Color(0.55f, 0.36f, 0.16f, 0.85f);
            pill.style.borderRightColor = new Color(0.55f, 0.36f, 0.16f, 0.85f);
            pill.style.borderTopColor = new Color(0.55f, 0.36f, 0.16f, 0.85f);
            pill.style.borderBottomWidth = 1f;
            pill.style.borderLeftWidth = 1f;
            pill.style.borderRightWidth = 1f;
            pill.style.borderTopWidth = 1f;
            pill.tooltip = $"{amount?.Label ?? string.Empty} x{Mathf.Max(0, amount?.Amount ?? 0)}";

            var icon = CreateCostIcon(amount?.IconKey, amount?.Id);
            if (icon != null)
            {
                pill.Add(icon);
            }

            var label = new Label($"{amount?.Label ?? string.Empty} x{Mathf.Max(0, amount?.Amount ?? 0)}") { name = "build-catalog-item-cost-amount" };
            label.style.color = new Color(0.98f, 0.87f, 0.64f, 1f);
            label.style.whiteSpace = WhiteSpace.NoWrap;
            pill.Add(label);
            return pill;
        }

        private static VisualElement CreateCostIcon(string iconKey, string fallbackId)
        {
            var sprite = ManagementPanelUiToolkitRenderer.LoadIconSprite(
                iconKey,
                fallbackId,
                "Icons/Resources",
                "Icons/Points");
            var icon = new VisualElement { name = "build-catalog-item-cost-icon" };
            icon.style.width = 20f;
            icon.style.height = 20f;
            icon.style.flexShrink = 0f;
            icon.style.marginRight = 5f;
            if (sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
            }
            else
            {
                icon.style.backgroundColor = new Color(0.24f, 0.15f, 0.08f, 1f);
                icon.style.borderBottomColor = new Color(0.78f, 0.55f, 0.25f, 0.9f);
                icon.style.borderLeftColor = new Color(0.78f, 0.55f, 0.25f, 0.9f);
                icon.style.borderRightColor = new Color(0.78f, 0.55f, 0.25f, 0.9f);
                icon.style.borderTopColor = new Color(0.78f, 0.55f, 0.25f, 0.9f);
                icon.style.borderBottomWidth = 1f;
                icon.style.borderLeftWidth = 1f;
                icon.style.borderRightWidth = 1f;
                icon.style.borderTopWidth = 1f;
            }

            return icon;
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            return value.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        private void RequestBuild(string buildingId, PlanningBuildPlacementRule placementRule)
        {
            BuildRequested?.Invoke(buildingId, placementRule);
            var cityCoreNodeId = _contextStore?.Current?.CityCoreNodeId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cityCoreNodeId))
            {
                return;
            }

            if (TryEnterBuildPlacementViaMapInput(buildingId, cityCoreNodeId, placementRule))
            {
                return;
            }

            _planningToolService?.EnterBuild(buildingId, cityCoreNodeId, placementRule);
        }

        private bool TryEnterBuildPlacementViaMapInput(
            string buildingId,
            string cityCoreNodeId,
            PlanningBuildPlacementRule placementRule)
        {
            var mapPlanningInputController = ResolveMapPlanningInputController();
            if (mapPlanningInputController == null)
            {
                return false;
            }

            switch (placementRule)
            {
                case PlanningBuildPlacementRule.ResourceOnly:
                    mapPlanningInputController.EnterBuildPlacementResource(buildingId, cityCoreNodeId);
                    return true;
                case PlanningBuildPlacementRule.CityOnly:
                    mapPlanningInputController.EnterBuildPlacementCity(buildingId, cityCoreNodeId);
                    return true;
                default:
                    mapPlanningInputController.EnterBuildPlacementAny(buildingId, cityCoreNodeId);
                    return true;
            }
        }

        private MapPlanningInputController ResolveMapPlanningInputController()
        {
            if (_mapPlanningInputController == null)
            {
                _mapPlanningInputController = FindAnyObjectByType<MapPlanningInputController>(FindObjectsInactive.Exclude);
            }

            return _mapPlanningInputController;
        }

        private void EnsureDocument()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
                if (_uiDocument == null)
                {
                    _uiDocument = gameObject.AddComponent<UIDocument>();
                }
            }

            if (_uiDocument != null)
            {
                UiToolkitRuntimeDocument.EnsureConfigured(_uiDocument);
            }
        }

        private void EnsureVisualTree()
        {
            if (_uiDocument == null)
            {
                return;
            }

            var root = _uiDocument.rootVisualElement;
            if (root == null)
            {
                return;
            }

            var existingRoot = root.Q<VisualElement>(RootName);
            if (existingRoot != null && existingRoot.ClassListContains("runtime-fallback-tree"))
            {
                return;
            }

            root.Clear();
            root.Add(BuildFallbackTree());
        }

        private static VisualElement BuildFallbackTree()
        {
            var root = new VisualElement { name = RootName };
            root.AddToClassList("build-catalog-root");
            root.AddToClassList("runtime-fallback-tree");
            root.style.flexDirection = FlexDirection.Column;

            var header = new VisualElement { name = "build-catalog-header" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 14f;

            var title = new Label("建造") { name = TitleName };
            title.style.flexGrow = 1f;
            header.Add(title);

            var close = new Button { name = CloseButtonName, text = "X" };
            close.style.width = 34f;
            close.style.height = 30f;
            close.style.flexShrink = 0f;
            close.style.backgroundColor = new Color(0.20f, 0.08f, 0.045f, 0.95f);
            close.style.color = new Color(1f, 0.86f, 0.62f, 1f);
            close.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(close);
            root.Add(header);

            root.Add(new Label("暂无可建造建筑") { name = EmptyName });
            var scroll = new ScrollView { name = "build-catalog-scroll" };
            scroll.style.flexGrow = 1f;
            scroll.style.minHeight = 0f;
            scroll.style.overflow = Overflow.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            ManagementPanelUiToolkitRenderer.EnableDragScroll(scroll);
            scroll.Add(new VisualElement { name = GroupsName });
            root.Add(scroll);
            return root;
        }

        private void CacheElements()
        {
            var root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
            _title = root?.Q<Label>(TitleName);
            _empty = root?.Q<Label>(EmptyName);
            _groups = root?.Q<VisualElement>(GroupsName);
            ConfigureScrollViews(root);
            var closeButton = root?.Q<Button>(CloseButtonName);
            if (!ReferenceEquals(_closeButton, closeButton))
            {
                if (_closeButton != null)
                {
                    _closeButton.clicked -= RequestClose;
                }

                _closeButton = closeButton;
                if (_closeButton != null)
                {
                    _closeButton.clicked -= RequestClose;
                    _closeButton.clicked += RequestClose;
                }
            }
        }

        private void RequestClose()
        {
            _visibilityStore?.Hide();
        }

        private static void ConfigureScrollViews(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            var scrollViews = root.Query<ScrollView>().ToList();
            for (var i = 0; i < scrollViews.Count; i++)
            {
                var scroll = scrollViews[i];
                scroll.style.flexGrow = 1f;
                scroll.style.minHeight = 0f;
                scroll.style.overflow = Overflow.Hidden;
                ManagementPanelUiToolkitRenderer.EnableDragScroll(scroll);
            }
        }

        private static void SetText(Label label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        private void StopSubscription()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void StopVisibilitySubscription()
        {
            _visibilitySubscription?.Dispose();
            _visibilitySubscription = null;
        }

        private void EnsureSubscription()
        {
            if (_viewModel != null && _subscription == null)
            {
                _subscription = _viewModel.State.Subscribe(this, static (state, self) => self.Render(state));
            }
        }

        private void EnsureVisibilitySubscription()
        {
            if (_visibilityStore != null && _visibilitySubscription == null)
            {
                _visibilitySubscription = _visibilityStore.State.Subscribe(this, static (_, self) => self.ApplyVisibility());
            }
        }

        private void ApplyVisibility()
        {
            EnsureDocument();
            EnsureVisualTree();
            CacheElements();
            if (_uiDocument?.rootVisualElement == null)
            {
                return;
            }

            _uiDocument.rootVisualElement.style.display =
                _visibilityStore != null && _visibilityStore.IsVisible(ManagementPanelId.BuildCatalog)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            var panelRoot = _uiDocument.rootVisualElement.Q<VisualElement>(RootName);
            if (panelRoot != null)
            {
                ManagementPanelRuntimeLayout.ApplyRightSidePanel(_uiDocument.rootVisualElement, panelRoot);
            }
        }
    }
}
