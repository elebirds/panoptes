using Panoptes.Core.Application.Stores;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class PlanningToolViewState
    {
        public PlanningToolViewState(
            PlanningToolMode mode = PlanningToolMode.None,
            PlanningBuildPlacementRule buildRule = PlanningBuildPlacementRule.AnyTerrain,
            string buildTypeId = "",
            string buildCityId = "",
            string selectedUnitId = "",
            string movePreviewNodeId = "",
            string buildPreviewNodeId = "",
            string prompt = "")
        {
            Mode = mode;
            BuildRule = buildRule;
            BuildTypeId = Normalize(buildTypeId);
            BuildCityId = Normalize(buildCityId);
            SelectedUnitId = Normalize(selectedUnitId);
            MovePreviewNodeId = Normalize(movePreviewNodeId);
            BuildPreviewNodeId = Normalize(buildPreviewNodeId);
            Prompt = string.IsNullOrWhiteSpace(prompt) ? BuildPrompt(mode, SelectedUnitId) : prompt.Trim();
        }

        public PlanningToolMode Mode { get; }
        public PlanningBuildPlacementRule BuildRule { get; }
        public string BuildTypeId { get; }
        public string BuildCityId { get; }
        public string SelectedUnitId { get; }
        public string MovePreviewNodeId { get; }
        public string BuildPreviewNodeId { get; }
        public string Prompt { get; }
        public bool HasSelectedUnit => !string.IsNullOrWhiteSpace(SelectedUnitId);
        public bool IsBuildPlacement => Mode == PlanningToolMode.Build;
        public bool IsCombatTargeting => Mode == PlanningToolMode.Attack || Mode == PlanningToolMode.Charge;
        public bool IsMoveTargeting => Mode == PlanningToolMode.Move;

        private static string BuildPrompt(PlanningToolMode mode, string selectedUnitId)
        {
            if (string.IsNullOrWhiteSpace(selectedUnitId) &&
                (mode == PlanningToolMode.Move ||
                 mode == PlanningToolMode.Attack ||
                 mode == PlanningToolMode.Charge))
            {
                return "Select your unit to start issuing commands.";
            }

            return mode switch
            {
                PlanningToolMode.Build => "选择地块放置建筑。",
                PlanningToolMode.Move => "悬停节点预览路径，点击后下达移动指令。",
                PlanningToolMode.Attack => "点击敌方单位或敌方建筑下达攻击指令。",
                PlanningToolMode.Charge => "点击敌方单位下达冲锋指令。",
                _ => string.IsNullOrWhiteSpace(selectedUnitId)
                    ? "Select your unit to start issuing commands."
                    : "选择动作后再指定目标"
            };
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
