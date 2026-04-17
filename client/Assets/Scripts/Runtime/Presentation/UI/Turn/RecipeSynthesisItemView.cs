using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class RecipeSynthesisItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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
        [SerializeField] private float preferredItemHeight = 178f;
        [SerializeField] private float minItemHeight = 156f;

        [Header("Activation")]
        [SerializeField] private bool isActive;
        [SerializeField] private string activeLabel = "\u6FC0\u6D3B";
        [SerializeField] private Image activeStateIcon;
        [SerializeField] private Sprite activeStatePlaceholderSprite;
        [SerializeField] private Color activeStateIconColor = new Color(1f, 1f, 1f, 0.16f);

        [Header("Locked State")]
        [SerializeField] private GameObject lockedOverlayRoot;
        [SerializeField] private Image lockedOverlayMask;
        [SerializeField] private Image lockedIcon;
        [SerializeField] private TMP_Text lockedText;
        [SerializeField] private string lockedLabel = "Locked";
        [SerializeField] private Color lockedOverlayColor = new Color(0f, 0f, 0f, 0.55f);

        private readonly List<SlotView> _outputSlots = new();
        private readonly List<SlotView> _inputSlots = new();
        private LayoutElement _layoutElement;

        public event Action<RecipeSynthesisItemView, bool> ActivationChanged;
        public event Action<RecipeSynthesisItemView> HoverEntered;
        public event Action<RecipeSynthesisItemView> HoverExited;

        public string RecipeId { get; private set; } = string.Empty;
        public bool IsLocked { get; private set; }
        public bool IsActiveState => isActive;

        private void Awake()
        {
            if (root == null)
            {
                root = transform as RectTransform;
            }

            EnsureLayoutElement();
            EnsureRoots();
            BindButtons();
            HideLegacyRefs();
            EnsureActivationVisual();
            HideLegacyActivationButtons();
            RefreshActivationStateVisual();
            EnsureLockOverlay();
            SetLocked(false);
        }

        public void SetRecipeId(string recipeId)
        {
            RecipeId = string.IsNullOrWhiteSpace(recipeId) ? string.Empty : recipeId.Trim();
        }

        public void SetActiveState(bool active, bool notify = false)
        {
            var changed = isActive != active;
            isActive = active;
            RefreshActivationStateVisual();
            if (notify && changed)
            {
                ActivationChanged?.Invoke(this, isActive);
            }
        }

        // Keep compatibility with older call sites during merge transitions.
        public void SetQuantity(int value, bool notify = false)
        {
            SetActiveState(value > 0, notify);
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

        public void SetLocked(bool locked)
        {
            IsLocked = locked;
            EnsureLockOverlay();
            if (lockedOverlayRoot != null)
            {
                lockedOverlayRoot.SetActive(locked);
            }

            if (minusButton != null)
            {
                minusButton.interactable = !locked && isActive;
            }

            if (plusButton != null)
            {
                plusButton.interactable = !locked && !isActive;
            }

            RefreshActivationStateVisual();
        }

        public void SetLockedVisual(Sprite icon, string text = null)
        {
            EnsureLockOverlay();
            if (lockedIcon != null)
            {
                lockedIcon.sprite = icon;
                lockedIcon.color = icon == null ? new Color(1f, 1f, 1f, 0.9f) : Color.white;
            }

            if (lockedText != null)
            {
                var label = string.IsNullOrWhiteSpace(text) ? lockedLabel : text.Trim();
                lockedText.text = label;
            }
        }

        private void BindButtons()
        {
            if (minusButton != null)
            {
                minusButton.onClick.RemoveListener(SetInactive);
                minusButton.onClick.AddListener(SetInactive);
            }

            if (plusButton != null)
            {
                plusButton.onClick.RemoveListener(SetActive);
                plusButton.onClick.AddListener(SetActive);
            }
        }

        private void SetActive()
        {
            if (IsLocked)
            {
                return;
            }

            SetActiveState(true, true);
        }

        private void SetInactive()
        {
            if (IsLocked)
            {
                return;
            }

            SetActiveState(false, true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsLocked || eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            SetActiveState(!isActive, true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            HoverEntered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HoverExited?.Invoke(this);
        }

        private void RefreshActivationStateVisual()
        {
            if (quantityValueText != null)
            {
                quantityValueText.text = activeLabel;
                quantityValueText.gameObject.SetActive(isActive);
            }

            if (minusButton != null)
            {
                minusButton.interactable = !IsLocked && isActive;
            }

            if (plusButton != null)
            {
                plusButton.interactable = !IsLocked && !isActive;
            }

            if (activeStateIcon != null)
            {
                activeStateIcon.gameObject.SetActive(isActive);
            }
        }

        private void HideLegacyActivationButtons()
        {
            if (minusButton != null)
            {
                minusButton.gameObject.SetActive(false);
            }

            if (plusButton != null)
            {
                plusButton.gameObject.SetActive(false);
            }
        }

        private void EnsureActivationVisual()
        {
            if (root == null)
            {
                root = transform as RectTransform;
            }

            if (root == null)
            {
                return;
            }

            if (activeStateIcon == null)
            {
                var existing = root.Find("ActiveStateIcon") as RectTransform;
                if (existing != null)
                {
                    activeStateIcon = existing.GetComponent<Image>();
                }
            }

            if (activeStateIcon == null)
            {
                var iconGo = new GameObject("ActiveStateIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = iconGo.GetComponent<RectTransform>();
                rect.SetParent(root, false);
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                activeStateIcon = iconGo.GetComponent<Image>();
                iconGo.transform.SetSiblingIndex(1);
            }

            if (activeStateIcon != null)
            {
                activeStateIcon.sprite = activeStatePlaceholderSprite;
                activeStateIcon.color = activeStateIconColor;
                activeStateIcon.raycastTarget = false;
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

            EnsureLayoutElement();
        }

        private void EnsureLayoutElement()
        {
            if (root == null)
            {
                return;
            }

            if (_layoutElement == null)
            {
                _layoutElement = root.GetComponent<LayoutElement>();
                if (_layoutElement == null)
                {
                    _layoutElement = root.gameObject.AddComponent<LayoutElement>();
                }
            }

            var resolvedPreferred = preferredItemHeight;
            if (resolvedPreferred <= 0f)
            {
                resolvedPreferred = root.sizeDelta.y;
            }
            if (resolvedPreferred <= 0f)
            {
                resolvedPreferred = 178f;
            }

            var resolvedMin = minItemHeight > 0f ? minItemHeight : resolvedPreferred;
            _layoutElement.preferredHeight = resolvedPreferred;
            _layoutElement.minHeight = Mathf.Min(resolvedMin, resolvedPreferred);

            // Force layout recalculation for freshly instantiated items under VerticalLayoutGroup.
            LayoutRebuilder.MarkLayoutForRebuild(root);
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

        private void EnsureLockOverlay()
        {
            if (root == null)
            {
                root = transform as RectTransform;
            }

            if (root == null)
            {
                return;
            }

            if (lockedOverlayRoot == null)
            {
                var existing = root.Find("LockedOverlay");
                if (existing != null)
                {
                    lockedOverlayRoot = existing.gameObject;
                }
            }

            if (lockedOverlayRoot != null && lockedOverlayMask == null)
            {
                lockedOverlayMask = lockedOverlayRoot.GetComponent<Image>();
            }

            if (lockedOverlayMask != null)
            {
                lockedOverlayMask.color = lockedOverlayColor;
                lockedOverlayMask.raycastTarget = true;
            }

            if (lockedIcon == null && lockedOverlayRoot != null)
            {
                var iconTransform = lockedOverlayRoot.transform.Find("LockedIcon") as RectTransform;
                if (iconTransform != null)
                {
                    lockedIcon = iconTransform.GetComponent<Image>();
                }
            }

            if (lockedText == null && lockedOverlayRoot != null)
            {
                var textTransform = lockedOverlayRoot.transform.Find("LockedText") as RectTransform;
                if (textTransform != null)
                {
                    lockedText = textTransform.GetComponent<TextMeshProUGUI>();
                }
            }
        }
    }
}
