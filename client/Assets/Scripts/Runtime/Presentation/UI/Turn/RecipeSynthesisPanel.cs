using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Cache;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class RecipeSynthesisPanel : MonoBehaviour
    {
        [Serializable] private sealed class Root { public RecipeCfg[] recipes; public RecipeCfg[] entries; }
        [Serializable] private sealed class RecipeCfg
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string building_id;
            public int work_amount;
            public int base_progress;
            public int sort_order;
            public AmountCfg[] resource_inputs;
            public AmountCfg[] point_inputs;
            public OutputCfg outputs;
        }
        [Serializable] private sealed class OutputCfg { public AmountCfg[] resources; public string[] units; public AmountCfg[] point_progress; }
        [Serializable] private sealed class AmountCfg { public string key; public int amount; }

        private sealed class RecipeViewData
        {
            public string Id;
            public string Name;
            public string IconKey;
            public int TurnCost;
            public int ProduceAmount;
            public int SortOrder;
            public readonly List<RecipeSynthesisItemView.IngredientViewData> Inputs = new();
            public readonly List<RecipeSynthesisItemView.IngredientViewData> Outputs = new();
        }

        [Header("Refs")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private ScrollRect listScrollRect;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private RecipeSynthesisItemView recipeItemPrefab;
        [SerializeField] private Button closeButton;

        [Header("Data Source")]
        [SerializeField] private bool startHidden = true;
        [SerializeField] private bool preferServerPushedConfig = true;
        [SerializeField] private bool listenServerUpdates = true;
        [SerializeField] private string[] serverConfigKeys = { "recipeconfig", "recipesconfig", "recipe_catalog", "recipes" };
        [SerializeField] private string[] iconRoots = { "Icons/Recipes", "Icons/Resources", "Icons/Units", "Icons/Points" };

        private readonly List<RecipeSynthesisItemView> _itemViews = new();
        private StaticCatalogCache _catalog;
        private ConfigCache _config;

        private void Awake()
        {
            if (panelRoot == null) panelRoot = transform as RectTransform;
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
                closeButton.onClick.AddListener(Hide);
            }

            if (startHidden) Hide();
        }

        private void OnEnable()
        {
            _catalog = StaticCatalogCache.EnsureInstance();
            if (_catalog != null) { _catalog.CatalogChanged -= RefreshList; _catalog.CatalogChanged += RefreshList; }
            if (listenServerUpdates)
            {
                _config = ConfigCache.EnsureInstance();
                if (_config != null) { _config.ConfigUpdated -= OnConfigUpdated; _config.ConfigUpdated += OnConfigUpdated; }
            }
            RefreshList();
        }

        private void OnDisable()
        {
            if (_catalog != null) _catalog.CatalogChanged -= RefreshList;
            if (_config != null) _config.ConfigUpdated -= OnConfigUpdated;
        }

        public void Show() { if (panelRoot != null) panelRoot.gameObject.SetActive(true); RefreshList(); }
        public void Hide() { if (panelRoot != null) panelRoot.gameObject.SetActive(false); }

        private void OnConfigUpdated(string key)
        {
            var normalized = Key(key);
            for (var i = 0; i < serverConfigKeys.Length; i++)
            {
                if (normalized == Key(serverConfigKeys[i])) { RefreshList(); return; }
            }
        }

        private void RefreshList()
        {
            if (listContent == null) return;
            ClearItems();
            var recipes = LoadRecipeData();
            recipes.Sort((a, b) =>
            {
                var sortCmp = a.SortOrder.CompareTo(b.SortOrder);
                if (sortCmp != 0) return sortCmp;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            for (var i = 0; i < recipes.Count; i++)
            {
                var item = CreateItem();
                if (item == null) continue;
                var r = recipes[i];
                item.Configure(r.Outputs, r.Inputs, r.ProduceAmount, r.TurnCost);
                item.SetQuantity(0);
            }
        }

        private List<RecipeViewData> LoadRecipeData()
        {
            var fromConfig = LoadFromConfig();
            if (fromConfig.Count > 0) return fromConfig;
            return LoadFromCatalog();
        }

        private List<RecipeViewData> LoadFromConfig()
        {
            var result = new List<RecipeViewData>();
            if (!preferServerPushedConfig || _config == null) return result;
            for (var i = 0; i < serverConfigKeys.Length; i++)
            {
                if (!_config.TryGetJson(serverConfigKeys[i], out var json) || string.IsNullOrWhiteSpace(json)) continue;
                Root root = null; try { root = JsonUtility.FromJson<Root>(json); } catch { }
                var source = root?.recipes != null && root.recipes.Length > 0 ? root.recipes : root?.entries;
                if (source == null || source.Length == 0) continue;
                for (var r = 0; r < source.Length; r++)
                {
                    var cfg = source[r]; if (cfg == null || string.IsNullOrWhiteSpace(cfg.id)) continue;
                    result.Add(BuildRecipeFromConfig(cfg));
                }
                if (result.Count > 0) return result;
            }
            return result;
        }

        private RecipeViewData BuildRecipeFromConfig(RecipeCfg cfg)
        {
            var view = new RecipeViewData
            {
                Id = cfg.id.Trim(),
                Name = string.IsNullOrWhiteSpace(cfg.name) ? cfg.id.Trim() : cfg.name.Trim(),
                IconKey = cfg.icon_key ?? string.Empty,
                TurnCost = Mathf.Max(1, cfg.work_amount),
                ProduceAmount = Mathf.Max(1, cfg.base_progress),
                SortOrder = cfg.sort_order
            };

            AddAmounts(view.Inputs, cfg.resource_inputs);
            AddAmounts(view.Inputs, cfg.point_inputs);
            if (cfg.outputs != null)
            {
                AddAmounts(view.Outputs, cfg.outputs.resources);
                AddAmounts(view.Outputs, cfg.outputs.point_progress);
                if (cfg.outputs.units != null)
                {
                    for (var i = 0; i < cfg.outputs.units.Length; i++)
                    {
                        var id = (cfg.outputs.units[i] ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(id))
                        {
                            view.Outputs.Add(new RecipeSynthesisItemView.IngredientViewData(id, 1, LoadIcon(id)));
                        }
                    }
                }
            }

            if (view.Outputs.Count == 0)
            {
                view.Outputs.Add(new RecipeSynthesisItemView.IngredientViewData(view.IconKey, 1, LoadIcon(view.IconKey)));
            }

            if (view.Inputs.Count == 0)
            {
                view.Inputs.Add(new RecipeSynthesisItemView.IngredientViewData("input", 1, null));
            }

            view.ProduceAmount = Mathf.Max(1, view.Outputs.Sum(x => Mathf.Max(1, x.Amount)));
            return view;
        }

        private List<RecipeViewData> LoadFromCatalog()
        {
            var result = new List<RecipeViewData>();
            if (_catalog == null || _catalog.Recipes == null) return result;
            foreach (var pair in _catalog.Recipes)
            {
                var r = pair.Value; if (r == null || string.IsNullOrWhiteSpace(r.id)) continue;
                var view = new RecipeViewData
                {
                    Id = r.id.Trim(),
                    Name = string.IsNullOrWhiteSpace(r.name) ? r.id.Trim() : r.name.Trim(),
                    IconKey = r.icon_key ?? string.Empty,
                    TurnCost = Mathf.Max(1, r.work_amount),
                    ProduceAmount = Mathf.Max(1, r.base_progress),
                    SortOrder = r.sort_order
                };

                AddAmounts(view.Inputs, r.resource_inputs);
                AddAmounts(view.Inputs, r.point_inputs);
                if (r.outputs != null)
                {
                    AddAmounts(view.Outputs, r.outputs.resources);
                    AddAmounts(view.Outputs, r.outputs.point_progress);
                    if (r.outputs.units != null)
                    {
                        for (var i = 0; i < r.outputs.units.Length; i++)
                        {
                            var unit = (r.outputs.units[i] ?? string.Empty).Trim();
                            if (!string.IsNullOrEmpty(unit))
                            {
                                view.Outputs.Add(new RecipeSynthesisItemView.IngredientViewData(unit, 1, LoadIcon(unit)));
                            }
                        }
                    }
                }

                if (view.Outputs.Count == 0)
                {
                    view.Outputs.Add(new RecipeSynthesisItemView.IngredientViewData(view.IconKey, 1, LoadIcon(view.IconKey)));
                }

                if (view.Inputs.Count == 0)
                {
                    view.Inputs.Add(new RecipeSynthesisItemView.IngredientViewData("input", 1, null));
                }

                view.ProduceAmount = Mathf.Max(1, view.Outputs.Sum(x => Mathf.Max(1, x.Amount)));
                result.Add(view);
            }
            return result;
        }

        private void AddAmounts(List<RecipeSynthesisItemView.IngredientViewData> target, AmountCfg[] source)
        {
            if (target == null || source == null) return;
            for (var i = 0; i < source.Length; i++)
            {
                var amount = source[i];
                if (amount == null || string.IsNullOrWhiteSpace(amount.key)) continue;
                var key = amount.key.Trim();
                target.Add(new RecipeSynthesisItemView.IngredientViewData(key, Mathf.Max(0, amount.amount), LoadIcon(key)));
            }
        }

        private void AddAmounts(List<RecipeSynthesisItemView.IngredientViewData> target, StaticCatalogCache.IntAmountEntryJson[] source)
        {
            if (target == null || source == null) return;
            for (var i = 0; i < source.Length; i++)
            {
                var amount = source[i];
                if (amount == null || string.IsNullOrWhiteSpace(amount.key)) continue;
                var key = amount.key.Trim();
                target.Add(new RecipeSynthesisItemView.IngredientViewData(key, Mathf.Max(0, amount.amount), LoadIcon(key)));
            }
        }

        private Sprite LoadIcon(string key)
        {
            key = (key ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(key)) return null;
            for (var i = 0; i < iconRoots.Length; i++)
            {
                var root = (iconRoots[i] ?? string.Empty).Trim().Trim('/');
                if (string.IsNullOrEmpty(root)) continue;
                var sprite = Resources.Load<Sprite>($"{root}/{key}");
                if (sprite != null) return sprite;
            }
            return Resources.Load<Sprite>(key);
        }

        private RecipeSynthesisItemView CreateItem()
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
            _itemViews.Add(view);
            return view;
        }

        private void ClearItems()
        {
            for (var i = 0; i < _itemViews.Count; i++)
            {
                if (_itemViews[i] != null)
                {
                    Destroy(_itemViews[i].gameObject);
                }
            }
            _itemViews.Clear();
        }

        private static string Key(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
