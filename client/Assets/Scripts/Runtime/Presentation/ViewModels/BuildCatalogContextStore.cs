using System;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class BuildCatalogContextState
    {
        public BuildCatalogContextState(string cityCoreNodeId = "")
        {
            CityCoreNodeId = Normalize(cityCoreNodeId);
        }

        public string CityCoreNodeId { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    public sealed class BuildCatalogContextStore : IDisposable
    {
        private readonly BehaviorSubject<BuildCatalogContextState> _state;
        private BuildCatalogContextState _current;
        private bool _disposed;

        public BuildCatalogContextStore()
        {
            _current = new BuildCatalogContextState();
            _state = new BehaviorSubject<BuildCatalogContextState>(_current);
        }

        public BuildCatalogContextState Current => _current;
        public Observable<BuildCatalogContextState> State => _state;

        public void SetCityCoreNode(string nodeId)
        {
            Publish(new BuildCatalogContextState(nodeId));
        }

        public void Clear()
        {
            Publish(new BuildCatalogContextState());
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

        private void Publish(BuildCatalogContextState state)
        {
            if (_disposed)
            {
                return;
            }

            _current = state ?? new BuildCatalogContextState();
            _state.OnNext(_current);
        }
    }
}
