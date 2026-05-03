using System.Collections.Generic;

namespace Panoptes.Core.Domain
{
    public sealed class MarchTurnStopDto
    {
        public int TurnIndex;
        public string NodeId;
    }

    public sealed class PathPreviewDto
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

    public sealed class BuildPreviewDto
    {
        public string RequestId;
        public string NodeId;
        public string BuildingTypeId;
        public string CityId;
        public bool Valid;
        public string ErrorCode;
        public string Message;
        public Dictionary<string, string> Details;
    }

    public sealed class RecipePreviewDto
    {
        public string RequestId;
        public string NodeId;
        public string RecipeId;
        public bool Valid;
        public string ErrorCode;
        public string Message;
        public Dictionary<string, string> Details;
    }

    public sealed class QueuedUnitOrderDto
    {
        public string UnitId;
        public string Action;
        public string TargetNodeId;
        public string TargetUnitId;
        public string SecondaryNodeId;
        public Dictionary<string, string> Params;
        public List<string> PathNodeIds;
        public string FirstTurnNodeId;
        public int TotalTurns;
        public List<MarchTurnStopDto> TurnStops;
    }

    public sealed class QueuedBuildOrderDto
    {
        public string NodeId;
        public string BuildingTypeId;
        public string CityId;
    }

    public sealed class QueuedRecipeSelectionDto
    {
        public string NodeId;
        public string RecipeId;
    }

    public sealed class QueuedWarZoneDirectiveDto
    {
        public string ZoneId;
        public string Directive;
        public string TargetNode;
    }

    public sealed class PlanningWarZoneDto
    {
        public string Id;
        public string Name;
        public List<string> NodeIds;
        public string Directive;
        public string TargetNode;
    }

    public sealed class BuiltStructureDto
    {
        public string NodeId;
        public string BuildingType;
        public string OwnerId;
        public string CityId;
        public int BuildingHp;
    }

    public sealed class CityBuiltBuildingDto
    {
        public string NodeId;
        public string BuildingType;
        public int Q;
        public int R;
        public bool HasCoordinates;
    }

    public sealed class SettlementSectionDto
    {
        public string Section;
        public List<TurnEventDto> Events;
    }

    public sealed class TurnSettlementDto
    {
        public string Phase;
        public string NextPhase;
        public List<SettlementSectionDto> Sections;
        public List<string> BuiltNodeIDs;
        public List<BuiltStructureDto> BuiltBuildings;
        public List<string> MovedUnitIDs;
        public List<string> DeadUnitIDs;
        public bool CityCoreDamaged;
    }

    public sealed class TurnEventDto
    {
        public string Section;
        public string Type;
        public Dictionary<string, string> Data;
        public string ReasonMessage;
        public string BlockedReasonMessage;
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
        public int PosQ;
        public int PosR;
        public int FromQ;
        public int FromR;
        public int ToQ;
        public int ToR;
    }
}
