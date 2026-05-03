namespace Panoptes.Presentation.ViewModels
{
    public sealed class TokenHudState
    {
        public TokenHudState(
            int tokensLeft = 0,
            string statusText = "Ready",
            string phase = "",
            bool isGameOver = false,
            bool isActionLocked = false)
        {
            IsActionLocked = isActionLocked;
            IsGameOver = isGameOver;
            Phase = phase ?? string.Empty;
            StatusText = string.IsNullOrWhiteSpace(statusText) ? "Ready" : statusText;
            TokensLeft = tokensLeft;
        }

        public bool IsActionLocked { get; }
        public bool IsGameOver { get; }
        public string Phase { get; }
        public string StatusText { get; }
        public int TokensLeft { get; }

        public string TokenText => $"Tokens {TokensLeft}";
    }
}
