using System;
using Panoptes.Presentation.ViewModels;
using R3;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class TurnSummaryUiToolkitBinder : MonoBehaviour, IBinder<TurnSummaryViewModel>
    {
        public const string RootName = "turn-summary-root";
        public const string TitleName = "turn-summary-title";
        public const string TurnValueName = "turn-summary-turn-value";
        public const string PhaseValueName = "turn-summary-phase-value";
        public const string TokensValueName = "turn-summary-tokens-value";
        public const string NodesValueName = "turn-summary-nodes-value";
        public const string UnitsValueName = "turn-summary-units-value";
        public const string EventsListName = "turn-summary-events";
        public const string EmptyEventsName = "turn-summary-empty-events";

        [SerializeField] private VisualTreeAsset visualTreeAsset;
        [SerializeField] private StyleSheet styleSheet;

        private Label _emptyEvents;
        private VisualElement _eventsList;
        private Label _nodesValue;
        private Label _phaseValue;
        private IDisposable _subscription;
        private Label _title;
        private Label _tokensValue;
        private Label _turnValue;
        private TurnSummaryViewModel _viewModel;
        private Label _unitsValue;
        private UIDocument _uiDocument;
        private IDisposable _visibilitySubscription;
        private ManagementPanelVisibilityStore _visibilityStore;

        [Inject]
        private void Construct(TurnSummaryViewModel viewModel, ManagementPanelVisibilityStore visibilityStore)
        {
            _visibilityStore = visibilityStore;
            Bind(viewModel);
            EnsureVisibilitySubscription();
            ApplyVisibility();
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

        public void Bind(TurnSummaryViewModel viewModel)
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

        public void Render(TurnSummaryState state)
        {
            EnsureDocument();
            EnsureVisualTree();
            CacheElements();

            state ??= new TurnSummaryState();
            SetText(_title, state.StatusText);
            SetText(_turnValue, state.TurnText);
            SetText(_phaseValue, state.PhaseLabel);
            SetText(_tokensValue, state.TokensText);
            SetText(_nodesValue, state.VisibleNodeCount.ToString());
            SetText(_unitsValue, state.UnitCount.ToString());
            RenderEvents(state);
            ApplyVisibility();
        }

        private void RenderEvents(TurnSummaryState state)
        {
            if (_eventsList == null)
            {
                return;
            }

            _eventsList.Clear();
            if (state == null || !state.HasEvents)
            {
                if (_emptyEvents != null)
                {
                    _emptyEvents.style.display = DisplayStyle.Flex;
                }

                return;
            }

            if (_emptyEvents != null)
            {
                _emptyEvents.style.display = DisplayStyle.None;
            }

            for (var i = 0; i < state.Events.Count; i++)
            {
                _eventsList.Add(CreateEventRow(state.Events[i]));
            }
        }

        private static VisualElement CreateEventRow(TurnSummaryEventState evt)
        {
            var row = new VisualElement { name = "turn-summary-event-row" };
            row.AddToClassList("turn-summary-event-row");
            var title = new Label(evt?.Title ?? "Event") { name = "turn-summary-event-title" };
            title.AddToClassList("turn-summary-event-title");
            var detail = new Label(evt?.Detail ?? string.Empty) { name = "turn-summary-event-detail" };
            detail.AddToClassList("turn-summary-event-detail");
            row.Add(title);
            row.Add(detail);
            return row;
        }

        private void EnsureDocument()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
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

            if (root.Q<VisualElement>(RootName) != null)
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

        private void EnsureVisibilitySubscription()
        {
            if (_visibilityStore != null && _visibilitySubscription == null)
            {
                _visibilitySubscription = _visibilityStore.State.Subscribe(this, static (_, self) => self.ApplyVisibility());
            }
        }

        private void StopVisibilitySubscription()
        {
            _visibilitySubscription?.Dispose();
            _visibilitySubscription = null;
        }

        private void ApplyVisibility()
        {
            EnsureDocument();
            if (_uiDocument?.rootVisualElement == null)
            {
                return;
            }

            var root = _uiDocument.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            var summaryRoot = root.Q<VisualElement>(RootName);
            if (summaryRoot != null)
            {
                summaryRoot.pickingMode = PickingMode.Position;
            }

            root.style.display = _visibilityStore != null && _visibilityStore.IsVisible(ManagementPanelId.TurnSummary)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private static VisualElement BuildFallbackTree()
        {
            var root = new VisualElement { name = RootName };
            root.AddToClassList("turn-summary-root");
            root.Add(new Label("Turn Summary") { name = TitleName });
            root.Add(BuildMetricRow("Turn", TurnValueName));
            root.Add(BuildMetricRow("Phase", PhaseValueName));
            root.Add(BuildMetricRow("Tokens", TokensValueName));
            root.Add(BuildMetricRow("Visible Nodes", NodesValueName));
            root.Add(BuildMetricRow("Units", UnitsValueName));
            root.Add(new Label("Recent Events") { name = "turn-summary-events-heading" });
            root.Add(new Label("No recent events") { name = EmptyEventsName });
            root.Add(new VisualElement { name = EventsListName });
            return root;
        }

        private static VisualElement BuildMetricRow(string label, string valueName)
        {
            var row = new VisualElement();
            row.AddToClassList("turn-summary-metric-row");
            row.Add(new Label(label) { name = valueName + "-label" });
            row.Add(new Label("--") { name = valueName });
            return row;
        }

        private void CacheElements()
        {
            var root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
            _title = root?.Q<Label>(TitleName);
            _turnValue = root?.Q<Label>(TurnValueName);
            _phaseValue = root?.Q<Label>(PhaseValueName);
            _tokensValue = root?.Q<Label>(TokensValueName);
            _nodesValue = root?.Q<Label>(NodesValueName);
            _unitsValue = root?.Q<Label>(UnitsValueName);
            _eventsList = root?.Q<VisualElement>(EventsListName);
            _emptyEvents = root?.Q<Label>(EmptyEventsName);
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

        private void EnsureSubscription()
        {
            if (_viewModel != null && _subscription == null)
            {
                _subscription = _viewModel.State.Subscribe(this, static (state, self) => self.Render(state));
            }
        }
    }
}
