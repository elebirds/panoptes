using Panoptes.Core.Domain;
using Panoptes.Presentation.Planning.Feedback;

namespace Panoptes.Presentation.Map
{
    public static class MapCombatTargetingResolver
    {
        public static bool CanAttack(CatalogUnitDto entry)
        {
            return entry != null &&
                   entry.Attack > 0 &&
                   entry.AttackRange > 0 &&
                   !MapInputTokens.HasTag(entry.Tags, "civilian");
        }

        public static bool CanAttackStructures(CatalogUnitDto entry)
        {
            return entry != null &&
                   entry.Flags != null &&
                   entry.Flags.CanAttackStructures;
        }

        public static bool CanCharge(CatalogUnitDto entry)
        {
            return entry != null && MapInputTokens.HasTag(entry.Tags, "charge");
        }

        public static bool IsEnemyStructure(NodeDto nodeState, string localOwnerId)
        {
            if (nodeState == null || string.IsNullOrWhiteSpace(nodeState.BuildingType))
            {
                return false;
            }

            var localOwner = MapInputTokens.Normalize(localOwnerId);
            var owner = MapInputTokens.Normalize(nodeState.Owner);
            if (string.IsNullOrWhiteSpace(owner))
            {
                return false;
            }

            return !string.Equals(owner, localOwner, System.StringComparison.Ordinal);
        }
    }
}
