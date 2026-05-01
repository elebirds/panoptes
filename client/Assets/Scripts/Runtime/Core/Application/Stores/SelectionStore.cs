namespace Panoptes.Core.Application.Stores
{
    public sealed class SelectionStore : ReactiveStore<SelectionState>
    {
        public SelectionStore()
            : base(new SelectionState())
        {
        }

        internal void Clear()
        {
            Publish(new SelectionState());
        }

        internal void SelectCity(string cityId)
        {
            Publish(new SelectionState(selectedCityId: cityId));
        }

        internal void SelectNode(string nodeId)
        {
            Publish(new SelectionState(selectedNodeId: nodeId));
        }

        internal void SelectUnit(string unitId)
        {
            Publish(new SelectionState(selectedUnitId: unitId));
        }

        protected override SelectionState CloneState(SelectionState state)
        {
            return state == null ? new SelectionState() : state.Clone();
        }
    }
}
