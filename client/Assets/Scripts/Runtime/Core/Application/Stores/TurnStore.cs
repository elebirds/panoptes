namespace Panoptes.Core.Application.Stores
{
    public sealed class TurnStore : ReactiveStore<TurnState>
    {
        public TurnStore()
            : base(new TurnState())
        {
        }

        internal void Replace(TurnState state)
        {
            Publish(state ?? new TurnState());
        }

        protected override TurnState CloneState(TurnState state)
        {
            return state == null ? new TurnState() : state.Clone();
        }
    }
}
