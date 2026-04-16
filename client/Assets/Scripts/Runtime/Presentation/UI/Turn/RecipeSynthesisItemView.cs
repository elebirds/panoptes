using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class RecipeSynthesisItemView : MonoBehaviour
    {
        public readonly struct IngredientViewData
        {
            public readonly string Key;
            public readonly int Amount;
            public readonly Sprite Icon;

            public IngredientViewData(string key, int amount, Sprite icon = null)
            {
                Key = key ?? string.Empty;
                Amount = Mathf.Max(0, amount);
                Icon = icon;
            }
        }

        private sealed class SlotView
        {
            public RectTransform Root;
            public Image Icon;
            public TMP_Text Count;
        }

        [Header("Roots")]
        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform outputSlotsRoot;
        [SerializeField] private RectTransform inputSlotsRoot;

        [Header("Templates")]
        [SerializeField] private Image outputIconTemplate;
        [SerializeField] private Image inputIconTemplate;

        [Header("Legacy Refs (auto hidden)")]
        [SerializeField] private Image outputIcon;
        [SerializeField] private Image inputIconA;
        [SerializeField] private Image inputIconB;
        [SerializeField] private TMP_Text outputCountText;
        [SerializeField] private TMP_Text inputCountAText;
        [SerializeField] private TMP_Text inputCountBText;

        [Header("Texts")]
        [SerializeField] private TMP_Text produceAmountText;
        [SerializeField] private TMP_Text turnCostText;
        [SerializeField] private TMP_Text quantityValueText;

        [Header("Buttons")]
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;

        [Header("Layout")]
        [SerializeField] private Vector2 outputIconSize = new Vector2(58f, 58f);
        [SerializeField] private Vector2 inputIconSize = new Vector2(44f, 44f);
        [SerializeField] private float outputSpacing = 10f;
        [SerializeField] private float inputSpacing = 8f;
        [SerializeField] private int maxSlotsPerSide = 3;

        [Header("Quantity")]
        [SerializeField] private int quantity;
        [SerializeField] private int minQuantity;
        [SerializeField] private int maxQuantity = 99;

        private readonly List<SlotView> _outputSlots = new();
        private readonly List<SlotView> _inputSlots = new();

        private void Awake()
        {
            if (root == null)
            {
                root = transform as RectTransform;
            }

            EnsureRoots();
            BindButtons();
            HideLegacyRefs();
            RefreshQuantityText();
        }

        public void SetQuantity(int value)
        {
            quantity = Mathf.Clamp(value, minQuantity, maxQuantity);
            RefreshQuantityText();
        }

        public void Configure(
            IReadOnlyList<IngredientViewData> outputs,
            IReadOnlyList<IngredientViewData> inputs,
            int producePerTurn,
            int turnCost)
        {
            EnsureRoots();
            HideLegacyRefs();

            RenderSlots(outputs, _outputSlots, outputSlotsRoot, outputIconTemplate, outputIconSize, outputSpacing);
            RenderSlots(inputs, _inputSlots, inputSlotsRoot, inputIconTemplate, inputIconSize, inputSpacing);

            if (produceAmountText != null)
            {
                produceAmountText.text = $"+{Mathf.Max(0, producePerTurn)}";
            }

            if (turnCostText != null)
            {
                turnCostText.text = $"{Mathf.Max(1, turnCost)}t";
            }
        }

        private void BindButtons()
        {
            if (minusButton != null)
            {
                minusButton.onClick.RemoveListener(DecreaseQuantity);
                minusButton.onClick.AddListener(DecreaseQuantity);
            }

            if (plusButton != null)
            {
                plusButton.onClick.RemoveListener(IncreaseQuantity);
                plusButton.onClick.AddListener(IncreaseQuantity);
            }
        }

        private void IncreaseQuantity() => SetQuantity(quantity + 1);
        private void DecreaseQuantity() => SetQuantity(quantity - 1);

        private void RefreshQuantityText()
        {
            if (quantityValueText != null)
            {
                quantityValueText.text = quantity.ToString();
            }
        }

        private void EnsureRoots()
        {
            if (root == null)
            {
                return;
            }

            if (outputSlotsRoot == null)
            {
                outputSlotsRoot = EnsureChildRoot("OutputSlots", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -20f));
            }

            if (inputSlotsRoot == null)
            {
                inputSlotsRoot = EnsureChildRoot("InputSlots", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -20f));
            }
        }

        private static RectTransform EnsureChildRoot(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition)
        {
            var child = parent.Find(name) as RectTransform;
            if (child != null)
            {
                return child;
            }

            var go = new GameObject(name, typeof(RectTransform));
            child = go.GetComponent<RectTransform>();
            child.SetParent(parent, false);
            child.anchorMin = anchorMin;
            child.anchorMax = anchorMax;
            child.pivot = pivot;
            child.anchoredPosition = anchoredPosition;
            child.sizeDelta = Vector2.zero;
            return child;
        }

        private void HideLegacyRefs()
        {
            if (outputIcon != null) outputIcon.gameObject.SetActive(false);
            if (inputIconA != null) inputIconA.gameObject.SetActive(false);
            if (inputIconB != null) inputIconB.gameObject.SetActive(false);
            if (outputCountText != null) outputCountText.gameObject.SetActive(false);
            if (inputCountAText != null) inputCountAText.gameObject.SetActive(false);
            if (inputCountBText != null) inputCountBText.gameObject.SetActive(false);
        }

        private void RenderSlots(
            IReadOnlyList<IngredientViewData> source,
            List<SlotView> cache,
            RectTransform parent,
            Image template,
            Vector2 iconSize,
            float spacing)
        {
            if (parent == null)
            {
                return;
            }

            var count = Mathf.Min(maxSlotsPerSide, source != null ? source.Count : 0);
            EnsureSlotCache(cache, parent, template, count);

            for (var i = 0; i < cache.Count; i++)
            {
                var slot = cache[i];
                var active = i < count;
                slot.Root.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                var item = source[i];
                slot.Icon.sprite = item.Icon;
                slot.Icon.color = item.Icon != null ? Color.white : new Color(1f, 1f, 1f, 0.78f);
                slot.Icon.preserveAspect = true;
                slot.Count.text = $"x{Mathf.Max(0, item.Amount)}";
            }

            var width = count > 0 ? count * iconSize.x + (count - 1) * spacing : 0f;
            var startX = -width * 0.5f + iconSize.x * 0.5f;
            for (var i = 0; i < count; i++)
            {
                var slot = cache[i];
                slot.Root.sizeDelta = iconSize;
                slot.Root.anchoredPosition = new Vector2(startX + i * (iconSize.x + spacing), 0f);
            }
        }

        private static void EnsureSlotCache(List<SlotView> cache, RectTransform parent, Image template, int required)
        {
            while (cache.Count < required)
            {
                var index = cache.Count;
                var slotRoot = new GameObject($"Slot_{index + 1}", typeof(RectTransform)).GetComponent<RectTransform>();
                slotRoot.SetParent(parent, false);
                slotRoot.anchorMin = new Vector2(0.5f, 0.5f);
                slotRoot.anchorMax = new Vector2(0.5f, 0.5f);
                slotRoot.pivot = new Vector2(0.5f, 0.5f);

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.SetParent(slotRoot, false);
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(48f, 48f);
                var icon = iconGo.GetComponent<Image>();
                if (template != null)
                {
                    icon.sprite = template.sprite;
                    icon.material = template.material;
                    icon.type = template.type;
                    icon.preserveAspect = template.preserveAspect;
                }
                icon.color = new Color(1f, 1f, 1f, 0.78f);

                var countGo = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                var countRect = countGo.GetComponent<RectTransform>();
                countRect.SetParent(slotRoot, false);
                countRect.anchorMin = new Vector2(0.5f, 0f);
                countRect.anchorMax = new Vector2(0.5f, 0f);
                countRect.pivot = new Vector2(0.5f, 1f);
                countRect.anchoredPosition = new Vector2(0f, -4f);
                countRect.sizeDelta = new Vector2(70f, 22f);
                var count = countGo.GetComponent<TextMeshProUGUI>();
                count.alignment = TextAlignmentOptions.Center;
                count.fontSize = 18f;
                count.color = Color.white;
                count.enableWordWrapping = false;
                count.overflowMode = TextOverflowModes.Truncate;
                if (TMP_Settings.defaultFontAsset != null)
                {
                    count.font = TMP_Settings.defaultFontAsset;
                }

                cache.Add(new SlotView { Root = slotRoot, Icon = icon, Count = count });
            }
        }
    }
}
