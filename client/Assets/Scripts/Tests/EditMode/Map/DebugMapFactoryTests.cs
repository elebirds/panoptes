using System.Linq;
using NUnit.Framework;
using Panoptes.Presentation.Map;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class DebugMapFactoryTests
    {
        [Test]
        public void CreateNodes_ShouldGenerateRequestedMinimumSize()
        {
            var nodes = DebugMapFactory.CreateNodes(new DebugMapFactory.Options
            {
                Width = 4,
                Height = 5,
                Seed = 7
            });

            Assert.That(nodes, Has.Count.EqualTo(30 * 30));
        }

        [Test]
        public void CreateNodes_ShouldPlaceCityCores_WhenTerritoriesEnabled()
        {
            var nodes = DebugMapFactory.CreateNodes(new DebugMapFactory.Options
            {
                Width = 30,
                Height = 30,
                Seed = 7,
                GenerateTerritories = true
            });

            Assert.That(nodes.Count(node => node.BuildingType == "city_core"), Is.EqualTo(4));
        }
    }
}
