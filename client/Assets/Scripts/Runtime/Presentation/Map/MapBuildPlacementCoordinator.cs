using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public interface IMapBuildPlacementCoordinatorContext
    {
        bool HasMapRenderer { get; }
        bool ShouldLogInvalidBuildClick { get; }

        bool IsPointerOverUI();
        bool TryRaycastBuildNode(out NodeView node);
        bool IsCurrentHoverNode(NodeView node);
        bool ShouldRequestBuildPreview(string nodeId);
        Color ResolveBuildPreviewColor(string nodeId);

        void ExitBuildMode();
        void ClearBuildHoverState();
        void MoveBuildHoverTo(NodeView node);
        void RenderBuildHover(NodeView node, Color highlightColor);
        void RenderBuildGhost(Color highlightColor);
        void RequestBuildPreview(string nodeId);
        void LogInvalidBuildTarget();
        void TryCommitBuildPlacement(NodeView node);
    }

    public sealed class MapBuildPlacementCoordinator
    {
        public void Tick(
            IMapBuildPlacementCoordinatorContext context,
            bool leftMouseDown,
            bool rightMouseDown)
        {
            if (context == null || !context.HasMapRenderer)
            {
                return;
            }

            if (rightMouseDown)
            {
                context.ExitBuildMode();
                return;
            }

            if (context.IsPointerOverUI())
            {
                context.ClearBuildHoverState();
                return;
            }

            if (!context.TryRaycastBuildNode(out var node) || node == null)
            {
                context.ClearBuildHoverState();

                if (leftMouseDown && context.ShouldLogInvalidBuildClick)
                {
                    context.LogInvalidBuildTarget();
                }

                return;
            }

            if (!context.IsCurrentHoverNode(node))
            {
                context.MoveBuildHoverTo(node);
                context.RequestBuildPreview(node.NodeId);
            }
            else if (context.ShouldRequestBuildPreview(node.NodeId))
            {
                context.RequestBuildPreview(node.NodeId);
            }

            var highlightColor = context.ResolveBuildPreviewColor(node.NodeId);
            context.RenderBuildHover(node, highlightColor);
            context.RenderBuildGhost(highlightColor);

            if (leftMouseDown)
            {
                context.TryCommitBuildPlacement(node);
            }
        }
    }
}
