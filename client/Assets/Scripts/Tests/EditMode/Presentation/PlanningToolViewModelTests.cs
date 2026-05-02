using NUnit.Framework;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.ViewModels;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class PlanningToolViewModelTests
    {
        [Test]
        public void ViewModel_ShouldProjectSelectionAndMovePrompt()
        {
            var planningToolStore = new PlanningToolStore();
            var selectionStore = new SelectionStore();
            using var viewModel = new PlanningToolViewModel(
                planningToolStore,
                selectionStore,
                new PlanningToolService(planningToolStore),
                new SelectionService(selectionStore));

            viewModel.SelectUnit("u1");
            viewModel.BeginMove();

            Assert.That(viewModel.Current.SelectedUnitId, Is.EqualTo("u1"));
            Assert.That(viewModel.Current.Mode, Is.EqualTo(PlanningToolMode.Move));
            Assert.That(viewModel.Current.IsMoveTargeting, Is.True);
            Assert.That(viewModel.Current.Prompt, Is.EqualTo("悬停节点预览路径，点击后下达移动指令。"));
        }

        [Test]
        public void ViewModel_ShouldProjectBuildPlacementState()
        {
            var planningToolStore = new PlanningToolStore();
            var selectionStore = new SelectionStore();
            using var viewModel = new PlanningToolViewModel(
                planningToolStore,
                selectionStore,
                new PlanningToolService(planningToolStore),
                new SelectionService(selectionStore));

            viewModel.EnterBuild("mine", "capital", PlanningBuildPlacementRule.CityOnly);

            Assert.That(viewModel.Current.Mode, Is.EqualTo(PlanningToolMode.Build));
            Assert.That(viewModel.Current.BuildTypeId, Is.EqualTo("mine"));
            Assert.That(viewModel.Current.BuildCityId, Is.EqualTo("capital"));
            Assert.That(viewModel.Current.BuildRule, Is.EqualTo(PlanningBuildPlacementRule.CityOnly));
            Assert.That(viewModel.Current.IsBuildPlacement, Is.True);
        }
    }
}
