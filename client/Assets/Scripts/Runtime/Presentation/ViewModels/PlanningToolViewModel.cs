using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class PlanningToolViewModel : IViewModel<PlanningToolViewState>, IDisposable
    {
        private readonly PlanningToolStore _planningToolStore;
        private readonly SelectionStore _selectionStore;
        private readonly BehaviorSubject<PlanningToolViewState> _state;
        private readonly List<IDisposable> _subscriptions = new();
        private bool _disposed;
        private PlanningToolViewState _current;

        public PlanningToolViewModel(
            PlanningToolStore planningToolStore,
            SelectionStore selectionStore)
        {
            _planningToolStore = planningToolStore ?? throw new ArgumentNullException(nameof(planningToolStore));
            _selectionStore = selectionStore ?? throw new ArgumentNullException(nameof(selectionStore));
            _current = Project();
            _state = new BehaviorSubject<PlanningToolViewState>(_current);
            _subscriptions.Add(_planningToolStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_selectionStore.State.Subscribe(this, static (_, self) => self.Publish()));
        }

        public PlanningToolViewState Current => _current;
        public Observable<PlanningToolViewState> State => _state;

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

        private PlanningToolViewState Project()
        {
            var tool = _planningToolStore.Snapshot;
            var selection = _selectionStore.Snapshot;
            return new PlanningToolViewState(
                tool.Mode,
                tool.BuildRule,
                tool.BuildTypeId,
                tool.BuildCityId,
                selection.SelectedUnitId,
                tool.MovePreviewNodeId,
                tool.BuildPreviewNodeId);
        }
    }
}
