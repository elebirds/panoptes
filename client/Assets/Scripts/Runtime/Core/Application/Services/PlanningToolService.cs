using Panoptes.Core.Application.Stores;

namespace Panoptes.Core.Application.Services
{
    public sealed class PlanningToolService
    {
        private readonly PlanningToolStore _planningToolStore;

        public PlanningToolService(PlanningToolStore planningToolStore)
        {
            _planningToolStore = planningToolStore;
        }

        public bool IsDisposed => _planningToolStore.IsDisposed;

        public void ClearTool()
        {
            _planningToolStore.ClearTool();
        }

        public void EnterBuild(
            string buildTypeId,
            string buildCityId,
            PlanningBuildPlacementRule buildRule)
        {
            _planningToolStore.EnterBuild(buildTypeId, buildCityId, buildRule);
        }

        public void BeginMove()
        {
            _planningToolStore.BeginMove();
        }

        public void BeginAttack()
        {
            _planningToolStore.BeginAttack();
        }

        public void BeginCharge()
        {
            _planningToolStore.BeginCharge();
        }

        public void SetMovePreviewTarget(string nodeId)
        {
            _planningToolStore.SetMovePreviewTarget(nodeId);
        }

        public void ClearMovePreviewTarget()
        {
            _planningToolStore.ClearMovePreviewTarget();
        }

        public void SetBuildPreviewTarget(string nodeId)
        {
            _planningToolStore.SetBuildPreviewTarget(nodeId);
        }

        public void ClearBuildPreviewTarget()
        {
            _planningToolStore.ClearBuildPreviewTarget();
        }
    }
}
