namespace Panoptes.Core.Application.Stores
{
    public sealed class GameOverState
    {
        public GameOverState(
            bool isGameOver = false,
            bool isWinner = false,
            string winnerId = "",
            string loserId = "",
            string reason = "",
            string narrative = "")
        {
            IsGameOver = isGameOver;
            IsWinner = isWinner;
            LoserId = loserId ?? string.Empty;
            Narrative = narrative ?? string.Empty;
            Reason = reason ?? string.Empty;
            WinnerId = winnerId ?? string.Empty;
        }

        public bool IsGameOver { get; }
        public bool IsWinner { get; }
        public string LoserId { get; }
        public string Narrative { get; }
        public string Reason { get; }
        public string WinnerId { get; }

        internal GameOverState Clone()
        {
            return new GameOverState(IsGameOver, IsWinner, WinnerId, LoserId, Reason, Narrative);
        }
    }
}
