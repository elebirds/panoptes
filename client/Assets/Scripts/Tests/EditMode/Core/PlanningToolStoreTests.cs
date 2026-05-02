using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using R3;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class PlanningToolStoreTests
    {
        [Test]
        public void Service_ShouldPublishBuildModeAndPreviewTargets()
        {
            var store = new PlanningToolStore();
            var service = new PlanningToolService(store);
            var observed = new List<PlanningToolState>();
            using var subscription = store.State.Subscribe(observed, static (state, target) => target.Add(state));

            service.EnterBuild(" farm ", " city-1 ", PlanningBuildPlacementRule.ResourceOnly);
            service.SetBuildPreviewTarget(" node-2 ");

            var state = store.Snapshot;
            Assert.That(state.Mode, Is.EqualTo(PlanningToolMode.Build));
            Assert.That(state.BuildTypeId, Is.EqualTo("farm"));
            Assert.That(state.BuildCityId, Is.EqualTo("city-1"));
            Assert.That(state.BuildRule, Is.EqualTo(PlanningBuildPlacementRule.ResourceOnly));
            Assert.That(state.BuildPreviewNodeId, Is.EqualTo("node-2"));
            Assert.That(observed.Last().BuildPreviewNodeId, Is.EqualTo("node-2"));
        }

        [Test]
        public void Service_ShouldPublishCombatModesAndClearToolState()
        {
            var store = new PlanningToolStore();
            var service = new PlanningToolService(store);

            service.BeginMove();
            service.SetMovePreviewTarget("n3");
            Assert.That(store.Snapshot.Mode, Is.EqualTo(PlanningToolMode.Move));
            Assert.That(store.Snapshot.MovePreviewNodeId, Is.EqualTo("n3"));

            service.BeginAttack();
            Assert.That(store.Snapshot.Mode, Is.EqualTo(PlanningToolMode.Attack));
            Assert.That(store.Snapshot.MovePreviewNodeId, Is.Empty);

            service.BeginCharge();
            Assert.That(store.Snapshot.Mode, Is.EqualTo(PlanningToolMode.Charge));

            service.ClearTool();
            Assert.That(store.Snapshot.Mode, Is.EqualTo(PlanningToolMode.None));
            Assert.That(store.Snapshot.MovePreviewNodeId, Is.Empty);
            Assert.That(store.Snapshot.BuildPreviewNodeId, Is.Empty);
        }
    }
}
