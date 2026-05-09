using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Infrastructure.Mapper
{
    public static class GameChatMapper
    {
        public static GameChatEntryDto ToDto(ChatEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            return new GameChatEntryDto
            {
                Sequence = entry.Sequence,
                SenderPlayerId = entry.SenderPlayerId ?? string.Empty,
                Turn = entry.Turn,
                Phase = entry.Phase ?? string.Empty,
                Payload = ToDto(entry.Payload)
            };
        }

        public static GameChatPayloadDto ToDto(ChatPayload payload)
        {
            if (payload == null)
            {
                return new GameChatPayloadDto
                {
                    Kind = GameChatPayloadKind.None,
                    Text = string.Empty
                };
            }

            return payload.BodyCase switch
            {
                ChatPayload.BodyOneofCase.Emote => new GameChatPayloadDto
                {
                    Kind = GameChatPayloadKind.Emote,
                    Emote = ToCore(payload.Emote),
                    EmoteId = ToEmoteId(payload.Emote),
                    Text = string.Empty
                },
                ChatPayload.BodyOneofCase.EmoteId => new GameChatPayloadDto
                {
                    Kind = GameChatPayloadKind.Emote,
                    Emote = GameChatEmoteKind.Unspecified,
                    EmoteId = payload.EmoteId ?? string.Empty,
                    Text = string.Empty
                },
                ChatPayload.BodyOneofCase.Text => new GameChatPayloadDto
                {
                    Kind = GameChatPayloadKind.Text,
                    Emote = GameChatEmoteKind.Unspecified,
                    EmoteId = string.Empty,
                    Text = payload.Text ?? string.Empty
                },
                _ => new GameChatPayloadDto
                {
                    Kind = GameChatPayloadKind.None,
                    Emote = GameChatEmoteKind.Unspecified,
                    EmoteId = string.Empty,
                    Text = string.Empty
                }
            };
        }

        public static ChatEmote ToProtocol(GameChatEmoteKind emote)
        {
            return emote switch
            {
                GameChatEmoteKind.ThumbsUp => ChatEmote.ThumbsUp,
                GameChatEmoteKind.Thinking => ChatEmote.Thinking,
                GameChatEmoteKind.Laugh => ChatEmote.Laugh,
                GameChatEmoteKind.Angry => ChatEmote.Angry,
                GameChatEmoteKind.Warning => ChatEmote.Warning,
                GameChatEmoteKind.Gg => ChatEmote.Gg,
                _ => ChatEmote.Unspecified
            };
        }

        public static GameChatEmoteKind ToCore(ChatEmote emote)
        {
            return emote switch
            {
                ChatEmote.ThumbsUp => GameChatEmoteKind.ThumbsUp,
                ChatEmote.Thinking => GameChatEmoteKind.Thinking,
                ChatEmote.Laugh => GameChatEmoteKind.Laugh,
                ChatEmote.Angry => GameChatEmoteKind.Angry,
                ChatEmote.Warning => GameChatEmoteKind.Warning,
                ChatEmote.Gg => GameChatEmoteKind.Gg,
                _ => GameChatEmoteKind.Unspecified
            };
        }

        public static string ToEmoteId(ChatEmote emote)
        {
            return emote switch
            {
                ChatEmote.ThumbsUp => "general.thumbs_up",
                ChatEmote.Thinking => "general.thinking",
                ChatEmote.Laugh => "general.laugh",
                ChatEmote.Angry => "general.angry",
                ChatEmote.Warning => "general.warning",
                ChatEmote.Gg => "general.gg",
                _ => string.Empty
            };
        }
    }
}
