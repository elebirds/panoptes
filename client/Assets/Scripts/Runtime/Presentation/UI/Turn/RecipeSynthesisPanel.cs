using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Events;
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
            public string BuildingId;
            public int TurnCost;
            public int ProduceAmount;
            public int SortOrder;
            public bool IsLocked;
            public readonly List<RecipeSynthesisItemView.IngredientViewData> Inputs = new();
            public readonly List<RecipeSynthesisItemView.IngredientViewData> Outputs = new();
        }

        [Header("Refs")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private ScrollRect listScrollRect;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private RecipeSynthesisItemView recipeItemPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private BuildPanelSlideToggle slideToggle;

        [Header("Data Source")]
        [SerializeField] private bool startHidden = true;
        [SerializeField] private bool preferServerPushedConfig = true;
        [SerializeField] private bool listenServerUpdates = true;
        [SerializeField] private string[] serverConfigKeys = { "recipeconfig", "recipesconfig", "recipe_catalog", "recipes" };
        [SerializeField] private string[] iconRoots = { "Icons/Recipes", "Icons/Resources", "Icons/Units", "Icons/Points" };

        [Header("Locked Visual")]
        [SerializeField] private Sprite lockedStateIcon;
        [SerializeField] private string lockedStateText = "Locked";

        private readonly List<RecipeSynthesisItemView> _itemViews = new();
        private readonly Dictionary<string, RecipeSynthesisItemView> _itemByRecipeId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _quantityByRecipeId = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _knownLockedRecipeIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _knownUnlockedRecipeIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _recipeUnlockTechMap = new(StringComparer.OrdinalIgnoreCase);

        private StaticCatalogCache _catalog;
        private ConfigCache _config;
        private GameStateCache _stateCache;
        private bool _lastVisible;
        private string _activeNodeId = string.Empty;
        private string _activeBuildingTypeId = string.Empty;
        private string _activeOwnerPlayerId = string.Empty;
        private string _selectedRecipeId = string.Empty;

        public event Action<bool> VisibilityChanged;

        public bool IsVisible => ComputeVisible();

        private void Awake()
        {
            if (panelRoot == null) panelRoot = transform as RectTransform;
            if (slideToggle == null)
            {
                slideToggle = GetComponentInParent<BuildPanelSlideToggle>(true);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
                closeButton.onClick.AddListener(Hide);
            }

            if (startHidden)
            {
                Hide(immediate: true);
            }
        }

        private void OnEnable()
        {
            _catalog = StaticCatalogCache.EnsureInstance();
            if (_catalog != null)
            {
                _catalog.CatalogChanged -= OnCatalogChanged;
                _catalog.CatalogChanged += OnCatalogChanged;
            }

            if (listenServerUpdates)
            {
                _config = ConfigCache.EnsureInstance();
                if (_config != null)
                {
                    _config.ConfigUpdated -= OnConfigUpdated;
                    _config.ConfigUpdated += OnConfigUpdated;
                }
            }

            _stateCache = GameStateCache.Instance;
            if (_stateCache != null)
            {
                _stateCache.OnGameError -= OnGameError;
                _stateCache.OnGameError += OnGameError;
                _stateCache.OnStateChanged -= OnStateChanged;
                _stateCache.OnStateChanged += OnStateChanged;
            }

            RefreshList();
            UpdateVisibilityIfChanged(force: true);
        }

        private void OnDisable()
        {
            if (_catalog != null) _catalog.CatalogChanged -= OnCatalogChanged;
            if (_config != null) _config.ConfigUpdated -= OnConfigUpdated;
            if (_stateCache != null)
            {
                _stateCache.OnGameError -= OnGameError;
                _stateCache.OnStateChanged -= OnStateChanged;
            }
        }

        private void LateUpdate()
        {
            UpdateVisibilityIfChanged(force: false);
        }

        public void OpenForBuilding(string nodeId, string buildingTypeId, string ownerPlayerId = null)
        {
            _activeNodeId = string.IsNullOrWhiteSpace(nodeId) ? string.Empty : nodeId.Trim();
            _activeBuildingTypeId = NormalizeToken(buildingTypeId);
            _activeOwnerPlayerId = string.IsNullOrWhiteSpace(ownerPlayerId) ? string.Empty : ownerPlayerId.Trim();
            SeedSelectionFromState();
            Show();
        }

        public void Show()
        {
            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(true);
            }

            if (slideToggle != null)
            {
                slideToggle.Expand();
            }

            RefreshList();
            UpdateVisibilityIfChanged(force: true);
        }

        public void Hide()
        {
            Hide(immediate: false);
        }

        public float GetPanelWidth()
        {
            if (panelRoot == null)
            {
                return 0f;
            }

            var width = Mathf.Abs(panelRoot.rect.width);
            if (width > 0.01f)
            {
                return width;
            }

            return panelRoot.sizeDelta.x;
        }

        public float GetSlideDuration()
        {
            if (slideToggle != null)
            {
                return slideToggle.GetDuration();
            }

            return 0.22f;
        }

        private void Hide(bool immediate)
        {
            if (slideToggle != null)
            {
                slideToggle.SetCollapsed(true, immediate);
            }
            else if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(false);
            }

            UpdateVisibilityIfChanged(force: true);
        }

        private void OnCatalogChanged()
        {
            RefreshList();
        }

        private void OnStateChanged()
        {
            if (!IsVisible)
            {
                return;
            }

            RefreshList();
        }

        private void OnConfigUpdated(string key)
        {
            var normalized = NormalizeToken(key);
            for (var i = 0; i < serverConfigKeys.Length; i++)
            {
                if (normalized == NormalizeToken(serverConfigKeys[i]))
                {
                    RefreshList();
                    return;
                }
            }
        }

        private void OnGameError(GameErrorEvent evt)
        {
            if (evt == null || !string.Equals(NormalizeToken(evt.Code), "invalid_directive", StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(evt.Message))
            {
                return;
            }

            var raw = evt.Message.Trim();
            var parts = raw.Split(':');
            if (parts.Length < 2)
            {
                return;
            }

            var nodeId = parts[0].Trim();
            var recipeId = parts[1].Trim();
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(recipeId))
            {
                return;
            }

            if (!string.Equals(nodeId, _activeNodeId, StringComparison.Ordinal))
            {
                return;
            }

            recipeId = NormalizeToken(recipeId);
            if (!IsRecipeForActiveBuilding(recipeId))
            {
                return;
            }

            _knownLockedRecipeIds.Add(recipeId);
            _knownUnlockedRecipeIds.Remove(recipeId);

            if (_itemByRecipeId.TryGetValue(recipeId, out var view) && view != null)
            {
                view.SetLockedVisual(lockedStateIcon, lockedStateText);
                view.SetLocked(true);
                view.SetQuantity(0, notify: false);
            }

            if (string.Equals(_selectedRecipeId, recipeId, StringComparison.Ordinal))
            {
                _selectedRecipeId = string.Empty;
            }
        }

        private void RefreshList()
        {
            if (listContent == null)
            {
                return;
            }

            ClearItems();
            var recipes = LoadRecipeData();
            recipes = FilterByBuilding(recipes);
            ApplyLockState(recipes);

            recipes.Sort((a, b) =>
            {
                var sortCmp = a.SortOrder.CompareTo(b.SortOrder);
                if (sortCmp != 0) return sortCmp;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                var item = CreateItem();
                if (item == null)
                {
                    continue;
                }

                item.SetRecipeId(recipe.Id);
                item.Configure(recipe.Outputs, recipe.Inputs, recipe.ProduceAmount, recipe.TurnCost);
                item.SetLockedVisual(lockedStateIcon, lockedStateText);
                item.SetLocked(recipe.IsLocked);
                item.SetQuantity(ResolveInitialQuantity(recipe.Id), notify: false);
                item.QuantityChanged -= OnItemQuantityChanged;
                item.QuantityChanged += OnItemQuantityChanged;

                _itemByRecipeId[NormalizeToken(recipe.Id)] = item;
            }
        }

        private List<RecipeViewData> FilterByBuilding(List<RecipeViewData> recipes)
        {
            if (recipes == null || recipes.Count == 0)
            {
                return new List<RecipeViewData>();
            }

            if (string.IsNullOrWhiteSpace(_activeBuildingTypeId))
            {
                return recipes;
            }

            var buildingType = NormalizeToken(_activeBuildingTypeId);
            var filtered = new List<RecipeViewData>(recipes.Count);
            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe == null)
                {
                    continue;
                }

                if (NormalizeToken(recipe.BuildingId) == buildingType)
                {
                    filtered.Add(recipe);
                }
            }

            return filtered;
        }

        private void ApplyLockState(List<RecipeViewData> recipes)
        {
            if (recipes == null)
            {
                return;
            }

            BuildRecipeUnlockTechIndex();
            var completedTech = GetCompletedTechnologySet();
            var ownedByMe = IsActiveNodeOwnedByMe();

            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe == null)
                {
                    continue;
                }

                var recipeId = NormalizeToken(recipe.Id);
                if (!ownedByMe)
                {
                    recipe.IsLocked = true;
                    continue;
                }

                if (_knownUnlockedRecipeIds.Contains(recipeId))
                {
                    recipe.IsLocked = false;
                    continue;
                }

                if (_knownLockedRecipeIds.Contains(recipeId))
                {
                    recipe.IsLocked = true;
                    continue;
                }

                var isCurrentSelection = string.Equals(recipeId, NormalizeToken(_selectedRecipeId), StringComparison.Ordinal);
                if (isCurrentSelection)
                {
                    recipe.IsLocked = false;
                    continue;
                }

                if (_recipeUnlockTechMap.TryGetValue(recipeId, out var unlockTechIds) && unlockTechIds != null && unlockTechIds.Count > 0)
                {
                    var unlocked = false;
                    foreach (var techId in unlockTechIds)
                    {
                        if (completedTech.Contains(techId))
                        {
                            unlocked = true;
                            break;
                        }
                    }

                    recipe.IsLocked = !unlocked;
                }
                else
                {
                    recipe.IsLocked = false;
                }
            }
        }

        private void BuildRecipeUnlockTechIndex()
        {
            _recipeUnlockTechMap.Clear();
            if (_catalog == null || _catalog.Technologies == null || _catalog.Technologies.Count == 0)
            {
                return;
            }

            foreach (var pair in _catalog.Technologies)
            {
                var tech = pair.Value;
                if (tech == null || string.IsNullOrWhiteSpace(tech.id) || tech.explicit_effects == null)
                {
                    continue;
                }

                var techId = NormalizeToken(tech.id);
                for (var i = 0; i < tech.explicit_effects.Length; i++)
                {
                    var effect = tech.explicit_effects[i];
                    if (effect == null)
                    {
                        continue;
                    }

                    if (!string.Equals(NormalizeToken(effect.type), "unlock_recipe", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var recipeId = NormalizeToken(effect.target_id);
                    if (string.IsNullOrWhiteSpace(recipeId))
                    {
                        continue;
                    }

                    if (!_recipeUnlockTechMap.TryGetValue(recipeId, out var unlockTechIds))
                    {
                        unlockTechIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        _recipeUnlockTechMap[recipeId] = unlockTechIds;
                    }

                    unlockTechIds.Add(techId);
                }
            }
        }

        private HashSet<string> GetCompletedTechnologySet()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cache = _stateCache ?? GameStateCache.Instance;
            var completedIds = cache != null ? cache.GetCompletedTechnologyIds() : null;
            if (completedIds == null || completedIds.Count == 0)
            {
                return set;
            }

            for (var i = 0; i < completedIds.Count; i++)
            {
                var id = NormalizeToken(completedIds[i]);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    set.Add(id);
                }
            }

            return set;
        }

        private bool IsActiveNodeOwnedByMe()
        {
            if (string.IsNullOrWhiteSpace(_activeNodeId))
            {
                return true;
            }

            var cache = _stateCache ?? GameStateCache.Instance;
            if (cache == null)
            {
                return true;
            }

            var node = cache.GetNode(_activeNodeId);
            if (node == null)
            {
                return true;
            }

            var me = NormalizeToken(cache.MyPlayerID);
            var owner = NormalizeToken(!string.IsNullOrWhiteSpace(_activeOwnerPlayerId) ? _activeOwnerPlayerId : node.Owner);
            var territoryOwner = NormalizeToken(node.TerritoryOwner);
            if (string.IsNullOrWhiteSpace(me))
            {
                return true;
            }

            return string.Equals(owner, me, StringComparison.Ordinal) || string.Equals(territoryOwner, me, StringComparison.Ordinal);
        }

        private void SeedSelectionFromState()
        {
            _quantityByRecipeId.Clear();
            _selectedRecipeId = string.Empty;

            if (string.IsNullOrWhiteSpace(_activeNodeId))
            {
                return;
            }

            var draft = PlanningDraftCache.Instance;
            if (draft != null && draft.RecipeSelections != null)
            {
                for (var i = draft.RecipeSelections.Count - 1; i >= 0; i--)
                {
                    var selection = draft.RecipeSelections[i];
                    if (selection == null || !string.Equals(selection.NodeId, _activeNodeId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(selection.RecipeId))
                    {
                        _selectedRecipeId = NormalizeToken(selection.RecipeId);
                        _quantityByRecipeId[_selectedRecipeId] = 1;
                        return;
                    }
                }
            }

            var cache = _stateCache ?? GameStateCache.Instance;
            var node = cache?.GetNode(_activeNodeId);
            if (node != null && !string.IsNullOrWhiteSpace(node.OperationSelectedRecipeId))
            {
                _selectedRecipeId = NormalizeToken(node.OperationSelectedRecipeId);
                _quantityByRecipeId[_selectedRecipeId] = 1;
            }
        }

        private int ResolveInitialQuantity(string recipeId)
        {
            var key = NormalizeToken(recipeId);
            if (string.IsNullOrWhiteSpace(key))
            {
                return 0;
            }

            if (_quantityByRecipeId.TryGetValue(key, out var amount))
            {
                return Mathf.Max(0, amount);
            }

            return string.Equals(_selectedRecipeId, key, StringComparison.Ordinal) ? 1 : 0;
        }

        private void OnItemQuantityChanged(RecipeSynthesisItemView item, int quantity)
        {
            if (item == null || item.IsLocked)
            {
                return;
            }

            var recipeId = NormalizeToken(item.RecipeId);
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return;
            }

            _quantityByRecipeId[recipeId] = Mathf.Max(0, quantity);
            if (quantity <= 0)
            {
                if (string.Equals(_selectedRecipeId, recipeId, StringComparison.Ordinal))
                {
                    _selectedRecipeId = string.Empty;
                }
                return;
            }

            _selectedRecipeId = recipeId;
            _knownUnlockedRecipeIds.Add(recipeId);
            _knownLockedRecipeIds.Remove(recipeId);

            for (var i = 0; i < _itemViews.Count; i++)
            {
                var other = _itemViews[i];
                if (other == null || ReferenceEquals(other, item))
                {
                    continue;
                }

                other.SetQuantity(0, notify: false);
            }

            if (!string.IsNullOrWhiteSpace(_activeNodeId))
            {
                GameIntents.SetBuildingRecipe(_activeNodeId, recipeId);
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
                BuildingId = NormalizeToken(cfg.building_id),
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
                    BuildingId = NormalizeToken(r.building_id),
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
                var item = _itemViews[i];
                if (item != null)
                {
                    item.QuantityChanged -= OnItemQuantityChanged;
                    Destroy(item.gameObject);
                }
            }
            _itemViews.Clear();
            _itemByRecipeId.Clear();
        }

        private bool ComputeVisible()
        {
            if (panelRoot == null)
            {
                return false;
            }

            if (!panelRoot.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (slideToggle != null)
            {
                return !slideToggle.IsCollapsed;
            }

            return panelRoot.gameObject.activeSelf;
        }

        private void UpdateVisibilityIfChanged(bool force)
        {
            var visible = ComputeVisible();
            if (!force && visible == _lastVisible)
            {
                return;
            }

            _lastVisible = visible;
            VisibilityChanged?.Invoke(visible);
        }

        private bool IsRecipeForActiveBuilding(string recipeId)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return false;
            }

            var normalizedRecipeId = NormalizeToken(recipeId);
            if (_catalog == null || _catalog.Recipes == null || !_catalog.TryGetRecipe(normalizedRecipeId, out var recipe) || recipe == null)
            {
                return _itemByRecipeId.ContainsKey(normalizedRecipeId);
            }

            if (string.IsNullOrWhiteSpace(_activeBuildingTypeId))
            {
                return true;
            }

            return string.Equals(NormalizeToken(recipe.building_id), NormalizeToken(_activeBuildingTypeId), StringComparison.Ordinal);
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
