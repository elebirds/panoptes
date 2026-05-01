using System.Collections.Generic;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class TurnSummaryState
    {
        public TurnSummaryState(
            int turn = 0,
            string phase = "",
            string phaseLabel = "Unknown phase",
            int tokensLeft = 0,
            bool isGameOver = false,
            int visibleNodeCount = 0,
            int unitCount = 0,
            IReadOnlyList<TurnSummaryEventState> events = null)
        {
            Events = events != null
                ? new List<TurnSummaryEventState>(events)
                : new List<TurnSummaryEventState>();
            IsGameOver = isGameOver;
            Phase = phase ?? string.Empty;
            PhaseLabel = string.IsNullOrWhiteSpace(phaseLabel) ? "Unknown phase" : phaseLabel;
            TokensLeft = tokensLeft;
            Turn = turn;
            UnitCount = unitCount;
            VisibleNodeCount = visibleNodeCount;
        }

        public IReadOnlyList<TurnSummaryEventState> Events { get; }
        public bool HasEvents => Events.Count > 0;
        public bool IsGameOver { get; }
        public string Phase { get; }
        public string PhaseLabel { get; }
        public int TokensLeft { get; }
        public int Turn { get; }
        public int UnitCount { get; }
        public int VisibleNodeCount { get; }

        public string StatusText => IsGameOver ? "Game over" : PhaseLabel;
        public string TokensText => TokensLeft >= 0 ? TokensLeft.ToString() : "--";
        public string TurnText => Turn > 0 ? Turn.ToString() : "--";
    }

    public sealed class TurnSummaryEventState
    {
        public TurnSummaryEventState(string title = "", string detail = "")
        {
            Detail = detail ?? string.Empty;
            Title = string.IsNullOrWhiteSpace(title) ? "Event" : title;
        }

        public string Detail { get; }
        public string Title { get; }
    }
}
