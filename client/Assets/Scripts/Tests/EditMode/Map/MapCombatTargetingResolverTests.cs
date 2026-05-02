using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapCombatTargetingResolverTests
    {
        [Test]
        public void CanAttack_ShouldRejectCivilianUnits()
        {
            var entry = new StaticCatalogCache.UnitEntryJson
            {
                tags = new[] { "civilian" }
            };

            Assert.That(MapCombatTargetingResolver.CanAttack(entry), Is.False);
        }

        [Test]
        public void CanAttackStructures_ShouldReadCatalogFlag()
        {
            var entry = new StaticCatalogCache.UnitEntryJson
            {
                flags = new StaticCatalogCache.UnitEntryJson.UnitFlagsJson
                {
                    can_attack_structures = true
                }
            };

            Assert.That(MapCombatTargetingResolver.CanAttackStructures(entry), Is.True);
        }

        [Test]
        public void IsEnemyStructure_ShouldRequireBuildingAndDifferentOwner()
        {
            var enemy = new NodeDto
            {
                BuildingType = "farm",
                Owner = "red"
            };
            var empty = new NodeDto
            {
                Owner = "red"
            };

            Assert.That(MapCombatTargetingResolver.IsEnemyStructure(enemy, "blue"), Is.True);
            Assert.That(MapCombatTargetingResolver.IsEnemyStructure(enemy, "red"), Is.False);
            Assert.That(MapCombatTargetingResolver.IsEnemyStructure(empty, "blue"), Is.False);
        }
    }
}
