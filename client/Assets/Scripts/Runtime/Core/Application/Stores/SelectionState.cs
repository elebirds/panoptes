namespace Panoptes.Core.Application.Stores
{
    public sealed class SelectionState
    {
        public SelectionState(
            string selectedNodeId = "",
            string selectedUnitId = "",
            string selectedCityId = "")
        {
            SelectedCityId = selectedCityId ?? string.Empty;
            SelectedNodeId = selectedNodeId ?? string.Empty;
            SelectedUnitId = selectedUnitId ?? string.Empty;
        }

        public string SelectedCityId { get; }
        public string SelectedNodeId { get; }
        public string SelectedUnitId { get; }

        internal SelectionState Clone()
        {
            return new SelectionState(SelectedNodeId, SelectedUnitId, SelectedCityId);
        }
    }
}
