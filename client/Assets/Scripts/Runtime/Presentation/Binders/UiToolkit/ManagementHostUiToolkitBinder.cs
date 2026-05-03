using System;
using Panoptes.Presentation.ViewModels;
using R3;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ManagementHostUiToolkitBinder : MonoBehaviour, IBinder<NationalOverviewViewModel>
    {
        public const string RootName = "management-host-root";
        public const string TitleName = "management-host-title";
        public const string CloseButtonName = "management-host-close";
        public const string OverviewButtonName = "management-host-nav-overview";
        public const string TurnSummaryButtonName = "management-host-nav-turn-summary";
        public const string LedgerButtonName = "management-host-nav-ledger";
        public const string TechButtonName = "management-host-nav-tech";
        public const string BuildButtonName = "management-host-nav-build";
        public const string RecipeButtonName = "management-host-nav-recipe";
        public const string PolicyButtonName = "management-host-nav-policy";
        public const string OverviewPanelName = "management-host-overview-panel";
        public const string TurnValueName = "national-overview-turn-value";
        public const string PhaseValueName = "national-overview-phase-value";
        public const string TokensValueName = "national-overview-tokens-value";
        public const string ResearchValueName = "national-overview-research-value";
        public const string PolicyValueName = "national-overview-policy-value";
        public const string MetricsName = "national-overview-metrics";
        public const string ResourcesName = "national-overview-resources";
        public const string EventsName = "national-overview-events";
        public const string EmptyEventsName = "national-overview-empty-events";

        [SerializeField] private VisualTreeAsset visualTreeAsset;
        [SerializeField] private StyleSheet styleSheet;

        private Button _buildButton;
        private Button _closeButton;
        private VisualElement _events;
        private Label _emptyEvents;
        private Button _ledgerButton;
        private VisualElement _metrics;
        private Button _overviewButton;
        private VisualElement _overviewPanel;
        private Label _phaseValue;
        private Label _policyValue;
        private IDisposable _overviewSubscription;
        private Button _policyButton;
        private Button _recipeButton;
        private Label _researchValue;
        private VisualElement _resources;
        private Button _techButton;
        private Label _title;
        private Label _tokensValue;
        private Button _turnSummaryButton;
        private Label _turnValue;
        private UIDocument _uiDocument;
        private IDisposable _visibilitySubscription;
        private ManagementPanelVisibilityStore _visibilityStore;
        private NationalOverviewViewModel _viewModel;

        [Inject]
        private void Construct(
            NationalOverviewViewModel viewModel,
            ManagementPanelVisibilityStore visibilityStore)
        {
            _visibilityStore = visibilityStore;
            Bind(viewModel);
            EnsureVisibilitySubscription();
            EnsureDefaultPanel();
            ApplyVisibility();
        }

        private void Awake()
        {
            EnsureDocument();
            EnsureVisualTree();
            CacheElements();
            BindButtons();
        }

        private void OnEnable()
        {
            EnsureDocument();
            EnsureVisualTree();
            CacheElements();
            BindButtons();
            EnsureOverviewSubscription();
            EnsureVisibilitySubscription();
            EnsureDefaultPanel();
            Render(_viewModel?.Current);
            ApplyVisibility();
        }

        private void OnDisable()
        {
            UnbindButtons();
            StopOverviewSubscription();
            StopVisibilitySubscription();
        }

        private void OnDestroy()
        {
            Unbind();
            StopVisibilitySubscription();
        }

        public void Bind(NationalOverviewViewModel viewModel)
        {
            if (ReferenceEquals(_viewModel, viewModel))
            {
                EnsureOverviewSubscription();
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

            EnsureOverviewSubscription();
            Render(_viewModel.Current);
        }

        public void Unbind()
        {
            StopOverviewSubscription();
            _viewModel = null;
        }

        public void Render(NationalOverviewState state)
        {
            EnsureDocument();
            EnsureVisualTree();
            CacheElements();

            state ??= new NationalOverviewState();
            SetText(_turnValue, state.TurnText);
            SetText(_phaseValue, state.PhaseText);
            SetText(_tokensValue, state.TokensText);
            SetText(_researchValue, state.PlannedResearchText);
            SetText(_policyValue, state.PlannedPolicyText);
            RenderMetrics(state);
            RenderResources(state);
            RenderEvents(state);
            ApplyVisibility();
        }

        private void RenderMetrics(NationalOverviewState state)
        {
            if (_metrics == null)
            {
                return;
            }

            _metrics.Clear();
            for (var i = 0; i < state.Metrics.Count; i++)
            {
                _metrics.Add(CreatePairRow(
                    "national-overview-metric-" + SafeName(state.Metrics[i].Id),
                    state.Metrics[i].Label,
                    state.Metrics[i].Value));
            }
        }

        private void RenderResources(NationalOverviewState state)
        {
            if (_resources == null)
            {
                return;
            }

            _resources.Clear();
            for (var i = 0; i < state.Resources.Count; i++)
            {
                _resources.Add(CreatePairRow(
                    "national-overview-resource-" + SafeName(state.Resources[i].Id),
                    state.Resources[i].Label,
                    state.Resources[i].AmountText));
            }
        }

        private void RenderEvents(NationalOverviewState state)
        {
            if (_events == null)
            {
                return;
            }

            _events.Clear();
            if (!state.HasEvents)
            {
                SetDisplay(_emptyEvents, DisplayStyle.Flex);
                return;
            }

            SetDisplay(_emptyEvents, DisplayStyle.None);
            for (var i = 0; i < state.Events.Count; i++)
            {
                var row = new VisualElement { name = "national-overview-event-" + i };
                row.AddToClassList("national-overview-event");
                row.Add(new Label(state.Events[i].Title) { name = "national-overview-event-title" });
                if (!string.IsNullOrWhiteSpace(state.Events[i].Detail))
                {
                    row.Add(new Label(state.Events[i].Detail) { name = "national-overview-event-detail" });
                }

                _events.Add(row);
            }
        }

        private static VisualElement CreatePairRow(string name, string label, string value)
        {
            var row = new VisualElement { name = name };
            row.AddToClassList("national-overview-pair-row");
            row.Add(new Label(label ?? string.Empty) { name = name + "-label" });
            row.Add(new Label(value ?? string.Empty) { name = name + "-value" });
            return row;
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
            root.AddToClassList("management-host-root");
            var header = new VisualElement { name = "management-host-header" };
            header.AddToClassList("management-host-header");
            header.Add(new Label("National Overview") { name = TitleName });
            header.Add(new Button { name = CloseButtonName, text = "Close" });
            root.Add(header);

            var nav = new VisualElement { name = "management-host-nav" };
            nav.AddToClassList("management-host-nav");
            nav.Add(new Button { name = OverviewButtonName, text = "Overview" });
            nav.Add(new Button { name = TurnSummaryButtonName, text = "Turn" });
            nav.Add(new Button { name = LedgerButtonName, text = "Ledger" });
            nav.Add(new Button { name = TechButtonName, text = "Tech" });
            nav.Add(new Button { name = BuildButtonName, text = "Build" });
            nav.Add(new Button { name = RecipeButtonName, text = "Recipe" });
            nav.Add(new Button { name = PolicyButtonName, text = "Policy" });
            root.Add(nav);

            var overview = new VisualElement { name = OverviewPanelName };
            overview.AddToClassList("national-overview-panel");
            overview.Add(BuildSummaryRow("Turn", TurnValueName));
            overview.Add(BuildSummaryRow("Phase", PhaseValueName));
            overview.Add(BuildSummaryRow("Tokens", TokensValueName));
            overview.Add(BuildSummaryRow("Research", ResearchValueName));
            overview.Add(BuildSummaryRow("Policy", PolicyValueName));
            overview.Add(new Label("Metrics") { name = "national-overview-metrics-title" });
            overview.Add(new VisualElement { name = MetricsName });
            overview.Add(new Label("Resources") { name = "national-overview-resources-title" });
            overview.Add(new VisualElement { name = ResourcesName });
            overview.Add(new Label("Recent Events") { name = "national-overview-events-title" });
            overview.Add(new Label("No recent events") { name = EmptyEventsName });
            overview.Add(new VisualElement { name = EventsName });
            root.Add(overview);
            return root;
        }

        private static VisualElement BuildSummaryRow(string label, string valueName)
        {
            var row = new VisualElement();
            row.AddToClassList("national-overview-summary-row");
            row.Add(new Label(label) { name = valueName + "-label" });
            row.Add(new Label("--") { name = valueName });
            return row;
        }

        private void CacheElements()
        {
            var root = _uiDocument != null ? _uiDocument.rootVisualElement : null;
            _title = root?.Q<Label>(TitleName);
            _closeButton = root?.Q<Button>(CloseButtonName);
            _overviewButton = root?.Q<Button>(OverviewButtonName);
            _turnSummaryButton = root?.Q<Button>(TurnSummaryButtonName);
            _ledgerButton = root?.Q<Button>(LedgerButtonName);
            _techButton = root?.Q<Button>(TechButtonName);
            _buildButton = root?.Q<Button>(BuildButtonName);
            _recipeButton = root?.Q<Button>(RecipeButtonName);
            _policyButton = root?.Q<Button>(PolicyButtonName);
            _overviewPanel = root?.Q<VisualElement>(OverviewPanelName);
            _turnValue = root?.Q<Label>(TurnValueName);
            _phaseValue = root?.Q<Label>(PhaseValueName);
            _tokensValue = root?.Q<Label>(TokensValueName);
            _researchValue = root?.Q<Label>(ResearchValueName);
            _policyValue = root?.Q<Label>(PolicyValueName);
            _metrics = root?.Q<VisualElement>(MetricsName);
            _resources = root?.Q<VisualElement>(ResourcesName);
            _events = root?.Q<VisualElement>(EventsName);
            _emptyEvents = root?.Q<Label>(EmptyEventsName);
        }

        private void BindButtons()
        {
            BindButton(_closeButton, Close);
            BindButton(_overviewButton, ShowNationalOverview);
            BindButton(_turnSummaryButton, ShowTurnSummary);
            BindButton(_ledgerButton, ShowNationalLedger);
            BindButton(_techButton, ShowTechTree);
            BindButton(_buildButton, ShowBuildCatalog);
            BindButton(_recipeButton, ShowRecipeSynthesis);
            BindButton(_policyButton, ShowPolicyFocus);
        }

        private void UnbindButtons()
        {
            UnbindButton(_closeButton, Close);
            UnbindButton(_overviewButton, ShowNationalOverview);
            UnbindButton(_turnSummaryButton, ShowTurnSummary);
            UnbindButton(_ledgerButton, ShowNationalLedger);
            UnbindButton(_techButton, ShowTechTree);
            UnbindButton(_buildButton, ShowBuildCatalog);
            UnbindButton(_recipeButton, ShowRecipeSynthesis);
            UnbindButton(_policyButton, ShowPolicyFocus);
        }

        private static void BindButton(Button button, Action action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.clicked -= action;
            button.clicked += action;
        }

        private static void UnbindButton(Button button, Action action)
        {
            if (button != null && action != null)
            {
                button.clicked -= action;
            }
        }

        private void Close()
        {
            _visibilityStore?.Hide();
        }

        private void ShowNationalOverview()
        {
            _visibilityStore?.Show(ManagementPanelId.NationalOverview);
        }

        private void ShowTurnSummary()
        {
            _visibilityStore?.Show(ManagementPanelId.TurnSummary);
        }

        private void ShowNationalLedger()
        {
            _visibilityStore?.Show(ManagementPanelId.NationalLedger);
        }

        private void ShowTechTree()
        {
            _visibilityStore?.Show(ManagementPanelId.TechTree);
        }

        private void ShowBuildCatalog()
        {
            _visibilityStore?.Show(ManagementPanelId.BuildCatalog);
        }

        private void ShowRecipeSynthesis()
        {
            _visibilityStore?.Show(ManagementPanelId.RecipeSynthesis);
        }

        private void ShowPolicyFocus()
        {
            _visibilityStore?.Show(ManagementPanelId.PolicyFocus);
        }

        private void EnsureDefaultPanel()
        {
            if (_visibilityStore != null && _visibilityStore.Current.ActivePanel == ManagementPanelId.None)
            {
                _visibilityStore.Show(ManagementPanelId.NationalOverview);
            }
        }

        private void EnsureOverviewSubscription()
        {
            if (_viewModel != null && _overviewSubscription == null)
            {
                _overviewSubscription = _viewModel.State.Subscribe(this, static (state, self) => self.Render(state));
            }
        }

        private void EnsureVisibilitySubscription()
        {
            if (_visibilityStore != null && _visibilitySubscription == null)
            {
                _visibilitySubscription = _visibilityStore.State.Subscribe(this, static (_, self) => self.ApplyVisibility());
            }
        }

        private void StopOverviewSubscription()
        {
            _overviewSubscription?.Dispose();
            _overviewSubscription = null;
        }

        private void StopVisibilitySubscription()
        {
            _visibilitySubscription?.Dispose();
            _visibilitySubscription = null;
        }

        private void ApplyVisibility()
        {
            EnsureDocument();
            var root = _uiDocument?.rootVisualElement;
            if (root == null)
            {
                return;
            }

            var activePanel = _visibilityStore?.Current.ActivePanel ?? ManagementPanelId.None;
            root.style.display = activePanel == ManagementPanelId.None ? DisplayStyle.None : DisplayStyle.Flex;
            SetDisplay(_overviewPanel, activePanel == ManagementPanelId.NationalOverview ? DisplayStyle.Flex : DisplayStyle.None);
            SetText(_title, ResolveTitle(activePanel));
        }

        private static string ResolveTitle(ManagementPanelId panel)
        {
            return panel switch
            {
                ManagementPanelId.NationalOverview => "National Overview",
                ManagementPanelId.TurnSummary => "Turn Summary",
                ManagementPanelId.NationalLedger => "National Ledger",
                ManagementPanelId.TechTree => "Tech Tree",
                ManagementPanelId.BuildCatalog => "Build Catalog",
                ManagementPanelId.RecipeSynthesis => "Recipe Synthesis",
                ManagementPanelId.PolicyFocus => "Policy Focus",
                ManagementPanelId.MinisterReport => "Minister Report",
                _ => string.Empty
            };
        }

        private static string SafeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        private static void SetDisplay(VisualElement element, DisplayStyle display)
        {
            if (element != null)
            {
                element.style.display = display;
            }
        }

        private static void SetText(Label label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }
    }
}
