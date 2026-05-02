using System.Collections.Generic;
using UnityEngine;

namespace Panoptes.Presentation.Map
{
    public sealed class MapAttackRangePresenter
    {
        public void Refresh(
            IReadOnlyDictionary<string, NodeView> tileViews,
            ISet<string> highlightedNodeIds,
            Vector2Int originGrid,
            int attackRange,
            Color highlightColor)
        {
            if (tileViews == null || highlightedNodeIds == null || attackRange <= 0)
            {
                return;
            }

            foreach (var pair in tileViews)
            {
                var nodeId = pair.Key;
                var nodeView = pair.Value;
                if (nodeView == null || string.IsNullOrWhiteSpace(nodeId))
                {
                    continue;
                }

                if (HexGrid.AxialDistance(originGrid, nodeView.GridPos) > attackRange)
                {
                    continue;
                }

                nodeView.SetHighlight(true, highlightColor);
                highlightedNodeIds.Add(nodeId);
            }
        }

        public bool IsGridInRange(Vector2Int originGrid, Vector2Int targetGrid, int attackRange)
        {
            return attackRange > 0 && HexGrid.AxialDistance(originGrid, targetGrid) <= attackRange;
        }
    }
}
