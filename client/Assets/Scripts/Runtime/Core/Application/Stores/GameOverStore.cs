namespace Panoptes.Core.Application.Stores
{
    public sealed class GameOverStore : ReactiveStore<GameOverState>
    {
        public GameOverStore()
            : base(new GameOverState())
        {
        }

        internal void Replace(GameOverState state)
        {
            Publish(state ?? new GameOverState());
        }

        internal void Clear()
        {
            Publish(new GameOverState());
        }

        protected override GameOverState CloneState(GameOverState state)
        {
            return state == null ? new GameOverState() : state.Clone();
        }
    }
}
