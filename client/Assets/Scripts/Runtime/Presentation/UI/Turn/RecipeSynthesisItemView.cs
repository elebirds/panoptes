using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    /// <summary>
    /// Single recipe row:
    /// right-side two inputs -> left-side one output, with amounts/turns and quantity +/- controls.
    /// </summary>
    public sealed class RecipeSynthesisItemView : MonoBehaviour
    {
        [Header("Auto Build")]
        [SerializeField] private bool autoBuildFallbackUi = false;

        [Header("Refs")]
        [SerializeField] private RectTransform root;
        [SerializeField] private Image outputIcon;
        [SerializeField] private Image inputIconA;
        [SerializeField] private Image inputIconB;
        [SerializeField] private TMP_Text outputCountText;
        [SerializeField] private TMP_Text inputCountAText;
        [SerializeField] private TMP_Text inputCountBText;
        [SerializeField] private TMP_Text arrowText;
        [SerializeField] private TMP_Text produceAmountText;
        [SerializeField] private TMP_Text turnCostText;
        [SerializeField] private TMP_Text quantityValueText;
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;

        [Header("Values")]
        [SerializeField] private int quantity = 0;
        [SerializeField] private int minQuantity = 0;
        [SerializeField] private int maxQuantity = 99;

        private void Awake()
        {
            if (root == null)
            {
                root = transform as RectTransform;
            }

            if (autoBuildFallbackUi)
            {
                EnsureFallbackLayout();
            }

            BindButtons();
            RefreshQuantityText();
        }

        public void SetQuantity(int value)
        {
            quantity = Mathf.Clamp(value, minQuantity, maxQuantity);
            RefreshQuantityText();
        }

        private void BindButtons()
        {
            if (minusButton != null)
            {
                minusButton.onClick.RemoveListener(DecQuantity);
                minusButton.onClick.AddListener(DecQuantity);
            }

            if (plusButton != null)
            {
                plusButton.onClick.RemoveListener(IncQuantity);
                plusButton.onClick.AddListener(IncQuantity);
            }
        }

        private void IncQuantity()
        {
            SetQuantity(quantity + 1);
        }

        private void DecQuantity()
        {
            SetQuantity(quantity - 1);
        }

        private void RefreshQuantityText()
        {
            if (quantityValueText != null)
            {
                quantityValueText.text = quantity.ToString();
            }
        }

        private void EnsureFallbackLayout()
        {
            if (root == null)
            {
                return;
            }

            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(0f, 178f);

            var bg = EnsureImage("Background", root, new Color(0.1f, 0.14f, 0.22f, 0.95f));
            Stretch(bg.rectTransform, 0f, 0f, 0f, 0f);

            var outputRect = EnsureRect("OutputIcon", root);
            outputRect.anchorMin = new Vector2(0f, 1f);
            outputRect.anchorMax = new Vector2(0f, 1f);
            outputRect.pivot = new Vector2(0f, 1f);
            outputRect.anchoredPosition = new Vector2(18f, -18f);
            outputRect.sizeDelta = new Vector2(78f, 78f);
            outputIcon = EnsureImage(outputRect.gameObject, new Color(0.75f, 0.75f, 0.75f, 1f));

            outputCountText = EnsureText("OutputCount", root, new Vector2(18f, -106f), new Vector2(78f, 28f), "x0");

            var inputARect = EnsureRect("InputIconA", root);
            inputARect.anchorMin = new Vector2(1f, 1f);
            inputARect.anchorMax = new Vector2(1f, 1f);
            inputARect.pivot = new Vector2(1f, 1f);
            inputARect.anchoredPosition = new Vector2(-84f, -20f);
            inputARect.sizeDelta = new Vector2(46f, 46f);
            inputIconA = EnsureImage(inputARect.gameObject, new Color(0.72f, 0.72f, 0.72f, 1f));

            var inputBRect = EnsureRect("InputIconB", root);
            inputBRect.anchorMin = new Vector2(1f, 1f);
            inputBRect.anchorMax = new Vector2(1f, 1f);
            inputBRect.pivot = new Vector2(1f, 1f);
            inputBRect.anchoredPosition = new Vector2(-28f, -20f);
            inputBRect.sizeDelta = new Vector2(46f, 46f);
            inputIconB = EnsureImage(inputBRect.gameObject, new Color(0.72f, 0.72f, 0.72f, 1f));

            inputCountAText = EnsureText("InputCountA", root, new Vector2(-84f, -70f), new Vector2(46f, 22f), "x0", true);
            inputCountBText = EnsureText("InputCountB", root, new Vector2(-28f, -70f), new Vector2(46f, 22f), "x0", true);

            produceAmountText = EnsureText("ProduceAmount", root, new Vector2(0f, -16f), new Vector2(120f, 26f), "+0");
            produceAmountText.alignment = TextAlignmentOptions.Center;

            arrowText = EnsureText("Arrow", root, new Vector2(0f, -50f), new Vector2(140f, 48f), "<=");
            arrowText.fontSize = 36f;
            arrowText.alignment = TextAlignmentOptions.Center;

            turnCostText = EnsureText("TurnCost", root, new Vector2(0f, -88f), new Vector2(120f, 24f), "1t");
            turnCostText.alignment = TextAlignmentOptions.Center;

            var qtyLabel = EnsureText("QuantityLabel", root, new Vector2(0f, -136f), new Vector2(110f, 28f), "Qty");
            qtyLabel.alignment = TextAlignmentOptions.Center;

            quantityValueText = EnsureText("QuantityValue", root, new Vector2(62f, -136f), new Vector2(52f, 28f), "0");
            quantityValueText.alignment = TextAlignmentOptions.Left;

            minusButton = EnsureButton("BtnMinus", root, new Vector2(1f, 0f), new Vector2(-110f, 14f), new Vector2(44f, 32f), "-", out _);
            plusButton = EnsureButton("BtnPlus", root, new Vector2(1f, 0f), new Vector2(-58f, 14f), new Vector2(44f, 32f), "+", out _);
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

        private static TMP_Text EnsureText(string name, RectTransform parent, Vector2 anchoredPos, Vector2 size, string value, bool rightAnchor = false)
        {
            var rect = EnsureRect(name, parent);
            rect.anchorMin = rightAnchor ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rect.anchorMax = rightAnchor ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rect.pivot = rightAnchor ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = rect.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            text.text = value;
            text.fontSize = 20f;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Truncate;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
            return text;
        }

        private static Button EnsureButton(string name, RectTransform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size, string label, out TMP_Text labelText)
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

            labelText = EnsureText("Label", rect, new Vector2(0f, 0f), Vector2.zero, label);
            labelText.rectTransform.anchorMin = Vector2.zero;
            labelText.rectTransform.anchorMax = Vector2.one;
            labelText.rectTransform.offsetMin = Vector2.zero;
            labelText.rectTransform.offsetMax = Vector2.zero;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 24f;
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
