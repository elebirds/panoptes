using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Stores;

namespace Panoptes.Core.Application.Services
{
    public sealed class LocalGameSessionResetService
    {
        private readonly GameStateStore _gameStateStore;
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly TurnStore _turnStore;
        private readonly GameChatStore _gameChatStore;
        private readonly GameOverStore _gameOverStore;
        private readonly SettlementStore _settlementStore;
        private readonly GameplayFeedbackStore _feedbackStore;

        public LocalGameSessionResetService(
            GameStateStore gameStateStore,
            PlanningDraftStore planningDraftStore,
            TurnStore turnStore,
            GameChatStore gameChatStore,
            GameOverStore gameOverStore,
            SettlementStore settlementStore,
            GameplayFeedbackStore feedbackStore)
        {
            _gameStateStore = gameStateStore;
            _planningDraftStore = planningDraftStore;
            _turnStore = turnStore;
            _gameChatStore = gameChatStore;
            _gameOverStore = gameOverStore;
            _settlementStore = settlementStore;
            _feedbackStore = feedbackStore;
        }

        public void ResetLocalGameSession()
        {
            _gameStateStore?.Replace(new GameStateStoreState());
            _planningDraftStore?.Replace(new PlanningDraftState());
            _turnStore?.Replace(new TurnState());
            _gameChatStore?.Clear();
            _gameOverStore?.Clear();
            _settlementStore?.Clear();
            _feedbackStore?.Clear();

            // Temporary until every legacy presentation surface reads Stores.
            GameStateCache.Instance?.Clear();
        }
    }
}
