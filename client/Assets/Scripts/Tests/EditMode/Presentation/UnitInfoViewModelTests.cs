using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.ViewModels;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class UnitInfoViewModelTests
    {
        [Test]
        public void SelectUnit_ShouldProjectCatalogHpPlanningAndDirectOrderState()
        {
            var gameStateStore = new GameStateStore();
            var selectionStore = new SelectionStore();
            var staticCatalogStore = new StaticCatalogStore();
            var planningDraftStore = new PlanningDraftStore();
            var selectionService = new SelectionService(selectionStore);
            using var viewModel = new UnitInfoViewModel(
                gameStateStore,
                selectionStore,
                staticCatalogStore,
                planningDraftStore,
                selectionService);

            staticCatalogStore.Replace(new StaticCatalogState(
                units: new Dictionary<string, CatalogUnitDto>
                {
                    ["fighter"] = new CatalogUnitDto
                    {
                        Id = "fighter",
                        Name = "Fighter",
                        Description = "Frontline unit",
                        Tags = new List<string> { "charge" }
                    }
                }));
            gameStateStore.Replace(new GameStateStoreState(
                myPlayerId: "p1",
                phase: GamePhases.Planning,
                units: new Dictionary<string, UnitDto>
                {
                    ["u1"] = new UnitDto
                    {
                        Id = "u1",
                        Type = "fighter",
                        Owner = "p1",
                        Hp = 8,
                        MaxHp = 10
                    }
                }));
            planningDraftStore.Replace(new PlanningDraftState(
                unitOrders: new List<QueuedUnitOrderDto>
                {
                    new QueuedUnitOrderDto
                    {
                        UnitId = "u1",
                        Action = "move",
                        TargetNodeId = "n2"
                    }
                }));

            viewModel.SelectUnit("u1");
            viewModel.SetActionLocked(true);

            var state = viewModel.Current;
            Assert.That(state.HasSelection, Is.True);
            Assert.That(state.UnitId, Is.EqualTo("u1"));
            Assert.That(state.DisplayName, Is.EqualTo("Fighter"));
            Assert.That(state.Description, Is.EqualTo("Frontline unit"));
            Assert.That(state.Hp, Is.EqualTo(8));
            Assert.That(state.MaxHp, Is.EqualTo(10));
            Assert.That(state.PlanningSummary, Is.EqualTo("Planned: move -> n2"));
            Assert.That(state.ShowDirectOrderButtons, Is.True);
            Assert.That(state.CanMove, Is.True);
            Assert.That(state.IsMilitaryUnit, Is.True);
            Assert.That(state.CanAttack, Is.True);
            Assert.That(state.CanCharge, Is.True);
            Assert.That(state.ActionLocked, Is.True);
        }

        [Test]
        public void SelectUnit_ShouldHideDirectOrdersForCivilianUnits()
        {
            var gameStateStore = new GameStateStore();
            var selectionStore = new SelectionStore();
            var staticCatalogStore = new StaticCatalogStore();
            var planningDraftStore = new PlanningDraftStore();
            using var viewModel = new UnitInfoViewModel(
                gameStateStore,
                selectionStore,
                staticCatalogStore,
                planningDraftStore,
                new SelectionService(selectionStore));

            staticCatalogStore.Replace(new StaticCatalogState(
                units: new Dictionary<string, CatalogUnitDto>
                {
                    ["settler"] = new CatalogUnitDto
                    {
                        Id = "settler",
                        Name = "Settler",
                        Tags = new List<string> { "civilian" }
                    }
                }));
            gameStateStore.Replace(new GameStateStoreState(
                myPlayerId: "p1",
                phase: GamePhases.Planning,
                units: new Dictionary<string, UnitDto>
                {
                    ["u1"] = new UnitDto { Id = "u1", Type = "settler", Owner = "p1", Hp = 5, MaxHp = 5 }
                }));

            viewModel.SelectUnit("u1");

            Assert.That(viewModel.Current.HasSelection, Is.True);
            Assert.That(viewModel.Current.ShowDirectOrderButtons, Is.True);
            Assert.That(viewModel.Current.CanMove, Is.True);
            Assert.That(viewModel.Current.IsMilitaryUnit, Is.False);
            Assert.That(viewModel.Current.CanAttack, Is.False);
            Assert.That(viewModel.Current.CanCharge, Is.False);
        }
    }
}
