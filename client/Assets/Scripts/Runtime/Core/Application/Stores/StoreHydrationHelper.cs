using System;
using Panoptes.Core.Application.Cache;

namespace Panoptes.Core.Application.Stores
{
    public sealed class StoreHydrationSnapshot
    {
        public StoreHydrationSnapshot(
            GameStateStoreState gameState = null,
            PlanningDraftState planningDraft = null,
            StaticCatalogState staticCatalog = null,
            TurnState turn = null)
        {
            GameState = gameState ?? new GameStateStoreState();
            PlanningDraft = planningDraft ?? new PlanningDraftState();
            StaticCatalog = staticCatalog ?? new StaticCatalogState();
            Turn = turn ?? new TurnState();
        }

        public GameStateStoreState GameState { get; }
        public PlanningDraftState PlanningDraft { get; }
        public StaticCatalogState StaticCatalog { get; }
        public TurnState Turn { get; }
    }

    public sealed class StoreHydrationHelper
    {
        private readonly GameStateStore _gameStateStore;
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly StaticCatalogStore _staticCatalogStore;
        private readonly TurnStore _turnStore;

        public StoreHydrationHelper(
            GameStateStore gameStateStore,
            PlanningDraftStore planningDraftStore,
            StaticCatalogStore staticCatalogStore,
            TurnStore turnStore)
        {
            _gameStateStore = gameStateStore ?? throw new ArgumentNullException(nameof(gameStateStore));
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            _staticCatalogStore = staticCatalogStore ?? throw new ArgumentNullException(nameof(staticCatalogStore));
            _turnStore = turnStore ?? throw new ArgumentNullException(nameof(turnStore));
        }

        public void Hydrate(StoreHydrationSnapshot snapshot)
        {
            var nextSnapshot = snapshot ?? new StoreHydrationSnapshot();
            HydrateGameState(nextSnapshot.GameState);
            HydratePlanningDraft(nextSnapshot.PlanningDraft);
            HydrateStaticCatalog(nextSnapshot.StaticCatalog);
            HydrateTurn(nextSnapshot.Turn);
        }

        public void HydrateGameState(GameStateStoreState state)
        {
            _gameStateStore.Replace(state);
        }

        public void HydratePlanningDraft(PlanningDraftState state)
        {
            _planningDraftStore.Replace(state);
        }

        public void HydrateStaticCatalog(StaticCatalogState state)
        {
            _staticCatalogStore.Replace(state);
        }

        public void HydrateTurn(TurnState state)
        {
            _turnStore.Replace(state);
        }

        public bool HydrateGameRuntimeFromCache(GameStateCache cache)
        {
            if (cache == null || !HasRuntimeGameState(cache))
            {
                return false;
            }

            HydrateGameState(StoreHydrationProtocolMapper.ToGameState(cache));
            HydrateTurn(StoreHydrationProtocolMapper.ToTurn(cache));
            return true;
        }

        private static bool HasRuntimeGameState(GameStateCache cache)
        {
            if (cache == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(cache.GameID)
                || !string.IsNullOrWhiteSpace(cache.ActiveGameSessionID)
                || cache.Nodes.Count > 0
                || cache.Units.Count > 0
                || cache.Turn > 0;
        }
    }
}
