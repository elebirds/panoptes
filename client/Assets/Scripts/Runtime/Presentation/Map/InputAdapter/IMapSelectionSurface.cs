/*************************************************
 * Project: Panoptes
 * File: IMapSelectionSurface.cs
 * Author: Panoptes Team
 * Date: 2026-04-29
 * Description: Map selection surface abstraction for pointer-to-map hit resolution.
 *************************************************/

using Panoptes.Core.Domain;

namespace Panoptes.Presentation.Map.InputAdapter
{
    /// <summary>
    /// Resolves map visual targets from pointer hits without owning planning command flow.
    /// </summary>
    public interface IMapSelectionSurface
    {
        bool TryRaycastNode(out NodeView nodeView);
        bool TryRaycastUnit(out UnitView unitView);
        bool TryGetClickedNodeContext(out NodeView nodeView, out NodeDto nodeState);
    }
}
