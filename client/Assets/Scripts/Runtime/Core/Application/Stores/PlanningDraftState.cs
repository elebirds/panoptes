using System.Collections.Generic;
using Panoptes.Core.Domain;

namespace Panoptes.Core.Application.Stores
{
    public sealed class PlanningDraftState
    {
        public PlanningDraftState(
            int snapshotTurn = 0,
            string snapshotPhase = "",
            IReadOnlyList<QueuedUnitOrderDto> unitOrders = null,
            IReadOnlyList<QueuedBuildOrderDto> buildOrders = null,
            IReadOnlyList<QueuedDemolishOrderDto> demolishOrders = null,
            IReadOnlyList<QueuedRecipeSelectionDto> recipeSelections = null,
            IReadOnlyList<QueuedWarZoneDirectiveDto> warZoneDirectives = null,
            IReadOnlyList<PlanningWarZoneDto> warZones = null,
            IReadOnlyList<MinisterDraftDto> ministerDrafts = null,
            PathPreviewDto currentPreview = null,
            BuildPreviewDto currentBuildPreview = null,
            RecipePreviewDto currentRecipePreview = null,
            string plannedResearchTargetTechnologyId = "",
            string plannedNationalPolicyId = "",
            IReadOnlyList<string> plannedInstitutionIds = null)
        {
            BuildOrders = StoreSnapshotCloner.CloneBuildOrders(buildOrders);
            DemolishOrders = StoreSnapshotCloner.CloneDemolishOrders(demolishOrders);
            CurrentBuildPreview = StoreSnapshotCloner.CloneBuildPreview(currentBuildPreview);
            CurrentPreview = StoreSnapshotCloner.ClonePathPreview(currentPreview);
            CurrentRecipePreview = StoreSnapshotCloner.CloneRecipePreview(currentRecipePreview);
            MinisterDrafts = StoreSnapshotCloner.CloneMinisterDrafts(ministerDrafts);
            PlannedInstitutionIds = StoreSnapshotCloner.CloneStrings(plannedInstitutionIds);
            PlannedNationalPolicyId = plannedNationalPolicyId ?? string.Empty;
            PlannedResearchTargetTechnologyId = plannedResearchTargetTechnologyId ?? string.Empty;
            RecipeSelections = StoreSnapshotCloner.CloneRecipeSelections(recipeSelections);
            SnapshotPhase = snapshotPhase ?? string.Empty;
            SnapshotTurn = snapshotTurn;
            UnitOrders = StoreSnapshotCloner.CloneUnitOrders(unitOrders);
            WarZoneDirectives = StoreSnapshotCloner.CloneWarZoneDirectives(warZoneDirectives);
            WarZones = StoreSnapshotCloner.CloneWarZones(warZones);
        }

        public IReadOnlyList<QueuedBuildOrderDto> BuildOrders { get; }
        public IReadOnlyList<QueuedDemolishOrderDto> DemolishOrders { get; }
        public BuildPreviewDto CurrentBuildPreview { get; }
        public PathPreviewDto CurrentPreview { get; }
        public RecipePreviewDto CurrentRecipePreview { get; }
        public IReadOnlyList<MinisterDraftDto> MinisterDrafts { get; }
        public IReadOnlyList<string> PlannedInstitutionIds { get; }
        public string PlannedNationalPolicyId { get; }
        public string PlannedResearchTargetTechnologyId { get; }
        public IReadOnlyList<QueuedRecipeSelectionDto> RecipeSelections { get; }
        public string SnapshotPhase { get; }
        public int SnapshotTurn { get; }
        public IReadOnlyList<QueuedUnitOrderDto> UnitOrders { get; }
        public IReadOnlyList<QueuedWarZoneDirectiveDto> WarZoneDirectives { get; }
        public IReadOnlyList<PlanningWarZoneDto> WarZones { get; }

        internal PlanningDraftState Clone()
        {
            return new PlanningDraftState(
                SnapshotTurn,
                SnapshotPhase,
                UnitOrders,
                BuildOrders,
                DemolishOrders,
                RecipeSelections,
                WarZoneDirectives,
                WarZones,
                MinisterDrafts,
                CurrentPreview,
                CurrentBuildPreview,
                CurrentRecipePreview,
                PlannedResearchTargetTechnologyId,
                PlannedNationalPolicyId,
                PlannedInstitutionIds);
        }
    }
}
