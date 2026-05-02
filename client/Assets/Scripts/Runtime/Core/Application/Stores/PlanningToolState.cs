namespace Panoptes.Core.Application.Stores
{
    public sealed class PlanningToolState
    {
        public PlanningToolState(
            PlanningToolMode mode = PlanningToolMode.None,
            PlanningBuildPlacementRule buildRule = PlanningBuildPlacementRule.AnyTerrain,
            string buildTypeId = "",
            string buildCityId = "",
            string movePreviewNodeId = "",
            string buildPreviewNodeId = "")
        {
            Mode = mode;
            BuildRule = buildRule;
            BuildTypeId = Normalize(buildTypeId);
            BuildCityId = Normalize(buildCityId);
            MovePreviewNodeId = Normalize(movePreviewNodeId);
            BuildPreviewNodeId = Normalize(buildPreviewNodeId);
        }

        public PlanningToolMode Mode { get; }
        public PlanningBuildPlacementRule BuildRule { get; }
        public string BuildTypeId { get; }
        public string BuildCityId { get; }
        public string MovePreviewNodeId { get; }
        public string BuildPreviewNodeId { get; }

        internal PlanningToolState Clone()
        {
            return new PlanningToolState(
                Mode,
                BuildRule,
                BuildTypeId,
                BuildCityId,
                MovePreviewNodeId,
                BuildPreviewNodeId);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
