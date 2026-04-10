using System.Collections.Generic;
using Panoptes.Protocol.V1;

namespace Panoptes.Runtime.Events
{
    public class Resources
    {
        public int Ore;
        public int Wood;
        public int Food;
        public int RefinedOre;
        public int EngineerMaterial;
        public int BuildPoints;
    }

    public class PhaseChangedEvent
    {
        public int Turn;
        public string Phase;
        public int TimeoutSeconds;
        public int TokensLeft;
    }

    public class ResourcesChangedEvent
    {
        public Resources Resources;
        public Resources Delta;
    }

    public class TokensChangedEvent
    {
        public int TokensLeft;
        public string Action;
    }

    public class NodeChangedEvent
    {
        public string NodeID;
        public NodeView Node;
        public string ChangeType;
    }

    public class UnitsChangedEvent
    {
        public List<UnitView> Added;
        public List<string> RemovedIDs;
        public List<UnitView> Moved;
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
        public MsgDomesticSettlement Raw;
        public Resources ResourcesAfter;
        public List<string> BuiltNodeIDs;
    }

    public class CombatSettledEvent
    {
        public MsgCombatSettlement Raw;
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
        public List<MetricItem> Metrics;
    }

    public class MinisterActionEvent
    {
        public string MinisterRole;
        public string ActionID;
        public List<MinisterActionItem> Actions;
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
        public NodeView TrueState;
    }

    public class GameOverEvent
    {
        public string WinnerID;
        public string Reason;
        public string Narrative;
        public bool IsWinner;
    }
}