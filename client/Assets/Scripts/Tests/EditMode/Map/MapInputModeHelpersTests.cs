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
