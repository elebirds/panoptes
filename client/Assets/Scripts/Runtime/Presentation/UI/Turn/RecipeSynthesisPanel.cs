using System;
using System.Collections.Generic;
using System.Linq;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Feedback;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class RecipeSynthesisPanel : MonoBehaviour
    {
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
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private BuildPanelSlideToggle slideToggle;
        [SerializeField] private bool keepSlideToggleButtonVisible = true;

        [Header("Data Source")]
        [SerializeField] private bool startHidden = true;
        [SerializeField] private string[] iconRoots = { "Icons/Recipes", "Icons/Resources", "Icons/Units", "Icons/Points" };

        [Header("Locked Visual")]
        [SerializeField] private Sprite lockedStateIcon;
        [SerializeField] private string lockedStateText = "Locked";

        private readonly List<RecipeSynthesisItemView> _itemViews = new();
        private readonly Dictionary<string, RecipeSynthesisItemView> _itemByRecipeId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _quantityByRecipeId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, int>> _quantityByNodeId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _selectedRecipeByNodeId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _lastSentRecipeByNodeId = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _knownLockedRecipeIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _knownUnlockedRecipeIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _recipeUnlockTechMap = new(StringComparer.OrdinalIgnoreCase);

        private StaticCatalogCache _catalog;
        private GameStateCache _stateCache;
        private PlanningDraftCache _draftCache;
        private bool _lastVisible;
        private string _activeNodeId = string.Empty;
        private string _activeBuildingTypeId = string.Empty;
        private string _activeOwnerPlayerId = string.Empty;
        private string _activeBlockedMessage = string.Empty;
        private string _selectedRecipeId = string.Empty;
        private string _hoveredRecipeId = string.Empty;
        private string _queuedPreviewRecipeId = string.Empty;
        private string _lastRecipeFailureMessage = string.Empty;
        private float _nextRecipePreviewAt;
        private int _recipePreviewSequence;

        public event Action<bool> VisibilityChanged;

        public bool IsVisible => ComputeVisible();

        private void Awake()
        {
            if (panelRoot == null) panelRoot = transform as RectTransform;
            ResolveSlideToggleReference();

            if (slideToggle != null)
            {
                slideToggle.SetToggleButtonVisible(keepSlideToggleButtonVisible);
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

            _stateCache = GameStateCache.Instance;
            if (_stateCache != null)
            {
                _stateCache.OnGameError -= OnGameError;
                _stateCache.OnGameError += OnGameError;
                _stateCache.OnPlanningCommandResult -= OnPlanningCommandResult;
                _stateCache.OnPlanningCommandResult += OnPlanningCommandResult;
                _stateCache.OnStateChanged -= OnStateChanged;
                _stateCache.OnStateChanged += OnStateChanged;
            }

            _draftCache = PlanningDraftCache.Instance ?? PlanningDraftCache.EnsureInstance();
            if (_draftCache != null)
            {
                _draftCache.OrdersChanged -= OnPlanningDraftChanged;
                _draftCache.OrdersChanged += OnPlanningDraftChanged;
                _draftCache.RecipePreviewChanged -= OnRecipePreviewChanged;
                _draftCache.RecipePreviewChanged += OnRecipePreviewChanged;
            }

            RefreshList();
            RefreshStatusMessage();
            UpdateVisibilityIfChanged(force: true);
        }

        private void OnDisable()
        {
            _hoveredRecipeId = string.Empty;
            _queuedPreviewRecipeId = string.Empty;
            (_draftCache ?? PlanningDraftCache.Instance)?.ClearRecipePreview();
            if (_catalog != null) _catalog.CatalogChanged -= OnCatalogChanged;
            if (_stateCache != null)
            {
                _stateCache.OnGameError -= OnGameError;
                _stateCache.OnPlanningCommandResult -= OnPlanningCommandResult;
                _stateCache.OnStateChanged -= OnStateChanged;
            }
            if (_draftCache != null)
            {
                _draftCache.OrdersChanged -= OnPlanningDraftChanged;
                _draftCache.RecipePreviewChanged -= OnRecipePreviewChanged;
            }
        }

        private void LateUpdate()
        {
            TryDispatchQueuedRecipePreview();
            UpdateVisibilityIfChanged(force: false);
        }

        public void OpenForBuilding(string nodeId, string buildingTypeId, string ownerPlayerId = null)
        {
            _activeNodeId = string.IsNullOrWhiteSpace(nodeId) ? string.Empty : nodeId.Trim();
            _activeBuildingTypeId = NormalizeToken(buildingTypeId);
            _activeOwnerPlayerId = string.IsNullOrWhiteSpace(ownerPlayerId) ? string.Empty : ownerPlayerId.Trim();
            _hoveredRecipeId = string.Empty;
            _queuedPreviewRecipeId = string.Empty;
            _lastRecipeFailureMessage = string.Empty;
            (_draftCache ?? PlanningDraftCache.Instance ?? PlanningDraftCache.EnsureInstance())?.ClearRecipePreview();
            RefreshActiveBuildingContextFromAuthority();
            SeedSelectionFromState();
            Show();
        }

        public void Show()
        {
            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(true);
            }

            ResolveSlideToggleReference();
            if (slideToggle != null)
            {
                if (!slideToggle.gameObject.activeSelf)
                {
                    slideToggle.gameObject.SetActive(true);
                }
                if (!slideToggle.IsCollapsed)
                {
                    slideToggle.SetCollapsed(true, true);
                }
                slideToggle.Expand();
            }

            RefreshList();
            RefreshStatusMessage();
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

        public void SetSlideToggleButtonVisible(bool visible)
        {
            ResolveSlideToggleReference();
            if (slideToggle == null)
            {
                return;
            }

            keepSlideToggleButtonVisible = visible;
            slideToggle.SetToggleButtonVisible(visible);
        }

        private void Hide(bool immediate)
        {
            _hoveredRecipeId = string.Empty;
            _queuedPreviewRecipeId = string.Empty;
            (_draftCache ?? PlanningDraftCache.Instance)?.ClearRecipePreview();
            ResolveSlideToggleReference();
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

        private void ResolveSlideToggleReference()
        {
            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            if (slideToggle != null && !slideToggle.ControlsPanel(panelRoot))
            {
                slideToggle = null;
            }

            if (slideToggle == null && panelRoot != null)
            {
                var candidate = panelRoot.GetComponent<BuildPanelSlideToggle>();
                if (candidate != null && candidate.ControlsPanel(panelRoot))
                {
                    slideToggle = candidate;
                }
            }

            if (slideToggle == null && panelRoot != null)
            {
                var candidates = panelRoot.GetComponentsInChildren<BuildPanelSlideToggle>(true);
                if (candidates != null)
                {
                    for (var i = 0; i < candidates.Length; i++)
                    {
                        var candidate = candidates[i];
                        if (candidate != null && candidate.ControlsPanel(panelRoot))
                        {
                            slideToggle = candidate;
                            break;
                        }
                    }
                }
            }

            if (slideToggle == null)
            {
                var candidate = GetComponent<BuildPanelSlideToggle>();
                if (candidate != null && candidate.ControlsPanel(panelRoot))
                {
                    slideToggle = candidate;
                }
            }

            if (slideToggle == null)
            {
                var candidates = GetComponentsInChildren<BuildPanelSlideToggle>(true);
                if (candidates != null)
                {
                    for (var i = 0; i < candidates.Length; i++)
                    {
                        var candidate = candidates[i];
                        if (candidate != null && candidate.ControlsPanel(panelRoot))
                        {
                            slideToggle = candidate;
                            break;
                        }
                    }
                }
            }

            if (slideToggle == null)
            {
                var candidate = GetComponentInParent<BuildPanelSlideToggle>(true);
                if (candidate != null && candidate.ControlsPanel(panelRoot))
                {
                    slideToggle = candidate;
                }
            }
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

            RefreshActiveBuildingContextFromAuthority();
            SeedSelectionFromState();
            RefreshList();
        }

        private void OnPlanningDraftChanged()
        {
            if (string.IsNullOrWhiteSpace(_activeNodeId))
            {
                return;
            }

            SeedSelectionFromState();
            if (IsVisible)
            {
                RefreshList();
            }
            RefreshStatusMessage();
        }

        private void OnRecipePreviewChanged()
        {
            RefreshStatusMessage();
        }

        private void OnPlanningCommandResult(PlanningCommandResultEvent evt)
        {
            if (evt == null ||
                !string.Equals(NormalizeToken(evt.CommandType), "building_recipe", StringComparison.Ordinal) ||
                !string.Equals(evt.PrimaryId ?? string.Empty, _activeNodeId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (evt.Success)
            {
                _lastRecipeFailureMessage = string.Empty;
            }
            else
            {
                _lastRecipeFailureMessage = GameplayFeedbackText.ResolveMessage(evt.Message, evt.ErrorCode);
            }

            if (!evt.Success)
            {
                ClearLocalSelectionCache(_activeNodeId);
            }
            SeedSelectionFromState();
            if (IsVisible)
            {
                RefreshList();
            }
            RefreshStatusMessage();
        }

        private void OnGameError(GameErrorEvent evt)
        {
            if (evt == null || !string.Equals(NormalizeToken(evt.Code), "invalid_directive", StringComparison.Ordinal))
            {
                return;
            }

            var runtimeConfig = ClientRuntimeConfigCache.Instance;
            if (runtimeConfig != null && runtimeConfig.DevMode)
            {
                return;
            }

            _lastRecipeFailureMessage = GameplayFeedbackText.ResolveMessage(evt.Message, evt.Code);
            if (evt.Details == null ||
                !evt.Details.TryGetValue("node_id", out var nodeId) ||
                !evt.Details.TryGetValue("recipe_id", out var recipeId) ||
                string.IsNullOrWhiteSpace(nodeId) ||
                string.IsNullOrWhiteSpace(recipeId))
            {
                RefreshStatusMessage();
                return;
            }

            if (!string.Equals(nodeId, _activeNodeId, StringComparison.Ordinal))
            {
                RefreshStatusMessage();
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
                view.SetActiveState(false, notify: false);
            }

            if (string.Equals(_selectedRecipeId, recipeId, StringComparison.Ordinal))
            {
                _selectedRecipeId = string.Empty;
            }

            _quantityByRecipeId.Remove(recipeId);
            SaveCurrentSelectionToLocalCache();
            RefreshItemActivationStates();
            RefreshStatusMessage();
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
            var orderIndex = BuildRecipeOrderIndex();

            recipes.Sort((a, b) =>
            {
                var leftIndex = orderIndex.TryGetValue(NormalizeToken(a.Id), out var leftOrder) ? leftOrder : int.MaxValue;
                var rightIndex = orderIndex.TryGetValue(NormalizeToken(b.Id), out var rightOrder) ? rightOrder : int.MaxValue;
                var layoutCmp = leftIndex.CompareTo(rightIndex);
                if (layoutCmp != 0) return layoutCmp;
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
                item.SetActiveState(string.Equals(NormalizeToken(recipe.Id), NormalizeToken(_selectedRecipeId), StringComparison.Ordinal), notify: false);
                item.ActivationChanged -= OnItemActivationChanged;
                item.ActivationChanged += OnItemActivationChanged;
                item.HoverEntered -= OnItemHoverEntered;
                item.HoverEntered += OnItemHoverEntered;
                item.HoverExited -= OnItemHoverExited;
                item.HoverExited += OnItemHoverExited;

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

            var runtimeConfig = ClientRuntimeConfigCache.Instance;
            var devModeUnlocked = runtimeConfig != null && runtimeConfig.DevMode;
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

                if (devModeUnlocked)
                {
                    recipe.IsLocked = false;
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
            var completedIds = cache?.GetCurrentResearchState()?.CompletedTechnologyIds;
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

            var me = NormalizeToken(cache.MyPlayerID);
            if (string.IsNullOrWhiteSpace(me))
            {
                return true;
            }

            var owner = NormalizeToken(_activeOwnerPlayerId);
            if (string.IsNullOrWhiteSpace(owner) &&
                TryGetAuthoritativeBuilding(_activeNodeId, out var building) &&
                building != null)
            {
                owner = NormalizeToken(ResolveBuildingOwner(cache, building));
            }

            if (string.IsNullOrWhiteSpace(owner))
            {
                return true;
            }

            return string.Equals(owner, me, StringComparison.Ordinal);
        }

        private void SeedSelectionFromState()
        {
            _quantityByRecipeId.Clear();
            _selectedRecipeId = string.Empty;

            if (string.IsNullOrWhiteSpace(_activeNodeId))
            {
                return;
            }

            RefreshActiveBuildingContextFromAuthority();

            if (TryRestoreSelectionFromLocalCache(_activeNodeId))
            {
                SyncSelectionCacheFromSelectedRecipe();
                SaveCurrentSelectionToLocalCache();
                return;
            }

            if (TryRestoreSelectionFromDraft(_activeNodeId))
            {
                SyncSelectionCacheFromSelectedRecipe();
                SaveCurrentSelectionToLocalCache();
                return;
            }

            if (TryRestoreSelectionFromAuthoritativeBuilding(_activeNodeId))
            {
                SyncSelectionCacheFromSelectedRecipe();
                SaveCurrentSelectionToLocalCache();
                return;
            }

            SyncSelectionCacheFromSelectedRecipe();
            SaveCurrentSelectionToLocalCache();
        }

        private void OnItemActivationChanged(RecipeSynthesisItemView item, bool isActive)
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

            if (!isActive)
            {
                if (string.Equals(_selectedRecipeId, recipeId, StringComparison.Ordinal))
                {
                    _selectedRecipeId = string.Empty;
                }

                _quantityByRecipeId.Remove(recipeId);
                SaveCurrentSelectionToLocalCache();
                RefreshItemActivationStates();
                return;
            }

            _selectedRecipeId = recipeId;
            _knownUnlockedRecipeIds.Add(recipeId);
            _knownLockedRecipeIds.Remove(recipeId);
            SyncSelectionCacheFromSelectedRecipe();

            SaveCurrentSelectionToLocalCache();
            RefreshItemActivationStates();
            SendRecipeSelectionChangeToServer();
        }

        private void OnItemHoverEntered(RecipeSynthesisItemView item)
        {
            var recipeId = item != null ? NormalizeToken(item.RecipeId) : string.Empty;
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return;
            }

            _hoveredRecipeId = recipeId;
            _queuedPreviewRecipeId = recipeId;
            _nextRecipePreviewAt = Time.unscaledTime + 0.12f;
            RefreshStatusMessage();
        }

        private void OnItemHoverExited(RecipeSynthesisItemView item)
        {
            var recipeId = item != null ? NormalizeToken(item.RecipeId) : string.Empty;
            if (!string.IsNullOrWhiteSpace(recipeId) &&
                string.Equals(_hoveredRecipeId, recipeId, StringComparison.Ordinal))
            {
                _hoveredRecipeId = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(recipeId) &&
                string.Equals(_queuedPreviewRecipeId, recipeId, StringComparison.Ordinal))
            {
                _queuedPreviewRecipeId = string.Empty;
            }

            _draftCache?.ClearRecipePreview();
            RefreshStatusMessage();
        }

        private void TryDispatchQueuedRecipePreview()
        {
            if (string.IsNullOrWhiteSpace(_queuedPreviewRecipeId) ||
                string.IsNullOrWhiteSpace(_activeNodeId) ||
                Time.unscaledTime < _nextRecipePreviewAt)
            {
                return;
            }

            _recipePreviewSequence++;
            var requestId = $"recipe-preview-{_activeNodeId}-{_recipePreviewSequence}";
            var draft = _draftCache ?? PlanningDraftCache.Instance ?? PlanningDraftCache.EnsureInstance();
            _draftCache = draft;
            draft?.TrackRecipePreviewRequest(requestId, _activeNodeId, _queuedPreviewRecipeId);
            GameIntents.PreviewRecipe(requestId, _activeNodeId, _queuedPreviewRecipeId);
            _queuedPreviewRecipeId = string.Empty;
        }

        private bool TryRestoreSelectionFromLocalCache(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            if (!_quantityByNodeId.TryGetValue(nodeId, out var recipeQuantities) ||
                recipeQuantities == null)
            {
                return false;
            }

            _quantityByRecipeId.Clear();

            if (_selectedRecipeByNodeId.TryGetValue(nodeId, out var selected))
            {
                _selectedRecipeId = NormalizeToken(selected);
            }

            if (string.IsNullOrWhiteSpace(_selectedRecipeId))
            {
                foreach (var pair in recipeQuantities)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    if (Mathf.Max(0, pair.Value) <= 0)
                    {
                        continue;
                    }

                    _selectedRecipeId = NormalizeToken(pair.Key);
                    break;
                }
            }

            return true;
        }

        private bool TryRestoreSelectionFromDraft(string nodeId)
        {
            if (!TryGetDraftRecipeSelection(nodeId, out var selection) ||
                selection == null ||
                string.IsNullOrWhiteSpace(selection.RecipeId))
            {
                return false;
            }

            _selectedRecipeId = NormalizeToken(selection.RecipeId);
            if (!string.IsNullOrWhiteSpace(_selectedRecipeId))
            {
                _lastSentRecipeByNodeId[nodeId] = _selectedRecipeId;
                return true;
            }

            return false;
        }

        private bool TryRestoreSelectionFromAuthoritativeBuilding(string nodeId)
        {
            if (!TryGetAuthoritativeBuilding(nodeId, out var building) ||
                building == null ||
                string.IsNullOrWhiteSpace(building.OperationSelectedRecipeId))
            {
                return false;
            }

            _selectedRecipeId = NormalizeToken(building.OperationSelectedRecipeId);
            if (!string.IsNullOrWhiteSpace(_selectedRecipeId))
            {
                _lastSentRecipeByNodeId[nodeId] = _selectedRecipeId;
                return true;
            }

            return false;
        }

        private void SaveCurrentSelectionToLocalCache()
        {
            if (string.IsNullOrWhiteSpace(_activeNodeId))
            {
                return;
            }

            if (!_quantityByNodeId.TryGetValue(_activeNodeId, out var cache))
            {
                cache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _quantityByNodeId[_activeNodeId] = cache;
            }

            cache.Clear();
            if (!string.IsNullOrWhiteSpace(_selectedRecipeId))
            {
                cache[NormalizeToken(_selectedRecipeId)] = 1;
            }

            if (!string.IsNullOrWhiteSpace(_selectedRecipeId))
            {
                _selectedRecipeByNodeId[_activeNodeId] = NormalizeToken(_selectedRecipeId);
            }
            else
            {
                _selectedRecipeByNodeId.Remove(_activeNodeId);
            }
        }

        private void SyncSelectionCacheFromSelectedRecipe()
        {
            _quantityByRecipeId.Clear();
            if (!string.IsNullOrWhiteSpace(_selectedRecipeId))
            {
                _quantityByRecipeId[NormalizeToken(_selectedRecipeId)] = 1;
            }
        }

        private void RefreshItemActivationStates()
        {
            foreach (var pair in _itemByRecipeId)
            {
                var recipeId = pair.Key;
                var item = pair.Value;
                if (item == null)
                {
                    continue;
                }

                item.SetActiveState(
                    !string.IsNullOrWhiteSpace(_selectedRecipeId) &&
                    string.Equals(recipeId, NormalizeToken(_selectedRecipeId), StringComparison.Ordinal),
                    notify: false);
            }
        }

        private void ClearLocalSelectionCache(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            _quantityByNodeId.Remove(nodeId);
            _selectedRecipeByNodeId.Remove(nodeId);
        }

        private void RefreshActiveBuildingContextFromAuthority()
        {
            _activeBlockedMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(_activeNodeId))
            {
                return;
            }

            var cache = _stateCache ?? GameStateCache.Instance;
            if (!TryGetAuthoritativeBuilding(_activeNodeId, out var building) || building == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(building.BuildingTypeId))
            {
                _activeBuildingTypeId = NormalizeToken(building.BuildingTypeId);
            }

            var owner = ResolveBuildingOwner(cache, building);
            if (!string.IsNullOrWhiteSpace(owner))
            {
                _activeOwnerPlayerId = owner;
            }

            _activeBlockedMessage = !string.IsNullOrWhiteSpace(building.OperationBlockedMessage)
                ? building.OperationBlockedMessage
                : GameplayFeedbackText.ResolveMessage(building.OperationBlockedMessage, building.OperationBlockedReason);
        }

        private bool TryGetDraftRecipeSelection(string nodeId, out QueuedRecipeSelectionDto selection)
        {
            selection = null;
            var draft = _draftCache ?? PlanningDraftCache.Instance ?? PlanningDraftCache.EnsureInstance();
            _draftCache = draft;
            return draft != null && draft.TryGetRecipeSelection(nodeId, out selection);
        }

        private bool TryGetAuthoritativeBuilding(string nodeId, out BuildingDto building)
        {
            building = null;
            var cache = _stateCache ?? GameStateCache.Instance;
            return cache != null && cache.TryGetBuilding(nodeId, out building);
        }

        private static string ResolveBuildingOwner(GameStateCache cache, BuildingDto building)
        {
            if (building == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(building.OwnerId))
            {
                return building.OwnerId.Trim();
            }

            var cityId = !string.IsNullOrWhiteSpace(building.CityId)
                ? building.CityId
                : building.ServiceCityId;
            if (cache != null &&
                !string.IsNullOrWhiteSpace(cityId) &&
                cache.TryGetCity(cityId, out var city) &&
                city != null &&
                !string.IsNullOrWhiteSpace(city.OwnerId))
            {
                return city.OwnerId.Trim();
            }

            return string.Empty;
        }

        private string ResolvePreferredRecipeFromLocalQuantities(string preferredRecipeId = null)
        {
            var normalizedPreferred = NormalizeToken(preferredRecipeId);
            if (!string.IsNullOrWhiteSpace(normalizedPreferred) &&
                _quantityByRecipeId.TryGetValue(normalizedPreferred, out var preferredAmount) &&
                preferredAmount > 0)
            {
                return normalizedPreferred;
            }

            var bestRecipe = string.Empty;
            var bestAmount = int.MinValue;
            foreach (var pair in _quantityByRecipeId)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                var amount = Mathf.Max(0, pair.Value);
                if (amount <= 0)
                {
                    continue;
                }

                if (amount > bestAmount ||
                    (amount == bestAmount && string.Compare(pair.Key, bestRecipe, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    bestAmount = amount;
                    bestRecipe = pair.Key;
                }
            }

            if (!string.IsNullOrWhiteSpace(bestRecipe))
            {
                return NormalizeToken(bestRecipe);
            }

            return string.Empty;
        }

        private string ResolvePreferredRecipeForCurrentNode(string preferredRecipeId = null)
        {
            var local = ResolvePreferredRecipeFromLocalQuantities(preferredRecipeId);
            if (!string.IsNullOrWhiteSpace(local))
            {
                return local;
            }

            var serverSelected = NormalizeToken(ResolveServerSelectedRecipeForActiveNode());
            return string.IsNullOrWhiteSpace(serverSelected) ? string.Empty : serverSelected;
        }

        private string ResolveServerSelectedRecipeForActiveNode()
        {
            if (string.IsNullOrWhiteSpace(_activeNodeId))
            {
                return string.Empty;
            }

            if (TryGetDraftRecipeSelection(_activeNodeId, out var selection) &&
                selection != null &&
                !string.IsNullOrWhiteSpace(selection.RecipeId))
            {
                return selection.RecipeId;
            }

            return TryGetAuthoritativeBuilding(_activeNodeId, out var building) && building != null
                ? (building.OperationSelectedRecipeId ?? string.Empty)
                : string.Empty;
        }

        private void SendRecipeSelectionChangeToServer()
        {
            if (string.IsNullOrWhiteSpace(_activeNodeId))
            {
                return;
            }

            var recipeToSend = ResolvePreferredRecipeForCurrentNode();
            if (string.IsNullOrWhiteSpace(recipeToSend))
            {
                Debug.Log($"[RecipeSynthesisPanel] No active recipe for node={_activeNodeId}, skip sync.");
                return;
            }

            var normalized = NormalizeToken(recipeToSend);
            GameIntents.SetBuildingRecipe(_activeNodeId, normalized);
            _lastSentRecipeByNodeId[_activeNodeId] = normalized;
        }

        private void RefreshStatusMessage()
        {
            if (statusText == null)
            {
                return;
            }

            if (TryGetCurrentRecipePreview(out var preview) && preview != null)
            {
                if (string.Equals(preview.Message, "检查中", StringComparison.Ordinal))
                {
                    statusText.text = "检查中";
                    return;
                }

                if (!string.IsNullOrWhiteSpace(preview.Message) || !string.IsNullOrWhiteSpace(preview.ErrorCode))
                {
                    statusText.text = preview.Valid
                        ? preview.Message
                        : GameplayFeedbackText.ResolveMessage(preview.Message, preview.ErrorCode);
                    if (string.IsNullOrWhiteSpace(statusText.text))
                    {
                        statusText.text = preview.Valid ? "当前可切换到该配方" : string.Empty;
                    }
                    return;
                }

                if (preview.Valid)
                {
                    statusText.text = "当前可切换到该配方";
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(_lastRecipeFailureMessage))
            {
                statusText.text = _lastRecipeFailureMessage;
                return;
            }

            statusText.text = _activeBlockedMessage ?? string.Empty;
        }

        private bool TryGetCurrentRecipePreview(out RecipePreviewDto preview)
        {
            preview = null;
            if (string.IsNullOrWhiteSpace(_activeNodeId) || string.IsNullOrWhiteSpace(_hoveredRecipeId))
            {
                return false;
            }

            var draft = _draftCache ?? PlanningDraftCache.Instance;
            preview = draft != null ? draft.CurrentRecipePreview : null;
            return preview != null &&
                   string.Equals(preview.NodeId, _activeNodeId, StringComparison.Ordinal) &&
                   string.Equals(NormalizeToken(preview.RecipeId), _hoveredRecipeId, StringComparison.Ordinal);
        }

        private List<RecipeViewData> LoadRecipeData()
        {
            return LoadFromCatalog();
        }

        private Dictionary<string, int> BuildRecipeOrderIndex()
        {
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var layout = _catalog != null ? _catalog.RecipeLayout : null;
            if (layout == null || layout.recipe_order == null)
            {
                return index;
            }

            for (var i = 0; i < layout.recipe_order.Length; i++)
            {
                var recipeId = NormalizeToken(layout.recipe_order[i]);
                if (string.IsNullOrWhiteSpace(recipeId) || index.ContainsKey(recipeId))
                {
                    continue;
                }
                index[recipeId] = i;
            }

            return index;
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
                    item.ActivationChanged -= OnItemActivationChanged;
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
