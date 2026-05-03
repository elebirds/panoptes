using System;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
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
        public const string GroupsName = "build-catalog-groups";
        public const string EmptyName = "build-catalog-empty";

        [SerializeField] private VisualTreeAsset visualTreeAsset;
        [SerializeField] private StyleSheet styleSheet;

        private Label _empty;
        private VisualElement _groups;
        private IDisposable _subscription;
        private IDisposable _visibilitySubscription;
        private BuildCatalogContextStore _contextStore;
        private ManagementPanelVisibilityStore _visibilityStore;
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
            BuildCatalogContextStore contextStore)
        {
            _planningToolService = planningToolService;
            _visibilityStore = visibilityStore;
            _contextStore = contextStore;
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
            SetText(_title, "Build Catalog");
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
            groupElement.Add(new Label(group?.Title ?? "Other") { name = "build-catalog-group-title" });

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
            button.Add(new Label(item?.Title ?? "Unknown Building") { name = "build-catalog-item-title" });
            if (!string.IsNullOrWhiteSpace(item?.Description))
            {
                button.Add(new Label(item.Description) { name = "build-catalog-item-description" });
            }

            if (item != null && item.IsPending)
            {
                var pending = new Label(item.PendingText) { name = "build-catalog-item-pending" };
                pending.AddToClassList("build-catalog-item-pending");
                button.Add(pending);
            }

            var captured = item;
            button.clicked += () =>
            {
                if (captured == null || string.IsNullOrWhiteSpace(captured.BuildingId))
                {
                    return;
                }

                RequestBuild(captured.BuildingId, captured.PlacementRule);
            };
            return button;
        }

        private void RequestBuild(string buildingId, PlanningBuildPlacementRule placementRule)
        {
            BuildRequested?.Invoke(buildingId, placementRule);
            _planningToolService?.EnterBuild(
                buildingId,
                _contextStore?.Current?.CityCoreNodeId ?? string.Empty,
                placementRule);
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
            if (root == null || root.Q<VisualElement>(RootName) != null)
            {
                return;
            }

            root.Clear();
            if (visualTreeAsset != null)
            {
                visualTreeAsset.CloneTree(root);
            }
            else
            {
                root.Add(BuildFallbackTree());
            }

            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }
        }

        private static VisualElement BuildFallbackTree()
        {
            var root = new VisualElement { name = RootName };
            root.AddToClassList("build-catalog-root");
            root.Add(new Label("Build Catalog") { name = TitleName });
            root.Add(new Label("No buildings available") { name = EmptyName });
            root.Add(new VisualElement { name = GroupsName });
            return root;
        }

        private void CacheElements()
        {
            var root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
            _title = root?.Q<Label>(TitleName);
            _empty = root?.Q<Label>(EmptyName);
            _groups = root?.Q<VisualElement>(GroupsName);
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
            if (_uiDocument?.rootVisualElement == null)
            {
                return;
            }

            _uiDocument.rootVisualElement.style.display =
                _visibilityStore != null && _visibilityStore.IsVisible(ManagementPanelId.BuildCatalog)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }
    }
}
