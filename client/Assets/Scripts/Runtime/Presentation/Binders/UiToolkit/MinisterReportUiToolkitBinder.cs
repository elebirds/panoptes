using System;
using System.Collections.Generic;
using System.Text;
using Panoptes.Presentation.ViewModels;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.UIElements;
using VContainer;
using UguiButton = UnityEngine.UI.Button;
using UguiImage = UnityEngine.UI.Image;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class MinisterReportUiToolkitBinder : MonoBehaviour, IBinder<MinisterReportViewModel>
    {
        private const int MinisterSortingOrder = 5000;
        private const int HiddenManagementSortingOrder = -1000;
        public const string RootName = "minister-report-root";
        public const string TitleName = "minister-report-title";
        public const string CloseButtonName = "minister-report-close";
        public const string TabListName = "minister-report-tabs";
        public const string ChatScrollName = "minister-report-scroll";
        public const string ChatListName = "minister-report-chat";
        public const string EmptyName = "minister-report-empty";
        public const string OptionsName = "minister-report-options";

        [SerializeField] private string avatarTextureRoot = "Icons/Ministers";

        private static readonly Color PanelColor = new(0.038f, 0.027f, 0.022f, 0.98f);
        private static readonly Color HeaderColor = new(0.11f, 0.09f, 0.075f, 0.96f);
        private static readonly Color ConversationColor = new(0.03f, 0.04f, 0.045f, 0.82f);
        private static readonly Color BorderColor = new(0.86f, 0.52f, 0.18f, 0.95f);
        private static readonly Color TextColor = new(0.98f, 0.9f, 0.72f, 1f);
        private static readonly Color MutedTextColor = new(0.66f, 0.72f, 0.75f, 1f);
        private static readonly Dictionary<string, Sprite> AvatarSprites = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<UguiButton> _optionButtons = new();
        private readonly List<UguiButton> _tabButtons = new();
        private Canvas _canvas;
        private RectTransform _chatContent;
        private RectTransform _optionsRoot;
        private RectTransform _panelRoot;
        private ScrollRect _scrollRect;
        private TextMeshProUGUI _titleText;
        private RectTransform _tabsContent;
        private IDisposable _stateSubscription;
        private IDisposable _visibilitySubscription;
        private ManagementPanelVisibilityStore _visibilityStore;
        private MinisterReportViewModel _viewModel;
        private string _lastMessagesSignature;
        private string _lastOptionsSignature;
        private string _lastTabsSignature;

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
            EnsureCanvas();
            ApplyVisibility();
        }

        private void OnEnable()
        {
            EnsureCanvas();
            EnsureStateSubscription();
            EnsureVisibilitySubscription();
            Render(_viewModel?.Current);
            ApplyVisibility();
        }

        private void OnDisable()
        {
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
            EnsureCanvas();
            state ??= new MinisterReportState("大臣汇报", string.Empty, null, null, null);
            SetText(_titleText, state.Title);
            RenderTabs(state);
            RenderMessages(state);
            RenderOptions(state);
            ApplyVisibility();
        }

        private void RenderTabs(MinisterReportState state)
        {
            var signature = BuildTabsSignature(state);
            if (string.Equals(signature, _lastTabsSignature, StringComparison.Ordinal))
            {
                return;
            }

            _lastTabsSignature = signature;
            ClearChildren(_tabsContent);
            _tabButtons.Clear();

            for (var i = 0; i < state.Ministers.Count; i++)
            {
                var minister = state.Ministers[i];
                var tab = CreateTab(minister);
                _tabButtons.Add(tab);
            }
        }

        private UguiButton CreateTab(MinisterTabState minister)
        {
            var rect = CreateUiObject("minister-tab-" + SafeName(minister.Role), _tabsContent);
            rect.sizeDelta = new Vector2(0f, 82f);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 82f;
            layout.preferredHeight = 82f;

            var image = rect.gameObject.AddComponent<UguiImage>();
            image.color = minister.IsSelected
                ? new Color(0.55f, 0.34f, 0.12f, 0.92f)
                : new Color(1f, 1f, 1f, 0.07f);
            var button = rect.gameObject.AddComponent<UguiButton>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => _viewModel?.SelectMinister(minister.Role));

            var avatarRect = CreateUiObject("Avatar", rect);
            AnchorFixed(avatarRect, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(54f, 54f));
            var avatarImage = avatarRect.gameObject.AddComponent<UguiImage>();
            avatarImage.color = new Color(0.26f, 0.28f, 0.32f, 1f);
            var sprite = LoadAvatarSprite(minister.IconResource, minister.Role);
            if (sprite != null)
            {
                avatarImage.sprite = sprite;
                avatarImage.color = Color.white;
                avatarImage.preserveAspect = true;
            }

            var avatarLabel = CreateText(avatarRect, "AvatarText", minister.AvatarText, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(avatarLabel.rectTransform, Vector2.zero, Vector2.zero);
            avatarLabel.gameObject.SetActive(sprite == null);

            var title = CreateText(rect, "Title", minister.Title, 15f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(74f, -30f), new Vector2(-10f, -8f));

            var name = CreateText(rect, "Name", minister.Name, 12.5f, FontStyles.Normal, TextAlignmentOptions.Left);
            name.color = new Color(0.86f, 0.91f, 0.94f, 1f);
            Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(74f, -51f), new Vector2(-10f, -31f));

            var affection = CreateText(rect, "Affection", "好感 " + minister.Affection, 12f, FontStyles.Bold, TextAlignmentOptions.Left);
            affection.color = new Color(1f, 0.77f, 0.88f, 1f);
            Anchor(affection.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(74f, 8f), new Vector2(-10f, 26f));

            return button;
        }

        private void RenderMessages(MinisterReportState state)
        {
            var signature = BuildMessagesSignature(state);
            if (string.Equals(signature, _lastMessagesSignature, StringComparison.Ordinal))
            {
                return;
            }

            _lastMessagesSignature = signature;
            ClearChildren(_chatContent);
            if (state.Messages.Count == 0)
            {
                var empty = CreateText(_chatContent, EmptyName, "暂无大臣汇报", 16f, FontStyles.Normal, TextAlignmentOptions.Left);
                empty.color = MutedTextColor;
                empty.textWrappingMode = TextWrappingModes.Normal;
                empty.rectTransform.sizeDelta = new Vector2(0f, 32f);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
                return;
            }

            for (var i = 0; i < state.Messages.Count; i++)
            {
                CreateMessageRow(state.Messages[i]);
            }

            Canvas.ForceUpdateCanvases();
            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void CreateMessageRow(MinisterChatMessageState message)
        {
            var row = CreateUiObject("message-" + SafeName(message.Id), _chatContent);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childAlignment = message.IsPlayer ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            var rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.minHeight = 58f;
            rowElement.flexibleWidth = 1f;

            if (!message.IsPlayer)
            {
                var avatarRect = CreateUiObject("Avatar", row);
                avatarRect.sizeDelta = new Vector2(42f, 42f);
                avatarRect.gameObject.AddComponent<LayoutElement>().preferredWidth = 42f;
                var avatarImage = avatarRect.gameObject.AddComponent<UguiImage>();
                avatarImage.color = new Color(0.26f, 0.28f, 0.32f, 1f);
                var sprite = LoadAvatarSprite(message.IconResource, message.MinisterRole);
                if (sprite != null)
                {
                    avatarImage.sprite = sprite;
                    avatarImage.color = Color.white;
                    avatarImage.preserveAspect = true;
                }

                var avatarLabel = CreateText(avatarRect, "AvatarText", message.AvatarText, 15f, FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(avatarLabel.rectTransform, Vector2.zero, Vector2.zero);
                avatarLabel.gameObject.SetActive(sprite == null);
            }

            if (message.IsPlayer)
            {
                var spacer = CreateUiObject("Spacer", row);
                spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            }

            var bubbleRoot = CreateUiObject("Bubble", row);
            var bubbleLayoutElement = bubbleRoot.gameObject.AddComponent<LayoutElement>();
            bubbleLayoutElement.preferredWidth = 920f;
            bubbleLayoutElement.flexibleWidth = 0f;
            var bubbleImage = bubbleRoot.gameObject.AddComponent<UguiImage>();
            bubbleImage.color = message.IsPlayer
                ? new Color(0.24f, 0.42f, 0.55f, 0.92f)
                : new Color(1f, 1f, 1f, 0.09f);

            var bubbleLayout = bubbleRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            bubbleLayout.padding = new RectOffset(12, 12, 10, 10);
            bubbleLayout.spacing = 4f;
            bubbleLayout.childControlWidth = true;
            bubbleLayout.childControlHeight = true;
            bubbleLayout.childForceExpandWidth = true;
            bubbleLayout.childForceExpandHeight = false;
            bubbleRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (!message.IsPlayer)
            {
                var header = CreateText(bubbleRoot, "Header", BuildMessageHeader(message), 12f, FontStyles.Bold, TextAlignmentOptions.Left);
                header.color = new Color(0.96f, 0.83f, 0.55f, 1f);
            }

            var body = CreateText(bubbleRoot, "Text", message.Text + (message.IsStreaming ? " ..." : string.Empty), 14f, FontStyles.Normal, TextAlignmentOptions.Left);
            body.color = new Color(0.93f, 0.96f, 0.97f, 1f);
            body.textWrappingMode = TextWrappingModes.Normal;

            if (!message.IsPlayer)
            {
                var spacer = CreateUiObject("Spacer", row);
                spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            }
        }

        private void RenderOptions(MinisterReportState state)
        {
            var signature = BuildOptionsSignature(state);
            if (string.Equals(signature, _lastOptionsSignature, StringComparison.Ordinal))
            {
                return;
            }

            _lastOptionsSignature = signature;
            ClearChildren(_optionsRoot);
            _optionButtons.Clear();

            if (state.Options.Count == 0)
            {
                var empty = CreateText(_optionsRoot, "minister-report-option-empty", "暂无待回复选项", 13f, FontStyles.Normal, TextAlignmentOptions.Left);
                empty.color = MutedTextColor;
                empty.gameObject.AddComponent<LayoutElement>().preferredWidth = 220f;
                return;
            }

            for (var i = 0; i < state.Options.Count; i++)
            {
                var option = state.Options[i];
                var button = CreateOptionButton(option);
                _optionButtons.Add(button);
            }
        }

        private UguiButton CreateOptionButton(MinisterReplyOptionState option)
        {
            var rect = CreateUiObject("option-" + SafeName(option.Id), _optionsRoot);
            rect.sizeDelta = new Vector2(150f, 38f);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = Mathf.Max(120f, 22f + (option.Label?.Length ?? 0) * 15f);
            layout.preferredHeight = 38f;

            var image = rect.gameObject.AddComponent<UguiImage>();
            image.color = option.Accept
                ? new Color(0.34f, 0.48f, 0.25f, 0.94f)
                : new Color(0.52f, 0.31f, 0.28f, 0.92f);
            var button = rect.gameObject.AddComponent<UguiButton>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => _viewModel?.ChooseOption(option));

            var label = CreateText(rect, "Label", option.Label, 14f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static string BuildTabsSignature(MinisterReportState state)
        {
            if (state?.Ministers == null || state.Ministers.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(state.Ministers.Count * 64);
            for (var i = 0; i < state.Ministers.Count; i++)
            {
                var item = state.Ministers[i];
                if (item == null)
                {
                    builder.Append("<null>|");
                    continue;
                }

                builder.Append(item.Role).Append('|')
                    .Append(item.Name).Append('|')
                    .Append(item.Title).Append('|')
                    .Append(item.IconResource).Append('|')
                    .Append(item.AvatarText).Append('|')
                    .Append(item.IsSelected).Append('|')
                    .Append(item.Affection).Append('|')
                    .Append(item.AffectionPulseSequence).Append('|')
                    .Append(item.AffectionPulseDelta).Append('\n');
            }

            return builder.ToString();
        }

        private static string BuildMessagesSignature(MinisterReportState state)
        {
            if (state?.Messages == null || state.Messages.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(state.Messages.Count * 96);
            for (var i = 0; i < state.Messages.Count; i++)
            {
                var item = state.Messages[i];
                if (item == null)
                {
                    builder.Append("<null>|");
                    continue;
                }

                builder.Append(item.Id).Append('|')
                    .Append(item.MinisterRole).Append('|')
                    .Append(item.MinisterName).Append('|')
                    .Append(item.MinisterTitle).Append('|')
                    .Append(item.IconResource).Append('|')
                    .Append(item.AvatarText).Append('|')
                    .Append(item.IsPlayer).Append('|')
                    .Append(item.IsStreaming).Append('|')
                    .Append(item.Text).Append('\n');
            }

            return builder.ToString();
        }

        private static string BuildOptionsSignature(MinisterReportState state)
        {
            if (state?.Options == null || state.Options.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(state.Options.Count * 64);
            for (var i = 0; i < state.Options.Count; i++)
            {
                var item = state.Options[i];
                if (item == null)
                {
                    builder.Append("<null>|");
                    continue;
                }

                builder.Append(item.Id).Append('|')
                    .Append(item.DraftId).Append('|')
                    .Append(item.MinisterRole).Append('|')
                    .Append(item.Label).Append('|')
                    .Append(item.PlayerText).Append('|')
                    .Append(item.Accept).Append('\n');
            }

            return builder.ToString();
        }

        private void EnsureCanvas()
        {
            DisableLegacyDocument();
            var rect = EnsureRectTransform(gameObject);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
            }

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = MinisterSortingOrder;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            EnsureEventSystem();
            EnsureLayout(rect);
        }

        private void DisableLegacyDocument()
        {
            var document = GetComponent<UIDocument>();
            if (document != null)
            {
                document.enabled = false;
                if (document.rootVisualElement != null)
                {
                    document.rootVisualElement.style.display = DisplayStyle.None;
                }
            }
        }

        private void EnsureLayout(RectTransform root)
        {
            if (_panelRoot != null)
            {
                return;
            }

            _panelRoot = CreateUiObject(RootName, root);
            _panelRoot.anchorMin = new Vector2(0.05f, 0.05f);
            _panelRoot.anchorMax = new Vector2(0.95f, 0.95f);
            _panelRoot.offsetMin = Vector2.zero;
            _panelRoot.offsetMax = Vector2.zero;

            var panelImage = _panelRoot.gameObject.AddComponent<UguiImage>();
            panelImage.color = PanelColor;

            CreateBorder(_panelRoot);

            var header = CreateUiObject("minister-report-header", _panelRoot);
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -82f), new Vector2(-28f, -24f));
            header.gameObject.AddComponent<UguiImage>().color = HeaderColor;

            _titleText = CreateText(header, TitleName, "大臣汇报", 22f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(_titleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 0f), new Vector2(-58f, 0f));

            var closeButton = CreateHeaderButton(header, CloseButtonName, "X");
            closeButton.onClick.AddListener(Close);

            var body = CreateUiObject("minister-report-body", _panelRoot);
            Anchor(body, Vector2.zero, Vector2.one, new Vector2(28f, 24f), new Vector2(-28f, -102f));

            var tabsPanel = CreateUiObject(TabListName, body);
            Anchor(tabsPanel, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(230f, 0f));
            tabsPanel.gameObject.AddComponent<UguiImage>().color = new Color(1f, 1f, 1f, 0.035f);

            _tabsContent = CreateUiObject("TabsContent", tabsPanel);
            Stretch(_tabsContent, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            var tabsLayout = _tabsContent.gameObject.AddComponent<VerticalLayoutGroup>();
            tabsLayout.spacing = 10f;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childForceExpandHeight = false;

            var conversation = CreateUiObject("minister-report-conversation", body);
            Anchor(conversation, new Vector2(0f, 0f), Vector2.one, new Vector2(254f, 0f), Vector2.zero);
            conversation.gameObject.AddComponent<UguiImage>().color = ConversationColor;

            CreateChatScroll(conversation);
            CreateOptionsRoot(conversation);
        }

        private void CreateChatScroll(RectTransform parent)
        {
            var scrollRoot = CreateUiObject(ChatScrollName, parent);
            Anchor(scrollRoot, new Vector2(0f, 0f), Vector2.one, new Vector2(18f, 92f), new Vector2(-18f, -16f));

            var viewport = CreateUiObject("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<UguiImage>().color = Color.clear;

            _chatContent = CreateUiObject(ChatListName, viewport);
            _chatContent.anchorMin = new Vector2(0f, 1f);
            _chatContent.anchorMax = new Vector2(1f, 1f);
            _chatContent.pivot = new Vector2(0.5f, 1f);
            _chatContent.offsetMin = Vector2.zero;
            _chatContent.offsetMax = Vector2.zero;
            var layout = _chatContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _chatContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            _scrollRect.viewport = viewport;
            _scrollRect.content = _chatContent;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        private void CreateOptionsRoot(RectTransform parent)
        {
            _optionsRoot = CreateUiObject(OptionsName, parent);
            Anchor(_optionsRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 16f), new Vector2(-18f, 78f));
            var layout = _optionsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
        }

        private UguiButton CreateHeaderButton(RectTransform parent, string name, string label)
        {
            var rect = CreateUiObject(name, parent);
            AnchorFixed(rect, new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(36f, 32f));
            var image = rect.gameObject.AddComponent<UguiImage>();
            image.color = new Color(0.46f, 0.28f, 0.13f, 0.95f);
            var button = rect.gameObject.AddComponent<UguiButton>();
            button.targetGraphic = image;
            var text = CreateText(rect, "Label", label, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static void CreateBorder(RectTransform parent)
        {
            CreateLine(parent, "BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), Vector2.zero);
            CreateLine(parent, "BorderBottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f));
            CreateLine(parent, "BorderLeft", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(2f, 0f));
            CreateLine(parent, "BorderRight", new Vector2(1f, 0f), Vector2.one, new Vector2(-2f, 0f), Vector2.zero);
        }

        private static void CreateLine(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = CreateUiObject(name, parent);
            Anchor(rect, anchorMin, anchorMax, offsetMin, offsetMax);
            rect.gameObject.AddComponent<UguiImage>().color = BorderColor;
        }

        private void ApplyVisibility()
        {
            EnsureCanvas();
            var isVisible = _visibilityStore != null && _visibilityStore.IsVisible(ManagementPanelId.MinisterReport);
            _canvas.enabled = isVisible;
            if (_panelRoot != null)
            {
                _panelRoot.gameObject.SetActive(isVisible);
            }

            if (isVisible)
            {
                HideOtherManagementDocuments();
                PanoptesLog.Log($"[MinisterReportUGUI] visible ministers={_viewModel?.Current?.Ministers.Count ?? 0} messages={_viewModel?.Current?.Messages.Count ?? 0} options={_viewModel?.Current?.Options.Count ?? 0}");
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

        private Sprite LoadAvatarSprite(string iconResource, string role)
        {
            var path = !string.IsNullOrWhiteSpace(iconResource)
                ? iconResource.Trim().Trim('/')
                : (!string.IsNullOrWhiteSpace(role) && !string.IsNullOrWhiteSpace(avatarTextureRoot)
                    ? avatarTextureRoot.Trim().Trim('/') + "/" + role.Trim().ToLowerInvariant()
                    : string.Empty);
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (AvatarSprites.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                AvatarSprites[path] = null;
                return null;
            }

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name + "_Sprite";
            AvatarSprites[path] = sprite;
            return sprite;
        }

        private static string BuildMessageHeader(MinisterChatMessageState message)
        {
            if (string.IsNullOrWhiteSpace(message.MinisterTitle))
            {
                return message.MinisterName ?? string.Empty;
            }

            return string.IsNullOrWhiteSpace(message.MinisterName)
                ? message.MinisterTitle
                : message.MinisterName + "  " + message.MinisterTitle;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string name, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var rect = CreateUiObject(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text ?? string.Empty;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = TextColor;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            if (TMP_Settings.defaultFontAsset != null)
            {
                label.font = TMP_Settings.defaultFontAsset;
            }

            return label;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static RectTransform CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform EnsureRectTransform(GameObject target)
        {
            var rect = target.GetComponent<RectTransform>();
            if (rect != null)
            {
                return rect;
            }

            return target.AddComponent<RectTransform>();
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static void AnchorFixed(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            Anchor(rect, Vector2.zero, Vector2.one, offsetMin, offsetMax);
        }

        private static void ClearChildren(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "empty";
            }

            var chars = value.Trim().ToLowerInvariant().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_')
                {
                    chars[i] = '-';
                }
            }

            return new string(chars);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || HasSceneEventSystem())
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static bool HasSceneEventSystem()
        {
            var eventSystems = Resources.FindObjectsOfTypeAll<EventSystem>();
            for (var i = 0; i < eventSystems.Length; i++)
            {
                var eventSystem = eventSystems[i];
                if (eventSystem != null &&
                    eventSystem.gameObject != null &&
                    eventSystem.gameObject.scene.IsValid())
                {
                    return true;
                }
            }

            return false;
        }

        private static void HideOtherManagementDocuments()
        {
            var documents = Resources.FindObjectsOfTypeAll<UIDocument>();
            for (var i = 0; i < documents.Length; i++)
            {
                var document = documents[i];
                if (document == null ||
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

                document.sortingOrder = HiddenManagementSortingOrder;
                if (document.panelSettings != null)
                {
                    document.panelSettings.sortingOrder = HiddenManagementSortingOrder;
                    document.panelSettings.clearColor = false;
                    document.panelSettings.clearDepthStencil = false;
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
    }
}
