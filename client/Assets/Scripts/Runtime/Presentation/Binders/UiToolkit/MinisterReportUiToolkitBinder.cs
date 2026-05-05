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
    public sealed class MinisterReportUiToolkitBinder : MonoBehaviour, IBinder<MinisterReportViewModel>
    {
        public const string RootName = "minister-report-root";
        public const string TitleName = "minister-report-title";
        public const string CloseButtonName = "minister-report-close";
        public const string TabListName = "minister-report-tabs";
        public const string ChatScrollName = "minister-report-scroll";
        public const string ChatListName = "minister-report-chat";
        public const string EmptyName = "minister-report-empty";
        public const string OptionsName = "minister-report-options";

        [SerializeField] private VisualTreeAsset visualTreeAsset;
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private string avatarTextureRoot = "Icons/Ministers";
        [SerializeField] private string affectionHeartTextureResource = "Icons/UI/icon_affection_heart";
        [SerializeField] private string backgroundTextureResource = "Textures/UI/minister_report_background";

        private readonly Dictionary<string, int> _lastAffectionPulseByRole = new(StringComparer.OrdinalIgnoreCase);
        private VisualElement _chatList;
        private ScrollView _chatScroll;
        private Button _closeButton;
        private Label _emptyLabel;
        private VisualElement _options;
        private IDisposable _stateSubscription;
        private VisualElement _tabs;
        private Label _title;
        private UIDocument _uiDocument;
        private IDisposable _visibilitySubscription;
        private ManagementPanelVisibilityStore _visibilityStore;
        private MinisterReportViewModel _viewModel;

        [Inject]
        private void Construct(
            MinisterReportViewModel viewModel,
            ManagementPanelVisibilityStore visibilityStore)
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
            BindButtons();
        }

        private void OnEnable()
        {
            EnsureDocument();
            EnsureVisualTree();
            CacheElements();
            BindButtons();
            EnsureStateSubscription();
            EnsureVisibilitySubscription();
            Render(_viewModel?.Current);
            ApplyVisibility();
        }

        private void OnDisable()
        {
            UnbindButtons();
            StopStateSubscription();
            StopVisibilitySubscription();
        }

        private void OnDestroy()
        {
            Unbind();
            StopVisibilitySubscription();
        }

        public void Bind(MinisterReportViewModel viewModel)
        {
            if (ReferenceEquals(_viewModel, viewModel))
            {
                EnsureStateSubscription();
                Render(viewModel?.Current);
                return;
            }

            Unbind();
            _viewModel = viewModel;
            EnsureStateSubscription();
            Render(_viewModel?.Current);
        }

        public void Unbind()
        {
            StopStateSubscription();
            _viewModel = null;
        }

        public void Render(MinisterReportState state)
        {
            EnsureDocument();
            EnsureVisualTree();
            CacheElements();

            state ??= new MinisterReportState("大臣汇报", string.Empty, null, null, null);
            SetText(_title, state.Title);
            RenderTabs(state);
            RenderMessages(state);
            RenderOptions(state);
            ApplyVisibility();
        }

        private void RenderTabs(MinisterReportState state)
        {
            if (_tabs == null)
            {
                return;
            }

            _tabs.Clear();
            for (var i = 0; i < state.Ministers.Count; i++)
            {
                var minister = state.Ministers[i];
                var button = new Button(() => _viewModel?.SelectMinister(minister.Role))
                {
                    name = "minister-report-tab-" + SafeName(minister.Role)
                };
                button.AddToClassList("minister-report-tab");
                if (minister.IsSelected)
                {
                    button.AddToClassList("is-selected");
                }

                button.Add(CreateAvatar(minister.IconResource, minister.Role, minister.AvatarText, "minister-report-tab-avatar"));

                var text = new VisualElement();
                text.AddToClassList("minister-report-tab-text");
                text.Add(CreateLabel(minister.Name, "minister-report-tab-name"));
                text.Add(CreateLabel(minister.Title, "minister-report-tab-title"));
                button.Add(text);

                var affection = CreateAffectionBadge(minister, out var heartIcon, out var deltaLabel);
                button.Add(affection);
                _tabs.Add(button);
                PlayAffectionPulseIfNeeded(minister, heartIcon, deltaLabel);
            }
        }

        private VisualElement CreateAffectionBadge(MinisterTabState minister, out VisualElement heartIcon, out Label deltaLabel)
        {
            var badge = new VisualElement();
            badge.AddToClassList("minister-report-affection");

            heartIcon = new VisualElement();
            heartIcon.AddToClassList("minister-report-affection-heart");
            var texture = LoadAffectionHeartTexture();
            if (texture != null)
            {
                heartIcon.style.backgroundImage = new StyleBackground(texture);
            }

            var value = CreateLabel(minister.Affection.ToString(), "minister-report-affection-value");
            deltaLabel = CreateLabel("+" + Math.Max(0, minister.AffectionPulseDelta) + " \u597d\u611f", "minister-report-affection-delta");
            deltaLabel.style.display = DisplayStyle.None;

            badge.Add(heartIcon);
            badge.Add(value);
            badge.Add(deltaLabel);
            return badge;
        }

        private void PlayAffectionPulseIfNeeded(MinisterTabState minister, VisualElement heartIcon, Label deltaLabel)
        {
            if (minister == null || heartIcon == null || minister.AffectionPulseSequence <= 0)
            {
                return;
            }

            var role = minister.Role ?? string.Empty;
            _lastAffectionPulseByRole.TryGetValue(role, out var lastSequence);
            if (lastSequence >= minister.AffectionPulseSequence)
            {
                return;
            }

            _lastAffectionPulseByRole[role] = minister.AffectionPulseSequence;
            PlayAffectionPulse(heartIcon, deltaLabel);
        }

        private static void PlayAffectionPulse(VisualElement heartIcon, Label deltaLabel)
        {
            heartIcon.AddToClassList("is-pulsing");
            heartIcon.style.width = 24;
            heartIcon.style.height = 24;
            if (deltaLabel != null)
            {
                deltaLabel.style.display = DisplayStyle.Flex;
                deltaLabel.style.opacity = 1f;
            }

            heartIcon.schedule.Execute(() =>
            {
                heartIcon.style.width = 34;
                heartIcon.style.height = 34;
            }).ExecuteLater(25);
            heartIcon.schedule.Execute(() =>
            {
                heartIcon.style.width = 24;
                heartIcon.style.height = 24;
            }).ExecuteLater(210);
            heartIcon.schedule.Execute(() =>
            {
                heartIcon.RemoveFromClassList("is-pulsing");
                if (deltaLabel != null)
                {
                    deltaLabel.style.opacity = 0f;
                }
            }).ExecuteLater(650);
            heartIcon.schedule.Execute(() =>
            {
                if (deltaLabel != null)
                {
                    deltaLabel.style.display = DisplayStyle.None;
                }
            }).ExecuteLater(900);
        }

        private void RenderMessages(MinisterReportState state)
        {
            if (_chatList == null)
            {
                return;
            }

            _chatList.Clear();
            SetDisplay(_emptyLabel, state.HasMessages ? DisplayStyle.None : DisplayStyle.Flex);

            for (var i = 0; i < state.Messages.Count; i++)
            {
                _chatList.Add(CreateMessageRow(state.Messages[i]));
            }

            ScrollToBottom();
        }

        private VisualElement CreateMessageRow(MinisterChatMessageState message)
        {
            var row = new VisualElement
            {
                name = "minister-report-message-" + SafeName(message.Id)
            };
            row.AddToClassList("minister-report-message");
            row.AddToClassList(message.IsPlayer ? "is-player" : "is-minister");

            if (!message.IsPlayer)
            {
                row.Add(CreateAvatar(message.IconResource, message.MinisterRole, message.AvatarText, "minister-report-message-avatar"));
            }

            var body = new VisualElement();
            body.AddToClassList("minister-report-message-body");

            if (!message.IsPlayer)
            {
                var header = new VisualElement();
                header.AddToClassList("minister-report-message-header");
                header.Add(CreateLabel(message.MinisterName, "minister-report-message-name"));
                header.Add(CreateLabel(message.MinisterTitle, "minister-report-message-title"));
                body.Add(header);
            }

            var bubbleRow = new VisualElement();
            bubbleRow.AddToClassList("minister-report-bubble-row");
            if (!message.IsPlayer)
            {
                var tail = new VisualElement { name = "minister-report-bubble-tail-left" };
                tail.AddToClassList("minister-report-bubble-tail");
                tail.AddToClassList("is-left");
                bubbleRow.Add(tail);
            }

            var bubble = CreateLabel(message.Text, "minister-report-message-bubble");
            if (message.IsStreaming)
            {
                bubble.text += " ...";
            }

            bubbleRow.Add(bubble);
            if (message.IsPlayer)
            {
                var tail = new VisualElement { name = "minister-report-bubble-tail-right" };
                tail.AddToClassList("minister-report-bubble-tail");
                tail.AddToClassList("is-right");
                bubbleRow.Add(tail);
            }

            body.Add(bubbleRow);
            row.Add(body);
            return row;
        }

        private void RenderOptions(MinisterReportState state)
        {
            if (_options == null)
            {
                return;
            }

            _options.Clear();
            if (state.Options.Count == 0)
            {
                _options.Add(CreateLabel("暂无待回复选项", "minister-report-option-empty"));
                return;
            }

            for (var i = 0; i < state.Options.Count; i++)
            {
                var option = state.Options[i];
                var button = new Button(() => _viewModel?.ChooseOption(option))
                {
                    name = "minister-report-option-" + SafeName(option.Id),
                    text = option.Label
                };
                button.AddToClassList("minister-report-option");
                button.AddToClassList(option.Accept ? "is-accept" : "is-reject");
                _options.Add(button);
            }
        }

        private VisualElement CreateAvatar(string iconResource, string role, string fallbackText, string className)
        {
            var avatar = new VisualElement();
            avatar.AddToClassList(className);
            avatar.AddToClassList("minister-report-avatar");

            var texture = LoadAvatarTexture(iconResource, role);
            if (texture != null)
            {
                avatar.style.backgroundImage = new StyleBackground(texture);
            }

            var label = CreateLabel(fallbackText, "minister-report-avatar-text");
            if (texture != null)
            {
                label.style.display = DisplayStyle.None;
            }

            avatar.Add(label);
            return avatar;
        }

        private Texture2D LoadAvatarTexture(string iconResource, string role)
        {
            if (!string.IsNullOrWhiteSpace(iconResource))
            {
                var explicitTexture = Resources.Load<Texture2D>(iconResource.Trim().Trim('/'));
                if (explicitTexture != null)
                {
                    return explicitTexture;
                }
            }

            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(avatarTextureRoot))
            {
                return null;
            }

            return Resources.Load<Texture2D>(avatarTextureRoot.Trim().Trim('/') + "/" + role.Trim().ToLowerInvariant());
        }

        private Texture2D LoadAffectionHeartTexture()
        {
            return string.IsNullOrWhiteSpace(affectionHeartTextureResource)
                ? null
                : Resources.Load<Texture2D>(affectionHeartTextureResource.Trim().Trim('/'));
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
                UiToolkitRuntimeDocument.EnsureConfigured(_uiDocument, 430);
            }
        }

        private void EnsureVisualTree()
        {
            if (_uiDocument?.rootVisualElement == null)
            {
                return;
            }

            var root = _uiDocument.rootVisualElement;
            var existingRoot = root.Q<VisualElement>(RootName);
            if (existingRoot != null && existingRoot.ClassListContains("runtime-fallback-tree"))
            {
                EnsureRuntimeStyleSheet(root);
                return;
            }

            root.Clear();
            root.Add(BuildFallbackTree());
            EnsureRuntimeStyleSheet(root);
        }

        private void EnsureRuntimeStyleSheet(VisualElement root)
        {
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }
        }

        private static VisualElement BuildFallbackTree()
        {
            var root = new VisualElement { name = RootName };
            root.AddToClassList("minister-report-root");
            root.AddToClassList("runtime-fallback-tree");
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1f;
            root.style.color = new Color(0.95f, 0.89f, 0.78f, 1f);
            root.style.backgroundColor = new Color(0.038f, 0.027f, 0.022f, 0.98f);
            root.style.borderBottomColor = new Color(0.86f, 0.52f, 0.18f, 0.95f);
            root.style.borderLeftColor = new Color(0.86f, 0.52f, 0.18f, 0.95f);
            root.style.borderRightColor = new Color(0.86f, 0.52f, 0.18f, 0.95f);
            root.style.borderTopColor = new Color(0.86f, 0.52f, 0.18f, 0.95f);
            root.style.borderBottomWidth = 2f;
            root.style.borderLeftWidth = 2f;
            root.style.borderRightWidth = 2f;
            root.style.borderTopWidth = 2f;

            var header = new VisualElement();
            header.AddToClassList("minister-report-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.height = 48f;
            header.style.marginBottom = 12f;
            header.style.paddingLeft = 14f;
            header.style.paddingRight = 8f;
            header.style.backgroundColor = new Color(0.11f, 0.09f, 0.075f, 0.92f);
            header.style.borderBottomColor = new Color(0.86f, 0.52f, 0.18f, 0.55f);
            header.style.borderBottomWidth = 1f;

            var title = CreateLabel("大臣汇报", TitleName, "minister-report-title");
            title.style.fontSize = 20f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.98f, 0.89f, 0.68f, 1f);
            header.Add(title);

            var close = new Button { name = CloseButtonName, text = "X" };
            close.AddToClassList("minister-report-close");
            close.style.width = 34f;
            close.style.height = 30f;
            close.style.paddingBottom = 0f;
            close.style.paddingLeft = 0f;
            close.style.paddingRight = 0f;
            close.style.paddingTop = 0f;
            close.style.backgroundColor = new Color(0.46f, 0.28f, 0.13f, 0.95f);
            close.style.color = new Color(1f, 0.92f, 0.78f, 1f);
            close.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(close);
            root.Add(header);

            var body = new VisualElement();
            body.AddToClassList("minister-report-body");
            body.style.flexGrow = 1f;
            body.style.minHeight = 0f;
            body.style.flexDirection = FlexDirection.Row;

            var tabs = new VisualElement { name = TabListName };
            tabs.AddToClassList("minister-report-tabs");
            tabs.style.width = 210f;
            tabs.style.flexShrink = 0f;
            tabs.style.paddingTop = 4f;
            tabs.style.paddingRight = 14f;
            tabs.style.borderRightColor = new Color(0.82f, 0.58f, 0.28f, 0.45f);
            tabs.style.borderRightWidth = 1f;
            body.Add(tabs);

            var conversation = new VisualElement();
            conversation.AddToClassList("minister-report-conversation");
            conversation.style.flexGrow = 1f;
            conversation.style.minWidth = 0f;
            conversation.style.minHeight = 0f;
            conversation.style.paddingLeft = 18f;
            conversation.style.paddingRight = 12f;
            conversation.style.paddingTop = 10f;
            conversation.style.backgroundColor = new Color(0.03f, 0.04f, 0.045f, 0.62f);

            var empty = CreateLabel("暂无大臣汇报", EmptyName, "minister-report-empty");
            empty.style.color = new Color(0.63f, 0.67f, 0.69f, 1f);
            empty.style.marginBottom = 8f;
            conversation.Add(empty);

            var scroll = new ScrollView { name = ChatScrollName };
            scroll.AddToClassList("minister-report-scroll");
            scroll.style.flexGrow = 1f;
            scroll.style.minHeight = 0f;
            scroll.style.marginBottom = 12f;
            scroll.style.overflow = Overflow.Hidden;

            var chat = new VisualElement { name = ChatListName };
            chat.AddToClassList("minister-report-chat");
            chat.style.flexGrow = 1f;
            chat.style.minHeight = 0f;
            scroll.Add(chat);
            conversation.Add(scroll);

            var options = new VisualElement { name = OptionsName };
            options.AddToClassList("minister-report-options");
            options.style.minHeight = 48f;
            options.style.flexDirection = FlexDirection.Row;
            options.style.flexWrap = Wrap.Wrap;
            options.style.alignItems = Align.Center;
            options.style.paddingTop = 10f;
            options.style.borderTopColor = new Color(0.82f, 0.58f, 0.28f, 0.45f);
            options.style.borderTopWidth = 1f;
            conversation.Add(options);
            body.Add(conversation);
            root.Add(body);
            return root;
        }

        private void CacheElements()
        {
            var root = _uiDocument?.rootVisualElement;
            _title = root?.Q<Label>(TitleName);
            _closeButton = root?.Q<Button>(CloseButtonName);
            _tabs = root?.Q<VisualElement>(TabListName);
            _chatScroll = root?.Q<ScrollView>(ChatScrollName);
            _chatList = root?.Q<VisualElement>(ChatListName);
            _emptyLabel = root?.Q<Label>(EmptyName);
            _options = root?.Q<VisualElement>(OptionsName);
            if (_chatScroll != null)
            {
                _chatScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                _chatScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                ManagementPanelUiToolkitRenderer.EnableDragScroll(_chatScroll);
            }
        }

        private void BindButtons()
        {
            if (_closeButton != null)
            {
                _closeButton.clicked -= Close;
                _closeButton.clicked += Close;
            }
        }

        private void UnbindButtons()
        {
            if (_closeButton != null)
            {
                _closeButton.clicked -= Close;
            }
        }

        private void Close()
        {
            _visibilityStore?.Hide();
        }

        private void EnsureStateSubscription()
        {
            if (_viewModel != null && _stateSubscription == null)
            {
                _stateSubscription = _viewModel.State.Subscribe(this, static (state, self) => self.Render(state));
            }
        }

        private void StopStateSubscription()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;
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
            EnsureVisualTree();
            CacheElements();

            var root = _uiDocument?.rootVisualElement;
            var panelRoot = root?.Q<VisualElement>(RootName);
            if (root == null || panelRoot == null)
            {
                return;
            }

            root.pickingMode = PickingMode.Ignore;
            ManagementPanelRuntimeLayout.ApplyLargeModalPanel(root, panelRoot);
            ApplyRuntimePaint(root, panelRoot);
            var isVisible = _visibilityStore != null && _visibilityStore.IsVisible(ManagementPanelId.MinisterReport);

            root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void ApplyRuntimePaint(VisualElement root, VisualElement panelRoot)
        {
            if (root == null || panelRoot == null)
            {
                return;
            }

            root.style.backgroundColor = Color.clear;
            root.style.backgroundImage = StyleKeyword.None;
            panelRoot.style.backgroundImage = StyleKeyword.None;
            panelRoot.style.backgroundColor = new Color(0.038f, 0.027f, 0.022f, 0.98f);

            var header = panelRoot.Q<VisualElement>(className: "minister-report-header");
            if (header != null)
            {
                header.style.backgroundImage = StyleKeyword.None;
                header.style.backgroundColor = new Color(0.11f, 0.09f, 0.075f, 0.92f);
            }

            var conversation = panelRoot.Q<VisualElement>(className: "minister-report-conversation");
            if (conversation != null)
            {
                conversation.style.backgroundImage = StyleKeyword.None;
                conversation.style.backgroundColor = new Color(0.03f, 0.04f, 0.045f, 0.62f);
            }
        }

        private static void HideOtherManagementDocuments(UIDocument currentDocument)
        {
            var documents = Resources.FindObjectsOfTypeAll<UIDocument>();
            for (var i = 0; i < documents.Length; i++)
            {
                var document = documents[i];
                if (document == null ||
                    ReferenceEquals(document, currentDocument) ||
                    document.gameObject == null ||
                    !document.gameObject.scene.IsValid() ||
                    !IsManagementDocument(document.gameObject.name))
                {
                    continue;
                }

                var root = document.rootVisualElement;
                if (root != null)
                {
                    root.style.display = DisplayStyle.None;
                }

            }
        }

        private static bool IsManagementDocument(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            return objectName.StartsWith("ManagementHost", StringComparison.Ordinal) ||
                objectName.StartsWith("BuildCatalog", StringComparison.Ordinal) ||
                objectName.StartsWith("TechTree", StringComparison.Ordinal) ||
                objectName.StartsWith("RecipeSynthesis", StringComparison.Ordinal) ||
                objectName.StartsWith("PolicyFocus", StringComparison.Ordinal) ||
                objectName.StartsWith("NationalLedger", StringComparison.Ordinal);
        }

        private void ApplyPanelBackground(VisualElement panelRoot)
        {
            if (panelRoot == null || string.IsNullOrWhiteSpace(backgroundTextureResource))
            {
                return;
            }

            var texture = Resources.Load<Texture2D>(backgroundTextureResource.Trim());
            if (texture != null)
            {
                panelRoot.style.backgroundImage = new StyleBackground(texture);
            }
        }

        private void ScrollToBottom()
        {
            if (_chatScroll == null)
            {
                return;
            }

            _chatScroll.schedule.Execute(() =>
            {
                if (_chatScroll != null)
                {
                    _chatScroll.scrollOffset = new Vector2(0f, float.MaxValue);
                }
            });
        }

        private static Label CreateLabel(string text, string className)
        {
            return CreateLabel(text, string.Empty, className);
        }

        private static Label CreateLabel(string text, string name, string className)
        {
            var label = new Label(text ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(name))
            {
                label.name = name;
            }

            if (!string.IsNullOrWhiteSpace(className))
            {
                label.AddToClassList(className);
            }

            return label;
        }

        private static void SetDisplay(VisualElement element, DisplayStyle display)
        {
            if (element != null)
            {
                element.style.display = display;
            }
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }

        private static string SafeName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "empty"
                : value.Trim().ToLowerInvariant().Replace(' ', '-').Replace(':', '-');
        }
    }
}
