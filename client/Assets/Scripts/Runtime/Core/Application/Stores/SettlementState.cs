using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    public sealed class SettlementState
    {
        public SettlementState(TurnSettlementDto settlement = null, int sequence = 0)
        {
            Sequence = sequence;
            Settlement = StoreSnapshotCloner.CloneTurnSettlement(settlement);
        }

        public int Sequence { get; }
        public TurnSettlementDto Settlement { get; }

        internal SettlementState Clone()
        {
            return new SettlementState(Settlement, Sequence);
        }
    }
}
