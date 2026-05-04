using System;
using System.Collections.Generic;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public abstract class ManagementPanelViewModelBase : IViewModel<ManagementPanelState>, IDisposable
    {
        private readonly BehaviorSubject<ManagementPanelState> _state;
        private readonly List<IDisposable> _subscriptions = new();
        private bool _disposed;
        private ManagementPanelState _current;

        protected ManagementPanelViewModelBase()
        {
            _current = new ManagementPanelState();
            _state = new BehaviorSubject<ManagementPanelState>(_current);
        }

        public ManagementPanelState Current => _current;
        public Observable<ManagementPanelState> State => _state;

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

        protected void AddSubscription(IDisposable subscription)
        {
            if (subscription != null)
            {
                _subscriptions.Add(subscription);
            }
        }

        protected void AddCleanup(Action cleanup)
        {
            if (cleanup != null)
            {
                _subscriptions.Add(new CleanupSubscription(cleanup));
            }
        }

        protected void Publish()
        {
            if (_disposed)
            {
                return;
            }

            _current = Project();
            _state.OnNext(_current);
        }

        protected abstract ManagementPanelState Project();

        protected static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private sealed class CleanupSubscription : IDisposable
        {
            private Action _cleanup;

            public CleanupSubscription(Action cleanup)
            {
                _cleanup = cleanup;
            }

            public void Dispose()
            {
                var cleanup = _cleanup;
                _cleanup = null;
                cleanup?.Invoke();
            }
        }
    }
}
