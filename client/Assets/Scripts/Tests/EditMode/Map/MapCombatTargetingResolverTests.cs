using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapCombatTargetingResolverTests
    {
        [Test]
        public void CanAttack_ShouldRejectCivilianUnits()
        {
            var entry = new CatalogUnitDto
            {
                Attack = 1,
                AttackRange = 1,
                Tags = new System.Collections.Generic.List<string> { "civilian" }
            };

            Assert.That(MapCombatTargetingResolver.CanAttack(entry), Is.False);
        }

        [Test]
        public void CanAttack_ShouldRequireAttackAndRange()
        {
            Assert.That(MapCombatTargetingResolver.CanAttack(new CatalogUnitDto { Attack = 0, AttackRange = 1 }), Is.False);
            Assert.That(MapCombatTargetingResolver.CanAttack(new CatalogUnitDto { Attack = 1, AttackRange = 0 }), Is.False);
            Assert.That(MapCombatTargetingResolver.CanAttack(new CatalogUnitDto { Attack = 1, AttackRange = 2 }), Is.True);
        }

        [Test]
        public void CanAttackStructures_ShouldReadCatalogFlag()
        {
            var entry = new CatalogUnitDto
            {
                Flags = new CatalogUnitFlagsDto
                {
                    CanAttackStructures = true
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
