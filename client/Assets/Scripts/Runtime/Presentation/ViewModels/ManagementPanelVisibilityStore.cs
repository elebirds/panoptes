using System;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public enum ManagementPanelId
    {
        None = 0,
        TechTree = 1,
        BuildCatalog = 2,
        RecipeSynthesis = 3,
        NationalOverview = 4,
        TurnSummary = 5,
        PolicyFocus = 6,
        NationalLedger = 7,
        MinisterReport = 8,
        Institutions = 9
    }

    public sealed class ManagementPanelVisibilityState
    {
        public ManagementPanelVisibilityState(ManagementPanelId activePanel = ManagementPanelId.None, bool isVisible = false)
        {
            ActivePanel = isVisible ? activePanel : ManagementPanelId.None;
            IsVisible = isVisible && activePanel != ManagementPanelId.None;
        }

        public ManagementPanelId ActivePanel { get; }
        public bool IsVisible { get; }

        public bool IsPanelVisible(ManagementPanelId panel)
        {
            return IsVisible && ActivePanel == panel;
        }
    }

    public sealed class ManagementPanelVisibilityStore : IDisposable
    {
        private readonly BehaviorSubject<ManagementPanelVisibilityState> _state;
        private bool _disposed;
        private ManagementPanelVisibilityState _current;

        public ManagementPanelVisibilityStore()
        {
            _current = new ManagementPanelVisibilityState();
            _state = new BehaviorSubject<ManagementPanelVisibilityState>(_current);
        }

        public ManagementPanelVisibilityState Current => _current;
        public Observable<ManagementPanelVisibilityState> State => _state;

        public void Show(ManagementPanelId panel)
        {
            if (panel == ManagementPanelId.None)
            {
                Hide();
                return;
            }

            Publish(new ManagementPanelVisibilityState(panel, true));
        }

        public void Hide()
        {
            Publish(new ManagementPanelVisibilityState());
        }

        public void Toggle(ManagementPanelId panel)
        {
            if (_current.IsPanelVisible(panel))
            {
                Hide();
                return;
            }

            Show(panel);
        }

        public bool IsVisible(ManagementPanelId panel)
        {
            return _current.IsPanelVisible(panel);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _state.Dispose();
        }

        private void Publish(ManagementPanelVisibilityState state)
        {
            if (_disposed)
            {
                return;
            }

            _current = state ?? new ManagementPanelVisibilityState();
            _state.OnNext(_current);
        }
    }
}
