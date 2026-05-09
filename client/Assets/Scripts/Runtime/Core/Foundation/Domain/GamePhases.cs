namespace Panoptes.Core.Domain
{
    public static class GamePhases
    {
        public const string Planning = "planning";
        public const string Resolving = "resolving";
        public const string Presentation = "presentation";
        public const string TurnReport = "turn_report";

        public static bool IsPlanning(string phase)
        {
            return string.Equals(Normalize(phase), Planning, System.StringComparison.Ordinal);
        }

        public static bool IsResolving(string phase)
        {
            return string.Equals(Normalize(phase), Resolving, System.StringComparison.Ordinal);
        }

        public static bool IsPresentation(string phase)
        {
            return string.Equals(Normalize(phase), Presentation, System.StringComparison.Ordinal);
        }

        public static bool IsAuthoritativePlayback(string phase)
        {
            var normalized = Normalize(phase);
            return string.Equals(normalized, Resolving, System.StringComparison.Ordinal) ||
                   string.Equals(normalized, Presentation, System.StringComparison.Ordinal);
        }

        public static string ToDisplayText(string phase)
        {
            return Normalize(phase) switch
            {
                Planning => "回合规划",
                Resolving => "回合结算中",
                Presentation => "演示阶段",
                TurnReport => "战报整理中",
                _ => string.IsNullOrWhiteSpace(phase) ? "未知阶段" : phase
            };
        }

        private static string Normalize(string phase)
        {
            return (phase ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
