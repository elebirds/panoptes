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
        private readonly GameStateCache _gameStateCache;
        private readonly RoomCache _roomCache;

        public LocalGameSessionResetService(
            GameStateStore gameStateStore,
            PlanningDraftStore planningDraftStore,
            TurnStore turnStore,
            GameChatStore gameChatStore,
            GameOverStore gameOverStore,
            SettlementStore settlementStore,
            GameplayFeedbackStore feedbackStore,
            GameStateCache gameStateCache,
            RoomCache roomCache)
        {
            _gameStateStore = gameStateStore;
            _planningDraftStore = planningDraftStore;
            _turnStore = turnStore;
            _gameChatStore = gameChatStore;
            _gameOverStore = gameOverStore;
            _settlementStore = settlementStore;
            _feedbackStore = feedbackStore;
            _gameStateCache = gameStateCache;
            _roomCache = roomCache;
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
            _roomCache?.Clear();

            // Temporary until every legacy presentation surface reads Stores.
            _gameStateCache?.Clear();
        }
    }
}
