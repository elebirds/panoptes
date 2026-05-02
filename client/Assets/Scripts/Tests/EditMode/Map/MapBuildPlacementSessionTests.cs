using NUnit.Framework;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapBuildPlacementSessionTests
    {
        [Test]
        public void RememberPendingBuild_ShouldTrackAndRemoveByNode()
        {
            var session = new MapBuildPlacementSession();
            session.RememberPendingBuild(new MapPlanningInputController.PendingBuildRecord
            {
                buildingType = "farm",
                nodeId = "node-1",
                ownerId = "blue",
                isGhost = true
            });

            Assert.That(session.PendingBuilds.Count, Is.EqualTo(1));
            Assert.That(session.HasPendingBuild("node-1"), Is.True);

            session.RemovePendingBuild("node-1");

            Assert.That(session.PendingBuilds.Count, Is.Zero);
        }

        [Test]
        public void RollbackPendingBuild_WithoutMap_ShouldStillClearPendingRecord()
        {
            var session = new MapBuildPlacementSession();
            session.RememberPendingBuild(new MapPlanningInputController.PendingBuildRecord
            {
                buildingType = "farm",
                nodeId = "node-1",
                ownerId = "blue",
                isGhost = true
            });

            session.RollbackPendingBuild("node-1");

            Assert.That(session.HasPendingBuild("node-1"), Is.False);
        }

        [Test]
        public void EnterBuildPlacement_ShouldRejectMissingCityContext()
        {
            var session = new MapBuildPlacementSession();
            string error = null;

            var entered = session.EnterBuildPlacement(
                " farm ",
                string.Empty,
                MapPlanningInputController.BuildPlacementRule.AnyTerrain,
                CreateSettings(),
                message => error = message);

            Assert.That(entered, Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        private static MapBuildPlacementVisualSettings CreateSettings()
        {
            return new MapBuildPlacementVisualSettings(
                Color.green,
                Color.red,
                Color.yellow,
                Color.cyan,
                0.1f,
                false,
                System.Array.Empty<string>());
        }
    }
}
