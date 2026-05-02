using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Events;

namespace Panoptes.Presentation.Map
{
    public sealed class MapPlanningCacheEventBridge
    {
        private GameStateCache _cache;
        private PlanningDraftCache _draftCache;

        public GameStateCache Cache => _cache;

        public PlanningDraftCache DraftCache => _draftCache;

        public bool IsGameStateSubscribed { get; private set; }

        public void SubscribeGameState(
            Action<TurnSettledEvent> onTurnSettled,
            Action<NodeChangedEvent> onNodeChanged,
            Action<UnitsChangedEvent> onUnitsChanged,
            Action<TokenResultEvent> onTokenResult,
            Action<PlanningCommandResultEvent> onPlanningCommandResult)
        {
            if (IsGameStateSubscribed)
            {
                return;
            }

            _cache = GameStateCache.Instance;
            _draftCache = PlanningDraftCache.EnsureInstance();
            if (_cache == null)
            {
                return;
            }

            _cache.OnTurnSettled += onTurnSettled;
            _cache.OnNodeChanged += onNodeChanged;
            _cache.OnUnitsChanged += onUnitsChanged;
            _cache.OnTokenResult += onTokenResult;
            _cache.OnPlanningCommandResult += onPlanningCommandResult;
            IsGameStateSubscribed = true;
        }

        public void SubscribeDraft(
            Action onPreviewChanged,
            Action onBuildPreviewChanged,
            Action onOrdersChanged)
        {
            _draftCache = PlanningDraftCache.EnsureInstance();
            if (_draftCache == null)
            {
                return;
            }

            _draftCache.PreviewChanged -= onPreviewChanged;
            _draftCache.PreviewChanged += onPreviewChanged;
            _draftCache.BuildPreviewChanged -= onBuildPreviewChanged;
            _draftCache.BuildPreviewChanged += onBuildPreviewChanged;
            _draftCache.OrdersChanged -= onOrdersChanged;
            _draftCache.OrdersChanged += onOrdersChanged;
        }

        public void UnsubscribeGameState(
            Action<TurnSettledEvent> onTurnSettled,
            Action<NodeChangedEvent> onNodeChanged,
            Action<UnitsChangedEvent> onUnitsChanged,
            Action<TokenResultEvent> onTokenResult,
            Action<PlanningCommandResultEvent> onPlanningCommandResult)
        {
            if (!IsGameStateSubscribed)
            {
                return;
            }

            if (_cache != null)
            {
                _cache.OnTurnSettled -= onTurnSettled;
                _cache.OnNodeChanged -= onNodeChanged;
                _cache.OnUnitsChanged -= onUnitsChanged;
                _cache.OnTokenResult -= onTokenResult;
                _cache.OnPlanningCommandResult -= onPlanningCommandResult;
            }

            _cache = null;
            IsGameStateSubscribed = false;
        }

        public void UnsubscribeDraft(
            Action onPreviewChanged,
            Action onBuildPreviewChanged,
            Action onOrdersChanged)
        {
            if (_draftCache == null)
            {
                return;
            }

            _draftCache.PreviewChanged -= onPreviewChanged;
            _draftCache.BuildPreviewChanged -= onBuildPreviewChanged;
            _draftCache.OrdersChanged -= onOrdersChanged;
            _draftCache = null;
        }
    }
}
