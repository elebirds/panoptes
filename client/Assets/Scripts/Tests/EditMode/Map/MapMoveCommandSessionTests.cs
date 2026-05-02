using NUnit.Framework;
using Panoptes.Presentation.Map;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapMoveCommandSessionTests
    {
        [Test]
        public void MarkPendingMove_ShouldTrackUnit()
        {
            var session = new MapMoveCommandSession();

            session.MarkPendingMove(" unit-1 ", "node-2", null);

            Assert.That(session.IsUnitMovePending("unit-1"), Is.True);
        }

        [Test]
        public void ClearUnit_ShouldRemovePendingMove()
        {
            var session = new MapMoveCommandSession();
            session.MarkPendingMove("unit-1", "node-2", null);

            session.ClearUnit(" unit-1 ");

            Assert.That(session.IsUnitMovePending("unit-1"), Is.False);
        }

        [Test]
        public void RememberPath_ShouldReturnRememberedPath()
        {
            var session = new MapMoveCommandSession();
            var path = new[] { "a", "b" };

            session.RememberPath("unit-1", path);

            Assert.That(session.GetRememberedPath("unit-1"), Is.EqualTo(path));
        }
    }
}
