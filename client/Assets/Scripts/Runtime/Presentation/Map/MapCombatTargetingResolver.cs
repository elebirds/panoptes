using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Planning.Feedback;

namespace Panoptes.Presentation.Map
{
    public static class MapCombatTargetingResolver
    {
        public static bool CanAttack(StaticCatalogCache.UnitEntryJson entry)
        {
            return entry != null && !MapInputTokens.HasTag(entry, "civilian");
        }

        public static bool CanAttackStructures(StaticCatalogCache.UnitEntryJson entry)
        {
            return entry != null &&
                   entry.flags != null &&
                   entry.flags.can_attack_structures;
        }

        public static bool CanCharge(StaticCatalogCache.UnitEntryJson entry)
        {
            return MapInputTokens.HasTag(entry, "charge");
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
