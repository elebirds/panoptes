using Panoptes.Core.Domain;

namespace Panoptes.Presentation.Map
{
    public enum MapMovePreviewRenderKind
    {
        None = 0,
        Invalid = 1,
        Valid = 2
    }

    public readonly struct MapMovePreviewRenderPlan
    {
        public MapMovePreviewRenderPlan(
            MapMovePreviewRenderKind kind,
            string targetNodeId,
            PathPreviewDto preview)
        {
            Kind = kind;
            TargetNodeId = targetNodeId ?? string.Empty;
            Preview = preview;
        }

        public MapMovePreviewRenderKind Kind { get; }
        public string TargetNodeId { get; }
        public PathPreviewDto Preview { get; }
    }

    public static class MapMovePreviewRenderPlanner
    {
        public static MapMovePreviewRenderPlan Create(
            PathPreviewDto preview,
            string hoverNodeId,
            string selectedUnitId,
            bool hasMapRenderer)
        {
            if (!hasMapRenderer)
            {
                return default;
            }

            if (preview == null || !preview.Valid)
            {
                if (preview != null &&
                    !string.IsNullOrEmpty(hoverNodeId) &&
                    !string.IsNullOrEmpty(selectedUnitId) &&
                    string.Equals(preview.UnitId, selectedUnitId, System.StringComparison.Ordinal) &&
                    string.Equals(preview.TargetNodeId, hoverNodeId, System.StringComparison.Ordinal))
                {
                    return new MapMovePreviewRenderPlan(
                        MapMovePreviewRenderKind.Invalid,
                        hoverNodeId,
                        preview);
                }

                return default;
            }

            if (string.IsNullOrEmpty(selectedUnitId) ||
                !string.Equals(preview.UnitId, selectedUnitId, System.StringComparison.Ordinal) ||
                !string.Equals(preview.TargetNodeId, hoverNodeId, System.StringComparison.Ordinal))
            {
                return default;
            }

            return new MapMovePreviewRenderPlan(
                MapMovePreviewRenderKind.Valid,
                preview.TargetNodeId,
                preview);
        }
    }
}
