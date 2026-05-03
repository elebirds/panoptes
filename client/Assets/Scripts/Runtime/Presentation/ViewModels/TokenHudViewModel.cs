using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class TokenHudViewModel : IViewModel<TokenHudState>, IDisposable
    {
        private readonly ActionLockStore _actionLockStore;
        private readonly GameStateStore _gameStateStore;
        private readonly BehaviorSubject<TokenHudState> _state;
        private readonly List<IDisposable> _subscriptions = new();
        private TokenHudState _current;
        private bool _disposed;

        public TokenHudViewModel(GameStateStore gameStateStore, ActionLockStore actionLockStore)
        {
            _gameStateStore = gameStateStore ?? throw new ArgumentNullException(nameof(gameStateStore));
            _actionLockStore = actionLockStore ?? throw new ArgumentNullException(nameof(actionLockStore));
            _current = Project();
            _state = new BehaviorSubject<TokenHudState>(_current);
            _subscriptions.Add(_gameStateStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_actionLockStore.State.Subscribe(this, static (_, self) => self.Publish()));
        }

        public TokenHudState Current => _current;
        public Observable<TokenHudState> State => _state;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (var i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i]?.Dispose();
            }

            _subscriptions.Clear();
            _state.Dispose();
        }

        private void Publish()
        {
            if (_disposed)
            {
                return;
            }

            _current = Project();
            _state.OnNext(_current);
        }

        private TokenHudState Project()
        {
            var game = _gameStateStore.Snapshot;
            var actionLock = _actionLockStore.Snapshot;
            var phase = game?.Phase ?? string.Empty;
            var isGameOver = game?.IsGameOver == true;
            var isActionLocked = actionLock?.IsLocked == true;
            return new TokenHudState(
                tokensLeft: game?.TokensLeft ?? 0,
                statusText: ResolveStatusText(phase, isGameOver, isActionLocked),
                phase: phase,
                isGameOver: isGameOver,
                isActionLocked: isActionLocked);
        }

        private static string ResolveStatusText(string phase, bool isGameOver, bool actionLocked)
        {
            if (isGameOver)
            {
                return "Game Over";
            }

            if (actionLocked)
            {
                return "Submitted";
            }

            return GamePhases.IsResolving(phase) ? "Resolving" : "Ready";
        }
    }
}
