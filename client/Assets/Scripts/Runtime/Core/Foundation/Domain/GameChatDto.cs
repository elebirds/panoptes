namespace Panoptes.Core.Domain
{
    public enum GameChatEmoteKind
    {
        Unspecified = 0,
        ThumbsUp = 1,
        Thinking = 2,
        Laugh = 3,
        Angry = 4,
        Warning = 5,
        Gg = 6
    }

    public enum GameChatPayloadKind
    {
        None = 0,
        Emote = 1,
        Text = 2
    }

    public sealed class GameChatPayloadDto
    {
        public GameChatPayloadKind Kind;
        public GameChatEmoteKind Emote;
        public string EmoteId;
        public string Text;
    }

    public sealed class GameChatEntryDto
    {
        public long Sequence;
        public string SenderPlayerId;
        public int Turn;
        public string Phase;
        public GameChatPayloadDto Payload;
    }
}
