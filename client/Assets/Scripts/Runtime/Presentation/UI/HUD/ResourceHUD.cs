/*************************************************
 * Project: Panoptes
 * File: ResourceHUD.cs
 * Author: Panoptes Team
 * Date: 2026-04-16
 * Description: Dynamic resource panel HUD with per-item delta hints and tech button.
 *************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.UI.Domestic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class ResourceHUD : MonoBehaviour
    {
        private readonly struct HudEntry
        {
            public readonly string Key;
            public readonly int Amount;
            public readonly string IconKey;
            public readonly bool IsPoint;

            public HudEntry(string key, int amount, string iconKey, bool isPoint)
            {
                Key = key;
                Amount = amount;
                IconKey = iconKey;
                IsPoint = isPoint;
            }
        }

        private sealed class ResourceItemBinding
        {
            public RectTransform root;
            public Image icon;
            public TMP_Text baseNumText;
            public TMP_Text changeNumText;
            public Coroutine hideCoroutine;
            public string key = string.Empty;
        }

        [Header("Root")]
        [SerializeField] private RectTransform resourceListRoot;
        [SerializeField] private Button techButton;
        [SerializeField] private TechTreePanelController techTreePanelController;

        [Header("Data")]
        [SerializeField] private bool includePoints = true;
        [SerializeField] private bool logWarnings = false;

        [Header("Icons")]
        [SerializeField] private string[] iconResourcesRoots = { "Icons/Resources", "Icons/Points", "Icons" };

        [Header("Change Hint")]
        [SerializeField] private float changeVisibleSeconds = 3f;
        [SerializeField] private Color increaseColor = new Color(0.15f, 0.95f, 0.35f, 1f);
        [SerializeField] private Color decreaseColor = new Color(0.95f, 0.25f, 0.25f, 1f);

        private readonly List<ResourceItemBinding> _items = new();
        private readonly Dictionary<string, int> _lastAmounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly EventSubscriptionBag _subscriptions = new();
        private bool _hasSnapshot;
        private bool _techButtonBound;

        private void OnEnable()
        {
            Subscribe();
            BindTechButton();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnbindTechButton();
            StopAllHideCoroutines();
        }

        private void Subscribe()
        {
            _subscriptions.Clear();

            var gameState = GameStateCache.Instance;
            if (gameState != null)
            {
                _subscriptions.Add(
                    () => gameState.OnStateChanged += Refresh,
                    () => gameState.OnStateChanged -= Refresh);
            }

            var catalog = StaticCatalogCache.Instance;
            if (catalog != null)
            {
                _subscriptions.Add(
                    () => catalog.CatalogChanged += Refresh,
                    () => catalog.CatalogChanged -= Refresh);
            }
        }

        private void Unsubscribe()
        {
            _subscriptions.Clear();
        }

        private void BindTechButton()
        {
            if (techButton == null)
            {
                var techBtnTransform = transform.Find("TechBtn");
                if (techBtnTransform != null)
                {
                    techButton = techBtnTransform.GetComponent<Button>();
                }
            }

            if (techButton == null || _techButtonBound)
            {
                return;
            }

            techButton.onClick.AddListener(OnTechButtonClicked);
            _techButtonBound = true;
        }

        private void UnbindTechButton()
        {
            if (!_techButtonBound || techButton == null)
            {
                return;
            }

            techButton.onClick.RemoveListener(OnTechButtonClicked);
            _techButtonBound = false;
        }

        private void OnTechButtonClicked()
        {
            ResolveTechTreePanelController();
            if (techTreePanelController == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[ResourceHUD] TechTreePanelController not found.");
                }
                return;
            }

            var panelGo = techTreePanelController.gameObject;
            var nextState = !panelGo.activeSelf;
            panelGo.SetActive(nextState);
            if (nextState && techTreePanelController.transform is RectTransform panelRect)
            {
                panelRect.SetAsLastSibling();
            }
        }

        private void ResolveTechTreePanelController()
        {
            if (techTreePanelController != null)
            {
                return;
            }

            techTreePanelController = SceneObjectFinder.FindFirstSceneObject<TechTreePanelController>();
        }

        private void Refresh()
        {
            EnsureItemBindings();
            if (resourceListRoot == null || _items.Count == 0)
            {
                return;
            }

            var entries = BuildEntries();
            EnsureItemCount(entries.Count);

            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null || item.root == null)
                {
                    continue;
                }

                if (i >= entries.Count)
                {
                    item.root.gameObject.SetActive(false);
                    continue;
                }

                var entry = entries[i];
                item.root.gameObject.SetActive(true);
                item.key = entry.Key;

                if (item.icon != null)
                {
                    var icon = LoadIcon(entry.IconKey);
                    if (icon != null)
                    {
                        item.icon.sprite = icon;
                    }
                    item.icon.preserveAspect = true;
                }

                if (item.baseNumText != null)
                {
                    item.baseNumText.text = entry.Amount.ToString();
                }

                var previous = 0;
                var hasPrevious = _hasSnapshot && _lastAmounts.TryGetValue(entry.Key, out previous);
                var delta = hasPrevious ? entry.Amount - previous : 0;
                ApplyChangeDelta(item, delta);

                _lastAmounts[entry.Key] = entry.Amount;
            }

            _hasSnapshot = true;
        }

        private void EnsureItemBindings()
        {
            if (resourceListRoot == null)
            {
                var list = transform.Find("ResourceList");
                resourceListRoot = list as RectTransform;
            }

            if (resourceListRoot == null)
            {
                return;
            }

            if (_items.Count > 0)
            {
                return;
            }

            for (var i = 0; i < resourceListRoot.childCount; i++)
            {
                var child = resourceListRoot.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                var binding = CreateBinding(child);
                if (binding != null)
                {
                    _items.Add(binding);
                }
            }
        }

        private void EnsureItemCount(int count)
        {
            if (resourceListRoot == null || count <= _items.Count)
            {
                return;
            }

            if (_items.Count == 0 || _items[0] == null || _items[0].root == null)
            {
                return;
            }

            var template = _items[0].root.gameObject;
            while (_items.Count < count)
            {
                var clone = Instantiate(template, resourceListRoot, false);
                clone.name = $"ResourceItem ({_items.Count})";
                var binding = CreateBinding(clone.transform as RectTransform);
                if (binding == null)
                {
                    break;
                }

                _items.Add(binding);
            }
        }

        private ResourceItemBinding CreateBinding(RectTransform root)
        {
            if (root == null)
            {
                return null;
            }

            var binding = new ResourceItemBinding
            {
                root = root,
                icon = root.Find("Image")?.GetComponent<Image>(),
                baseNumText = root.Find("BaseNum")?.GetComponent<TMP_Text>(),
                changeNumText = root.Find("ChangeNum")?.GetComponent<TMP_Text>()
            };

            if (binding.changeNumText != null)
            {
                binding.changeNumText.gameObject.SetActive(false);
            }

            return binding;
        }

        private List<HudEntry> BuildEntries()
        {
            var entries = new List<HudEntry>();
            var cache = GameStateCache.Instance;
            var resourceAmounts = cache != null
                ? cache.GetMyResourceAmounts()
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var pointAmounts = cache != null
                ? cache.GetMyPointAmounts()
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var catalog = StaticCatalogCache.Instance;

            if (catalog != null && catalog.Resources.Count > 0)
            {
                foreach (var resource in catalog.Resources.Values
                             .Where(item => item != null && item.visible_in_hud)
                             .OrderBy(item => item.sort_order)
                             .ThenBy(item => item.key, StringComparer.OrdinalIgnoreCase))
                {
                    var key = (resource.key ?? string.Empty).Trim();
                    resourceAmounts.TryGetValue(key, out var amount);
                    entries.Add(new HudEntry(key, amount, resource.icon_key, false));
                }
            }

            if (includePoints && catalog != null && catalog.Points.Count > 0)
            {
                foreach (var point in catalog.Points.Values
                             .Where(item => item != null && item.visible_in_hud)
                             .OrderBy(item => item.sort_order)
                             .ThenBy(item => item.key, StringComparer.OrdinalIgnoreCase))
                {
                    var key = (point.key ?? string.Empty).Trim();
                    pointAmounts.TryGetValue(key, out var amount);
                    entries.Add(new HudEntry(key, amount, point.icon_key, true));
                }
            }

            if (entries.Count == 0)
            {
                entries.AddRange(resourceAmounts
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => new HudEntry(pair.Key, pair.Value, string.Empty, false)));

                if (includePoints)
                {
                    entries.AddRange(pointAmounts
                        .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(pair => new HudEntry(pair.Key, pair.Value, string.Empty, true)));
                }
            }

            return entries;
        }

        private void ApplyChangeDelta(ResourceItemBinding item, int delta)
        {
            if (item == null || item.changeNumText == null)
            {
                return;
            }

            if (item.hideCoroutine != null)
            {
                StopCoroutine(item.hideCoroutine);
                item.hideCoroutine = null;
            }

            if (delta == 0)
            {
                item.changeNumText.gameObject.SetActive(false);
                return;
            }

            item.changeNumText.gameObject.SetActive(true);
            item.changeNumText.color = delta > 0 ? increaseColor : decreaseColor;
            item.changeNumText.text = delta > 0 ? $"+{delta}" : delta.ToString();
            item.hideCoroutine = StartCoroutine(HideChangeAfterDelay(item));
        }

        private IEnumerator HideChangeAfterDelay(ResourceItemBinding item)
        {
            var delay = Mathf.Max(0.05f, changeVisibleSeconds);
            yield return new WaitForSecondsRealtime(delay);

            if (item != null && item.changeNumText != null)
            {
                item.changeNumText.gameObject.SetActive(false);
                item.hideCoroutine = null;
            }
        }

        private void StopAllHideCoroutines()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null || item.changeNumText == null)
                {
                    continue;
                }

                if (item.hideCoroutine != null)
                {
                    StopCoroutine(item.hideCoroutine);
                    item.hideCoroutine = null;
                }

                item.changeNumText.gameObject.SetActive(false);
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
            for (var i = 0; i < iconResourcesRoots.Length; i++)
            {
                var root = (iconResourcesRoots[i] ?? string.Empty).Trim().Trim('/');
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
