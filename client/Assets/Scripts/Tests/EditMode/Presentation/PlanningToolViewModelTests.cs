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
            var planningToolService = new PlanningToolService(planningToolStore);
            var selectionService = new SelectionService(selectionStore);
            using var viewModel = new PlanningToolViewModel(planningToolStore, selectionStore);

            selectionService.SelectUnit("u1");
            planningToolService.BeginMove();

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
            var planningToolService = new PlanningToolService(planningToolStore);
            using var viewModel = new PlanningToolViewModel(planningToolStore, selectionStore);

            planningToolService.EnterBuild("mine", "capital", PlanningBuildPlacementRule.CityOnly);

            Assert.That(viewModel.Current.Mode, Is.EqualTo(PlanningToolMode.Build));
            Assert.That(viewModel.Current.BuildTypeId, Is.EqualTo("mine"));
            Assert.That(viewModel.Current.BuildCityId, Is.EqualTo("capital"));
            Assert.That(viewModel.Current.BuildRule, Is.EqualTo(PlanningBuildPlacementRule.CityOnly));
            Assert.That(viewModel.Current.IsBuildPlacement, Is.True);
        }
    }
}
