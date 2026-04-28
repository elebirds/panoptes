using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;

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
    }
}
