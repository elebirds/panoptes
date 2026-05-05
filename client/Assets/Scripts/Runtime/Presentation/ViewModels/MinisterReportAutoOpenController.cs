using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class MinisterReportAutoOpenController : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new();
        private readonly ManagementPanelVisibilityStore _visibilityStore;
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly TurnStore _turnStore;
        private bool _disposed;
        private int _lastOpenedTurn = int.MinValue;

        public MinisterReportAutoOpenController(
            PlanningDraftStore planningDraftStore,
            TurnStore turnStore,
            ManagementPanelVisibilityStore visibilityStore)
        {
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            _turnStore = turnStore ?? throw new ArgumentNullException(nameof(turnStore));
            _visibilityStore = visibilityStore ?? throw new ArgumentNullException(nameof(visibilityStore));

            _subscriptions.Add(_planningDraftStore.State.Subscribe(this, static (_, self) => self.Evaluate()));
            _subscriptions.Add(_turnStore.State.Subscribe(this, static (_, self) => self.Evaluate()));
            Evaluate();
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
        }

        private void Evaluate()
        {
            if (_disposed)
            {
                return;
            }

            var turn = _turnStore.Snapshot;
            if (turn == null || !turn.IsInteractive)
            {
                return;
            }

            if (_lastOpenedTurn == turn.Turn || !HasInteractiveDrafts(_planningDraftStore.Snapshot?.MinisterDrafts))
            {
                return;
            }

            _lastOpenedTurn = turn.Turn;
            _visibilityStore.Show(ManagementPanelId.MinisterReport);
        }

        private static bool HasInteractiveDrafts(IReadOnlyList<MinisterDraftDto> drafts)
        {
            if (drafts == null)
            {
                return false;
            }

            for (var i = 0; i < drafts.Count; i++)
            {
                if (drafts[i]?.IsInteractive == true)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
