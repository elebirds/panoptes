using System;
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
        private ManagementPanelId _visibilityPanelId;
        private ManagementPanelVisibilityStore _visibilityStore;
        private bool _hasVisibilityBinding;
        private UIDocument _uiDocument;
        private TViewModel _viewModel;

        public event Action<string> RowActionRequested;

        protected abstract string DefaultTitle { get; }
        protected virtual bool AllowFallbackTree => true;

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

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void EnsureDocument()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }

            if (_uiDocument != null && _uiDocument.panelSettings == null)
            {
                _uiDocument.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
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

            if (root.Q<VisualElement>(ManagementPanelUiToolkitRenderer.RootName) == null)
            {
                root.Clear();
                if (visualTreeAsset != null)
                {
                    visualTreeAsset.CloneTree(root);
                }
                else if (AllowFallbackTree)
                {
                    root.Add(ManagementPanelUiToolkitRenderer.BuildFallbackTree(DefaultTitle));
                }
                else
                {
                    Debug.LogError($"[{GetType().Name}] Missing VisualTreeAsset. Assign a prefab-backed UXML asset instead of using fallback UI generation.");
                    return;
                }
            }

            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }
        }

        private void CacheElements()
        {
            _renderer.Cache(_uiDocument != null ? _uiDocument.rootVisualElement : null, Close);
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
            if (_hasVisibilityBinding && _visibilitySubscription == null)
            {
                _visibilitySubscription = _visibilityStore.State.Subscribe(this, static (_, self) => self.ApplyVisibility());
            }
        }

        private void ApplyVisibility()
        {
            if (!_hasVisibilityBinding)
            {
                return;
            }

            EnsureDocument();
            if (_uiDocument?.rootVisualElement == null)
            {
                return;
            }

            _uiDocument.rootVisualElement.style.display = _visibilityStore.IsVisible(_visibilityPanelId)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
