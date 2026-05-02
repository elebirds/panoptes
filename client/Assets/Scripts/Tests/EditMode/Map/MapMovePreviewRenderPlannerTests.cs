using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapMovePreviewRenderPlannerTests
    {
        [Test]
        public void Create_WithMatchingValidPreview_ShouldRenderValid()
        {
            var preview = new PathPreviewDto
            {
                UnitId = "u1",
                TargetNodeId = "n2",
                Valid = true,
                PathNodeIds = new List<string> { "n1", "n2" }
            };

            var plan = MapMovePreviewRenderPlanner.Create(
                preview,
                hoverNodeId: "n2",
                selectedUnitId: "u1",
                hasMapRenderer: true);

            Assert.That(plan.Kind, Is.EqualTo(MapMovePreviewRenderKind.Valid));
            Assert.That(plan.Preview, Is.SameAs(preview));
        }

        [Test]
        public void Create_WithMatchingInvalidPreview_ShouldRenderInvalid()
        {
            var preview = new PathPreviewDto
            {
                UnitId = "u1",
                TargetNodeId = "n2",
                Valid = false
            };

            var plan = MapMovePreviewRenderPlanner.Create(
                preview,
                hoverNodeId: "n2",
                selectedUnitId: "u1",
                hasMapRenderer: true);

            Assert.That(plan.Kind, Is.EqualTo(MapMovePreviewRenderKind.Invalid));
            Assert.That(plan.TargetNodeId, Is.EqualTo("n2"));
        }

        [Test]
        public void Create_WithMismatchedSelection_ShouldRenderNothing()
        {
            var preview = new PathPreviewDto
            {
                UnitId = "u1",
                TargetNodeId = "n2",
                Valid = true
            };

            var plan = MapMovePreviewRenderPlanner.Create(
                preview,
                hoverNodeId: "n2",
                selectedUnitId: "u2",
                hasMapRenderer: true);

            Assert.That(plan.Kind, Is.EqualTo(MapMovePreviewRenderKind.None));
        }
    }
}
