using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class TurnSummaryViewModel : IViewModel<TurnSummaryState>, IDisposable
    {
        private const int MaxEvents = 6;

        private readonly GameStateStore _gameStateStore;
        private readonly BehaviorSubject<TurnSummaryState> _state;
        private readonly List<IDisposable> _subscriptions = new();
        private readonly TurnStore _turnStore;
        private bool _disposed;
        private TurnSummaryState _current;

        public TurnSummaryViewModel(TurnStore turnStore, GameStateStore gameStateStore)
        {
            _turnStore = turnStore ?? throw new ArgumentNullException(nameof(turnStore));
            _gameStateStore = gameStateStore ?? throw new ArgumentNullException(nameof(gameStateStore));
            _current = Project();
            _state = new BehaviorSubject<TurnSummaryState>(_current);
            _subscriptions.Add(_turnStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_gameStateStore.State.Subscribe(this, static (_, self) => self.Publish()));
        }

        public TurnSummaryState Current => _current;
        public Observable<TurnSummaryState> State => _state;

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

        private TurnSummaryState Project()
        {
            var turn = _turnStore.Snapshot;
            var game = _gameStateStore.Snapshot;
            var phase = !string.IsNullOrWhiteSpace(turn.Phase) ? turn.Phase : game.Phase;
            var isGameOver = turn.IsGameOver || game.IsGameOver;
            var tokensLeft = turn.TokensLeft > 0 ? turn.TokensLeft : game.TokensLeft;
            var turnNumber = turn.Turn > 0 ? turn.Turn : game.Turn;
            return new TurnSummaryState(
                turnNumber,
                phase,
                GamePhases.ToDisplayText(phase),
                tokensLeft,
                isGameOver,
                CountVisibleNodes(game),
                game.Units != null ? game.Units.Count : 0,
                BuildEvents(turn.PlanningStartEvents));
        }

        private static int CountVisibleNodes(GameStateStoreState game)
        {
            if (game?.Nodes == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var pair in game.Nodes)
            {
                if (pair.Value != null && pair.Value.IsVisible)
                {
                    count++;
                }
            }

            return count;
        }

        private static IReadOnlyList<TurnSummaryEventState> BuildEvents(IReadOnlyList<TurnEventDto> events)
        {
            var result = new List<TurnSummaryEventState>();
            if (events == null)
            {
                return result;
            }

            for (var i = 0; i < events.Count && result.Count < MaxEvents; i++)
            {
                var evt = events[i];
                if (evt == null)
                {
                    continue;
                }

                result.Add(new TurnSummaryEventState(BuildEventTitle(evt), BuildEventDetail(evt)));
            }

            return result;
        }

        private static string BuildEventTitle(TurnEventDto evt)
        {
            if (!string.IsNullOrWhiteSpace(evt.Type))
            {
                return NormalizeTitle(evt.Type);
            }

            if (!string.IsNullOrWhiteSpace(evt.Section))
            {
                return NormalizeTitle(evt.Section);
            }

            return "Event";
        }

        private static string BuildEventDetail(TurnEventDto evt)
        {
            if (!string.IsNullOrWhiteSpace(evt.ReasonMessage))
            {
                return evt.ReasonMessage.Trim();
            }

            if (!string.IsNullOrWhiteSpace(evt.BlockedReasonMessage))
            {
                return evt.BlockedReasonMessage.Trim();
            }

            if (!string.IsNullOrWhiteSpace(evt.UnitId) && !string.IsNullOrWhiteSpace(evt.NodeId))
            {
                return $"{evt.UnitId.Trim()} -> {evt.NodeId.Trim()}";
            }

            if (!string.IsNullOrWhiteSpace(evt.UnitId) && !string.IsNullOrWhiteSpace(evt.TargetUnitId))
            {
                return $"{evt.UnitId.Trim()} -> {evt.TargetUnitId.Trim()}";
            }

            if (!string.IsNullOrWhiteSpace(evt.NodeId))
            {
                return evt.NodeId.Trim();
            }

            return string.Empty;
        }

        private static string NormalizeTitle(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "Event"
                : value.Trim().Replace('_', ' ');
        }
    }
}
