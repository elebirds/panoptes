namespace Panoptes.Core.Application.Stores
{
    public sealed class StaticCatalogStore : ReactiveStore<StaticCatalogState>
    {
        public StaticCatalogStore()
            : base(new StaticCatalogState())
        {
        }

        internal void Replace(StaticCatalogState state)
        {
            Publish(state ?? new StaticCatalogState());
        }

        protected override StaticCatalogState CloneState(StaticCatalogState state)
        {
            return state == null ? new StaticCatalogState() : state.Clone();
        }
    }
}
