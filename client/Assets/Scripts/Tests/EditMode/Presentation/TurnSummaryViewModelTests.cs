using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.ViewModels;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class TurnSummaryViewModelTests
    {
        [Test]
        public void Current_ShouldProjectTurnGameAndEventState()
        {
            var turnStore = new TurnStore();
            var gameStateStore = new GameStateStore();
            using var viewModel = new TurnSummaryViewModel(turnStore, gameStateStore);

            gameStateStore.Replace(new GameStateStoreState(
                turn: 4,
                phase: GamePhases.Planning,
                tokensLeft: 3,
                nodes: new Dictionary<string, NodeDto>
                {
                    ["n1"] = new NodeDto { Id = "n1", IsVisible = true },
                    ["n2"] = new NodeDto { Id = "n2", IsVisible = false },
                    ["n3"] = new NodeDto { Id = "n3", IsVisible = true }
                },
                units: new Dictionary<string, UnitDto>
                {
                    ["u1"] = new UnitDto { Id = "u1" },
                    ["u2"] = new UnitDto { Id = "u2" }
                }));
            turnStore.Replace(new TurnState(
                planningStartEvents: new List<TurnEventDto>
                {
                    new TurnEventDto { Type = "unit_moved", UnitId = "u1", NodeId = "n3" }
                }));

            var state = viewModel.Current;
            Assert.That(state.Turn, Is.EqualTo(4));
            Assert.That(state.Phase, Is.EqualTo(GamePhases.Planning));
            Assert.That(state.TokensLeft, Is.EqualTo(3));
            Assert.That(state.VisibleNodeCount, Is.EqualTo(2));
            Assert.That(state.UnitCount, Is.EqualTo(2));
            Assert.That(state.Events, Has.Count.EqualTo(1));
            Assert.That(state.Events[0].Title, Is.EqualTo("unit moved"));
            Assert.That(state.Events[0].Detail, Is.EqualTo("u1 -> n3"));
        }

        [Test]
        public void Current_ShouldPreferTurnStorePhaseAndGameOver()
        {
            var turnStore = new TurnStore();
            var gameStateStore = new GameStateStore();
            using var viewModel = new TurnSummaryViewModel(turnStore, gameStateStore);

            gameStateStore.Replace(new GameStateStoreState(turn: 2, phase: GamePhases.Planning));
            turnStore.Replace(new TurnState(turn: 7, phase: GamePhases.Resolving, tokensLeft: 5, isGameOver: true));

            Assert.That(viewModel.Current.Turn, Is.EqualTo(7));
            Assert.That(viewModel.Current.Phase, Is.EqualTo(GamePhases.Resolving));
            Assert.That(viewModel.Current.TokensLeft, Is.EqualTo(5));
            Assert.That(viewModel.Current.IsGameOver, Is.True);
            Assert.That(viewModel.Current.StatusText, Is.EqualTo("Game over"));
        }
    }
}
