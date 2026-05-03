using Panoptes.Core.Application.Stores;

namespace Panoptes.Core.Application.Services
{
    public sealed class SelectionService
    {
        private readonly SelectionStore _selectionStore;

        public SelectionService(SelectionStore selectionStore)
        {
            _selectionStore = selectionStore;
        }

        public bool IsDisposed => _selectionStore.IsDisposed;

        public void Clear()
        {
            _selectionStore.Clear();
        }

        public void SelectCity(string cityId)
        {
            _selectionStore.SelectCity(cityId);
        }

        public void SelectNode(string nodeId)
        {
            _selectionStore.SelectNode(nodeId);
        }

        public void SelectUnit(string unitId)
        {
            _selectionStore.SelectUnit(unitId);
        }
    }
}
