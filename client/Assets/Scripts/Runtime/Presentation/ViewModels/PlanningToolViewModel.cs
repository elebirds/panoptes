using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class PlanningToolViewModel : IViewModel<PlanningToolViewState>, IDisposable
    {
        private readonly PlanningToolService _planningToolService;
        private readonly PlanningToolStore _planningToolStore;
        private readonly SelectionService _selectionService;
        private readonly SelectionStore _selectionStore;
        private readonly BehaviorSubject<PlanningToolViewState> _state;
        private readonly List<IDisposable> _subscriptions = new();
        private bool _disposed;
        private PlanningToolViewState _current;

        public PlanningToolViewModel(
            PlanningToolStore planningToolStore,
            SelectionStore selectionStore,
            PlanningToolService planningToolService,
            SelectionService selectionService)
        {
            _planningToolStore = planningToolStore ?? throw new ArgumentNullException(nameof(planningToolStore));
            _selectionStore = selectionStore ?? throw new ArgumentNullException(nameof(selectionStore));
            _planningToolService = planningToolService ?? throw new ArgumentNullException(nameof(planningToolService));
            _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
            _current = Project();
            _state = new BehaviorSubject<PlanningToolViewState>(_current);
            _subscriptions.Add(_planningToolStore.State.Subscribe(this, static (_, self) => self.Publish()));
            _subscriptions.Add(_selectionStore.State.Subscribe(this, static (_, self) => self.Publish()));
        }

        public PlanningToolViewState Current => _current;
        public Observable<PlanningToolViewState> State => _state;

        public void ClearTool()
        {
            _planningToolService.ClearTool();
        }

        public void EnterBuild(
            string buildTypeId,
            string buildCityId,
            PlanningBuildPlacementRule buildRule)
        {
            _planningToolService.EnterBuild(buildTypeId, buildCityId, buildRule);
        }

        public void BeginMove()
        {
            _planningToolService.BeginMove();
        }

        public void BeginAttack()
        {
            _planningToolService.BeginAttack();
        }

        public void BeginCharge()
        {
            _planningToolService.BeginCharge();
        }

        public void SelectUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                _selectionService.Clear();
                return;
            }

            _selectionService.SelectUnit(unitId);
        }

        public void ClearSelection()
        {
            _selectionService.Clear();
        }

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
