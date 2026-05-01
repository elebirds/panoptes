using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Planning.Feedback;
using Panoptes.Presentation.Planning.Input;
using Panoptes.Presentation.Planning.Input.Modes;
using Panoptes.Presentation.Planning.Input.State;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapInputModeHelpersTests
    {
        [Test]
        public void BuildPlacementInputMode_ShouldBlockCityCoreWhenConfigured()
        {
            Assert.That(BuildPlacementInputMode.IsManualPlacementBlocked(
                " CITY_CORE ",
                disallowCityCorePlacement: true,
                blockedBuildingTypes: null), Is.True);
        }

        [Test]
        public void BuildPlacementInputMode_ShouldBlockConfiguredBuildingTypes()
        {
            Assert.That(BuildPlacementInputMode.IsManualPlacementBlocked(
                "tower",
                disallowCityCorePlacement: false,
                blockedBuildingTypes: new[] { "farm", "Tower" }), Is.True);
        }

        [Test]
        public void MoveSelectionInputMode_ShouldPreferPendingTarget()
        {
            var pending = new PendingMoveState();
            pending.MarkPending("unit-1", "N2");
            var queuedOrders = new Dictionary<string, QueuedUnitOrderDto>
            {
                ["unit-1"] = new QueuedUnitOrderDto
                {
                    UnitId = "unit-1",
                    Action = "move",
                    TargetNodeId = "N3"
                }
            };

            Assert.That(MoveSelectionInputMode.TryResolvePlannedTargetNodeId(
                " unit-1 ",
                pending,
                queuedOrders,
                out var targetNodeId), Is.True);
            Assert.That(targetNodeId, Is.EqualTo("N2"));
        }

        [Test]
        public void MoveSelectionInputMode_ShouldResolveQueuedPathDestination()
        {
            var queuedOrders = new Dictionary<string, QueuedUnitOrderDto>
            {
                ["unit-2"] = new QueuedUnitOrderDto
                {
                    UnitId = "unit-2",
                    Action = "move",
                    TargetNodeId = "fallback",
                    PathNodeIds = new List<string> { "A1", " ", "C3" }
                }
            };

            Assert.That(MoveSelectionInputMode.TryResolvePlannedTargetNodeId(
                "unit-2",
                null,
                queuedOrders,
                out var targetNodeId), Is.True);
            Assert.That(targetNodeId, Is.EqualTo("C3"));
        }

        [Test]
        public void MoveSelectionInputMode_ShouldMatchCurrentPreview()
        {
            var preview = new PathPreviewDto
            {
                UnitId = "unit-3",
                TargetNodeId = "D4"
            };

            Assert.That(MoveSelectionInputMode.MatchesPreview(preview, "unit-3", "D4"), Is.True);
            Assert.That(MoveSelectionInputMode.MatchesPreview(preview, "unit-3", "D5"), Is.False);
        }

        [Test]
        public void TerritoryDeployInputMode_ShouldMatchExpansionUnitTypes()
        {
            Assert.That(TerritoryDeployInputMode.IsTerritoryExpansionUnitType(
                " Pioneer ",
                new[] { "settler", "pioneer" }), Is.True);
        }

        [Test]
        public void MovePreviewPresenter_ShouldResolveKnownErrorCodes()
        {
            var message = MovePreviewPresenter.ResolveErrorMessage(new PathPreviewDto
            {
                ErrorCode = "invalid_target"
            }, "C3");

            Assert.That(message, Is.EqualTo("目标节点 C3 当前无法抵达"));
        }

        [Test]
        public void BuildPreviewPresenter_ShouldFallbackWhileWaiting()
        {
            Assert.That(BuildPreviewPresenter.ResolveMessage(null), Is.EqualTo("检查中"));
        }

        [Test]
        public void PlanningInputTypes_ShouldLiveOutsideMapNamespace()
        {
            Assert.That(typeof(IPlanningInputMode).Namespace, Is.EqualTo("Panoptes.Presentation.Planning.Input"));
            Assert.That(typeof(BuildPlacementInputMode).Namespace, Is.EqualTo("Panoptes.Presentation.Planning.Input.Modes"));
            Assert.That(typeof(PendingMoveState).Namespace, Is.EqualTo("Panoptes.Presentation.Planning.Input.State"));
            Assert.That(typeof(MovePreviewPresenter).Namespace, Is.EqualTo("Panoptes.Presentation.Planning.Feedback"));
        }
    }
}
