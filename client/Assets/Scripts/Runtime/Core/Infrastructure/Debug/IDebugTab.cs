#if UNITY_EDITOR || DEVELOPMENT_BUILD || PANOPTES_DEBUG_PANEL
using UnityEngine;

namespace Panoptes.DebugTools
{
    public interface IDebugTab
    {
        string Id { get; }
        string Title { get; }
        bool IsAvailable(DebugPanelContext context, out string reason);
        void Draw(DebugPanelContext context, Rect rect);
    }
}
#endif
