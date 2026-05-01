using System.Collections.Generic;
using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    public sealed class PlanningDraftStore : ReactiveStore<PlanningDraftState>
    {
        public PlanningDraftStore()
            : base(new PlanningDraftState())
        {
        }

        internal void Replace(PlanningDraftState state)
        {
            Publish(state ?? new PlanningDraftState());
        }

        internal void ReplaceOrders(IEnumerable<QueuedUnitOrderDto> unitOrders)
        {
            var current = Snapshot;
            Replace(new PlanningDraftState(
                current.SnapshotTurn,
                current.SnapshotPhase,
                StoreSnapshotCloner.CloneUnitOrders(unitOrders),
                current.BuildOrders,
                current.RecipeSelections,
                current.WarZoneDirectives,
                current.WarZones,
                current.MinisterDrafts,
                current.CurrentPreview,
                current.CurrentBuildPreview,
                current.CurrentRecipePreview,
                current.PlannedResearchTargetTechnologyId,
                current.PlannedNationalPolicyId,
                current.PlannedInstitutionPolicyIds));
        }

        protected override PlanningDraftState CloneState(PlanningDraftState state)
        {
            return state == null ? new PlanningDraftState() : state.Clone();
        }
    }
}
