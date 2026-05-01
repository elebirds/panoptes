using System;

namespace Panoptes.Core.Domain
{
    public sealed class MinisterMetricDto
    {
        public string Key;
        public float Value;
        public string Label;
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
}
