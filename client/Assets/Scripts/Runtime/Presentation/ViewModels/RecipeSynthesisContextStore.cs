using System;
using R3;

namespace Panoptes.Presentation.ViewModels
{
    public sealed class RecipeSynthesisContextState : IEquatable<RecipeSynthesisContextState>
    {
        public RecipeSynthesisContextState(
            string nodeId = "",
            string buildingTypeId = "",
            string ownerId = "")
        {
            NodeId = Normalize(nodeId);
            BuildingTypeId = Normalize(buildingTypeId);
            OwnerId = Normalize(ownerId);
        }

        public string NodeId { get; }
        public string BuildingTypeId { get; }
        public string OwnerId { get; }
        public bool HasContext => !string.IsNullOrWhiteSpace(NodeId) && !string.IsNullOrWhiteSpace(BuildingTypeId);

        public bool Equals(RecipeSynthesisContextState other)
        {
            return other != null &&
                   string.Equals(NodeId, other.NodeId, StringComparison.Ordinal) &&
                   string.Equals(BuildingTypeId, other.BuildingTypeId, StringComparison.Ordinal) &&
                   string.Equals(OwnerId, other.OwnerId, StringComparison.Ordinal);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    public sealed class RecipeSynthesisContextStore : IDisposable
    {
        private readonly BehaviorSubject<RecipeSynthesisContextState> _state;
        private RecipeSynthesisContextState _current;
        private bool _disposed;

        public RecipeSynthesisContextStore()
        {
            _current = new RecipeSynthesisContextState();
            _state = new BehaviorSubject<RecipeSynthesisContextState>(_current);
        }

        public RecipeSynthesisContextState Current => _current;
        public Observable<RecipeSynthesisContextState> State => _state;

        public void SetContext(string nodeId, string buildingTypeId, string ownerId)
        {
            Publish(new RecipeSynthesisContextState(nodeId, buildingTypeId, ownerId));
        }

        public void Clear()
        {
            Publish(new RecipeSynthesisContextState());
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

        private void Publish(RecipeSynthesisContextState state)
        {
            if (_disposed)
            {
                return;
            }

            var next = state ?? new RecipeSynthesisContextState();
            if (_current != null && _current.Equals(next))
            {
                return;
            }

            _current = next;
            _state.OnNext(_current);
        }
    }
}
