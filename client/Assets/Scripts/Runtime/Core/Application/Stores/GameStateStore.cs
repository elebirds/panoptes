namespace Panoptes.Core.Application.Stores
{
    public sealed class GameStateStore : ReactiveStore<GameStateStoreState>
    {
        public GameStateStore()
            : base(new GameStateStoreState())
        {
        }

        internal void Replace(GameStateStoreState state)
        {
            Publish(state ?? new GameStateStoreState());
        }

        protected override GameStateStoreState CloneState(GameStateStoreState state)
        {
            return state == null ? new GameStateStoreState() : state.Clone();
        }
    }
}
