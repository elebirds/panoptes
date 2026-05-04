using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.ViewModels;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class NationalOverviewViewModelTests
    {
        [Test]
        public void Current_ShouldProjectNationalOverviewFromStores()
        {
            var gameStateStore = new GameStateStore();
            var turnStore = new TurnStore();
            var planningDraftStore = new PlanningDraftStore();
            var staticCatalogStore = new StaticCatalogStore();
            var settlementStore = new SettlementStore();
            using var viewModel = new NationalOverviewViewModel(
                gameStateStore,
                turnStore,
                planningDraftStore,
                staticCatalogStore,
                settlementStore);

            staticCatalogStore.Replace(new StaticCatalogState(
                technologies: new Dictionary<string, CatalogTechnologyDto>
                {
                    ["irrigation"] = new CatalogTechnologyDto { Id = "irrigation", Name = "Irrigation" }
                },
                policies: new Dictionary<string, CatalogPolicyDto>
                {
                    ["logistics"] = new CatalogPolicyDto { Id = "logistics", Name = "Logistics" }
                }));
            gameStateStore.Replace(new GameStateStoreState(
                turn: 2,
                phase: GamePhases.Planning,
                mapWidth: 5,
                mapHeight: 4,
                tokensLeft: 3,
                nodes: new Dictionary<string, NodeDto>
                {
                    ["n1"] = new NodeDto { Id = "n1", IsVisible = true, CityId = "city-a" },
                    ["n2"] = new NodeDto { Id = "n2", IsVisible = true, CityId = "city-a" },
                    ["n3"] = new NodeDto { Id = "n3", IsVisible = false, IsCityCore = true }
                },
                units: new Dictionary<string, UnitDto>
                {
                    ["u1"] = new UnitDto { Id = "u1" },
                    ["u2"] = new UnitDto { Id = "u2" }
                },
                myResources: new ResourceDto
                {
                    Food = 8,
                    Wood = 5,
                    Ore = 3,
                    IndustryOutput = 2
                }));
            turnStore.Replace(new TurnState(
                planningStartEvents: new List<TurnEventDto>
                {
                    new TurnEventDto { Type = "unit_moved", UnitId = "u1", NodeId = "n2" }
                }));
            planningDraftStore.Replace(new PlanningDraftState(
                plannedResearchTargetTechnologyId: "irrigation",
                plannedNationalPolicyId: "logistics"));

            var state = viewModel.Current;
            Assert.That(state.TurnText, Is.EqualTo("2"));
            Assert.That(state.PhaseText, Is.EqualTo("回合规划"));
            Assert.That(state.TokensText, Is.EqualTo("3"));
            Assert.That(state.PlannedResearchText, Is.EqualTo("Irrigation"));
            Assert.That(state.PlannedPolicyText, Is.EqualTo("Logistics"));
            Assert.That(state.Metrics, Has.Count.EqualTo(4));
            Assert.That(state.Metrics[0].Value, Is.EqualTo("2"));
            Assert.That(state.Metrics[1].Value, Is.EqualTo("2"));
            Assert.That(state.Metrics[2].Value, Is.EqualTo("2"));
            Assert.That(state.Resources[0].Amount, Is.EqualTo(8));
            Assert.That(state.Events, Has.Count.EqualTo(1));
            Assert.That(state.Events[0].Title, Is.EqualTo("unit moved"));
        }
    }
}
