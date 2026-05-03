using System;
using Panoptes.Core.Application.Intents;
using R3;

namespace Panoptes.Core.Application.Stores
{
    public sealed class ActionLockStore : IReadOnlyStore<ActionLockStoreState>, IDisposable
    {
        private readonly BehaviorSubject<ActionLockStoreState> _state;
        private ActionLockStoreState _current;
        private bool _disposed;

        public ActionLockStore()
        {
            _current = new ActionLockStoreState(ActionLock.IsLocked);
            _state = new BehaviorSubject<ActionLockStoreState>(_current);
            ActionLock.OnChanged += OnActionLockChanged;
        }

        public ActionLockStoreState Snapshot => _current.Clone();
        public Observable<ActionLockStoreState> State => _state;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ActionLock.OnChanged -= OnActionLockChanged;
            _state.Dispose();
        }

        private void OnActionLockChanged(bool locked)
        {
            if (_disposed || _current.IsLocked == locked)
            {
                return;
            }

            _current = new ActionLockStoreState(locked);
            _state.OnNext(_current.Clone());
        }
    }

    public sealed class ActionLockStoreState
    {
        public ActionLockStoreState(bool isLocked = false)
        {
            IsLocked = isLocked;
        }

        public bool IsLocked { get; }

        internal ActionLockStoreState Clone()
        {
            return new ActionLockStoreState(IsLocked);
        }
    }
}
