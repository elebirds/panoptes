using System.Collections.Generic;
using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    public sealed class TurnState
    {
        public TurnState(
            int turn = 0,
            string phase = "",
            int tokensLeft = 0,
            IReadOnlyList<TurnEventDto> planningStartEvents = null,
            bool isGameOver = false,
            int timeoutSeconds = 0,
            string nextPhase = "",
            bool isInteractive = false)
        {
            IsGameOver = isGameOver;
            IsInteractive = isInteractive;
            NextPhase = nextPhase ?? string.Empty;
            Phase = phase ?? string.Empty;
            PlanningStartEvents = StoreSnapshotCloner.CloneTurnEvents(planningStartEvents);
            TimeoutSeconds = timeoutSeconds;
            TokensLeft = tokensLeft;
            Turn = turn;
        }

        public bool IsGameOver { get; }
        public bool IsInteractive { get; }
        public string NextPhase { get; }
        public string Phase { get; }
        public IReadOnlyList<TurnEventDto> PlanningStartEvents { get; }
        public int TimeoutSeconds { get; }
        public int TokensLeft { get; }
        public int Turn { get; }

        internal TurnState Clone()
        {
            return new TurnState(
                Turn,
                Phase,
                TokensLeft,
                PlanningStartEvents,
                IsGameOver,
                TimeoutSeconds,
                NextPhase,
                IsInteractive);
        }
    }
}
