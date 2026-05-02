using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapNodeInfoProxyFactoryTests
    {
        [Test]
        public void TryGetInspectableNodeInfo_WithBuilding_ShouldReturnNormalizedType()
        {
            var node = new NodeDto { BuildingType = " City_Core " };

            var result = MapNodeInfoProxyFactory.TryGetInspectableNodeInfo(
                node,
                out var buildingType,
                out var isResourcePoint);

            Assert.That(result, Is.True);
            Assert.That(buildingType, Is.EqualTo("city_core"));
            Assert.That(isResourcePoint, Is.False);
        }

        [Test]
        public void TryGetInspectableNodeInfo_WithResourcePoint_ShouldReturnInspectable()
        {
            var node = new NodeDto { IsResourcePoint = true };

            var result = MapNodeInfoProxyFactory.TryGetInspectableNodeInfo(
                node,
                out var buildingType,
                out var isResourcePoint);

            Assert.That(result, Is.True);
            Assert.That(buildingType, Is.Empty);
            Assert.That(isResourcePoint, Is.True);
        }
    }
}
