using UnityEngine;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class BuildPanelScrollState
    {
        private Vector2 _baseAnchoredPosition;
        private float _scrollOffset;
        private bool _initialized;

        public void Reset(RectTransform root, bool forceReset)
        {
            if (root == null)
            {
                return;
            }

            if (forceReset || !_initialized)
            {
                _baseAnchoredPosition = new Vector2(root.anchoredPosition.x, 0f);
                _scrollOffset = 0f;
                _initialized = true;
                root.anchoredPosition = _baseAnchoredPosition;
                return;
            }

            root.anchoredPosition = _baseAnchoredPosition + new Vector2(0f, _scrollOffset);
        }
    }
}
