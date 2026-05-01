using System.Text;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Composition;
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

        private GameChatCache _chatCache;
        private GameStateCache _gameStateCache;
        private GameIntentService _gameIntentService;

        [Inject]
        private void Construct(GameIntentService gameIntentService)
        {
            _gameIntentService = gameIntentService;
        }

        private void OnEnable()
        {
            SceneCommandServiceInjector.InjectIfAvailable(this);
            _chatCache = GameChatCache.EnsureInstance();
            _gameStateCache = GameStateCache.Instance;

            if (_chatCache != null)
            {
                _chatCache.OnEntriesChanged += RefreshTranscript;
                _chatCache.OnEntryAdded += OnEntryAdded;
            }

            if (_gameStateCache != null)
            {
                _gameStateCache.OnStateChanged += RefreshTranscript;
            }

            RefreshTranscript();
        }

        private void OnDisable()
        {
            if (_chatCache != null)
            {
                _chatCache.OnEntriesChanged -= RefreshTranscript;
                _chatCache.OnEntryAdded -= OnEntryAdded;
            }

            if (_gameStateCache != null)
            {
                _gameStateCache.OnStateChanged -= RefreshTranscript;
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
            _gameIntentService?.SendChatEmote(emote);
        }

        private void OnEntryAdded(GameChatEntryDto _)
        {
            RefreshTranscript();
        }

        private void RefreshTranscript()
        {
            if (transcriptText == null)
            {
                return;
            }

            var entries = _chatCache != null ? _chatCache.Entries : null;
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
            var myPlayerId = _gameStateCache != null ? (_gameStateCache.MyPlayerID ?? string.Empty) : string.Empty;
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
