using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.ViewModels;
using R3;

namespace Panoptes.Tests.EditMode.Presentation
{
    public sealed class TokenHudViewModelTests
    {
        [SetUp]
        public void SetUp()
        {
            ActionLock.Release();
        }

        [TearDown]
        public void TearDown()
        {
            ActionLock.Release();
        }

        [Test]
        public void Current_ShouldProjectTokensAndResolvingStateFromGameStateStore()
        {
            var store = new GameStateStore();
            using var actionLockStore = new ActionLockStore();
            using var viewModel = new TokenHudViewModel(store, actionLockStore);

            store.Replace(new GameStateStoreState(
                phase: GamePhases.Resolving,
                tokensLeft: 2));

            Assert.That(viewModel.Current.TokensLeft, Is.EqualTo(2));
            Assert.That(viewModel.Current.TokenText, Is.EqualTo("Tokens 2"));
            Assert.That(viewModel.Current.StatusText, Is.EqualTo("Resolving"));
            Assert.That(viewModel.Current.Phase, Is.EqualTo(GamePhases.Resolving));
        }

        [Test]
        public void Current_ShouldPrioritizeGameOverOverActionLock()
        {
            var store = new GameStateStore();
            using var actionLockStore = new ActionLockStore();
            using var viewModel = new TokenHudViewModel(store, actionLockStore);

            ActionLock.Acquire();
            store.Replace(new GameStateStoreState(
                phase: GamePhases.Planning,
                isGameOver: true,
                tokensLeft: 1));

            Assert.That(viewModel.Current.IsGameOver, Is.True);
            Assert.That(viewModel.Current.IsActionLocked, Is.True);
            Assert.That(viewModel.Current.StatusText, Is.EqualTo("Game Over"));
        }

        [Test]
        public void State_ShouldPublishWhenActionLockChanges()
        {
            var store = new GameStateStore();
            using var actionLockStore = new ActionLockStore();
            using var viewModel = new TokenHudViewModel(store, actionLockStore);
            var observed = new List<string>();
            using var subscription = viewModel.State.Subscribe(observed, static (state, target) => target.Add(state.StatusText));

            ActionLock.Acquire();

            Assert.That(viewModel.Current.IsActionLocked, Is.True);
            Assert.That(viewModel.Current.StatusText, Is.EqualTo("Submitted"));
            Assert.That(observed, Does.Contain("Submitted"));
        }
    }
}
