using System;
using System.Collections;
using System.Collections.Generic;
using Panoptes.Presentation.ViewModels;
using TMPro;
using UnityEngine;
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
            public Coroutine HideCoroutine;
            public string Key = string.Empty;
        }

        private readonly MonoBehaviour _coroutineOwner;
        private readonly List<ResourceItemBinding> _items = new();
        private readonly Dictionary<string, int> _lastAmounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private RectTransform _resourceListRoot;
        private IReadOnlyList<string> _iconResourcesRoots;
        private float _changeVisibleSeconds;
        private Color _increaseColor;
        private Color _decreaseColor;
        private bool _hasSnapshot;

        public ResourceHudUguiBinder(
            MonoBehaviour coroutineOwner,
            RectTransform resourceListRoot,
            IReadOnlyList<string> iconResourcesRoots,
            float changeVisibleSeconds,
            Color increaseColor,
            Color decreaseColor)
        {
            _coroutineOwner = coroutineOwner ?? throw new ArgumentNullException(nameof(coroutineOwner));
            RebindReferences(
                resourceListRoot,
                iconResourcesRoots,
                changeVisibleSeconds,
                increaseColor,
                decreaseColor);
        }

        public void RebindReferences(
            RectTransform resourceListRoot,
            IReadOnlyList<string> iconResourcesRoots,
            float changeVisibleSeconds,
            Color increaseColor,
            Color decreaseColor)
        {
            if (_resourceListRoot != resourceListRoot)
            {
                StopAllHideCoroutines();
                _items.Clear();
                _hasSnapshot = false;
                _lastAmounts.Clear();
            }

            _resourceListRoot = resourceListRoot;
            _iconResourcesRoots = iconResourcesRoots ?? Array.Empty<string>();
            _changeVisibleSeconds = changeVisibleSeconds;
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
                    item.Root.gameObject.SetActive(false);
                    continue;
                }

                RenderRow(item, rows[i]);
            }

            _hasSnapshot = true;
        }

        public void StopAllHideCoroutines()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null || item.ChangeNumText == null)
                {
                    continue;
                }

                if (item.HideCoroutine != null)
                {
                    _coroutineOwner.StopCoroutine(item.HideCoroutine);
                    item.HideCoroutine = null;
                }

                item.ChangeNumText.gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            StopAllHideCoroutines();
            _items.Clear();
            _lastAmounts.Clear();
            _iconCache.Clear();
            _hasSnapshot = false;
        }

        private void RenderRow(ResourceItemBinding item, ResourceHudRowState row)
        {
            row ??= new ResourceHudRowState(string.Empty, 0);
            item.Root.gameObject.SetActive(true);
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

            if (item.HideCoroutine != null)
            {
                _coroutineOwner.StopCoroutine(item.HideCoroutine);
                item.HideCoroutine = null;
            }

            if (delta == 0)
            {
                item.ChangeNumText.gameObject.SetActive(false);
                return;
            }

            item.ChangeNumText.gameObject.SetActive(true);
            item.ChangeNumText.color = delta > 0 ? _increaseColor : _decreaseColor;
            item.ChangeNumText.text = delta > 0 ? $"+{delta}" : delta.ToString();
            item.HideCoroutine = _coroutineOwner.StartCoroutine(HideChangeAfterDelay(item));
        }

        private IEnumerator HideChangeAfterDelay(ResourceItemBinding item)
        {
            var delay = Mathf.Max(0.05f, _changeVisibleSeconds);
            yield return new WaitForSecondsRealtime(delay);

            if (item != null && item.ChangeNumText != null)
            {
                item.ChangeNumText.gameObject.SetActive(false);
                item.HideCoroutine = null;
            }
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
    }
}
