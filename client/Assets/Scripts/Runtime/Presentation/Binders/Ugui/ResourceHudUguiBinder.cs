using System;
using System.Collections.Generic;
using Panoptes.Presentation.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Panoptes.Presentation.Binders.Ugui
{
    public sealed class ResourceHudUguiBinder : IDisposable
    {
        private sealed class ResourceItemBinding
        {
            public RectTransform Root;
            public Image Icon;
            public TMP_Text BaseNumText;
            public TMP_Text ChangeNumText;
            public string Key = string.Empty;
        }

        private readonly List<ResourceItemBinding> _items = new();
        private readonly Dictionary<string, int> _lastAmounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ResourcePointTooltip _tooltip = new();
        private RectTransform _resourceListRoot;
        private IReadOnlyList<string> _iconResourcesRoots;
        private Color _increaseColor;
        private Color _decreaseColor;
        private ResourceItemBinding _hoveredItem;
        private bool _hasSnapshot;

        public ResourceHudUguiBinder(
            RectTransform resourceListRoot,
            IReadOnlyList<string> iconResourcesRoots,
            Color increaseColor,
            Color decreaseColor)
        {
            RebindReferences(
                resourceListRoot,
                iconResourcesRoots,
                increaseColor,
                decreaseColor);
        }

        public void RebindReferences(
            RectTransform resourceListRoot,
            IReadOnlyList<string> iconResourcesRoots,
            Color increaseColor,
            Color decreaseColor)
        {
            if (_resourceListRoot != resourceListRoot)
            {
                HideAllChangeHints();
                _tooltip.Hide();
                _hoveredItem = null;
                _items.Clear();
                _hasSnapshot = false;
                _lastAmounts.Clear();
            }

            _resourceListRoot = resourceListRoot;
            _iconResourcesRoots = iconResourcesRoots ?? Array.Empty<string>();
            _increaseColor = increaseColor;
            _decreaseColor = decreaseColor;
        }

        public void Render(ResourceHudState state)
        {
            EnsureItemBindings();
            if (_resourceListRoot == null || _items.Count == 0)
            {
                return;
            }

            var rows = state?.Rows ?? Array.Empty<ResourceHudRowState>();
            EnsureItemCount(rows.Count);

            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null || item.Root == null)
                {
                    continue;
                }

                if (i >= rows.Count)
                {
                    HideChangeHint(item);
                    DisableTooltip(item);
                    item.Key = string.Empty;
                    item.Root.gameObject.SetActive(false);
                    continue;
                }

                RenderRow(item, rows[i]);
            }

            _hasSnapshot = true;
        }

        public void StopAllHideCoroutines()
        {
            HideAllChangeHints();
            _tooltip.Hide();
            _hoveredItem = null;
        }

        public void HideAllChangeHints()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                HideChangeHint(_items[i]);
            }
        }

        public void Dispose()
        {
            StopAllHideCoroutines();
            _tooltip.Dispose();
            _items.Clear();
            _lastAmounts.Clear();
            _iconCache.Clear();
            _hoveredItem = null;
            _hasSnapshot = false;
        }

        private void RenderRow(ResourceItemBinding item, ResourceHudRowState row)
        {
            row ??= new ResourceHudRowState(string.Empty, 0);
            item.Root.gameObject.SetActive(true);
            if (!string.Equals(item.Key, row.Key, StringComparison.OrdinalIgnoreCase))
            {
                HideChangeHint(item);
                HideTooltip(item);
            }

            item.Key = row.Key;

            if (item.Icon != null)
            {
                var icon = LoadIcon(row.IconKey);
                if (icon != null)
                {
                    item.Icon.sprite = icon;
                }

                item.Icon.preserveAspect = true;
            }

            if (item.BaseNumText != null)
            {
                item.BaseNumText.text = row.Amount.ToString();
            }

            var previous = 0;
            var hasPrevious = _hasSnapshot && _lastAmounts.TryGetValue(row.Key, out previous);
            var delta = hasPrevious ? row.Amount - previous : 0;
            ApplyChangeDelta(item, delta);
            ConfigureTooltip(item, row);
            _lastAmounts[row.Key] = row.Amount;
        }

        private void EnsureItemBindings()
        {
            if (_resourceListRoot == null || _items.Count > 0)
            {
                return;
            }

            for (var i = 0; i < _resourceListRoot.childCount; i++)
            {
                var child = _resourceListRoot.GetChild(i) as RectTransform;
                var binding = CreateBinding(child);
                if (binding != null)
                {
                    _items.Add(binding);
                }
            }
        }

        private void EnsureItemCount(int count)
        {
            if (_resourceListRoot == null || count <= _items.Count)
            {
                return;
            }

            if (_items.Count == 0 || _items[0] == null || _items[0].Root == null)
            {
                return;
            }

            var template = _items[0].Root.gameObject;
            while (_items.Count < count)
            {
                var clone = UnityEngine.Object.Instantiate(template, _resourceListRoot, false);
                clone.name = $"ResourceItem ({_items.Count})";
                var binding = CreateBinding(clone.transform as RectTransform);
                if (binding == null)
                {
                    break;
                }

                _items.Add(binding);
            }
        }

        private static ResourceItemBinding CreateBinding(RectTransform root)
        {
            if (root == null)
            {
                return null;
            }

            var binding = new ResourceItemBinding
            {
                Root = root,
                Icon = root.Find("Image")?.GetComponent<Image>(),
                BaseNumText = root.Find("BaseNum")?.GetComponent<TMP_Text>(),
                ChangeNumText = root.Find("ChangeNum")?.GetComponent<TMP_Text>()
            };

            if (binding.ChangeNumText != null)
            {
                binding.ChangeNumText.gameObject.SetActive(false);
            }

            return binding;
        }

        private void ApplyChangeDelta(ResourceItemBinding item, int delta)
        {
            if (item == null || item.ChangeNumText == null)
            {
                return;
            }

            if (delta == 0)
            {
                return;
            }

            item.ChangeNumText.gameObject.SetActive(true);
            item.ChangeNumText.color = delta > 0 ? _increaseColor : _decreaseColor;
            item.ChangeNumText.text = delta > 0 ? $"+{delta}" : delta.ToString();
        }

        private static void HideChangeHint(ResourceItemBinding item)
        {
            if (item != null && item.ChangeNumText != null)
            {
                item.ChangeNumText.gameObject.SetActive(false);
            }
        }

        private void ConfigureTooltip(ResourceItemBinding item, ResourceHudRowState row)
        {
            if (item?.Icon == null)
            {
                return;
            }

            var trigger = item.Icon.GetComponent<EventTrigger>();
            var tooltipText = BuildTooltipText(row);
            if (row == null || string.IsNullOrWhiteSpace(tooltipText))
            {
                item.Icon.raycastTarget = false;
                if (trigger != null)
                {
                    trigger.triggers.Clear();
                }

                HideTooltip(item);
                return;
            }

            if (trigger == null)
            {
                trigger = item.Icon.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.Clear();
            item.Icon.raycastTarget = true;
            AddTooltipEvent(trigger, EventTriggerType.PointerEnter, data =>
            {
                _hoveredItem = item;
                _tooltip.Show(item.Icon, tooltipText, ((PointerEventData)data).position);
            });
            AddTooltipEvent(trigger, EventTriggerType.Drag, data =>
            {
                if (ReferenceEquals(_hoveredItem, item))
                {
                    _tooltip.Move(((PointerEventData)data).position);
                }
            });
            AddTooltipEvent(trigger, EventTriggerType.PointerExit, _ => HideTooltip(item));
        }

        private void DisableTooltip(ResourceItemBinding item)
        {
            if (item?.Icon == null)
            {
                return;
            }

            var trigger = item.Icon.GetComponent<EventTrigger>();
            if (trigger != null)
            {
                trigger.triggers.Clear();
            }

            item.Icon.raycastTarget = false;
            HideTooltip(item);
        }

        private void HideTooltip(ResourceItemBinding item)
        {
            if (!ReferenceEquals(_hoveredItem, item))
            {
                return;
            }

            _hoveredItem = null;
            _tooltip.Hide();
        }

        private static void AddTooltipEvent(
            EventTrigger trigger,
            EventTriggerType eventType,
            UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private static string BuildTooltipText(ResourceHudRowState row)
        {
            if (row == null)
            {
                return string.Empty;
            }

            var name = string.IsNullOrWhiteSpace(row.DisplayName) ? row.Key : row.DisplayName.Trim();
            var description = string.IsNullOrWhiteSpace(row.Description) ? string.Empty : row.Description.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                return name;
            }

            return $"{name}\n{description}";
        }

        private Sprite LoadIcon(string iconKey)
        {
            var key = (iconKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            if (_iconCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            Sprite loaded = null;
            for (var i = 0; i < _iconResourcesRoots.Count; i++)
            {
                var root = (_iconResourcesRoots[i] ?? string.Empty).Trim().Trim('/');
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                loaded = Resources.Load<Sprite>($"{root}/{key}");
                if (loaded != null)
                {
                    break;
                }
            }

            if (loaded == null)
            {
                loaded = Resources.Load<Sprite>(key);
            }

            _iconCache[key] = loaded;
            return loaded;
        }

        private sealed class ResourcePointTooltip : IDisposable
        {
            private const float MaxWidth = 260f;
            private static readonly Vector2 Padding = new(12f, 8f);
            private static readonly Vector2 CursorOffset = new(12f, -24f);

            private Canvas _canvas;
            private RectTransform _canvasRect;
            private RectTransform _root;
            private TMP_Text _text;

            public void Show(Graphic owner, string content, Vector2 screenPosition)
            {
                Ensure(owner);
                if (_root == null || _text == null)
                {
                    return;
                }

                _text.text = content ?? string.Empty;
                _text.ForceMeshUpdate();
                var preferred = _text.GetPreferredValues(_text.text, MaxWidth, 0f);
                _root.sizeDelta = new Vector2(
                    Mathf.Min(MaxWidth, preferred.x) + Padding.x * 2f,
                    preferred.y + Padding.y * 2f);
                _root.gameObject.SetActive(true);
                _root.SetAsLastSibling();
                Move(screenPosition);
            }

            public void Move(Vector2 screenPosition)
            {
                if (_root == null || _canvasRect == null)
                {
                    return;
                }

                var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? _canvas.worldCamera
                    : null;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _canvasRect,
                        screenPosition,
                        camera,
                        out var localPosition))
                {
                    return;
                }

                localPosition += CursorOffset;
                var canvasBounds = _canvasRect.rect;
                var size = _root.sizeDelta;
                localPosition.x = Mathf.Clamp(
                    localPosition.x,
                    canvasBounds.xMin + 4f,
                    canvasBounds.xMax - size.x - 4f);
                localPosition.y = Mathf.Clamp(
                    localPosition.y,
                    canvasBounds.yMin + size.y + 4f,
                    canvasBounds.yMax - 4f);
                _root.anchoredPosition = localPosition;
            }

            public void Hide()
            {
                if (_root != null)
                {
                    _root.gameObject.SetActive(false);
                }
            }

            public void Dispose()
            {
                if (_root == null)
                {
                    return;
                }

                var target = _root.gameObject;
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(target);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }

                _root = null;
                _text = null;
                _canvas = null;
                _canvasRect = null;
            }

            private void Ensure(Graphic owner)
            {
                var canvas = owner != null ? owner.GetComponentInParent<Canvas>() : null;
                if (canvas == null)
                {
                    return;
                }

                if (_root != null && ReferenceEquals(_canvas, canvas))
                {
                    return;
                }

                Dispose();
                _canvas = canvas;
                _canvasRect = canvas.transform as RectTransform;
                if (_canvasRect == null)
                {
                    return;
                }

                var rootObject = new GameObject(
                    "ResourcePointTooltip",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup));
                rootObject.transform.SetParent(canvas.transform, false);
                _root = rootObject.GetComponent<RectTransform>();
                _root.anchorMin = new Vector2(0.5f, 0.5f);
                _root.anchorMax = new Vector2(0.5f, 0.5f);
                _root.pivot = new Vector2(0f, 1f);

                var background = rootObject.GetComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0.9f);
                background.raycastTarget = false;

                var group = rootObject.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(rootObject.transform, false);
                var textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Padding;
                textRect.offsetMax = -Padding;

                _text = textObject.GetComponent<TMP_Text>();
                _text.alignment = TextAlignmentOptions.TopLeft;
                _text.color = Color.white;
                _text.textWrappingMode = TextWrappingModes.Normal;
                _text.fontSize = 18f;
                _text.raycastTarget = false;
                _root.gameObject.SetActive(false);
            }
        }
    }
}
