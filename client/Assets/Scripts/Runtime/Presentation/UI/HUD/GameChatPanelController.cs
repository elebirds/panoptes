using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Core.Infrastructure.Service;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class GameChatPanelController : MonoBehaviour
    {
        private const int EmotesPerPage = 6;
        private const float BubbleLifetimeSeconds = 3f;
        private const float PanelSpacing = 8f;
        private const float PanelLeftOffset = 0f;
        private const float PanelTopOffset = 340f;
        private const float RailWidth = 112f;
        private const float PickerWidth = 196f;
        private const float RailPadding = 8f;
        private const float RailToggleHeight = 34f;
        private const float RailAvatarHeight = 72f;
        private const float RailAvatarSpacing = 6f;
        private const float PickerPadding = 8f;
        private const float PickerTabsHeight = 36f;
        private const float SeriesTabIconSize = 30f;
        private const float PickerPageRowHeight = 24f;
        private const float PickerGridCellWidth = 58f;
        private const float PickerGridCellHeight = 52f;
        private const float PickerGridSpacing = 6f;
        private const int PickerGridColumns = 3;
        private const int PickerGridRows = 2;
        private const string LockIconResourcePath = "Textures/Emotes/lock";
        private const string PlayerAvatarResourcePath = "Textures/Emotes/player_avatar";

        [SerializeField] private TextMeshProUGUI transcriptText;

        private readonly Dictionary<string, AvatarSlotView> _avatarSlots = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<SeriesButtonView> _seriesButtons = new();
        private readonly List<EmoteButtonView> _emoteButtons = new();

        private IDisposable _chatSubscription;
        private IDisposable _catalogSubscription;
        private IDisposable _gameStateSubscription;

        private GameIntentService _gameIntentService;
        private GameChatStore _gameChatStore;
        private GameStateStore _gameStateStore;
        private StaticCatalogStore _staticCatalogStore;
        private RoomCache _roomCache;
        private SessionManager _sessionManager;

        private RectTransform _canvasRoot;
        private RectTransform _root;
        private RectTransform _avatarList;
        private RectTransform _pickerRoot;
        private RectTransform _seriesRow;
        private RectTransform _pageRow;
        private RectTransform _emoteGrid;
        private LayoutElement _avatarListLayout;
        private LayoutElement _seriesRowLayout;
        private LayoutElement _pageRowLayout;
        private LayoutElement _emoteGridLayout;
        private Button _pagePrevButton;
        private Button _pageNextButton;
        private TextMeshProUGUI _pageLabel;
        private Sprite _lockIconSprite;
        private Sprite _playerAvatarSprite;
        private bool _uiBuilt;
        private bool _pickerOpen;
        private int _seenChatEntries;
        private int _currentPageIndex;
        private string _activeSeriesId = string.Empty;
        private List<CatalogEmoteSeriesDto> _series = new();
        private List<CatalogEmoteDto> _emotes = new();
        private readonly Dictionary<string, Sprite> _emoteSpriteCache = new(StringComparer.OrdinalIgnoreCase);

        [Inject]
        private void Construct(
            GameIntentService gameIntentService,
            GameChatStore gameChatStore,
            GameStateStore gameStateStore,
            StaticCatalogStore staticCatalogStore,
            RoomCache roomCache,
            SessionManager sessionManager)
        {
            _gameIntentService = gameIntentService;
            _gameChatStore = gameChatStore;
            _gameStateStore = gameStateStore;
            _staticCatalogStore = staticCatalogStore;
            _roomCache = roomCache;
            _sessionManager = sessionManager;
        }

        private void Awake()
        {
            EnsureUiBuilt();
        }

        private void OnEnable()
        {
            EnsureUiBuilt();
            _seenChatEntries = _gameChatStore?.Snapshot?.Entries?.Count ?? 0;
            _chatSubscription = _gameChatStore?.State.Subscribe(this, static (state, self) => self.HandleChatState(state));
            _catalogSubscription = _staticCatalogStore?.State.Subscribe(this, static (state, self) => self.RefreshCatalog(state));
            _gameStateSubscription = _gameStateStore?.State.Subscribe(this, static (_, self) => self.RefreshRoster());

            if (_roomCache != null)
            {
                _roomCache.OnRoomStateChanged -= RefreshRoster;
                _roomCache.OnRoomStateChanged += RefreshRoster;
            }

            RefreshCatalog(_staticCatalogStore?.Snapshot);
            RefreshRoster();
        }

        private void OnDisable()
        {
            _chatSubscription?.Dispose();
            _catalogSubscription?.Dispose();
            _gameStateSubscription?.Dispose();
            _chatSubscription = null;
            _catalogSubscription = null;
            _gameStateSubscription = null;

            if (_roomCache != null)
            {
                _roomCache.OnRoomStateChanged -= RefreshRoster;
            }
        }

        public void SendThumbsUp()
        {
            SendEmote(GameChatEmoteKind.ThumbsUp);
        }

        public void SendThinking()
        {
            SendEmote(GameChatEmoteKind.Thinking);
        }

        public void SendLaugh()
        {
            SendEmote(GameChatEmoteKind.Laugh);
        }

        public void SendAngry()
        {
            SendEmote(GameChatEmoteKind.Angry);
        }

        public void SendWarning()
        {
            SendEmote(GameChatEmoteKind.Warning);
        }

        public void SendGg()
        {
            SendEmote(GameChatEmoteKind.Gg);
        }

        public void SendEmote(GameChatEmoteKind emote)
        {
            if (_gameIntentService == null)
            {
                return;
            }

            _gameIntentService.SendChatEmote(emote);
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt)
            {
                return;
            }

            var canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;

            var scaler = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            _canvasRoot = GetComponent<RectTransform>();
            if (_canvasRoot == null)
            {
                _canvasRoot = gameObject.AddComponent<RectTransform>();
            }

            _canvasRoot.anchorMin = Vector2.zero;
            _canvasRoot.anchorMax = Vector2.one;
            _canvasRoot.pivot = new Vector2(0.5f, 0.5f);
            _canvasRoot.anchoredPosition = Vector2.zero;
            _canvasRoot.offsetMin = Vector2.zero;
            _canvasRoot.offsetMax = Vector2.zero;

            _root = CreateRect("ChatHudRoot", _canvasRoot);
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 1f);
            _root.anchoredPosition = new Vector2(PanelLeftOffset, -PanelTopOffset);
            _root.sizeDelta = new Vector2(RailWidth + PanelSpacing + PickerWidth, ComputePanelHeight(1, 1));

            var rootImage = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = false;

            var rail = CreatePanel("AvatarRail", _root, TransparentColor(), raycastTarget: false);
            StretchLeft(rail, 0f, RailWidth);

            var railLayout = rail.gameObject.AddComponent<VerticalLayoutGroup>();
            railLayout.padding = new RectOffset((int)RailPadding, (int)RailPadding, (int)RailPadding, (int)RailPadding);
            railLayout.spacing = RailAvatarSpacing;
            railLayout.childControlHeight = false;
            railLayout.childControlWidth = true;
            railLayout.childForceExpandHeight = false;
            railLayout.childForceExpandWidth = true;

            var toggle = CreateButton("EmoteToggle", rail, "Emotes", new Color(0.23f, 0.32f, 0.39f, 0.96f));
            AddLayoutElement(toggle.gameObject, 0f, RailToggleHeight);
            toggle.onClick.AddListener(TogglePicker);

            _avatarList = CreatePanel("AvatarList", rail, TransparentColor(), raycastTarget: false);
            _avatarListLayout = AddLayoutElement(_avatarList.gameObject, 0f, 0f, 0f);
            var avatarLayout = _avatarList.gameObject.AddComponent<VerticalLayoutGroup>();
            avatarLayout.spacing = RailAvatarSpacing;
            avatarLayout.childControlHeight = false;
            avatarLayout.childControlWidth = true;
            avatarLayout.childForceExpandHeight = false;
            avatarLayout.childForceExpandWidth = true;

            _pickerRoot = CreatePanel("EmotePicker", _root, TransparentColor(), raycastTarget: false);
            StretchLeft(_pickerRoot, RailWidth + PanelSpacing, PickerWidth);
            var pickerLayout = _pickerRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            pickerLayout.padding = new RectOffset((int)PickerPadding, (int)PickerPadding, (int)PickerPadding, (int)PickerPadding);
            pickerLayout.spacing = PickerGridSpacing;
            pickerLayout.childControlHeight = false;
            pickerLayout.childControlWidth = true;
            pickerLayout.childForceExpandHeight = false;
            pickerLayout.childForceExpandWidth = true;

            _seriesRow = CreatePanel("SeriesTabs", _pickerRoot, TransparentColor(), raycastTarget: false);
            _seriesRowLayout = AddLayoutElement(_seriesRow.gameObject, 0f, PickerTabsHeight, 0f);
            var seriesLayout = _seriesRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            seriesLayout.spacing = 4f;
            seriesLayout.childControlHeight = true;
            seriesLayout.childControlWidth = true;
            seriesLayout.childForceExpandHeight = false;
            seriesLayout.childForceExpandWidth = false;

            _emoteGrid = CreatePanel("EmoteGrid", _pickerRoot, TransparentColor(), raycastTarget: false);
            _emoteGridLayout = AddLayoutElement(_emoteGrid.gameObject, 0f, ComputePickerGridHeight(), 0f);
            var grid = _emoteGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(PickerGridCellWidth, PickerGridCellHeight);
            grid.spacing = new Vector2(PickerGridSpacing, PickerGridSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = PickerGridColumns;

            _pageRow = CreatePanel("PageRow", _pickerRoot, TransparentColor(), raycastTarget: false);
            _pageRowLayout = AddLayoutElement(_pageRow.gameObject, 0f, PickerPageRowHeight, 0f);
            var pageLayout = _pageRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            pageLayout.spacing = 4f;
            pageLayout.childControlHeight = true;
            pageLayout.childControlWidth = true;
            pageLayout.childForceExpandHeight = true;
            pageLayout.childForceExpandWidth = false;

            _pagePrevButton = CreateButton("PagePrev", _pageRow, "<", new Color(0.20f, 0.24f, 0.27f, 0.92f));
            AddLayoutElement(_pagePrevButton.gameObject, 28f, PickerPageRowHeight);
            _pagePrevButton.onClick.AddListener(GoPreviousPage);

            _pageLabel = CreateText("PageLabel", _pageRow, string.Empty, 11, FontStyles.Bold, TextAlignmentOptions.Center);
            _pageLabel.color = new Color(0.92f, 0.94f, 0.96f, 1f);
            AddLayoutElement(_pageLabel.gameObject, 0f, PickerPageRowHeight, 0f, 1f);

            _pageNextButton = CreateButton("PageNext", _pageRow, ">", new Color(0.20f, 0.24f, 0.27f, 0.92f));
            AddLayoutElement(_pageNextButton.gameObject, 28f, PickerPageRowHeight);
            _pageNextButton.onClick.AddListener(GoNextPage);

            _pickerRoot.gameObject.SetActive(false);
            _uiBuilt = true;
            LoadUiSprites();
            RefreshPanelGeometry();
        }

        private void RefreshCatalog(StaticCatalogState catalog)
        {
            EnsureUiBuilt();
            _series = ResolveSeries(catalog).ToList();
            _emotes = ResolveEmotes(catalog).ToList();
            if (string.IsNullOrWhiteSpace(_activeSeriesId) || !_series.Any(series => series.Id == _activeSeriesId))
            {
                _activeSeriesId = _series.Count > 0 ? _series[0].Id : string.Empty;
            }
            _currentPageIndex = 0;

            RebuildSeriesTabs();
            RenderEmotePage();
            RefreshPanelGeometry();
        }

        private void RefreshRoster()
        {
            EnsureUiBuilt();
            foreach (var pair in _avatarSlots)
            {
                if (pair.Value.BubbleCoroutine != null)
                {
                    StopCoroutine(pair.Value.BubbleCoroutine);
                }

                Destroy(pair.Value.Root);
            }
            _avatarSlots.Clear();

            var players = ResolveRosterPlayers();
            foreach (var player in players)
            {
                EnsureAvatarSlot(player.PlayerId, player.DisplayName, player.IsSelf);
            }

            RefreshPanelGeometry();
        }

        private void HandleChatState(GameChatState state)
        {
            var entries = state?.Entries;
            if (entries == null)
            {
                _seenChatEntries = 0;
                return;
            }

            if (entries.Count < _seenChatEntries)
            {
                _seenChatEntries = entries.Count;
                return;
            }

            for (var i = _seenChatEntries; i < entries.Count; i++)
            {
                ShowEntryEmote(entries[i]);
            }

            _seenChatEntries = entries.Count;
        }

        private void RebuildSeriesTabs()
        {
            foreach (var view in _seriesButtons)
            {
                Destroy(view.Root);
            }
            _seriesButtons.Clear();

            foreach (var series in _series)
            {
                var icon = LoadSpriteFromResources(series.IconKey);
                var button = CreateSeriesTabButton("Series_" + SafeName(series.Id), _seriesRow, icon != null ? icon : _lockIconSprite);
                AddLayoutElement(button.gameObject, SeriesTabIconSize, SeriesTabIconSize, 0f, 0f);
                var seriesId = series.Id;
                button.onClick.AddListener(() => SelectSeries(seriesId));
                _seriesButtons.Add(new SeriesButtonView(button.gameObject, button, button.GetComponent<Image>(), button.transform.Find("Icon")?.GetComponent<Image>(), seriesId));
            }

            UpdateSeriesTabState();
        }

        private void SelectSeries(string seriesId)
        {
            _activeSeriesId = seriesId ?? string.Empty;
            _currentPageIndex = 0;
            UpdateSeriesTabState();
            RenderEmotePage();
        }

        private void UpdateSeriesTabState()
        {
            foreach (var view in _seriesButtons)
            {
                var image = view.BackgroundImage != null ? view.BackgroundImage : (view.Button != null ? view.Button.GetComponent<Image>() : null);
                var isActive = string.Equals(view.SeriesId, _activeSeriesId, StringComparison.OrdinalIgnoreCase);
                if (image != null)
                {
                    image.color = isActive ? ActiveTabColor() : InactiveTabColor();
                }

                if (view.IconImage != null)
                {
                    view.IconImage.color = Color.white;
                }
            }
        }

        private void RenderEmotePage()
        {
            while (_emoteButtons.Count < EmotesPerPage)
            {
                var button = CreateButton("EmoteSlot_" + _emoteButtons.Count, _emoteGrid, string.Empty, new Color(0.18f, 0.22f, 0.25f, 0.94f), null, false);
                _emoteButtons.Add(new EmoteButtonView(button.gameObject, button, button.GetComponentInChildren<TextMeshProUGUI>()));
            }

            var seriesEmotes = _emotes
                .Where(emote => string.Equals(emote.SeriesId, _activeSeriesId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(emote => emote.SortOrder)
                .ThenBy(emote => emote.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var totalPages = Math.Max(1, (seriesEmotes.Count + EmotesPerPage - 1) / EmotesPerPage);
            _currentPageIndex = Math.Max(0, Math.Min(_currentPageIndex, totalPages - 1));
            var page = seriesEmotes.Skip(_currentPageIndex * EmotesPerPage).Take(EmotesPerPage).ToList();
            SetPageControls(totalPages);

            for (var i = 0; i < _emoteButtons.Count; i++)
            {
                var view = _emoteButtons[i];
                view.Button.onClick.RemoveAllListeners();
                if (i >= page.Count)
                {
                    view.EmoteId = string.Empty;
                    view.Button.interactable = false;
                    SetButtonSprite(view.Button, null);
                    if (view.Label != null)
                    {
                        view.Label.text = string.Empty;
                        view.Label.color = new Color(1f, 1f, 1f, 0f);
                    }
                    continue;
                }

                var emote = page[i];
                view.EmoteId = emote.Id;
                view.Button.interactable = true;
                var sprite = LoadEmoteSprite(emote.AssetKey);
                SetButtonSprite(view.Button, sprite);
                if (view.Label != null)
                {
                    view.Label.text = sprite != null ? string.Empty : (string.IsNullOrWhiteSpace(emote.DisplayName) ? emote.Id : emote.DisplayName);
                    view.Label.color = sprite != null ? new Color(1f, 1f, 1f, 0f) : Color.white;
                }
                var emoteId = emote.Id;
                view.Button.onClick.AddListener(() => SendEmoteId(emoteId));
            }

            RefreshPanelGeometry();
        }

        private void SetPageControls(int totalPages)
        {
            if (_pageRow != null)
            {
                _pageRow.gameObject.SetActive(totalPages > 1);
            }

            if (_pagePrevButton != null)
            {
                _pagePrevButton.interactable = totalPages > 1 && _currentPageIndex > 0;
            }

            if (_pageNextButton != null)
            {
                _pageNextButton.interactable = totalPages > 1 && _currentPageIndex < totalPages - 1;
            }

            if (_pageLabel != null)
            {
                _pageLabel.text = totalPages > 1 ? string.Format("{0}/{1}", _currentPageIndex + 1, totalPages) : string.Empty;
            }
        }

        public void GoPreviousPage()
        {
            if (_currentPageIndex <= 0)
            {
                return;
            }

            _currentPageIndex--;
            RenderEmotePage();
        }

        public void GoNextPage()
        {
            var totalPages = GetCurrentSeriesPageCount();
            if (_currentPageIndex >= totalPages - 1)
            {
                return;
            }

            _currentPageIndex++;
            RenderEmotePage();
        }

        private int GetCurrentSeriesPageCount()
        {
            var count = _emotes.Count(emote => string.Equals(emote.SeriesId, _activeSeriesId, StringComparison.OrdinalIgnoreCase));
            return Math.Max(1, (count + EmotesPerPage - 1) / EmotesPerPage);
        }

        private void SendEmoteId(string emoteId)
        {
            if (_gameIntentService == null || string.IsNullOrWhiteSpace(emoteId))
            {
                return;
            }

            _gameIntentService.SendChatEmote(emoteId.Trim());
            SetPickerOpen(false);
        }

        private void TogglePicker()
        {
            SetPickerOpen(!_pickerOpen);
        }

        private void SetPickerOpen(bool open)
        {
            _pickerOpen = open;
            if (_pickerRoot != null)
            {
                _pickerRoot.gameObject.SetActive(open);
            }
        }

        private void ShowEntryEmote(GameChatEntryDto entry)
        {
            if (entry?.Payload == null || entry.Payload.Kind != GameChatPayloadKind.Emote)
            {
                return;
            }

            var emoteId = ResolveEmoteId(entry.Payload);
            if (string.IsNullOrWhiteSpace(emoteId))
            {
                return;
            }

            var playerId = entry.SenderPlayerId ?? string.Empty;
            var label = ResolveEmoteLabel(emoteId, entry.Payload);
            var slot = EnsureAvatarSlot(playerId, ResolvePlayerDisplayName(playerId), IsSelf(playerId));
            ShowBubble(slot, label, emoteId);
        }

        private AvatarSlotView EnsureAvatarSlot(string playerId, string displayName, bool isSelf)
        {
            playerId = string.IsNullOrWhiteSpace(playerId) ? "unknown" : playerId.Trim();
            if (_avatarSlots.TryGetValue(playerId, out var existing))
            {
                UpdateAvatarSlot(existing, playerId, displayName, isSelf);
                return existing;
            }

            var root = CreatePanel("Avatar_" + SafeName(playerId), _avatarList, new Color(0.11f, 0.14f, 0.17f, 0.92f));
            AddLayoutElement(root.gameObject, 0f, RailAvatarHeight);

            var avatar = CreatePanel(
                "AvatarMark",
                root,
                _playerAvatarSprite != null ? Color.white : (isSelf ? new Color(0.28f, 0.56f, 0.68f, 1f) : new Color(0.36f, 0.40f, 0.44f, 1f)),
                _playerAvatarSprite);
            StretchLeft(avatar, 8f, 52f);
            avatar.anchorMin = new Vector2(0f, 0.5f);
            avatar.anchorMax = new Vector2(0f, 0.5f);
            avatar.sizeDelta = new Vector2(52f, 52f);
            avatar.anchoredPosition = new Vector2(8f, 0f);

            var initial = CreateText("Initial", avatar, InitialFor(displayName), 17, FontStyles.Bold, TextAlignmentOptions.Center);
            initial.gameObject.SetActive(_playerAvatarSprite == null);
            Stretch(initial.rectTransform, 0f, 0f, 0f, 0f);

            var bubble = CreatePanel("EmoteBubble", root, new Color(0.86f, 0.80f, 0.55f, 0.96f));
            bubble.anchorMin = new Vector2(0f, 0.5f);
            bubble.anchorMax = new Vector2(0f, 0.5f);
            bubble.pivot = new Vector2(0f, 0.5f);
            bubble.sizeDelta = new Vector2(34f, 34f);
            bubble.anchoredPosition = new Vector2(58f, 14f);
            var bubbleImage = bubble.GetComponent<Image>();
            var bubbleText = CreateText("EmoteText", bubble, string.Empty, 9, FontStyles.Bold, TextAlignmentOptions.Center);
            bubbleText.color = new Color(0.08f, 0.08f, 0.07f, 1f);
            Stretch(bubbleText.rectTransform, 3f, 1f, 3f, 1f);
            bubble.gameObject.SetActive(false);

            var view = new AvatarSlotView(root.gameObject, avatar.GetComponent<Image>(), bubble.gameObject, bubbleImage, bubbleText);
            UpdateAvatarSlot(view, playerId, displayName, isSelf);
            _avatarSlots[playerId] = view;
            RefreshPanelGeometry();
            return view;
        }

        private void UpdateAvatarSlot(AvatarSlotView view, string playerId, string displayName, bool isSelf)
        {
            view.PlayerId = playerId;
            view.DisplayName = displayName;
            if (view.AvatarImage != null)
            {
                view.AvatarImage.color = view.AvatarImage.sprite != null
                    ? Color.white
                    : (isSelf ? new Color(0.28f, 0.56f, 0.68f, 1f) : new Color(0.36f, 0.40f, 0.44f, 1f));
            }
        }

        private void ShowBubble(AvatarSlotView slot, string label, string emoteId)
        {
            if (slot == null || slot.BubbleRoot == null)
            {
                return;
            }

            if (slot.BubbleCoroutine != null)
            {
                StopCoroutine(slot.BubbleCoroutine);
            }

            var sprite = ResolveEmoteSprite(emoteId);
            if (slot.BubbleImage != null)
            {
                slot.BubbleImage.sprite = sprite;
                slot.BubbleImage.type = sprite != null ? Image.Type.Simple : Image.Type.Sliced;
                slot.BubbleImage.preserveAspect = sprite != null;
                slot.BubbleImage.color = sprite != null ? Color.white : new Color(0.86f, 0.80f, 0.55f, 0.96f);
            }

            if (slot.BubbleText != null)
            {
                slot.BubbleText.gameObject.SetActive(sprite == null);
                slot.BubbleText.text = label;
            }

            slot.BubbleRoot.SetActive(true);
            slot.BubbleCoroutine = StartCoroutine(HideBubbleAfterDelay(slot));
        }

        private IEnumerator HideBubbleAfterDelay(AvatarSlotView slot)
        {
            yield return new WaitForSeconds(BubbleLifetimeSeconds);
            if (slot?.BubbleRoot != null)
            {
                slot.BubbleRoot.SetActive(false);
            }
            if (slot != null)
            {
                slot.BubbleCoroutine = null;
            }
        }

        private List<RosterPlayer> ResolveRosterPlayers()
        {
            var result = new List<RosterPlayer>();
            var myPlayerId = ResolveMyPlayerId();
            var rosterPlayers = ResolveSnapshotRosterPlayers();
            if (rosterPlayers.Count > 0)
            {
                foreach (var player in rosterPlayers)
                {
                    if (player == null || string.IsNullOrWhiteSpace(player.PlayerId))
                    {
                        continue;
                    }

                    result.Add(new RosterPlayer(
                        player.PlayerId,
                        string.IsNullOrWhiteSpace(player.Username) ? player.PlayerId : player.Username,
                        IsSelf(player.PlayerId)));
                }
            }
            else if (_roomCache?.Players != null)
            {
                foreach (var player in _roomCache.Players)
                {
                    if (player == null || string.IsNullOrWhiteSpace(player.PlayerId))
                    {
                        continue;
                    }

                    result.Add(new RosterPlayer(
                        player.PlayerId,
                        string.IsNullOrWhiteSpace(player.Username) ? player.PlayerId : player.Username,
                        IsSelf(player.PlayerId)));
                }
            }

            if (result.Count == 0 && !string.IsNullOrWhiteSpace(myPlayerId))
            {
                result.Add(new RosterPlayer(myPlayerId, "You", true));
            }

            return result;
        }

        private string ResolvePlayerDisplayName(string playerId)
        {
            var rosterPlayers = ResolveSnapshotRosterPlayers();
            foreach (var player in rosterPlayers)
            {
                if (player != null &&
                    string.Equals(player.PlayerId, playerId, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(player.Username))
                {
                    return player.Username;
                }
            }

            if (!string.IsNullOrWhiteSpace(playerId) && _roomCache?.Players != null)
            {
                foreach (var player in _roomCache.Players)
                {
                    if (player != null &&
                        string.Equals(player.PlayerId, playerId, StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(player.Username))
                    {
                        return player.Username;
                    }
                }
            }

            return IsSelf(playerId) ? "You" : (string.IsNullOrWhiteSpace(playerId) ? "Unknown" : playerId);
        }

        private IReadOnlyList<RoomPlayerDto> ResolveSnapshotRosterPlayers()
        {
            return _gameStateStore?.Snapshot?.RoomPlayers ?? Array.Empty<RoomPlayerDto>();
        }

        private bool IsSelf(string playerId)
        {
            var myPlayerId = ResolveMyPlayerId();
            return !string.IsNullOrWhiteSpace(playerId) &&
                   !string.IsNullOrWhiteSpace(myPlayerId) &&
                   string.Equals(playerId.Trim(), myPlayerId, StringComparison.Ordinal);
        }

        private string ResolveMyPlayerId()
        {
            return !string.IsNullOrWhiteSpace(_gameStateStore?.Snapshot?.MyPlayerId)
                ? _gameStateStore.Snapshot.MyPlayerId
                : (_sessionManager?.PlayerID ?? string.Empty);
        }

        private IEnumerable<CatalogEmoteSeriesDto> ResolveSeries(StaticCatalogState catalog)
        {
            var values = catalog?.EmoteSeries?.Values;
            if (values != null && values.Any())
            {
                return values.OrderBy(series => series.SortOrder).ThenBy(series => series.Id, StringComparer.OrdinalIgnoreCase);
            }

            return BuildFallbackSeries();
        }

        private IEnumerable<CatalogEmoteDto> ResolveEmotes(StaticCatalogState catalog)
        {
            var values = catalog?.Emotes?.Values;
            if (values != null && values.Any())
            {
                return values.OrderBy(emote => emote.SortOrder).ThenBy(emote => emote.Id, StringComparer.OrdinalIgnoreCase);
            }

            return BuildFallbackEmotes();
        }

        private string ResolveEmoteLabel(string emoteId, GameChatPayloadDto payload)
        {
            var emote = _emotes.FirstOrDefault(entry => string.Equals(entry.Id, emoteId, StringComparison.OrdinalIgnoreCase));
            if (emote != null && !string.IsNullOrWhiteSpace(emote.DisplayName))
            {
                return emote.DisplayName;
            }

            if (payload?.Emote != GameChatEmoteKind.Unspecified)
            {
                return payload.Emote.ToString();
            }

            return emoteId;
        }

        private Sprite ResolveEmoteSprite(string emoteId)
        {
            var emote = _emotes.FirstOrDefault(entry => string.Equals(entry.Id, emoteId, StringComparison.OrdinalIgnoreCase));
            if (emote == null)
            {
                return null;
            }

            return LoadEmoteSprite(emote.AssetKey);
        }

        private static string ResolveEmoteId(GameChatPayloadDto payload)
        {
            if (!string.IsNullOrWhiteSpace(payload?.EmoteId))
            {
                return payload.EmoteId.Trim();
            }

            return payload?.Emote switch
            {
                GameChatEmoteKind.ThumbsUp => "general.thumbs_up",
                GameChatEmoteKind.Thinking => "general.thinking",
                GameChatEmoteKind.Laugh => "general.laugh",
                GameChatEmoteKind.Angry => "general.angry",
                GameChatEmoteKind.Warning => "general.warning",
                GameChatEmoteKind.Gg => "general.gg",
                _ => string.Empty
            };
        }

        private static IEnumerable<CatalogEmoteSeriesDto> BuildFallbackSeries()
        {
            yield return new CatalogEmoteSeriesDto { Id = "general", DisplayName = "General", SortOrder = 10 };
            yield return new CatalogEmoteSeriesDto { Id = "tactics", DisplayName = "Tactics", SortOrder = 20 };
            yield return new CatalogEmoteSeriesDto { Id = "mood", DisplayName = "Mood", SortOrder = 30 };
        }

        private static IEnumerable<CatalogEmoteDto> BuildFallbackEmotes()
        {
            yield return Emote("general.thumbs_up", "general", "Thumbs Up", 10);
            yield return Emote("general.thinking", "general", "Thinking", 20);
            yield return Emote("general.laugh", "general", "Laugh", 30);
            yield return Emote("general.angry", "general", "Angry", 40);
            yield return Emote("general.warning", "general", "Warning", 50);
            yield return Emote("general.gg", "general", "GG", 60);
            yield return Emote("tactics.attack", "tactics", "Attack", 10);
            yield return Emote("tactics.defend", "tactics", "Defend", 20);
            yield return Emote("tactics.expand", "tactics", "Expand", 30);
            yield return Emote("tactics.wait", "tactics", "Wait", 40);
            yield return Emote("tactics.need_help", "tactics", "Need Help", 50);
            yield return Emote("tactics.ready", "tactics", "Ready", 60);
            yield return Emote("mood.happy", "mood", "Happy", 10);
            yield return Emote("mood.surprised", "mood", "Surprised", 20);
            yield return Emote("mood.sad", "mood", "Sad", 30);
            yield return Emote("mood.confused", "mood", "Confused", 40);
            yield return Emote("mood.proud", "mood", "Proud", 50);
            yield return Emote("mood.panic", "mood", "Panic", 60);
        }

        private static CatalogEmoteDto Emote(string id, string seriesId, string displayName, int sortOrder)
        {
            return new CatalogEmoteDto
            {
                Id = id,
                SeriesId = seriesId,
                DisplayName = displayName,
                AssetKey = string.Empty,
                SortOrder = sortOrder,
                Tags = new List<string>()
            };
        }

        private void LoadUiSprites()
        {
            _lockIconSprite = LoadSpriteFromResources(LockIconResourcePath);
            _playerAvatarSprite = LoadSpriteFromResources(PlayerAvatarResourcePath);
        }

        private Sprite LoadEmoteSprite(string assetKey)
        {
            if (string.IsNullOrWhiteSpace(assetKey))
            {
                return null;
            }

            assetKey = assetKey.Trim();
            if (_emoteSpriteCache.TryGetValue(assetKey, out var cached))
            {
                return cached;
            }

            var sprite = LoadSpriteFromResources(assetKey);
            _emoteSpriteCache[assetKey] = sprite;
            return sprite;
        }

        private static Sprite LoadSpriteFromResources(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void SetButtonSprite(Button button, Sprite sprite)
        {
            if (button == null)
            {
                return;
            }

            var image = button.targetGraphic as Image;
            if (image == null)
            {
                image = button.GetComponent<Image>();
            }

            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Simple : Image.Type.Sliced;
            image.preserveAspect = sprite != null;
            image.color = sprite != null ? Color.white : new Color(0.18f, 0.22f, 0.25f, 0.94f);
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color, Sprite iconSprite = null, bool showLabel = true)
        {
            var rect = CreatePanel(name, parent, color);
            var button = rect.gameObject.AddComponent<Button>();
            var image = rect.GetComponent<Image>();
            button.targetGraphic = image;
            if (image != null && iconSprite != null)
            {
                image.sprite = iconSprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
            }

            var text = CreateText("Label", rect, label, 12, FontStyles.Bold, TextAlignmentOptions.Center);
            text.color = showLabel ? Color.white : new Color(1f, 1f, 1f, 0f);
            Stretch(text.rectTransform, 4f, 2f, 4f, 2f);
            return button;
        }

        private static Button CreateSeriesTabButton(string name, Transform parent, Sprite iconSprite)
        {
            var rect = CreatePanel(name, parent, InactiveTabColor());
            var button = rect.gameObject.AddComponent<Button>();
            var image = rect.GetComponent<Image>();
            button.targetGraphic = image;

            var icon = CreatePanel("Icon", rect, Color.white, iconSprite);
            var iconImage = icon.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.raycastTarget = false;
            }
            icon.anchorMin = new Vector2(0.5f, 0.5f);
            icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(SeriesTabIconSize, SeriesTabIconSize);
            icon.anchoredPosition = Vector2.zero;

            return button;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color color, Sprite sprite = null, bool raycastTarget = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Color TransparentColor()
        {
            return new Color(0f, 0f, 0f, 0f);
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string text,
            int size,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text ?? string.Empty;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void StretchLeft(RectTransform rect, float left, float width)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(width, 0f);
        }

        private static LayoutElement AddLayoutElement(GameObject target, float preferredWidth, float preferredHeight, float flexibleHeight = 0f, float flexibleWidth = 0f)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            if (preferredWidth > 0f)
            {
                layout.preferredWidth = preferredWidth;
            }
            if (preferredHeight > 0f)
            {
                layout.preferredHeight = preferredHeight;
            }
            layout.flexibleHeight = flexibleHeight;
            layout.flexibleWidth = flexibleWidth;
            return layout;
        }

        private void RefreshPanelGeometry()
        {
            if (!_uiBuilt || _root == null)
            {
                return;
            }

            var avatarCount = Math.Max(1, _avatarSlots.Count);
            var pageCount = GetCurrentSeriesPageCount();
            var panelHeight = ComputePanelHeight(avatarCount, pageCount);
            _root.sizeDelta = new Vector2(RailWidth + PanelSpacing + PickerWidth, panelHeight);
            _root.anchoredPosition = new Vector2(PanelLeftOffset, -PanelTopOffset);

            if (_avatarListLayout != null)
            {
                _avatarListLayout.preferredHeight = ComputeRailAvatarListHeight(avatarCount);
                _avatarListLayout.flexibleHeight = 0f;
            }

            if (_seriesRowLayout != null)
            {
                _seriesRowLayout.preferredHeight = PickerTabsHeight;
                _seriesRowLayout.flexibleHeight = 0f;
            }

            if (_emoteGridLayout != null)
            {
                _emoteGridLayout.preferredHeight = ComputePickerGridHeight();
                _emoteGridLayout.flexibleHeight = 0f;
            }

            if (_pageRowLayout != null)
            {
                _pageRowLayout.preferredHeight = pageCount > 1 ? PickerPageRowHeight : 0f;
                _pageRowLayout.flexibleHeight = 0f;
            }

            if (_avatarList != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_avatarList);
            }

            if (_pickerRoot != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_pickerRoot);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
        }

        private static float ComputePanelHeight(int avatarCount, int pageCount)
        {
            return Math.Max(ComputeRailHeight(avatarCount), ComputePickerHeight(pageCount));
        }

        private static float ComputeRailHeight(int avatarCount)
        {
            return RailPadding * 2f + RailToggleHeight + RailAvatarSpacing + ComputeRailAvatarListHeight(avatarCount);
        }

        private static float ComputeRailAvatarListHeight(int avatarCount)
        {
            avatarCount = Math.Max(1, avatarCount);
            return avatarCount * RailAvatarHeight + Math.Max(0, avatarCount - 1) * RailAvatarSpacing;
        }

        private static float ComputePickerHeight(int pageCount)
        {
            var pageRowHeight = pageCount > 1 ? PickerGridSpacing + PickerPageRowHeight : 0f;
            return PickerPadding * 2f + PickerTabsHeight + PickerGridSpacing + ComputePickerGridHeight() + pageRowHeight;
        }

        private static float ComputePickerGridHeight()
        {
            return PickerGridRows * PickerGridCellHeight + Math.Max(0, PickerGridRows - 1) * PickerGridSpacing;
        }

        private static Color ActiveTabColor()
        {
            return new Color(0.31f, 0.47f, 0.58f, 0.98f);
        }

        private static Color InactiveTabColor()
        {
            return new Color(0.15f, 0.19f, 0.22f, 0.96f);
        }

        private static string InitialFor(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "?" : value.Trim().Substring(0, 1).ToUpperInvariant();
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            var chars = value.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }

        private sealed class AvatarSlotView
        {
            public AvatarSlotView(GameObject root, Image avatarImage, GameObject bubbleRoot, Image bubbleImage, TextMeshProUGUI bubbleText)
            {
                Root = root;
                AvatarImage = avatarImage;
                BubbleRoot = bubbleRoot;
                BubbleImage = bubbleImage;
                BubbleText = bubbleText;
            }

            public GameObject Root { get; }
            public Image AvatarImage { get; }
            public GameObject BubbleRoot { get; }
            public Image BubbleImage { get; }
            public TextMeshProUGUI BubbleText { get; }
            public string PlayerId { get; set; }
            public string DisplayName { get; set; }
            public Coroutine BubbleCoroutine { get; set; }
        }

        private sealed class SeriesButtonView
        {
            public SeriesButtonView(GameObject root, Button button, Image backgroundImage, Image iconImage, string seriesId)
            {
                Root = root;
                Button = button;
                BackgroundImage = backgroundImage;
                IconImage = iconImage;
                SeriesId = seriesId;
            }

            public GameObject Root { get; }
            public Button Button { get; }
            public Image BackgroundImage { get; }
            public Image IconImage { get; }
            public string SeriesId { get; }
        }

        private sealed class EmoteButtonView
        {
            public EmoteButtonView(GameObject root, Button button, TextMeshProUGUI label)
            {
                Root = root;
                Button = button;
                Label = label;
            }

            public GameObject Root { get; }
            public Button Button { get; }
            public TextMeshProUGUI Label { get; }
            public string EmoteId { get; set; }
        }

        private readonly struct RosterPlayer
        {
            public RosterPlayer(string playerId, string displayName, bool isSelf)
            {
                PlayerId = playerId ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                IsSelf = isSelf;
            }

            public string PlayerId { get; }
            public string DisplayName { get; }
            public bool IsSelf { get; }
        }
    }
}
