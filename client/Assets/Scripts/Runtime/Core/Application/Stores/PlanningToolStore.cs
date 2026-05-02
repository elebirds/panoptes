namespace Panoptes.Core.Application.Stores
{
    public sealed class PlanningToolStore : ReactiveStore<PlanningToolState>
    {
        public PlanningToolStore()
            : base(new PlanningToolState())
        {
        }

        internal void ClearTool()
        {
            Publish(new PlanningToolState());
        }

        internal void EnterBuild(
            string buildTypeId,
            string buildCityId,
            PlanningBuildPlacementRule buildRule)
        {
            Publish(new PlanningToolState(
                PlanningToolMode.Build,
                buildRule,
                buildTypeId,
                buildCityId));
        }

        internal void BeginMove()
        {
            Publish(new PlanningToolState(PlanningToolMode.Move));
        }

        internal void BeginAttack()
        {
            Publish(new PlanningToolState(PlanningToolMode.Attack));
        }

        internal void BeginCharge()
        {
            Publish(new PlanningToolState(PlanningToolMode.Charge));
        }

        internal void SetMovePreviewTarget(string nodeId)
        {
            var current = Snapshot;
            Publish(new PlanningToolState(
                current.Mode,
                current.BuildRule,
                current.BuildTypeId,
                current.BuildCityId,
                movePreviewNodeId: nodeId,
                buildPreviewNodeId: current.BuildPreviewNodeId));
        }

        internal void ClearMovePreviewTarget()
        {
            SetMovePreviewTarget(string.Empty);
        }

        internal void SetBuildPreviewTarget(string nodeId)
        {
            var current = Snapshot;
            Publish(new PlanningToolState(
                current.Mode,
                current.BuildRule,
                current.BuildTypeId,
                current.BuildCityId,
                current.MovePreviewNodeId,
                nodeId));
        }

        internal void ClearBuildPreviewTarget()
        {
            SetBuildPreviewTarget(string.Empty);
        }

        protected override PlanningToolState CloneState(PlanningToolState state)
        {
            return state == null ? new PlanningToolState() : state.Clone();
        }
    }
}
