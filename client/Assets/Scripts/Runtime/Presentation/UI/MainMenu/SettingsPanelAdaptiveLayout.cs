using UnityEngine;

namespace Panoptes.Presentation.UI.MainMenu
{
    [ExecuteAlways]
    public sealed class SettingsPanelAdaptiveLayout : MonoBehaviour
    {
        private static readonly Vector2 PanelMaxSize = new(920f, 680f);
        private static readonly Vector2 PanelPadding = new(48f, 48f);

        private void Awake()
        {
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            Apply();
        }

        public void Apply()
        {
            if (!TryGetComponent<RectTransform>(out var root))
            {
                return;
            }

            Stretch(root);

            var contentRoot = transform.Find("Canvas") as RectTransform;
            if (contentRoot == null)
            {
                contentRoot = root;
            }
            else
            {
                Stretch(contentRoot);
            }

            var size = root.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                size = new Vector2(1280f, 720f);
            }

            var panelSize = new Vector2(
                Mathf.Max(520f, Mathf.Min(PanelMaxSize.x, size.x - PanelPadding.x * 2f)),
                Mathf.Max(420f, Mathf.Min(PanelMaxSize.y, size.y - PanelPadding.y * 2f)));

            var background = contentRoot.Find("Background") as RectTransform;
            if (background != null)
            {
                Center(background, panelSize, Vector2.zero);
            }

            CenterChild(contentRoot, "Video", new Vector2(panelSize.x - 96f, 260f), new Vector2(0f, 64f));
            CenterChild(contentRoot, "Music", new Vector2(panelSize.x - 96f, 220f), new Vector2(0f, -190f));
        }

        private static void CenterChild(RectTransform parent, string childName, Vector2 size, Vector2 position)
        {
            var child = parent.Find(childName) as RectTransform;
            if (child != null)
            {
                Center(child, size, position);
            }
        }

        private static void Center(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }
}
