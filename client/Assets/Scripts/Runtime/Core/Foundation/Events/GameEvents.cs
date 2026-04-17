using System.Collections.Generic;
using Panoptes.Core.Domain;

namespace Panoptes.Core.Events
{
    public class PhaseChangedEvent
    {
        public int Turn;
        public string Phase;
        public int TimeoutSeconds;
        public int TokensLeft;
        public string NextPhase;
        public bool IsInteractive;
    }

    public class ResourcesChangedEvent
    {
        public ResourceDto Resources;
        public ResourceDto Delta;
    }

    public class TokensChangedEvent
    {
        public int TokensLeft;
        public string Action;
    }

    public class NodeChangedEvent
    {
        public string NodeID;
        public NodeDto Node;
        public string ChangeType;
    }

    public class UnitsChangedEvent
    {
        public List<UnitDto> Added;
        public List<string> RemovedIDs;
        public List<UnitDto> Moved;
        public string ChangeType;
    }

    public class CityCoreHpChangedEvent
    {
        public int MyHP;
        public int MyMaxHP;
        public int EnemyHP;
        public int EnemyMaxHP;
        public int MyDelta;
        public int EnemyDelta;
    }

    public class TurnSettledEvent
    {
        public TurnSettlementDto Settlement;
        public ResourceDto ResourcesAfter;
        public List<string> BuiltNodeIDs;
        public List<string> MovedUnitIDs;
        public List<string> DeadUnitIDs;
        public bool CityCoreDamaged;
    }

    public class MinisterChunkEvent
    {
        public string MinisterRole;
        public string Chunk;
        public bool IsFinal;
    }

    public class MinisterMetricsEvent
    {
        public string MinisterRole;
        public List<MinisterMetricDto> Metrics;
    }

    public class TokenResultEvent
    {
        public bool Success;
        public string Action;
        public int TokensLeft;
        public string ErrorCode;
    }

    public class RevealResultEvent
    {
        public string NodeID;
        public NodeDto TrueState;
    }

    public class PlanningCommandResultEvent
    {
        public string CommandType;
        public string Action;
        public bool Success;
        public string PrimaryId;
        public string SecondaryId;
        public string TertiaryId;
        public string ErrorCode;
        public List<string> RelatedIds;
    }

    public class GameOverEvent
    {
        public string WinnerID;
        public string LoserID;
        public string Reason;
        public string Narrative;
        public bool IsWinner;
    }

    public class GameErrorEvent
    {
        public string Code;
        public string Message;
    }
}
