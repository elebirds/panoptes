using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapPendingDeployGhostControllerTests
    {
        [Test]
        public void RememberGhost_ShouldNormalizeAndTrackUnit()
        {
            var controller = new MapPendingDeployGhostController();

            controller.RememberGhost(" unit-1 ", " node-1 ");

            Assert.That(controller.Count, Is.EqualTo(1));
            Assert.That(controller.HasGhostForUnit("unit-1"), Is.True);
        }

        [Test]
        public void ResolveCommittedNode_ShouldRemoveMatchingGhost()
        {
            var controller = new MapPendingDeployGhostController();
            controller.RememberGhost("unit-1", "node-1");

            controller.ResolveCommittedNode(
                "node-1",
                new NodeDto { BuildingType = "city_core" });

            Assert.That(controller.Count, Is.Zero);
            Assert.That(controller.HasGhostForUnit("unit-1"), Is.False);
        }

        [Test]
        public void ResolveCommittedNode_ShouldIgnoreEmptyBuilding()
        {
            var controller = new MapPendingDeployGhostController();
            controller.RememberGhost("unit-1", "node-1");

            controller.ResolveCommittedNode("node-1", new NodeDto());

            Assert.That(controller.Count, Is.EqualTo(1));
        }
    }
}
