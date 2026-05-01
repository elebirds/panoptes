using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.UI.HUD;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class UnitInfoHpStateResolverTests
    {
        [Test]
        public void Resolve_ShouldClampFallbackUnitHp()
        {
            var state = UnitInfoHpStateResolver.Resolve(
                fallbackHp: 15,
                fallbackMaxHp: 10,
                unitId: "unit-1",
                unitType: "infantry",
                cache: null);

            Assert.That(state.Hp, Is.EqualTo(15));
            Assert.That(state.MaxHp, Is.EqualTo(10));
            Assert.That(state.ClampedHp, Is.EqualTo(10));
            Assert.That(state.DisplayText, Is.EqualTo("10/10"));
        }

        [Test]
        public void ResolveBuildingMaxHp_ShouldUseNodeMaxHp_WhenPresent()
        {
            var maxHp = UnitInfoHpStateResolver.ResolveBuildingMaxHp(
                new NodeDto { BuildingType = "tower", BuildingHp = 6, BuildingMaxHp = 12 },
                fallbackType: "tower",
                hp: 6);

            Assert.That(maxHp, Is.EqualTo(12));
        }

        [Test]
        public void ResolveBuildingMaxHp_ShouldNotDropBelowCurrentHp()
        {
            var maxHp = UnitInfoHpStateResolver.ResolveBuildingMaxHp(
                new NodeDto { BuildingType = "tower", BuildingHp = 15, BuildingMaxHp = 8 },
                fallbackType: "tower",
                hp: 15);

            Assert.That(maxHp, Is.EqualTo(15));
        }

        [Test]
        public void ResolveBuildingMaxHp_ShouldUseCurrentHp_ForResourcePoints()
        {
            var maxHp = UnitInfoHpStateResolver.ResolveBuildingMaxHp(
                new NodeDto { IsResourcePoint = true, BuildingHp = 0, BuildingMaxHp = 99 },
                fallbackType: "resource_grain",
                hp: 4);

            Assert.That(maxHp, Is.EqualTo(4));
        }
    }
}
