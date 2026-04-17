/*************************************************
 * Project: Panoptes
 * File: BuildItemView.cs
 * Author: Panoptes Team
 * Date: 2026-04-17
 * Description: Build list item view for grouped build panel.
 *************************************************/

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class BuildItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Serializable]
        public struct MaterialRequirement
        {
            public string key;
            public string displayName;
            public int amount;
            public Sprite icon;
        }

        [Serializable]
        private sealed class MetricSlot
        {
            public RectTransform root;
            public Image backgroundImage;
            public Image iconImage;
            public TMP_Text text;
        }

        [Header("Item Root")]
        [SerializeField] private Button clickButton;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image buildingIcon;
        [SerializeField] private TMP_Text buildingNameText;
        [SerializeField] private TMP_Text buildDescriptionText;
        [SerializeField] private RectTransform metricsRoot;
        [SerializeField] private RectTransform metricTemplate;
        [SerializeField] private bool autoFindReferences = true;

        [Header("Locked State")]
        [SerializeField] private GameObject lockOverlayRoot;
        [SerializeField] private Image lockMaskImage;
        [SerializeField] private Image lockIconImage;
        [SerializeField] private Sprite fallbackLockIcon;
        [SerializeField] private Color lockMaskColor = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color lockIconColor = Color.white;

        [Header("State Colors")]
        [SerializeField] private Color availableBackgroundColor = new(0.035f, 0.12f, 0.2f, 0.98f);
        [SerializeField] private Color availableHoverBackgroundColor = new(0.065f, 0.18f, 0.29f, 0.99f);
        [SerializeField] private Color pendingBackgroundColor = new(0.13f, 0.14f, 0.19f, 0.98f);
        [SerializeField] private Color pendingHoverBackgroundColor = new(0.19f, 0.16f, 0.15f, 0.99f);
        [SerializeField] private Color lockedBackgroundColor = new(0.043f, 0.085f, 0.13f, 0.98f);
        [SerializeField] private Color lockedHoverBackgroundColor = new(0.06f, 0.105f, 0.15f, 0.99f);
        [SerializeField] private Color defaultTextColor = new(0.95f, 0.93f, 0.86f, 1f);
        [SerializeField] private Color lockedTextColor = new(0.73f, 0.78f, 0.84f, 0.92f);
        [SerializeField] private Color metricChipColor = new(0.11f, 0.2f, 0.3f, 0.9f);
        [SerializeField] private Color pendingMetricChipColor = new(0.28f, 0.2f, 0.12f, 0.9f);
        [SerializeField] private Color lockedMetricChipColor = new(0.11f, 0.16f, 0.22f, 0.88f);

        private readonly List<MetricSlot> _metricSlots = new();
        private BuildItemAvailabilityState _availabilityState = BuildItemAvailabilityState.Available;
        private bool _isHovered;

        public Button ClickButton => clickButton;

        private void Awake()
        {
            EnsureReferences();
            EnsureButton();
            EnsureLayoutElement();
            EnsureMetricTemplate();
            RebuildMetricSlots();
            EnsureLockOverlay();
            ApplyVisualState();
        }

        public void Bind(BuildItemRenderModel model)
        {
            EnsureReferences();
            EnsureButton();
            EnsureLayoutElement();
            EnsureMetricTemplate();
            RebuildMetricSlots();

            if (buildingNameText != null)
            {
                buildingNameText.text = string.IsNullOrWhiteSpace(model?.Title) ? "Unknown Building" : model.Title;
            }

            if (buildDescriptionText != null)
            {
                buildDescriptionText.text = model?.ShortDescription ?? string.Empty;
                buildDescriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(buildDescriptionText.text));
            }

            if (buildingIcon != null)
            {
                buildingIcon.sprite = model != null ? model.Icon : null;
                buildingIcon.enabled = buildingIcon.sprite != null;
                buildingIcon.preserveAspect = true;
            }

            _availabilityState = model != null ? model.AvailabilityState : BuildItemAvailabilityState.Available;
            ApplyMetrics(model != null ? model.SummaryMetrics : null);
            ApplyVisualState();
        }

        public void ConfigureVisual(
            string buildingName,
            string buildingDescription,
            Sprite buildingSprite,
            IReadOnlyList<MaterialRequirement> requirements)
        {
            var model = new BuildItemRenderModel
            {
                Title = buildingName,
                ShortDescription = buildingDescription,
                Icon = buildingSprite,
                AvailabilityState = BuildItemAvailabilityState.Available
            };

            if (requirements != null)
            {
                for (var i = 0; i < requirements.Count && model.SummaryMetrics.Count < 3; i++)
                {
                    var requirement = requirements[i];
                    model.SummaryMetrics.Add(new BuildMetricRenderModel
                    {
                        Key = requirement.key ?? string.Empty,
                        Text = $"{(string.IsNullOrWhiteSpace(requirement.displayName) ? requirement.key : requirement.displayName)} x{Mathf.Max(0, requirement.amount)}",
                        Icon = requirement.icon
                    });
                }
            }

            Bind(model);
        }

        public void SetClickAction(UnityAction action)
        {
            EnsureButton();
            if (clickButton == null)
            {
                return;
            }

            clickButton.onClick.RemoveAllListeners();
            if (action != null)
            {
                clickButton.onClick.AddListener(action);
            }
        }

        public void ClearClickAction()
        {
            if (clickButton != null)
            {
                clickButton.onClick.RemoveAllListeners();
            }
        }

        public void SetLocked(bool isLocked, Sprite lockIcon = null)
        {
            EnsureLockOverlay();
            if (isLocked)
            {
                _availabilityState = BuildItemAvailabilityState.Locked;
                if (lockIcon != null)
                {
                    fallbackLockIcon = lockIcon;
                }
            }
            else if (_availabilityState == BuildItemAvailabilityState.Locked)
            {
                _availabilityState = BuildItemAvailabilityState.Available;
            }

            ApplyVisualState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            ApplyVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            ApplyVisualState();
        }

        private void EnsureReferences()
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (backgroundImage == null)
            {
                var panelNode = transform.Find("Panel");
                if (panelNode != null)
                {
                    backgroundImage = panelNode.GetComponent<Image>();
                }

                if (backgroundImage == null)
                {
                    backgroundImage = GetComponent<Image>();
                }
            }

            if (buildingIcon == null)
            {
                var iconNode = transform.Find("Image");
                if (iconNode != null)
                {
                    buildingIcon = iconNode.GetComponent<Image>();
                }

                if (buildingIcon == null)
                {
                    var images = GetComponentsInChildren<Image>(true);
                    for (var i = 0; i < images.Length; i++)
                    {
                        if (images[i] != null && !ReferenceEquals(images[i], backgroundImage) && !ReferenceEquals(images[i], lockIconImage))
                        {
                            buildingIcon = images[i];
                            break;
                        }
                    }
                }
            }

            if (buildingNameText == null)
            {
                var nameNode = transform.Find("BuildName");
                if (nameNode != null)
                {
                    buildingNameText = nameNode.GetComponent<TMP_Text>();
                }

                if (buildingNameText == null)
                {
                    buildingNameText = FindTextByName("BuildName");
                }
            }

            if (buildDescriptionText == null)
            {
                var descNode = transform.Find("BuildDes");
                if (descNode != null)
                {
                    buildDescriptionText = descNode.GetComponent<TMP_Text>();
                }

                if (buildDescriptionText == null)
                {
                    buildDescriptionText = FindTextByName("BuildDes");
                }
            }

            if (metricsRoot == null)
            {
                var rootNode = transform.Find("MetricsRoot");
                if (rootNode == null)
                {
                    var panelNode = transform.Find("Panel");
                    rootNode = panelNode != null ? panelNode.Find("MetricsRoot") : null;
                }

                if (rootNode != null)
                {
                    metricsRoot = rootNode as RectTransform;
                }
            }

            if (metricTemplate == null && metricsRoot != null)
            {
                var templateNode = metricsRoot.Find("MetricTemplate");
                if (templateNode != null)
                {
                    metricTemplate = templateNode as RectTransform;
                }
            }

            if (lockOverlayRoot == null)
            {
                var lockNode = transform.Find("LockOverlay");
                if (lockNode != null)
                {
                    lockOverlayRoot = lockNode.gameObject;
                    lockMaskImage = lockNode.GetComponent<Image>();
                }
            }

            if (lockIconImage == null)
            {
                var lockIconNode = transform.Find("LockIcon");
                if (lockIconNode != null)
                {
                    lockIconImage = lockIconNode.GetComponent<Image>();
                }

                if (lockIconImage == null && lockOverlayRoot != null)
                {
                    var nestedLockIcon = lockOverlayRoot.transform.Find("LockIcon");
                    if (nestedLockIcon != null)
                    {
                        lockIconImage = nestedLockIcon.GetComponent<Image>();
                    }
                }
            }
        }

        private TMP_Text FindTextByName(string nodeName)
        {
            var texts = GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == null || text.transform == null)
                {
                    continue;
                }

                if (string.Equals(text.transform.name, nodeName, StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            return null;
        }

        private void EnsureButton()
        {
            if (clickButton == null)
            {
                clickButton = GetComponent<Button>();
            }

            if (clickButton == null)
            {
                clickButton = gameObject.AddComponent<Button>();
                clickButton.transition = Selectable.Transition.ColorTint;
            }

            if (clickButton.targetGraphic == null)
            {
                clickButton.targetGraphic = backgroundImage != null ? backgroundImage : GetComponent<Image>();
            }
        }

        private void EnsureLayoutElement()
        {
            var layout = GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<LayoutElement>();
            }

            layout.minHeight = 104f;
            layout.preferredHeight = 104f;
            layout.flexibleHeight = 0f;
        }

        private void EnsureMetricTemplate()
        {
            if (metricsRoot == null)
            {
                var parent = backgroundImage != null ? backgroundImage.transform : transform;
                var metricsRootObject = new GameObject("MetricsRoot", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                metricsRootObject.transform.SetParent(parent, false);
                metricsRoot = metricsRootObject.GetComponent<RectTransform>();
                metricsRoot.anchorMin = new Vector2(1f, 0f);
                metricsRoot.anchorMax = new Vector2(1f, 0f);
                metricsRoot.pivot = new Vector2(1f, 0f);
                metricsRoot.anchoredPosition = new Vector2(-18f, 10f);
                metricsRoot.sizeDelta = new Vector2(164f, 28f);

                var layoutGroup = metricsRootObject.GetComponent<HorizontalLayoutGroup>();
                layoutGroup.spacing = 6f;
                layoutGroup.childAlignment = TextAnchor.MiddleRight;
                layoutGroup.childControlWidth = false;
                layoutGroup.childControlHeight = true;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childForceExpandHeight = false;
            }

            if (metricTemplate != null)
            {
                metricTemplate.gameObject.SetActive(false);
                return;
            }

            var templateObject = new GameObject("MetricTemplate", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            templateObject.transform.SetParent(metricsRoot, false);
            metricTemplate = templateObject.GetComponent<RectTransform>();
            metricTemplate.anchorMin = new Vector2(1f, 0.5f);
            metricTemplate.anchorMax = new Vector2(1f, 0.5f);
            metricTemplate.pivot = new Vector2(1f, 0.5f);
            metricTemplate.sizeDelta = new Vector2(94f, 26f);

            var layoutElement = templateObject.GetComponent<LayoutElement>();
            layoutElement.minWidth = 72f;
            layoutElement.preferredWidth = 94f;
            layoutElement.minHeight = 26f;
            layoutElement.preferredHeight = 26f;

            var chipImage = templateObject.GetComponent<Image>();
            chipImage.type = Image.Type.Sliced;
            chipImage.color = metricChipColor;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(templateObject.transform, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(16f, 16f);
            iconObject.GetComponent<Image>().preserveAspect = true;

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(templateObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(28f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = 13f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.color = defaultTextColor;

            metricTemplate.gameObject.SetActive(false);
        }

        private void EnsureLockOverlay()
        {
            if (lockMaskImage != null)
            {
                lockMaskImage.color = lockMaskColor;
                lockMaskImage.raycastTarget = false;
            }

            if (lockIconImage != null)
            {
                lockIconImage.color = lockIconColor;
                lockIconImage.raycastTarget = false;
            }
        }

        private void RebuildMetricSlots()
        {
            _metricSlots.Clear();
            if (metricsRoot == null)
            {
                return;
            }

            for (var i = 0; i < metricsRoot.childCount; i++)
            {
                var child = metricsRoot.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                var slot = BuildMetricSlot(child);
                if (slot != null)
                {
                    _metricSlots.Add(slot);
                }
            }
        }

        private MetricSlot BuildMetricSlot(RectTransform root)
        {
            if (root == null)
            {
                return null;
            }

            return new MetricSlot
            {
                root = root,
                backgroundImage = root.GetComponent<Image>(),
                iconImage = FindNamedImage(root, "Icon"),
                text = root.GetComponentInChildren<TMP_Text>(true)
            };
        }

        private static Image FindNamedImage(RectTransform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            var child = root.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private void ApplyMetrics(IReadOnlyList<BuildMetricRenderModel> metrics)
        {
            if (metricsRoot == null)
            {
                return;
            }

            var metricCount = metrics != null ? Mathf.Min(3, metrics.Count) : 0;
            EnsureMetricSlotCount(metricCount);

            for (var i = 0; i < _metricSlots.Count; i++)
            {
                var slot = _metricSlots[i];
                if (slot == null || slot.root == null || ReferenceEquals(slot.root, metricTemplate))
                {
                    continue;
                }

                if (i >= metricCount)
                {
                    slot.root.gameObject.SetActive(false);
                    continue;
                }

                var metric = metrics[i];
                slot.root.gameObject.SetActive(true);
                if (slot.iconImage != null)
                {
                    slot.iconImage.sprite = metric.Icon;
                    slot.iconImage.enabled = metric.Icon != null;
                    slot.iconImage.preserveAspect = true;
                }

                if (slot.text != null)
                {
                    slot.text.text = metric.Text ?? string.Empty;
                }
            }
        }

        private void EnsureMetricSlotCount(int requiredCount)
        {
            if (metricTemplate == null || metricsRoot == null)
            {
                return;
            }

            var available = 0;
            for (var i = 0; i < _metricSlots.Count; i++)
            {
                if (_metricSlots[i] != null && _metricSlots[i].root != null && !ReferenceEquals(_metricSlots[i].root, metricTemplate))
                {
                    available++;
                }
            }

            while (available < requiredCount)
            {
                var clone = Instantiate(metricTemplate.gameObject, metricsRoot, false);
                clone.name = $"Metric_{available + 1}";
                clone.SetActive(true);
                var slot = BuildMetricSlot(clone.transform as RectTransform);
                if (slot == null)
                {
                    break;
                }

                _metricSlots.Add(slot);
                available++;
            }

            if (_metricSlots.Count == 0)
            {
                RebuildMetricSlots();
            }
        }

        private void ApplyVisualState()
        {
            EnsureLockOverlay();

            if (backgroundImage != null)
            {
                backgroundImage.color = ResolveBackgroundColor();
            }

            var textColor = _availabilityState == BuildItemAvailabilityState.Locked ? lockedTextColor : defaultTextColor;
            if (buildingNameText != null)
            {
                buildingNameText.color = textColor;
            }

            if (buildDescriptionText != null)
            {
                buildDescriptionText.color = textColor;
            }

            var metricColor = ResolveMetricChipColor();
            for (var i = 0; i < _metricSlots.Count; i++)
            {
                var slot = _metricSlots[i];
                if (slot == null || slot.root == null || ReferenceEquals(slot.root, metricTemplate))
                {
                    continue;
                }

                if (slot.backgroundImage != null)
                {
                    slot.backgroundImage.color = metricColor;
                }

                if (slot.text != null)
                {
                    slot.text.color = textColor;
                }
            }

            if (lockOverlayRoot != null)
            {
                lockOverlayRoot.SetActive(_availabilityState == BuildItemAvailabilityState.Locked);
            }

            if (lockIconImage != null)
            {
                var resolvedIcon = fallbackLockIcon;
                if (resolvedIcon != null)
                {
                    lockIconImage.sprite = resolvedIcon;
                    lockIconImage.enabled = _availabilityState == BuildItemAvailabilityState.Locked;
                }
                else
                {
                    lockIconImage.enabled = false;
                }
            }

            if (clickButton != null)
            {
                clickButton.interactable = true;
            }
        }

        private Color ResolveBackgroundColor()
        {
            return _availabilityState switch
            {
                BuildItemAvailabilityState.Pending => _isHovered ? pendingHoverBackgroundColor : pendingBackgroundColor,
                BuildItemAvailabilityState.Locked => _isHovered ? lockedHoverBackgroundColor : lockedBackgroundColor,
                _ => _isHovered ? availableHoverBackgroundColor : availableBackgroundColor
            };
        }

        private Color ResolveMetricChipColor()
        {
            return _availabilityState switch
            {
                BuildItemAvailabilityState.Pending => pendingMetricChipColor,
                BuildItemAvailabilityState.Locked => lockedMetricChipColor,
                _ => metricChipColor
            };
        }
    }
}
