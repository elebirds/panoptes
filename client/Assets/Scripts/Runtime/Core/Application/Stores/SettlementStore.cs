using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    public sealed class SettlementStore : ReactiveStore<SettlementState>
    {
        public SettlementStore()
            : base(new SettlementState())
        {
        }

        internal void Replace(TurnSettlementDto settlement)
        {
            Publish(new SettlementState(settlement));
        }

        internal void Clear()
        {
            Publish(new SettlementState());
        }

        protected override SettlementState CloneState(SettlementState state)
        {
            return state == null ? new SettlementState() : state.Clone();
        }
    }
}
