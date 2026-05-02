using NUnit.Framework;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.ViewModels;

namespace Panoptes.Tests.EditMode.Map
{
    public sealed class MapPlanningInputStateAdapterTests
    {
        [Test]
        public void SetCombatActionMode_ShouldPublishPlanningToolMode()
        {
            var planningToolStore = new PlanningToolStore();
            var selectionStore = new SelectionStore();
            var adapter = CreateAdapter(planningToolStore, selectionStore);

            adapter.SetCombatActionMode(MapPlanningInputController.CombatActionMode.Attack);

            Assert.That(adapter.CombatActionMode, Is.EqualTo(MapPlanningInputController.CombatActionMode.Attack));
            Assert.That(planningToolStore.Snapshot.Mode, Is.EqualTo(PlanningToolMode.Attack));

            adapter.SetCombatActionMode(MapPlanningInputController.CombatActionMode.None);

            Assert.That(planningToolStore.Snapshot.Mode, Is.EqualTo(PlanningToolMode.None));
        }

        [Test]
        public void BuildAndPreviewState_ShouldPublishThroughPlanningToolStore()
        {
            var planningToolStore = new PlanningToolStore();
            var selectionStore = new SelectionStore();
            var adapter = CreateAdapter(planningToolStore, selectionStore);

            adapter.SetBuildModeActive(
                true,
                "farm",
                "capital",
                MapPlanningInputController.BuildPlacementRule.CityOnly);
            adapter.SetBuildPreviewTarget("n1");

            var state = planningToolStore.Snapshot;
            Assert.That(adapter.IsBuildModeActive(), Is.True);
            Assert.That(state.Mode, Is.EqualTo(PlanningToolMode.Build));
            Assert.That(state.BuildTypeId, Is.EqualTo("farm"));
            Assert.That(state.BuildCityId, Is.EqualTo("capital"));
            Assert.That(state.BuildRule, Is.EqualTo(PlanningBuildPlacementRule.CityOnly));
            Assert.That(state.BuildPreviewNodeId, Is.EqualTo("n1"));

            adapter.SetBuildModeActive(
                false,
                "farm",
                "capital",
                MapPlanningInputController.BuildPlacementRule.CityOnly);

            Assert.That(adapter.IsBuildModeActive(), Is.False);
            Assert.That(planningToolStore.Snapshot.Mode, Is.EqualTo(PlanningToolMode.None));
        }

        [Test]
        public void PublishSelectedUnit_ShouldUpdateSelectionStore()
        {
            var planningToolStore = new PlanningToolStore();
            var selectionStore = new SelectionStore();
            var adapter = CreateAdapter(planningToolStore, selectionStore);

            adapter.PublishSelectedUnit("u1");

            Assert.That(selectionStore.Snapshot.SelectedUnitId, Is.EqualTo("u1"));

            adapter.PublishSelectedUnit(string.Empty);

            Assert.That(selectionStore.Snapshot.SelectedUnitId, Is.Empty);
        }

        private static MapPlanningInputStateAdapter CreateAdapter(
            PlanningToolStore planningToolStore,
            SelectionStore selectionStore)
        {
            var adapter = new MapPlanningInputStateAdapter();
            adapter.Configure(
                new PlanningToolService(planningToolStore),
                new SelectionService(selectionStore),
                new PlanningToolViewModel(planningToolStore, selectionStore));
            return adapter;
        }
    }
}
