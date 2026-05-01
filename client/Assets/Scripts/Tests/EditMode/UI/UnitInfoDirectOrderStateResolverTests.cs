using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.UI.HUD;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class UnitInfoDirectOrderStateResolverTests
    {
        [Test]
        public void Resolve_ShouldAllowMoveOnly_ForCivilianUnits()
        {
            var state = UnitInfoDirectOrderStateResolver.Resolve(
                new StaticCatalogCache.UnitEntryJson { tags = new[] { "civilian" } },
                isBuildingOrResourceType: false);

            Assert.That(state.CanMove, Is.True);
            Assert.That(state.IsMilitaryUnit, Is.False);
            Assert.That(state.CanAttack, Is.False);
            Assert.That(state.CanCharge, Is.False);
        }

        [Test]
        public void Resolve_ShouldAllowMilitaryActions_ForChargeUnits()
        {
            var state = UnitInfoDirectOrderStateResolver.Resolve(
                new StaticCatalogCache.UnitEntryJson { tags = new[] { "charge" } },
                isBuildingOrResourceType: false);

            Assert.That(state.CanMove, Is.True);
            Assert.That(state.IsMilitaryUnit, Is.True);
            Assert.That(state.CanAttack, Is.True);
            Assert.That(state.CanCharge, Is.True);
        }

        [Test]
        public void Resolve_ShouldDisableAllActions_ForBuildingOrResourceTypes()
        {
            var state = UnitInfoDirectOrderStateResolver.Resolve(
                new StaticCatalogCache.UnitEntryJson { tags = new[] { "charge" } },
                isBuildingOrResourceType: true);

            Assert.That(state.CanMove, Is.False);
            Assert.That(state.IsMilitaryUnit, Is.False);
            Assert.That(state.CanAttack, Is.False);
            Assert.That(state.CanCharge, Is.False);
        }
    }
}
