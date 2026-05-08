using System;
using System.Collections.Generic;

namespace Panoptes.Core.Domain
{
    public sealed class MinisterMetricDto
    {
        public string Key;
        public float Value;
        public string Label;
    }

    public sealed class MinisterProfileDto
    {
        public string MinisterId;
        public string Role;
        public string Name;
        public string IconKey;
        public string PersonalityDesc;
        public int Ability;
        public string Personality;
        public int Loyalty;
        public int Ambition;
        public int Cautiousness;
        public int Decisiveness;
        public int LoyaltyTendency;
        public int AmbitionStyle;
        public bool IsVacant;
        public bool IsCandidate;
    }

    public sealed class MinisterDraftDto
    {
        public string DraftId;
        public string PlayerId;
        public string MinisterRole;
        public string Kind;
        public string TargetId;
        public string TargetLabel;
        public string Title;
        public string Summary;
        public string Rationale;
        public string RiskNote;
        public string Status;
        public bool Available;
        public int Turn;
        public string Source;
        public string[] InstitutionIds;
        public string NodeId;
        public string BuildingTypeId;
        public string CityId;
        public string RecipeId;
        public string UnitId;
        public string Action;
        public string TargetNodeId;
        public string TargetUnitId;
        public string SecondaryNodeId;
        public string OperationId;
        public string Objective;
        public IReadOnlyList<MinisterOperationCommandDto> OperationCommands = Array.Empty<MinisterOperationCommandDto>();

        public bool IsDomestic => string.Equals(MinisterRole, "domestic", StringComparison.OrdinalIgnoreCase);

        public bool IsRejected => string.Equals(Status, "rejected", StringComparison.OrdinalIgnoreCase);

        public bool IsInteractive =>
            Available &&
            (string.Equals(Status, "pending", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(Status, "stale", StringComparison.OrdinalIgnoreCase));

        public string DisplayStatus
        {
            get
            {
                if (string.Equals(Status, "stale", StringComparison.OrdinalIgnoreCase))
                {
                    return "已偏离，可重新采纳";
                }

                return Status ?? string.Empty;
            }
        }
    }

    public sealed class MinisterOperationCommandDto
    {
        public string Label;
        public string Kind;
        public string RawJson;
        public string NodeId;
        public string BuildingTypeId;
        public string CityId;
        public string RecipeId;
        public string UnitId;
        public string Action;
        public string TargetNodeId;
        public string TargetUnitId;
        public string SecondaryNodeId;
    }
}
