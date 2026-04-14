namespace Panoptes.Core.Domain
{
    public static class GamePhases
    {
        public const string Planning = "planning";
        public const string Resolving = "resolving";
        public const string DomesticPlanning = "domestic_planning";
        public const string DomesticResolving = "domestic_resolving";
        public const string CombatPlanning = "combat_planning";
        public const string CombatResolving = "combat_resolving";

        public static bool IsPlanning(string phase)
        {
            return phase == Planning || phase == DomesticPlanning || phase == CombatPlanning;
        }

        public static bool IsResolving(string phase)
        {
            return phase == Resolving || phase == DomesticResolving || phase == CombatResolving;
        }

        public static string ToDisplayText(string phase)
        {
            return phase switch
            {
                Planning => "规划部署",
                Resolving => "结算中",
                DomesticPlanning => "内政部署",
                DomesticResolving => "内政结算中",
                CombatPlanning => "战斗部署",
                CombatResolving => "战斗结算中",
                _ => string.IsNullOrWhiteSpace(phase) ? "未知阶段" : phase
            };
        }
    }
}
