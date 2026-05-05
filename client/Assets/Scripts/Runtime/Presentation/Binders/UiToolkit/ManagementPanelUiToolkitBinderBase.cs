using System;
using System.Collections.Generic;
using Panoptes.Presentation.ViewModels;
using R3;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    [RequireComponent(typeof(UIDocument))]
    public abstract class ManagementPanelUiToolkitBinderBase<TViewModel> : MonoBehaviour, IBinder<TViewModel>
        where TViewModel : class, IViewModel<ManagementPanelState>
    {
        [SerializeField] private VisualTreeAsset visualTreeAsset;
        [SerializeField] private StyleSheet styleSheet;

        private readonly ManagementPanelUiToolkitRenderer _renderer = new();
        private IDisposable _subscription;
        private IDisposable _visibilitySubscription;
        private Button _closeButton;
        private ManagementPanelId _visibilityPanelId;
        private ManagementPanelVisibilityStore _visibilityStore;
        private bool _hasVisibilityBinding;
        private UIDocument _uiDocument;
        private TViewModel _viewModel;

        public event Action<string> RowActionRequested;

        protected abstract string DefaultTitle { get; }
        protected virtual bool UseFallbackVisualTree => false;

        [Inject]
        private void Construct(TViewModel viewModel)
        {
            Bind(viewModel);
        }

        private void Awake()
        {
            EnsureDocument();
            EnsureVisualTree();
            CacheElements();
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

        public void Bind(TViewModel viewModel)
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

        public void Render(ManagementPanelState state)
        {
            EnsureDocument();
            EnsureVisualTree();
            CacheElements();
            _renderer.Render(state, id => RowActionRequested?.Invoke(id));
            ApplyVisibility();
        }

        protected void BindVisibility(ManagementPanelVisibilityStore store, ManagementPanelId panelId)
        {
            StopVisibilitySubscription();
            _visibilityStore = store;
            _visibilityPanelId = panelId;
            _hasVisibilityBinding = store != null && panelId != ManagementPanelId.None;
            EnsureVisibilitySubscription();
            ApplyVisibility();
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

            var existingRoot = root.Q<VisualElement>(ManagementPanelUiToolkitRenderer.RootName);
            if (existingRoot != null &&
                (!UseFallbackVisualTree || existingRoot.ClassListContains("runtime-fallback-tree")))
            {
                return;
            }

            root.Clear();
            if (!UseFallbackVisualTree && visualTreeAsset != null)
            {
                visualTreeAsset.CloneTree(root);
            }
            else
            {
                var fallback = ManagementPanelUiToolkitRenderer.BuildFallbackTree(DefaultTitle);
                fallback.AddToClassList("runtime-fallback-tree");
                root.Add(fallback);
            }

            if (!UseFallbackVisualTree && styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }
        }

        private void CacheElements()
        {
            _renderer.Cache(_uiDocument != null ? _uiDocument.rootVisualElement : null);
            ConfigureScrollViews(_uiDocument != null ? _uiDocument.rootVisualElement : null);
            var closeButton = _uiDocument != null
                ? _uiDocument.rootVisualElement?.Q<Button>(ManagementPanelUiToolkitRenderer.CloseButtonName)
                : null;
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
                scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                ManagementPanelUiToolkitRenderer.EnableDragScroll(scroll);
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
            if (_hasVisibilityBinding && _visibilityStore != null && _visibilitySubscription == null)
            {
                _visibilitySubscription = _visibilityStore.State.Subscribe(this, static (_, self) => self.ApplyVisibility());
            }
        }

        private void ApplyVisibility()
        {
            if (!_hasVisibilityBinding || _visibilityStore == null)
            {
                return;
            }

            EnsureDocument();
            EnsureVisualTree();
            CacheElements();
            if (_uiDocument?.rootVisualElement == null)
            {
                return;
            }

            var root = _uiDocument.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            var panelRoot = root.Q<VisualElement>(ManagementPanelUiToolkitRenderer.RootName);
            if (panelRoot != null)
            {
                if (_visibilityPanelId == ManagementPanelId.TechTree)
                {
                    ManagementPanelRuntimeLayout.ApplyFullScreenPanel(root, panelRoot);
                }
                else if (_visibilityPanelId == ManagementPanelId.PolicyFocus)
                {
                    ManagementPanelRuntimeLayout.ApplyLargeModalPanel(root, panelRoot);
                }
                else
                {
                    ManagementPanelRuntimeLayout.ApplyRightSidePanel(root, panelRoot);
                }
            }

            root.style.display = _visibilityStore.IsVisible(_visibilityPanelId)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }

    internal static class ManagementPanelRuntimeLayout
    {
        public const float RightSideWidth = 440f;
        public const float RightSideMargin = 24f;
        public const float TopMargin = 84f;
        public const float BottomMargin = 28f;

        public static void ApplyRightSidePanel(VisualElement root, VisualElement panelRoot)
        {
            if (root == null || panelRoot == null)
            {
                return;
            }

            ApplyOverlayRoot(root);

            panelRoot.pickingMode = PickingMode.Position;
            panelRoot.style.position = Position.Absolute;
            panelRoot.style.left = StyleKeyword.Auto;
            panelRoot.style.right = RightSideMargin;
            panelRoot.style.top = TopMargin;
            panelRoot.style.bottom = BottomMargin;
            panelRoot.style.width = RightSideWidth;
            panelRoot.style.maxWidth = RightSideWidth;
            panelRoot.style.minWidth = 320f;
            panelRoot.style.flexShrink = 0f;
            panelRoot.style.overflow = Overflow.Hidden;
            panelRoot.style.paddingBottom = 18f;
            panelRoot.style.paddingLeft = 18f;
            panelRoot.style.paddingRight = 18f;
            panelRoot.style.paddingTop = 18f;
            panelRoot.style.backgroundColor = new Color(0.045f, 0.032f, 0.026f, 0.94f);
            panelRoot.style.borderBottomColor = new Color(0.78f, 0.52f, 0.24f, 0.92f);
            panelRoot.style.borderLeftColor = new Color(0.78f, 0.52f, 0.24f, 0.92f);
            panelRoot.style.borderRightColor = new Color(0.78f, 0.52f, 0.24f, 0.92f);
            panelRoot.style.borderTopColor = new Color(0.78f, 0.52f, 0.24f, 0.92f);
            panelRoot.style.borderBottomWidth = 1f;
            panelRoot.style.borderLeftWidth = 1f;
            panelRoot.style.borderRightWidth = 1f;
            panelRoot.style.borderTopWidth = 1f;
            panelRoot.style.color = new Color(0.96f, 0.88f, 0.74f, 1f);
        }

        public static void ApplyFullScreenPanel(VisualElement root, VisualElement panelRoot)
        {
            if (root == null || panelRoot == null)
            {
                return;
            }

            ApplyOverlayRoot(root);

            panelRoot.pickingMode = PickingMode.Position;
            panelRoot.style.position = Position.Absolute;
            panelRoot.style.left = 56f;
            panelRoot.style.right = 56f;
            panelRoot.style.top = 54f;
            panelRoot.style.bottom = 44f;
            panelRoot.style.width = StyleKeyword.Auto;
            panelRoot.style.maxWidth = StyleKeyword.None;
            panelRoot.style.minWidth = 0f;
            panelRoot.style.flexShrink = 0f;
            panelRoot.style.overflow = Overflow.Hidden;
            panelRoot.style.paddingBottom = 22f;
            panelRoot.style.paddingLeft = 24f;
            panelRoot.style.paddingRight = 24f;
            panelRoot.style.paddingTop = 22f;
            panelRoot.style.backgroundColor = new Color(0.035f, 0.026f, 0.022f, 0.96f);
            panelRoot.style.borderBottomColor = new Color(0.78f, 0.52f, 0.24f, 0.92f);
            panelRoot.style.borderLeftColor = new Color(0.78f, 0.52f, 0.24f, 0.92f);
            panelRoot.style.borderRightColor = new Color(0.78f, 0.52f, 0.24f, 0.92f);
            panelRoot.style.borderTopColor = new Color(0.78f, 0.52f, 0.24f, 0.92f);
            panelRoot.style.borderBottomWidth = 1f;
            panelRoot.style.borderLeftWidth = 1f;
            panelRoot.style.borderRightWidth = 1f;
            panelRoot.style.borderTopWidth = 1f;
            panelRoot.style.color = new Color(0.96f, 0.88f, 0.74f, 1f);
        }

        public static void ApplyLargeModalPanel(VisualElement root, VisualElement panelRoot)
        {
            if (root == null || panelRoot == null)
            {
                return;
            }

            ApplyOverlayRoot(root);

            panelRoot.pickingMode = PickingMode.Position;
            panelRoot.style.position = Position.Absolute;
            panelRoot.style.left = Length.Percent(5f);
            panelRoot.style.right = Length.Percent(5f);
            panelRoot.style.top = Length.Percent(5f);
            panelRoot.style.bottom = Length.Percent(5f);
            panelRoot.style.width = StyleKeyword.Auto;
            panelRoot.style.height = StyleKeyword.Auto;
            panelRoot.style.maxWidth = StyleKeyword.None;
            panelRoot.style.maxHeight = StyleKeyword.None;
            panelRoot.style.minWidth = 0f;
            panelRoot.style.minHeight = 0f;
            panelRoot.style.flexGrow = 1f;
            panelRoot.style.overflow = Overflow.Hidden;
            panelRoot.style.paddingBottom = 24f;
            panelRoot.style.paddingLeft = 28f;
            panelRoot.style.paddingRight = 28f;
            panelRoot.style.paddingTop = 24f;
            panelRoot.style.backgroundColor = new Color(0.038f, 0.027f, 0.022f, 0.97f);
            panelRoot.style.borderBottomColor = new Color(0.86f, 0.52f, 0.18f, 0.95f);
            panelRoot.style.borderLeftColor = new Color(0.86f, 0.52f, 0.18f, 0.95f);
            panelRoot.style.borderRightColor = new Color(0.86f, 0.52f, 0.18f, 0.95f);
            panelRoot.style.borderTopColor = new Color(0.86f, 0.52f, 0.18f, 0.95f);
            panelRoot.style.borderBottomWidth = 2f;
            panelRoot.style.borderLeftWidth = 2f;
            panelRoot.style.borderRightWidth = 2f;
            panelRoot.style.borderTopWidth = 2f;
            panelRoot.style.color = new Color(0.98f, 0.9f, 0.72f, 1f);
        }

        private static void ApplyOverlayRoot(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.right = 0f;
            root.style.top = 0f;
            root.style.bottom = 0f;
            root.style.backgroundColor = Color.clear;
        }
    }

    internal static class UiToolkitRuntimeDocument
    {
        private const string RuntimeThemeResourcePath = "UnityDefaultRuntimeTheme";
        private static readonly Dictionary<int, PanelSettings> SharedPanelSettingsBySortingOrder = new();
        private static ThemeStyleSheet _runtimeTheme;

        public static void EnsureConfigured(UIDocument document, int sortingOrder = 420)
        {
            if (document == null)
            {
                return;
            }

            var settingsName = document.panelSettings != null ? document.panelSettings.name : string.Empty;
            if (document.panelSettings == null ||
                string.Equals(settingsName, "RuntimePanelSettings", StringComparison.Ordinal) ||
                settingsName.StartsWith("PanoptesRuntimePanelSettings", StringComparison.Ordinal))
            {
                document.panelSettings = GetSharedPanelSettings(sortingOrder);
                return;
            }

            Configure(document.panelSettings, sortingOrder);
        }

        private static PanelSettings GetSharedPanelSettings(int sortingOrder)
        {
            if (!SharedPanelSettingsBySortingOrder.TryGetValue(sortingOrder, out var settings) || settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "PanoptesRuntimePanelSettings_" + sortingOrder;
                SharedPanelSettingsBySortingOrder[sortingOrder] = settings;
            }

            Configure(settings, sortingOrder);
            return settings;
        }

        private static void Configure(PanelSettings settings, int sortingOrder)
        {
            if (settings == null)
            {
                return;
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = sortingOrder;
            settings.targetDisplay = 0;
            settings.clearColor = false;
            settings.clearDepthStencil = false;
            var theme = ResolveRuntimeTheme();
            if (theme != null)
            {
                settings.themeStyleSheet = theme;
            }
        }

        private static ThemeStyleSheet ResolveRuntimeTheme()
        {
            if (_runtimeTheme == null)
            {
                _runtimeTheme = Resources.Load<ThemeStyleSheet>(RuntimeThemeResourcePath);
            }

            return _runtimeTheme;
        }
    }
}
