using NUnit.Framework;
using Panoptes.Core.Domain;
using Panoptes.Presentation.UI.HUD;

namespace Panoptes.Tests.EditMode.UI
{
    public sealed class UnitInfoPlanningSummaryPresenterTests
    {
        [Test]
        public void FormatSummary_ShouldIncludeActionAndNodeTarget()
        {
            var summary = UnitInfoPlanningSummaryPresenter.FormatSummary(new QueuedUnitOrderDto
            {
                Action = "move",
                TargetNodeId = "C3"
            });

            Assert.That(summary, Is.EqualTo("Planned: move -> C3"));
        }

        [Test]
        public void FormatSummary_ShouldFallbackToActionOnly_WhenTargetMissing()
        {
            var summary = UnitInfoPlanningSummaryPresenter.FormatSummary(new QueuedUnitOrderDto
            {
                Action = "hold"
            });

            Assert.That(summary, Is.EqualTo("Planned: hold"));
        }
    }
}
