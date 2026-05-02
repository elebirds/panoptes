using System;
using System.Text;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Composition;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class GameChatPanelController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI transcriptText;
        [SerializeField] private int maxVisibleEntries = 8;
        [SerializeField] private string emptyText = "聊天消息会显示在这里";

        private IDisposable _chatSubscription;
        private IDisposable _gameStateSubscription;
        private GameChatState _chatState = new();
        private GameStateStore _gameStateStore;
        private GameChatStore _gameChatStore;
        private GameIntentService _gameIntentService;

        [Inject]
        private void Construct(
            GameIntentService gameIntentService,
            GameChatStore gameChatStore,
            GameStateStore gameStateStore)
        {
            _gameIntentService = gameIntentService;
            _gameChatStore = gameChatStore;
            _gameStateStore = gameStateStore;
        }

        private void OnEnable()
        {
            SceneCommandServiceInjector.InjectIfAvailable(this);
            _chatSubscription?.Dispose();
            _gameStateSubscription?.Dispose();
            _chatSubscription = _gameChatStore?.State.Subscribe(this, static (state, self) => self.RefreshTranscript(state));
            _gameStateSubscription = _gameStateStore?.State.Subscribe(this, static (_, self) => self.RefreshTranscript());

            RefreshTranscript(_gameChatStore?.Snapshot);
        }

        private void OnDisable()
        {
            _chatSubscription?.Dispose();
            _chatSubscription = null;
            _gameStateSubscription?.Dispose();
            _gameStateSubscription = null;
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
            _gameIntentService?.SendChatEmote(emote);
        }

        private void RefreshTranscript(GameChatState state)
        {
            _chatState = state ?? new GameChatState();
            RefreshTranscript();
        }

        private void RefreshTranscript()
        {
            if (transcriptText == null)
            {
                return;
            }

            var entries = _chatState?.Entries;
            if (entries == null || entries.Count == 0)
            {
                transcriptText.text = emptyText ?? string.Empty;
                return;
            }

            var start = Mathf.Max(0, entries.Count - Mathf.Max(1, maxVisibleEntries));
            var builder = new StringBuilder();
            for (var i = start; i < entries.Count; i++)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(FormatEntry(entries[i]));
            }

            transcriptText.text = builder.ToString();
        }

        private string FormatEntry(GameChatEntryDto entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            var speaker = ResolveSpeaker(entry.SenderPlayerId);
            var content = FormatPayload(entry.Payload);
            if (entry.Turn > 0)
            {
                return $"T{entry.Turn} {speaker}: {content}";
            }

            return $"{speaker}: {content}";
        }

        private string ResolveSpeaker(string senderPlayerId)
        {
            var myPlayerId = _gameStateStore?.Snapshot.MyPlayerId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(myPlayerId) && string.Equals(senderPlayerId, myPlayerId))
            {
                return "我";
            }

            return string.IsNullOrWhiteSpace(senderPlayerId) ? "未知玩家" : senderPlayerId;
        }

        private static string FormatPayload(GameChatPayloadDto payload)
        {
            if (payload == null)
            {
                return string.Empty;
            }

            return payload.Kind switch
            {
                GameChatPayloadKind.Emote => payload.Emote switch
                {
                    GameChatEmoteKind.ThumbsUp => "点赞",
                    GameChatEmoteKind.Thinking => "思考",
                    GameChatEmoteKind.Laugh => "大笑",
                    GameChatEmoteKind.Angry => "愤怒",
                    GameChatEmoteKind.Warning => "警告",
                    GameChatEmoteKind.Gg => "GG",
                    _ => "表情"
                },
                GameChatPayloadKind.Text => payload.Text ?? string.Empty,
                _ => string.Empty
            };
        }
    }
}
