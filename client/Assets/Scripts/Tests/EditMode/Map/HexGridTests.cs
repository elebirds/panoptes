using NUnit.Framework;
using Panoptes.Presentation.Map;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class HexGridTests
    {
        [Test]
        public void AxialToWorld_UsesPointyTopFormula()
        {
            var world = HexGrid.AxialToWorld(2, 3, 1f);

            Assert.That(world.x, Is.EqualTo(Mathf.Sqrt(3f) * 3.5f).Within(0.0001f));
            Assert.That(world.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(world.z, Is.EqualTo(4.5f).Within(0.0001f));
        }

        [Test]
        public void WorldToAxial_UsesCubeRoundingForNearestHex()
        {
            var source = new Vector2Int(-2, 5);
            var world = HexGrid.AxialToWorld(source.x, source.y, 1f);
            world += new Vector3(0.2f, 0f, -0.18f);

            var rounded = HexGrid.WorldToAxial(world, 1f);

            Assert.That(rounded, Is.EqualTo(source));
        }

        [Test]
        public void AxialDistance_UsesCubeDistance()
        {
            Assert.That(HexGrid.AxialDistance(new Vector2Int(0, 0), new Vector2Int(2, -2)), Is.EqualTo(2));
            Assert.That(HexGrid.AxialDistance(new Vector2Int(-3, 4), new Vector2Int(2, -1)), Is.EqualTo(5));
        }
    }
}
