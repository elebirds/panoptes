using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    /// <summary>
    /// Recipe synthesis panel prefab.
    /// Sized and anchored like build panel, with slide toggle and recipe list content.
    /// </summary>
    public sealed class RecipeSynthesisPanel : MonoBehaviour
    {
        [Header("Auto Build")]
        [SerializeField] private bool autoBuildFallbackUi = false;
        [SerializeField] private bool startHidden = true;

        [Header("Refs")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private ScrollRect listScrollRect;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private RecipeSynthesisItemView recipeItemPrefab;
        [SerializeField] private Button closeButton;

        [Header("Style")]
        [SerializeField] private Vector2 panelSize = new Vector2(456f, 1080f);
        [SerializeField] private Color panelColor = new Color(0.06f, 0.09f, 0.16f, 0.95f);

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            if (autoBuildFallbackUi)
            {
                EnsureFallbackLayout();
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
                closeButton.onClick.AddListener(Hide);
            }

            if (startHidden)
            {
                Hide();
            }
        }

        public void Show()
        {
            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(false);
            }
        }

        private void EnsureFallbackLayout()
        {
            if (panelRoot == null)
            {
                return;
            }

            panelRoot.anchorMin = new Vector2(1f, 0.5f);
            panelRoot.anchorMax = new Vector2(1f, 0.5f);
            panelRoot.pivot = new Vector2(1f, 0.5f);
            panelRoot.anchoredPosition = Vector2.zero;
            panelRoot.sizeDelta = panelSize;

            var bg = EnsureImage("Background", panelRoot, panelColor);
            Stretch(bg.rectTransform, 0f, 0f, 0f, 0f);

            var title = EnsureText("Title", panelRoot, "配方合成", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -16f), new Vector2(-16f, -16f));
            title.alignment = TextAlignmentOptions.Left;
            title.fontSize = 36f;

            var viewport = EnsureRect("Viewport", panelRoot);
            Stretch(viewport, 16f, 16f, 88f, 16f);
            EnsureImage(viewport.gameObject, new Color(0f, 0f, 0f, 0.08f));
            var mask = viewport.GetComponent<Mask>();
            if (mask == null)
            {
                mask = viewport.gameObject.AddComponent<Mask>();
            }
            mask.showMaskGraphic = false;

            listContent = EnsureRect("Content", viewport);
            listContent.anchorMin = new Vector2(0f, 1f);
            listContent.anchorMax = new Vector2(1f, 1f);
            listContent.pivot = new Vector2(0.5f, 1f);
            listContent.anchoredPosition = Vector2.zero;
            listContent.sizeDelta = new Vector2(-8f, 0f);

            var vLayout = listContent.GetComponent<VerticalLayoutGroup>();
            if (vLayout == null)
            {
                vLayout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            vLayout.padding = new RectOffset(0, 0, 0, 0);
            vLayout.spacing = 10f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var fitter = listContent.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
            }
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            if (listScrollRect == null)
            {
                listScrollRect = panelRoot.GetComponent<ScrollRect>();
                if (listScrollRect == null)
                {
                    listScrollRect = panelRoot.gameObject.AddComponent<ScrollRect>();
                }
            }
            listScrollRect.viewport = viewport;
            listScrollRect.content = listContent;
            listScrollRect.horizontal = false;
            listScrollRect.vertical = true;
            listScrollRect.movementType = ScrollRect.MovementType.Clamped;

            closeButton = EnsureButton("BtnClose", panelRoot, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(96f, 40f), "关闭");
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);

            var toggleBtn = EnsureButton("BtnSlide", panelRoot, new Vector2(1f, 0.5f), new Vector2(64f, 0f), new Vector2(92f, 88f), "<<");
            var slide = toggleBtn.GetComponent<BuildPanelSlideToggle>();
            if (slide == null)
            {
                slide = toggleBtn.gameObject.AddComponent<BuildPanelSlideToggle>();
            }

            if (listContent.childCount == 0)
            {
                EnsurePlaceholderItem();
            }
        }

        private void EnsurePlaceholderItem()
        {
            RecipeSynthesisItemView view = null;
            if (recipeItemPrefab != null)
            {
                view = Instantiate(recipeItemPrefab, listContent, false);
            }
            else
            {
                var go = new GameObject("RecipeItem", typeof(RectTransform));
                var rect = go.GetComponent<RectTransform>();
                rect.SetParent(listContent, false);
                view = go.AddComponent<RecipeSynthesisItemView>();
            }

            if (view != null)
            {
                view.SetQuantity(0);
            }
        }

        private static RectTransform EnsureRect(string name, RectTransform parent)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image EnsureImage(string name, RectTransform parent, Color color)
        {
            var rect = EnsureRect(name, parent);
            return EnsureImage(rect.gameObject, color);
        }

        private static Image EnsureImage(GameObject go, Color color)
        {
            var image = go.GetComponent<Image>();
            if (image == null)
            {
                image = go.AddComponent<Image>();
            }
            image.color = color;
            return image;
        }

        private static TMP_Text EnsureText(string name, RectTransform parent, string value, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = EnsureRect(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }
            text.text = value;
            text.color = Color.white;
            text.fontSize = 24f;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Truncate;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
            return text;
        }

        private static Button EnsureButton(string name, RectTransform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size, string label)
        {
            var rect = EnsureRect(name, parent);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var image = EnsureImage(rect.gameObject, new Color(0.2f, 0.45f, 0.8f, 0.92f));
            var button = rect.GetComponent<Button>();
            if (button == null)
            {
                button = rect.gameObject.AddComponent<Button>();
            }
            button.targetGraphic = image;

            var text = EnsureText("Label", rect, label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 20f;
            return button;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
