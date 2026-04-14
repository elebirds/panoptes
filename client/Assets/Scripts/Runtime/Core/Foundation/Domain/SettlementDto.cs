using System.Collections.Generic;

namespace Panoptes.Core.Domain
{
    public class MarchTurnStopDto
    {
        public int TurnIndex;
        public string NodeId;
    }

    public class PathPreviewDto
    {
        public string RequestId;
        public string UnitId;
        public string Action;
        public string TargetNodeId;
        public bool Valid;
        public string ErrorCode;
        public List<string> PathNodeIds;
        public string FirstTurnNodeId;
        public int TotalTurns;
        public List<MarchTurnStopDto> TurnStops;
    }

    public class QueuedCombatOrderDto
    {
        public string UnitId;
        public string Action;
        public string TargetNodeId;
        public string TargetUnitId;
        public List<string> PathNodeIds;
        public string FirstTurnNodeId;
        public int TotalTurns;
        public List<MarchTurnStopDto> TurnStops;
    }

    public class DomesticBuildResultDto
    {
        public string NodeId;
        public string BuildingType;
        public string OwnerId;
        public string CastleId;
        public int BuildingHp;
    }

    public class CastleBuiltBuildingDto
    {
        public string NodeId;
        public string BuildingType;
        public int X;
        public int Y;
        public bool HasCoordinates;
    }

    public class DomesticSettlementDto
    {
        public List<string> BuiltNodeIDs;
        public List<string> ChangedNodeIDs;
        public List<DomesticBuildResultDto> BuiltBuildings;
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
        public string TargetUnitId;
        public string EnemyUnitId;
        public string KillerId;
        public string NodeId;
        public string Source;
        public string ConflictType;
        public int Damage;
        public int HpAfter;
        public int Sequence;
        public int PosX;
        public int PosY;
        public int FromX;
        public int FromY;
        public int ToX;
        public int ToY;
    }

    public class TurnSettlementSectionDto
    {
        public string Section;
        public List<CombatEventDto> Events;
    }

    public class TurnSettlementDto
    {
        public List<TurnSettlementSectionDto> Sections;
        public List<CombatEventDto> Events;
        public List<string> BuiltNodeIDs;
        public List<DomesticBuildResultDto> BuiltBuildings;
        public List<string> MovedUnitIDs;
        public List<string> DeadUnitIDs;
        public bool CastleDamaged;
    }
}
