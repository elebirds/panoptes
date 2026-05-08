using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.UI.Turn;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class SettlementTimelineEventFormatterTests
    {
        [Test]
        public void TryDescribeEvent_ShouldDescribeUnitMovement()
        {
            var evt = new TurnEventDto
            {
                Type = "unit_moved",
                UnitId = "scout-1",
                ToQ = 4,
                ToR = -1
            };

            var ok = SettlementTimelineEventFormatter.TryDescribeEvent(evt, out var description);

            Assert.That(ok, Is.True);
            Assert.That(description, Does.Contain("scout-1"));
            Assert.That(description, Does.Contain("(4,-1)"));
        }

        [Test]
        public void TryDescribeEvent_ShouldDescribeCityCoreDestroyedAsTimelineEvent()
        {
            var evt = new TurnEventDto
            {
                Type = "city_core_destroyed",
                NodeId = "A1"
            };

            var ok = SettlementTimelineEventFormatter.TryDescribeEvent(evt, out var description);

            Assert.That(ok, Is.True);
            Assert.That(description, Does.Contain("A1"));
            Assert.That(description, Does.Contain("摧毁"));
        }

        [Test]
        public void TryDescribeEvent_ShouldPreserveBuildingProgressDescriptions()
        {
            var evt = new TurnEventDto
            {
                Type = "recipe_progressed"
            };
            evt.Data.Add("node_id", "B2");
            evt.Data.Add("recipe_id", "iron_tools");
            evt.Data.Add("amount", "3");

            var ok = SettlementTimelineEventFormatter.TryDescribeEvent(evt, out var description);

            Assert.That(ok, Is.True);
            Assert.That(description, Does.Contain("B2"));
            Assert.That(description, Does.Contain("iron_tools"));
            Assert.That(description, Does.Contain("3"));
        }
    }
}
