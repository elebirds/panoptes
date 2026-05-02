using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    public sealed class SettlementState
    {
        public SettlementState(TurnSettlementDto settlement = null)
        {
            Settlement = StoreSnapshotCloner.CloneTurnSettlement(settlement);
        }

        public TurnSettlementDto Settlement { get; }

        internal SettlementState Clone()
        {
            return new SettlementState(Settlement);
        }
    }
}
