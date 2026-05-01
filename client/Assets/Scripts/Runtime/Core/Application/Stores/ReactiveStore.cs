using System;
using R3;

namespace Panoptes.Core.Application.Stores
{
    public abstract class ReactiveStore<TState> : IReadOnlyStore<TState>, IDisposable
    {
        private readonly BehaviorSubject<TState> _state;
        private TState _current;
        private bool _disposed;

        protected ReactiveStore(TState initialState)
        {
            _current = CloneState(initialState);
            _state = new BehaviorSubject<TState>(CloneState(_current));
        }

        public TState Snapshot => CloneState(_current);

        public Observable<TState> State => _state;

        protected void Publish(TState nextState)
        {
            ThrowIfDisposed();
            _current = CloneState(nextState);
            _state.OnNext(CloneState(_current));
        }

        protected abstract TState CloneState(TState state);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _state.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
