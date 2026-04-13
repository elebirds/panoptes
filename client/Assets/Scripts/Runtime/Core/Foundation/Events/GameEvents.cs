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
    }

    public class CastleHPChangedEvent
    {
        public int MyHP;
        public int MyMaxHP;
        public int EnemyHP;
        public int EnemyMaxHP;
        public int MyDelta;
        public int EnemyDelta;
    }

    public class DomesticSettledEvent
    {
        public DomesticSettlementDto Settlement;
        public ResourceDto ResourcesAfter;
        public List<string> BuiltNodeIDs;
    }

    public class CombatSettledEvent
    {
        public CombatSettlementDto Settlement;
        public List<string> MovedUnitIDs;
        public List<string> DeadUnitIDs;
        public bool CastleDamaged;
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

    public class MinisterActionEvent
    {
        public string MinisterRole;
        public string ActionID;
        public List<MinisterActionItemDto> Actions;
        public string Report;
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
