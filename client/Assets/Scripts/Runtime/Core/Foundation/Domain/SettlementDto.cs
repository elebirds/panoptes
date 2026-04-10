using System.Collections.Generic;

namespace Panoptes.Core.Domain
{
    public class DomesticSettlementDto
    {
        public List<string> BuiltNodeIDs;
        public List<string> ChangedNodeIDs;
    }

    public class CombatSettlementDto
    {
        public List<string> MovedUnitIDs;
        public List<string> DeadUnitIDs;
        public bool CastleDamaged;
        public List<CombatEventDto> Events;
    }

    public class CombatEventDto
    {
        public string Type;
        public string UnitId;
        public string NodeId;
        public int HpAfter;
        public int FromX;
        public int FromY;
        public int ToX;
        public int ToY;
    }
}
