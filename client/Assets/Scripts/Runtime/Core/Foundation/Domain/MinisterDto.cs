using System;
using Panoptes.Protocol.V1;
using UnityEngine;

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

        public static MinisterDraftDto FromView(MinisterDraftView view)
        {
            if (view == null || string.IsNullOrWhiteSpace(view.JsonPayload))
            {
                return null;
            }

            MinisterDraftPayload payload;
            try
            {
                payload = JsonUtility.FromJson<MinisterDraftPayload>(view.JsonPayload);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.draft_id))
            {
                return null;
            }

            return new MinisterDraftDto
            {
                DraftId = payload.draft_id ?? string.Empty,
                PlayerId = payload.player_id ?? string.Empty,
                MinisterRole = !string.IsNullOrWhiteSpace(payload.minister_role)
                    ? payload.minister_role
                    : (view.MinisterRole ?? string.Empty),
                Kind = payload.kind ?? string.Empty,
                TargetId = payload.target_id ?? string.Empty,
                TargetLabel = payload.target_label ?? string.Empty,
                Title = payload.title ?? string.Empty,
                Summary = payload.summary ?? string.Empty,
                Rationale = payload.rationale ?? string.Empty,
                RiskNote = payload.risk_note ?? string.Empty,
                Status = payload.status ?? string.Empty,
                Available = view.Available && payload.available,
                Turn = payload.turn,
                Source = payload.source ?? string.Empty
            };
        }

        [Serializable]
        private sealed class MinisterDraftPayload
        {
            public string draft_id;
            public string player_id;
            public string minister_role;
            public string kind;
            public string target_id;
            public string target_label;
            public string title;
            public string summary;
            public string rationale;
            public string risk_note;
            public string status;
            public bool available;
            public int turn;
            public string source;
        }
    }
}
