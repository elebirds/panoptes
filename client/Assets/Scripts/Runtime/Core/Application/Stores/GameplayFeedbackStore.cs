using System.Collections.Generic;

namespace Panoptes.Core.Application.Stores
{
    public sealed class GameplayFeedbackStore : ReactiveStore<GameplayFeedbackState>
    {
        private int _sequence;

        public GameplayFeedbackStore()
            : base(new GameplayFeedbackState())
        {
        }

        internal void PublishFeedback(
            string source,
            string code,
            string message,
            bool success = false,
            IReadOnlyDictionary<string, string> details = null)
        {
            Publish(new GameplayFeedbackState(++_sequence, source, code, message, success, details));
        }

        internal void Clear()
        {
            Publish(new GameplayFeedbackState());
        }

        protected override GameplayFeedbackState CloneState(GameplayFeedbackState state)
        {
            return state == null ? new GameplayFeedbackState() : state.Clone();
        }
    }
}
