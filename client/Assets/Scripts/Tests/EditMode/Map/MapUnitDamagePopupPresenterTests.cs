using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapUnitDamagePopupPresenterTests
    {
        [Test]
        public void RememberUnit_ShouldTrackKnownHp()
        {
            var presenter = new MapUnitDamagePopupPresenter();

            presenter.RememberUnit(new UnitDto { Id = " unit-1 ", Hp = 7 });

            Assert.That(presenter.KnownUnitCount, Is.EqualTo(1));
        }

        [Test]
        public void ForgetUnit_ShouldRemoveTrackedHp()
        {
            var presenter = new MapUnitDamagePopupPresenter();
            presenter.RememberUnit(new UnitDto { Id = "unit-1", Hp = 7 });

            presenter.ForgetUnit(" unit-1 ");

            Assert.That(presenter.KnownUnitCount, Is.Zero);
        }

        [Test]
        public void Clear_ShouldRemoveAllTrackedHp()
        {
            var presenter = new MapUnitDamagePopupPresenter();
            presenter.RememberUnit(new UnitDto { Id = "unit-1", Hp = 7 });
            presenter.RememberUnit(new UnitDto { Id = "unit-2", Hp = 3 });

            presenter.Clear();

            Assert.That(presenter.KnownUnitCount, Is.Zero);
        }
    }
}
