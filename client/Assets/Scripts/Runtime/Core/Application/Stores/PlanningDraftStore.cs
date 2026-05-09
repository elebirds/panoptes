using System;
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
            var next = state ?? new PlanningDraftState();
            if (StateEquals(Snapshot, next))
            {
                return;
            }

            Publish(next);
        }

        internal void ReplaceOrders(IEnumerable<QueuedUnitOrderDto> unitOrders)
        {
            var current = Snapshot;
            Replace(new PlanningDraftState(
                current.SnapshotTurn,
                current.SnapshotPhase,
                StoreSnapshotCloner.CloneUnitOrders(unitOrders),
                current.BuildOrders,
                current.DemolishOrders,
                current.RecipeSelections,
                current.WarZoneDirectives,
                current.WarZones,
                current.MinisterDrafts,
                current.CurrentPreview,
                current.CurrentBuildPreview,
                current.CurrentRecipePreview,
                current.PlannedResearchTargetTechnologyId,
                current.PlannedNationalPolicyId,
                current.PlannedInstitutionIds));
        }

        protected override PlanningDraftState CloneState(PlanningDraftState state)
        {
            return state == null ? new PlanningDraftState() : state.Clone();
        }

        private static bool StateEquals(PlanningDraftState left, PlanningDraftState right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return left.SnapshotTurn == right.SnapshotTurn &&
                   string.Equals(left.SnapshotPhase, right.SnapshotPhase, StringComparison.Ordinal) &&
                   string.Equals(left.PlannedResearchTargetTechnologyId, right.PlannedResearchTargetTechnologyId, StringComparison.Ordinal) &&
                   string.Equals(left.PlannedNationalPolicyId, right.PlannedNationalPolicyId, StringComparison.Ordinal) &&
                   StringListEquals(left.PlannedInstitutionIds, right.PlannedInstitutionIds) &&
                   UnitOrdersEqual(left.UnitOrders, right.UnitOrders) &&
                   BuildOrdersEqual(left.BuildOrders, right.BuildOrders) &&
                   DemolishOrdersEqual(left.DemolishOrders, right.DemolishOrders) &&
                   RecipeSelectionsEqual(left.RecipeSelections, right.RecipeSelections) &&
                   WarZoneDirectivesEqual(left.WarZoneDirectives, right.WarZoneDirectives) &&
                   WarZonesEqual(left.WarZones, right.WarZones) &&
                   MinisterDraftsEqual(left.MinisterDrafts, right.MinisterDrafts) &&
                   PathPreviewEquals(left.CurrentPreview, right.CurrentPreview) &&
                   BuildPreviewEquals(left.CurrentBuildPreview, right.CurrentBuildPreview) &&
                   RecipePreviewEquals(left.CurrentRecipePreview, right.CurrentRecipePreview);
        }

        private static bool UnitOrdersEqual(IReadOnlyList<QueuedUnitOrderDto> left, IReadOnlyList<QueuedUnitOrderDto> right)
        {
            if (!SameCount(left, right))
            {
                return false;
            }

            for (var i = 0; i < (left?.Count ?? 0); i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                    {
                        return false;
                    }
                    continue;
                }

                if (!string.Equals(a.UnitId, b.UnitId, StringComparison.Ordinal) ||
                    !string.Equals(a.Action, b.Action, StringComparison.Ordinal) ||
                    !string.Equals(a.TargetNodeId, b.TargetNodeId, StringComparison.Ordinal) ||
                    !string.Equals(a.TargetUnitId, b.TargetUnitId, StringComparison.Ordinal) ||
                    !string.Equals(a.SecondaryNodeId, b.SecondaryNodeId, StringComparison.Ordinal) ||
                    !string.Equals(a.FirstTurnNodeId, b.FirstTurnNodeId, StringComparison.Ordinal) ||
                    a.TotalTurns != b.TotalTurns ||
                    !StringListEquals(a.PathNodeIds, b.PathNodeIds) ||
                    !StringDictionaryEquals(a.Params, b.Params) ||
                    !TurnStopsEqual(a.TurnStops, b.TurnStops))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool BuildOrdersEqual(IReadOnlyList<QueuedBuildOrderDto> left, IReadOnlyList<QueuedBuildOrderDto> right)
        {
            if (!SameCount(left, right))
            {
                return false;
            }

            for (var i = 0; i < (left?.Count ?? 0); i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                    {
                        return false;
                    }
                    continue;
                }

                if (!string.Equals(a.NodeId, b.NodeId, StringComparison.Ordinal) ||
                    !string.Equals(a.BuildingTypeId, b.BuildingTypeId, StringComparison.Ordinal) ||
                    !string.Equals(a.CityId, b.CityId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool DemolishOrdersEqual(IReadOnlyList<QueuedDemolishOrderDto> left, IReadOnlyList<QueuedDemolishOrderDto> right)
        {
            if (!SameCount(left, right))
            {
                return false;
            }

            for (var i = 0; i < (left?.Count ?? 0); i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                    {
                        return false;
                    }
                    continue;
                }

                if (!string.Equals(a.NodeId, b.NodeId, StringComparison.Ordinal) ||
                    !string.Equals(a.BuildingTypeId, b.BuildingTypeId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool RecipeSelectionsEqual(IReadOnlyList<QueuedRecipeSelectionDto> left, IReadOnlyList<QueuedRecipeSelectionDto> right)
        {
            if (!SameCount(left, right))
            {
                return false;
            }

            for (var i = 0; i < (left?.Count ?? 0); i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                    {
                        return false;
                    }
                    continue;
                }

                if (!string.Equals(a.NodeId, b.NodeId, StringComparison.Ordinal) ||
                    !string.Equals(a.RecipeId, b.RecipeId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool WarZoneDirectivesEqual(IReadOnlyList<QueuedWarZoneDirectiveDto> left, IReadOnlyList<QueuedWarZoneDirectiveDto> right)
        {
            if (!SameCount(left, right))
            {
                return false;
            }

            for (var i = 0; i < (left?.Count ?? 0); i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                    {
                        return false;
                    }
                    continue;
                }

                if (!string.Equals(a.ZoneId, b.ZoneId, StringComparison.Ordinal) ||
                    !string.Equals(a.Directive, b.Directive, StringComparison.Ordinal) ||
                    !string.Equals(a.TargetNode, b.TargetNode, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool WarZonesEqual(IReadOnlyList<PlanningWarZoneDto> left, IReadOnlyList<PlanningWarZoneDto> right)
        {
            if (!SameCount(left, right))
            {
                return false;
            }

            for (var i = 0; i < (left?.Count ?? 0); i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                    {
                        return false;
                    }
                    continue;
                }

                if (!string.Equals(a.Id, b.Id, StringComparison.Ordinal) ||
                    !string.Equals(a.Name, b.Name, StringComparison.Ordinal) ||
                    !string.Equals(a.Directive, b.Directive, StringComparison.Ordinal) ||
                    !string.Equals(a.TargetNode, b.TargetNode, StringComparison.Ordinal) ||
                    !StringListEquals(a.NodeIds, b.NodeIds))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MinisterDraftsEqual(IReadOnlyList<MinisterDraftDto> left, IReadOnlyList<MinisterDraftDto> right)
        {
            if (!SameCount(left, right))
            {
                return false;
            }

            for (var i = 0; i < (left?.Count ?? 0); i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                    {
                        return false;
                    }
                    continue;
                }

                if (!string.Equals(a.DraftId, b.DraftId, StringComparison.Ordinal) ||
                    !string.Equals(a.PlayerId, b.PlayerId, StringComparison.Ordinal) ||
                    !string.Equals(a.MinisterRole, b.MinisterRole, StringComparison.Ordinal) ||
                    !string.Equals(a.Kind, b.Kind, StringComparison.Ordinal) ||
                    !string.Equals(a.TargetId, b.TargetId, StringComparison.Ordinal) ||
                    !string.Equals(a.TargetLabel, b.TargetLabel, StringComparison.Ordinal) ||
                    !string.Equals(a.Title, b.Title, StringComparison.Ordinal) ||
                    !string.Equals(a.Summary, b.Summary, StringComparison.Ordinal) ||
                    !string.Equals(a.Rationale, b.Rationale, StringComparison.Ordinal) ||
                    !string.Equals(a.RiskNote, b.RiskNote, StringComparison.Ordinal) ||
                    !string.Equals(a.Status, b.Status, StringComparison.Ordinal) ||
                    a.Available != b.Available ||
                    a.Turn != b.Turn ||
                    !string.Equals(a.Source, b.Source, StringComparison.Ordinal) ||
                    !StringArrayEquals(a.InstitutionIds, b.InstitutionIds) ||
                    !string.Equals(a.NodeId, b.NodeId, StringComparison.Ordinal) ||
                    !string.Equals(a.BuildingTypeId, b.BuildingTypeId, StringComparison.Ordinal) ||
                    !string.Equals(a.CityId, b.CityId, StringComparison.Ordinal) ||
                    !string.Equals(a.RecipeId, b.RecipeId, StringComparison.Ordinal) ||
                    !string.Equals(a.UnitId, b.UnitId, StringComparison.Ordinal) ||
                    !string.Equals(a.Action, b.Action, StringComparison.Ordinal) ||
                    !string.Equals(a.TargetNodeId, b.TargetNodeId, StringComparison.Ordinal) ||
                    !string.Equals(a.TargetUnitId, b.TargetUnitId, StringComparison.Ordinal) ||
                    !string.Equals(a.SecondaryNodeId, b.SecondaryNodeId, StringComparison.Ordinal) ||
                    !string.Equals(a.OperationId, b.OperationId, StringComparison.Ordinal) ||
                    !string.Equals(a.Objective, b.Objective, StringComparison.Ordinal) ||
                    !MinisterOperationCommandsEqual(a.OperationCommands, b.OperationCommands))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MinisterOperationCommandsEqual(
            IReadOnlyList<MinisterOperationCommandDto> left,
            IReadOnlyList<MinisterOperationCommandDto> right)
        {
            if (!SameCount(left, right))
            {
                return false;
            }

            for (var i = 0; i < (left?.Count ?? 0); i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                    {
                        return false;
                    }

                    continue;
                }

                if (!string.Equals(a.Label, b.Label, StringComparison.Ordinal) ||
                    !string.Equals(a.Kind, b.Kind, StringComparison.Ordinal) ||
                    !string.Equals(a.RawJson, b.RawJson, StringComparison.Ordinal) ||
                    !string.Equals(a.NodeId, b.NodeId, StringComparison.Ordinal) ||
                    !string.Equals(a.BuildingTypeId, b.BuildingTypeId, StringComparison.Ordinal) ||
                    !string.Equals(a.CityId, b.CityId, StringComparison.Ordinal) ||
                    !string.Equals(a.RecipeId, b.RecipeId, StringComparison.Ordinal) ||
                    !string.Equals(a.UnitId, b.UnitId, StringComparison.Ordinal) ||
                    !string.Equals(a.Action, b.Action, StringComparison.Ordinal) ||
                    !string.Equals(a.TargetNodeId, b.TargetNodeId, StringComparison.Ordinal) ||
                    !string.Equals(a.TargetUnitId, b.TargetUnitId, StringComparison.Ordinal) ||
                    !string.Equals(a.SecondaryNodeId, b.SecondaryNodeId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PathPreviewEquals(PathPreviewDto left, PathPreviewDto right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.RequestId, right.RequestId, StringComparison.Ordinal) &&
                   string.Equals(left.UnitId, right.UnitId, StringComparison.Ordinal) &&
                   string.Equals(left.Action, right.Action, StringComparison.Ordinal) &&
                   string.Equals(left.TargetNodeId, right.TargetNodeId, StringComparison.Ordinal) &&
                   left.Valid == right.Valid &&
                   string.Equals(left.ErrorCode, right.ErrorCode, StringComparison.Ordinal) &&
                   string.Equals(left.FirstTurnNodeId, right.FirstTurnNodeId, StringComparison.Ordinal) &&
                   left.TotalTurns == right.TotalTurns &&
                   StringListEquals(left.PathNodeIds, right.PathNodeIds) &&
                   TurnStopsEqual(left.TurnStops, right.TurnStops);
        }

        private static bool BuildPreviewEquals(BuildPreviewDto left, BuildPreviewDto right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.RequestId, right.RequestId, StringComparison.Ordinal) &&
                   string.Equals(left.NodeId, right.NodeId, StringComparison.Ordinal) &&
                   string.Equals(left.BuildingTypeId, right.BuildingTypeId, StringComparison.Ordinal) &&
                   string.Equals(left.CityId, right.CityId, StringComparison.Ordinal) &&
                   left.Valid == right.Valid &&
                   string.Equals(left.ErrorCode, right.ErrorCode, StringComparison.Ordinal) &&
                   string.Equals(left.Message, right.Message, StringComparison.Ordinal) &&
                   StringDictionaryEquals(left.Details, right.Details);
        }

        private static bool RecipePreviewEquals(RecipePreviewDto left, RecipePreviewDto right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.RequestId, right.RequestId, StringComparison.Ordinal) &&
                   string.Equals(left.NodeId, right.NodeId, StringComparison.Ordinal) &&
                   string.Equals(left.RecipeId, right.RecipeId, StringComparison.Ordinal) &&
                   left.Valid == right.Valid &&
                   string.Equals(left.ErrorCode, right.ErrorCode, StringComparison.Ordinal) &&
                   string.Equals(left.Message, right.Message, StringComparison.Ordinal) &&
                   StringDictionaryEquals(left.Details, right.Details);
        }

        private static bool TurnStopsEqual(IReadOnlyList<MarchTurnStopDto> left, IReadOnlyList<MarchTurnStopDto> right)
        {
            if (!SameCount(left, right))
            {
                return false;
            }

            for (var i = 0; i < (left?.Count ?? 0); i++)
            {
                var a = left[i];
                var b = right[i];
                if (a == null || b == null)
                {
                    if (!ReferenceEquals(a, b))
                    {
                        return false;
                    }
                    continue;
                }

                if (a.TurnIndex != b.TurnIndex ||
                    !string.Equals(a.NodeId, b.NodeId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StringListEquals(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (!SameCount(left, right))
            {
                return false;
            }

            for (var i = 0; i < (left?.Count ?? 0); i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StringArrayEquals(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            return StringListEquals(left, right);
        }

        private static bool StringDictionaryEquals(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return (left?.Count ?? 0) == (right?.Count ?? 0);
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (var pair in left)
            {
                if (!right.TryGetValue(pair.Key, out var value) ||
                    !string.Equals(pair.Value, value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameCount<T>(IReadOnlyCollection<T> left, IReadOnlyCollection<T> right)
        {
            return (left?.Count ?? 0) == (right?.Count ?? 0);
        }
    }
}
