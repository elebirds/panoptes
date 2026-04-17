using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.Domestic;
using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    /// <summary>
    /// Registers city-core-specific actions on UnitInfoPanel:
    /// 1) open production/refine panel
    /// 2) open derived build panel
    /// </summary>
    public sealed class CityCoreBuildingActionRegistrar : UnitInfoActionProviderBase
    {
        private const string CityCoreBuildingType = "city_core";

        [Header("Action IDs")]
        [SerializeField] private string productionActionId = "action_2";
        [SerializeField] private string buildActionId = "action_3";
        [SerializeField] private string techTreeActionId = "action_4";
        [SerializeField] private string recipeActionId = "open_recipe_synthesis";

        [Header("Labels")]
        [SerializeField] private string productionActionLabel = "Production";
        [SerializeField] private string buildActionLabel = "Build";
        [SerializeField] private string techTreeActionLabel = "Tech Tree";
        [SerializeField] private string recipeActionLabel = "Synthesis";

        [Header("References")]
        [SerializeField] private MapInputHandler mapInputHandler;
        [SerializeField] private UnitInfoPanelController unitInfoPanelController;
        [SerializeField] private RectTransform nextStageButtonRect;
        [SerializeField] private RectTransform turnPanelRect;
        [SerializeField] private CityCoreProductionPanel cityCoreProductionPanel;
        [SerializeField] private RecipeSynthesisPanel recipeSynthesisPanel;
        [SerializeField] private TechTreePanelController techTreePanelController;
        [SerializeField] private BuildPanelSlideToggle buildPanelSlideToggle;
        [SerializeField] private BuildCommandPanel buildCommandPanel;
        [SerializeField] private bool autoSpawnCityCoreProductionPanelIfMissing = true;
        [SerializeField] private string cityCoreProductionPanelResourcesPath = "Prefabs/UI/CityCoreProductionPanel";
        [SerializeField] private bool hideTechTreePanelOnStart = true;
        [SerializeField] private bool hideRecipePanelOnStart = true;
        [SerializeField] private bool autoFindNextStageButton = true;
        [SerializeField] private bool autoFindTurnPanel = true;

        [Header("Build Derived Panel")]
        [SerializeField] private bool startBuildPanelCollapsed = true;
        [SerializeField] private bool forceBuildPanelCollapsedOnStartup = true;
        [SerializeField] private bool hideBuildPanelToggleButton = false;
        [SerializeField] private bool hideRecipePanelToggleButton = false;
        [SerializeField] private bool hideBuildPanelCancelButton = true;
        [SerializeField] private bool fallbackHideBuildPanelGameObjectWhenNoSlideToggle = true;
        [SerializeField] private float unitInfoShiftXWhenBuildPanelOpen = 360f;
        [SerializeField] private bool useBuildPanelWidthForShift = true;
        [SerializeField] private float buildPanelWidthShiftFactor = 1.18f;
        [SerializeField] private float buildPanelWidthShiftExtra = 0f;

        [Header("Production Derived Panel")]
        [SerializeField] private float productionPanelShiftXWhenOpen = 360f;
        [SerializeField] private bool useProductionPanelWidthForShift = true;
        [SerializeField] private float productionPanelWidthShiftFactor = 1.12f;
        [SerializeField] private float productionPanelWidthShiftExtra = 0f;

        [Header("Recipe Derived Panel")]
        [SerializeField] private float recipePanelShiftXWhenOpen = 360f;
        [SerializeField] private bool useRecipePanelWidthForShift = true;
        [SerializeField] private float recipePanelWidthShiftFactor = 1.12f;
        [SerializeField] private float recipePanelWidthShiftExtra = 0f;

        [Header("Right-Bottom Group Shift")]
        [SerializeField] private float rightGroupShiftDuration = 0.2f;
        [SerializeField] private AnimationCurve rightGroupShiftCurve = null;

        private bool _buildPanelOpen;
        private CityCoreProductionPanel _subscribedProductionPanel;
        private RecipeSynthesisPanel _subscribedRecipePanel;
        private bool _nextStageBasePositionReady;
        private Vector2 _nextStageBaseAnchoredPos;
        private bool _turnPanelBasePositionReady;
        private Vector2 _turnPanelBaseAnchoredPos;
        private Coroutine _nextStageShiftRoutine;
        private Coroutine _panelSwitchRoutine;
        private MapInputHandler _subscribedMapInputHandler;
        private string _activeUnitInfoNodeId = string.Empty;
        private bool _lastBuildPanelVisible;
        private bool _lastProductionPanelVisible;
        private bool _lastRecipePanelVisible;

        protected override void RegisterActions(UnitInfoActionRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            ResolveReferences();
            EnsureInitialPanelState();

            registry.RegisterAction(
                productionActionId,
                OnProductionActionClicked,
                string.IsNullOrWhiteSpace(productionActionLabel) ? "Production" : productionActionLabel,
                IsOwnedCityCoreBuildingProxy);

            registry.RegisterAction(
                buildActionId,
                OnBuildActionClicked,
                string.IsNullOrWhiteSpace(buildActionLabel) ? "Build" : buildActionLabel,
                IsOwnedCityCoreBuildingProxy);

            registry.RegisterAction(
                techTreeActionId,
                OnTechTreeActionClicked,
                string.IsNullOrWhiteSpace(techTreeActionLabel) ? "Tech Tree" : techTreeActionLabel,
                IsOwnedCityCoreBuildingProxy);

            registry.RegisterAction(
                recipeActionId,
                OnRecipeActionClicked,
                string.IsNullOrWhiteSpace(recipeActionLabel) ? "Synthesis" : recipeActionLabel,
                IsOwnedRecipeBuildingProxy);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveReferences();
            EnsureRightGroupAnimationCurve();
            EnsureInitialPanelState();
            SubscribeInputEvents();
            UpdateDerivedPanelVisibilitySnapshot();
        }

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            EnsureRightGroupAnimationCurve();
            EnsureInitialPanelState();
        }

        private void OnDisable()
        {
            UnsubscribeInputEvents();

            UnsubscribeProductionPanelEvents();
            UnsubscribeRecipePanelEvents();
            if (_nextStageShiftRoutine != null)
            {
                StopCoroutine(_nextStageShiftRoutine);
                _nextStageShiftRoutine = null;
            }
            if (_panelSwitchRoutine != null)
            {
                StopCoroutine(_panelSwitchRoutine);
                _panelSwitchRoutine = null;
            }
        }

        private void LateUpdate()
        {
            if (_subscribedMapInputHandler == null || !ReferenceEquals(_subscribedMapInputHandler, mapInputHandler))
            {
                ResolveReferences();
                SubscribeInputEvents();
            }

            SyncDerivedPanelStateFromVisibility();

            if (unitInfoPanelController != null && !unitInfoPanelController.IsOpen && IsAnyDerivedPanelVisible())
            {
                CloseAllDerivedPanels(resetUnitInfoOffset: true, closeTechTree: false);
            }

            var currentSelection = unitInfoPanelController != null ? unitInfoPanelController.CurrentUnit : null;
            if (currentSelection == null && IsAnyDerivedPanelVisible())
            {
                CloseAllDerivedPanels(resetUnitInfoOffset: true, closeTechTree: false);
                return;
            }

            if (currentSelection != null && IsBuildPanelCurrentlyVisible() && !IsOwnedCityCoreBuildingProxy(currentSelection))
            {
                CloseBuildPanel(resetUnitInfoOffset: true);
            }

            if (currentSelection != null &&
                cityCoreProductionPanel != null &&
                cityCoreProductionPanel.IsVisible &&
                !IsOwnedCityCoreBuildingProxy(currentSelection))
            {
                cityCoreProductionPanel.Close();
                ReapplyRightBottomShift(false);
            }

            if (currentSelection != null &&
                recipeSynthesisPanel != null &&
                recipeSynthesisPanel.IsVisible &&
                !IsOwnedRecipeBuildingProxy(currentSelection))
            {
                recipeSynthesisPanel.Hide();
                ReapplyRightBottomShift(false);
            }
        }

        private void SubscribeInputEvents()
        {
            if (mapInputHandler == null)
            {
                return;
            }

            if (ReferenceEquals(_subscribedMapInputHandler, mapInputHandler))
            {
                return;
            }

            if (_subscribedMapInputHandler != null)
            {
                _subscribedMapInputHandler.NonBuildingMapClicked -= OnNonBuildingMapClicked;
                _subscribedMapInputHandler.UnitSelectionChanged -= OnUnitSelectionChanged;
            }

            mapInputHandler.NonBuildingMapClicked -= OnNonBuildingMapClicked;
            mapInputHandler.NonBuildingMapClicked += OnNonBuildingMapClicked;

            mapInputHandler.UnitSelectionChanged -= OnUnitSelectionChanged;
            mapInputHandler.UnitSelectionChanged += OnUnitSelectionChanged;
            _subscribedMapInputHandler = mapInputHandler;
        }

        private void UnsubscribeInputEvents()
        {
            if (_subscribedMapInputHandler == null)
            {
                return;
            }

            _subscribedMapInputHandler.NonBuildingMapClicked -= OnNonBuildingMapClicked;
            _subscribedMapInputHandler.UnitSelectionChanged -= OnUnitSelectionChanged;
            _subscribedMapInputHandler = null;
        }

        private void ResolveReferences()
        {
            if (mapInputHandler == null)
            {
                mapInputHandler = MapInputHandler.Instance;
                if (mapInputHandler == null)
                {
                    mapInputHandler = UnityEngine.Object.FindAnyObjectByType<MapInputHandler>();
                }
            }

            if (unitInfoPanelController == null)
            {
                unitInfoPanelController = UnityEngine.Object.FindAnyObjectByType<UnitInfoPanelController>();
            }

            if (cityCoreProductionPanel == null)
            {
                var productionPanels = UnityEngine.Object.FindObjectsByType<CityCoreProductionPanel>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (productionPanels != null && productionPanels.Length > 0)
                {
                    cityCoreProductionPanel = productionPanels[0];
                }
            }

            if (cityCoreProductionPanel == null && autoSpawnCityCoreProductionPanelIfMissing)
            {
                var prefab = !string.IsNullOrWhiteSpace(cityCoreProductionPanelResourcesPath)
                    ? Resources.Load<CityCoreProductionPanel>(cityCoreProductionPanelResourcesPath.Trim())
                    : null;

                var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                var parent = canvas != null ? canvas.transform : null;

                if (prefab != null)
                {
                    cityCoreProductionPanel = UnityEngine.Object.Instantiate(prefab, parent, false);
                }
                else
                {
                    var go = new GameObject("CityCoreProductionPanel", typeof(RectTransform));
                    if (parent != null)
                    {
                        go.transform.SetParent(parent, false);
                    }
                    cityCoreProductionPanel = go.AddComponent<CityCoreProductionPanel>();
                }
            }

            if (techTreePanelController == null)
            {
                var techPanels = UnityEngine.Object.FindObjectsByType<TechTreePanelController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (techPanels != null && techPanels.Length > 0)
                {
                    techTreePanelController = techPanels[0];
                }
            }

            if (techTreePanelController == null)
            {
                var allRects = UnityEngine.Object.FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (var i = 0; i < allRects.Length; i++)
                {
                    var rect = allRects[i];
                    if (rect == null || !rect.gameObject.scene.IsValid())
                    {
                        continue;
                    }

                    var name = rect.name ?? string.Empty;
                    if (!string.Equals(name, "TechTreePanel", StringComparison.OrdinalIgnoreCase) &&
                        name.IndexOf("techtree", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    techTreePanelController = rect.GetComponent<TechTreePanelController>();
                    if (techTreePanelController == null)
                    {
                        techTreePanelController = rect.gameObject.AddComponent<TechTreePanelController>();
                    }
                    break;
                }
            }

            if (buildCommandPanel == null)
            {
                var buildPanels = UnityEngine.Object.FindObjectsByType<BuildCommandPanel>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (buildPanels != null && buildPanels.Length > 0)
                {
                    buildCommandPanel = buildPanels[0];
                }
            }

            if (buildPanelSlideToggle == null && buildCommandPanel != null)
            {
                buildPanelSlideToggle = ResolveBuildPanelSlideToggleForBuildPanel(buildCommandPanel);
            }

            if (buildCommandPanel == null && buildPanelSlideToggle != null)
            {
                buildCommandPanel = buildPanelSlideToggle.GetComponentInParent<BuildCommandPanel>(true);
            }

            if (buildPanelSlideToggle == null)
            {
                var toggles = UnityEngine.Object.FindObjectsByType<BuildPanelSlideToggle>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (toggles != null && toggles.Length > 0)
                {
                    for (var i = 0; i < toggles.Length; i++)
                    {
                        var candidate = toggles[i];
                        if (candidate == null)
                        {
                            continue;
                        }

                        if (buildCommandPanel != null)
                        {
                            var owner = candidate.GetComponentInParent<BuildCommandPanel>(true);
                            if (!ReferenceEquals(owner, buildCommandPanel))
                            {
                                continue;
                            }
                        }

                        buildPanelSlideToggle = candidate;
                        break;
                    }
                }
            }

            if (recipeSynthesisPanel == null)
            {
                var recipePanels = UnityEngine.Object.FindObjectsByType<RecipeSynthesisPanel>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (recipePanels != null && recipePanels.Length > 0)
                {
                    recipeSynthesisPanel = recipePanels[0];
                }
            }

            if (nextStageButtonRect == null && autoFindNextStageButton)
            {
                var allRects = UnityEngine.Object.FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (var i = 0; i < allRects.Length; i++)
                {
                    var rect = allRects[i];
                    if (rect == null || !rect.gameObject.scene.IsValid())
                    {
                        continue;
                    }

                    if (string.Equals(rect.name, "NextStageBtn", StringComparison.OrdinalIgnoreCase))
                    {
                        nextStageButtonRect = rect;
                        break;
                    }
                }
            }

            if (turnPanelRect == null && autoFindTurnPanel)
            {
                var allRects = UnityEngine.Object.FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (var i = 0; i < allRects.Length; i++)
                {
                    var rect = allRects[i];
                    if (rect == null || !rect.gameObject.scene.IsValid())
                    {
                        continue;
                    }

                    if (string.Equals(rect.name, "TrunPanel", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(rect.name, "TurnPanel", StringComparison.OrdinalIgnoreCase))
                    {
                        turnPanelRect = rect;
                        break;
                    }
                }
            }

            SubscribeProductionPanelEvents();
            SubscribeRecipePanelEvents();
        }

        private void EnsureInitialPanelState()
        {
            EnsureRightGroupAnimationCurve();
            CacheRightGroupBasePositionIfNeeded();
            var collapseBuildPanelOnStartup = forceBuildPanelCollapsedOnStartup || startBuildPanelCollapsed;

            if (buildPanelSlideToggle != null)
            {
                if (collapseBuildPanelOnStartup)
                {
                    buildPanelSlideToggle.SetCollapsed(true, true);
                    _buildPanelOpen = false;
                }

                if (hideBuildPanelToggleButton)
                {
                    buildPanelSlideToggle.SetToggleButtonVisible(false);
                }
            }
            else if (fallbackHideBuildPanelGameObjectWhenNoSlideToggle && buildCommandPanel != null)
            {
                if (collapseBuildPanelOnStartup)
                {
                    SetBuildPanelDirectVisible(false);
                    _buildPanelOpen = false;
                }
            }

            if (hideBuildPanelCancelButton && buildCommandPanel != null)
            {
                buildCommandPanel.SetCancelButtonVisible(false);
            }

            if (recipeSynthesisPanel != null)
            {
                recipeSynthesisPanel.SetSlideToggleButtonVisible(!hideRecipePanelToggleButton);
            }

            if (!_buildPanelOpen)
            {
                ReapplyRightBottomShift(true);
            }

            if (hideTechTreePanelOnStart && techTreePanelController != null)
            {
                techTreePanelController.gameObject.SetActive(false);
            }

            if (hideRecipePanelOnStart && recipeSynthesisPanel != null)
            {
                recipeSynthesisPanel.Hide();
            }

            UpdateDerivedPanelVisibilitySnapshot();
        }

        private void EnsureRightGroupAnimationCurve()
        {
            if (rightGroupShiftCurve == null || rightGroupShiftCurve.length == 0)
            {
                rightGroupShiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
        }

        private void SubscribeProductionPanelEvents()
        {
            if (ReferenceEquals(_subscribedProductionPanel, cityCoreProductionPanel))
            {
                return;
            }

            UnsubscribeProductionPanelEvents();
            if (cityCoreProductionPanel == null)
            {
                return;
            }

            cityCoreProductionPanel.VisibilityChanged += OnProductionPanelVisibilityChanged;
            _subscribedProductionPanel = cityCoreProductionPanel;
        }

        private void SubscribeRecipePanelEvents()
        {
            if (ReferenceEquals(_subscribedRecipePanel, recipeSynthesisPanel))
            {
                return;
            }

            UnsubscribeRecipePanelEvents();
            if (recipeSynthesisPanel == null)
            {
                return;
            }

            recipeSynthesisPanel.VisibilityChanged += OnRecipePanelVisibilityChanged;
            _subscribedRecipePanel = recipeSynthesisPanel;
        }

        private void UnsubscribeProductionPanelEvents()
        {
            if (_subscribedProductionPanel == null)
            {
                return;
            }

            _subscribedProductionPanel.VisibilityChanged -= OnProductionPanelVisibilityChanged;
            _subscribedProductionPanel = null;
        }

        private void UnsubscribeRecipePanelEvents()
        {
            if (_subscribedRecipePanel == null)
            {
                return;
            }

            _subscribedRecipePanel.VisibilityChanged -= OnRecipePanelVisibilityChanged;
            _subscribedRecipePanel = null;
        }

        private void OnProductionPanelVisibilityChanged(bool _)
        {
            ReapplyRightBottomShift(false);
        }

        private void OnRecipePanelVisibilityChanged(bool _)
        {
            ReapplyRightBottomShift(false);
        }

        private bool IsBuildPanelCurrentlyVisible()
        {
            return buildPanelSlideToggle != null
                ? !buildPanelSlideToggle.IsCollapsed
                : (buildCommandPanel != null && buildCommandPanel.gameObject.activeSelf);
        }

        private bool IsAnyDerivedPanelVisible()
        {
            return IsBuildPanelCurrentlyVisible()
                   || (cityCoreProductionPanel != null && cityCoreProductionPanel.IsVisible)
                   || (recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible);
        }

        private void UpdateDerivedPanelVisibilitySnapshot()
        {
            _lastBuildPanelVisible = IsBuildPanelCurrentlyVisible();
            _lastProductionPanelVisible = cityCoreProductionPanel != null && cityCoreProductionPanel.IsVisible;
            _lastRecipePanelVisible = recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible;
        }

        private void SyncDerivedPanelStateFromVisibility()
        {
            var buildVisible = IsBuildPanelCurrentlyVisible();
            var productionVisible = cityCoreProductionPanel != null && cityCoreProductionPanel.IsVisible;
            var recipeVisible = recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible;

            if (!buildVisible && _buildPanelOpen)
            {
                _buildPanelOpen = false;
                if (buildCommandPanel != null)
                {
                    buildCommandPanel.ClearCityCoreContext();
                }
            }

            var changed = buildVisible != _lastBuildPanelVisible
                          || productionVisible != _lastProductionPanelVisible
                          || recipeVisible != _lastRecipePanelVisible;

            if (changed)
            {
                _lastBuildPanelVisible = buildVisible;
                _lastProductionPanelVisible = productionVisible;
                _lastRecipePanelVisible = recipeVisible;
                ReapplyRightBottomShift(false);
            }
        }

        private void CacheRightGroupBasePositionIfNeeded()
        {
            if (!_nextStageBasePositionReady && nextStageButtonRect != null)
            {
                _nextStageBaseAnchoredPos = nextStageButtonRect.anchoredPosition;
                _nextStageBasePositionReady = true;
            }

            if (!_turnPanelBasePositionReady && turnPanelRect != null)
            {
                _turnPanelBaseAnchoredPos = turnPanelRect.anchoredPosition;
                _turnPanelBasePositionReady = true;
            }
        }

        private void ReapplyRightBottomShift(bool immediate)
        {
            var shiftX = ResolveActiveDerivedPanelShiftX();
            ApplyRightBottomShift(shiftX, immediate);
        }

        private float ResolveActiveDerivedPanelShiftX()
        {
            var maxShift = 0f;

            if (IsBuildPanelCurrentlyVisible())
            {
                maxShift = Mathf.Max(maxShift, ResolveUnitInfoShiftX());
            }

            if (cityCoreProductionPanel != null && cityCoreProductionPanel.IsVisible)
            {
                maxShift = Mathf.Max(maxShift, ResolveProductionShiftX());
            }

            if (recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible)
            {
                maxShift = Mathf.Max(maxShift, ResolveRecipeShiftX());
            }

            return maxShift;
        }

        private void ApplyRightBottomShift(float shiftX, bool immediate)
        {
            var clampedShift = Mathf.Max(0f, shiftX);

            if (unitInfoPanelController != null)
            {
                unitInfoPanelController.SetExternalOffset(
                    clampedShift > 0.01f ? new Vector2(-clampedShift, 0f) : Vector2.zero,
                    immediate);
            }

            if (nextStageButtonRect == null && turnPanelRect == null)
            {
                return;
            }

            CacheRightGroupBasePositionIfNeeded();
            var nextStageTarget = _nextStageBaseAnchoredPos + new Vector2(-clampedShift, 0f);
            var turnPanelTarget = _turnPanelBaseAnchoredPos + new Vector2(-clampedShift, 0f);

            if (_nextStageShiftRoutine != null)
            {
                StopCoroutine(_nextStageShiftRoutine);
                _nextStageShiftRoutine = null;
            }

            if (immediate || !isActiveAndEnabled)
            {
                if (nextStageButtonRect != null)
                {
                    nextStageButtonRect.anchoredPosition = nextStageTarget;
                }
                if (turnPanelRect != null)
                {
                    turnPanelRect.anchoredPosition = turnPanelTarget;
                }
                return;
            }

            _nextStageShiftRoutine = StartCoroutine(AnimateNextStageShift(nextStageTarget, turnPanelTarget));
        }

        private System.Collections.IEnumerator AnimateNextStageShift(Vector2 nextStageTarget, Vector2 turnPanelTarget)
        {
            if (nextStageButtonRect == null && turnPanelRect == null)
            {
                yield break;
            }

            var nextStageStart = nextStageButtonRect != null ? nextStageButtonRect.anchoredPosition : Vector2.zero;
            var turnPanelStart = turnPanelRect != null ? turnPanelRect.anchoredPosition : Vector2.zero;
            var duration = Mathf.Max(0.01f, rightGroupShiftDuration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var k = rightGroupShiftCurve != null ? rightGroupShiftCurve.Evaluate(t) : t;
                if (nextStageButtonRect != null)
                {
                    nextStageButtonRect.anchoredPosition = Vector2.LerpUnclamped(nextStageStart, nextStageTarget, k);
                }
                if (turnPanelRect != null)
                {
                    turnPanelRect.anchoredPosition = Vector2.LerpUnclamped(turnPanelStart, turnPanelTarget, k);
                }
                yield return null;
            }

            if (nextStageButtonRect != null)
            {
                nextStageButtonRect.anchoredPosition = nextStageTarget;
            }
            if (turnPanelRect != null)
            {
                turnPanelRect.anchoredPosition = turnPanelTarget;
            }
            _nextStageShiftRoutine = null;
        }

        private void StopPanelSwitchRoutine()
        {
            if (_panelSwitchRoutine == null)
            {
                return;
            }

            StopCoroutine(_panelSwitchRoutine);
            _panelSwitchRoutine = null;
        }

        private float GetBuildPanelSlideDuration()
        {
            if (buildPanelSlideToggle != null)
            {
                return buildPanelSlideToggle.GetDuration();
            }

            return 0.22f;
        }

        private float GetRecipePanelSlideDuration()
        {
            if (recipeSynthesisPanel != null)
            {
                return recipeSynthesisPanel.GetSlideDuration();
            }

            return 0.22f;
        }

        private void OpenBuildPanelForNode(string nodeId)
        {
            if (buildCommandPanel != null)
            {
                if (!buildCommandPanel.gameObject.activeSelf)
                {
                    buildCommandPanel.gameObject.SetActive(true);
                }
                buildCommandPanel.SetCityCoreContext(nodeId);
            }

            if (buildPanelSlideToggle != null)
            {
                if (!buildPanelSlideToggle.gameObject.activeSelf)
                {
                    buildPanelSlideToggle.gameObject.SetActive(true);
                }
                buildPanelSlideToggle.Expand();
            }
            else if (fallbackHideBuildPanelGameObjectWhenNoSlideToggle && buildCommandPanel != null)
            {
                SetBuildPanelDirectVisible(true);
            }
            else
            {
                Debug.LogWarning("[CityCoreBuildingActionRegistrar] Build panel reference missing.");
                return;
            }

            _buildPanelOpen = true;
            ReapplyRightBottomShift(false);
            UpdateDerivedPanelVisibilitySnapshot();
        }

        private void OpenRecipePanelForBuilding(string nodeId, string buildingType, string ownerId)
        {
            if (recipeSynthesisPanel == null)
            {
                Debug.LogWarning("[CityCoreBuildingActionRegistrar] RecipeSynthesisPanel missing.");
                return;
            }

            recipeSynthesisPanel.OpenForBuilding(nodeId, buildingType, ownerId);
            var panelRect = recipeSynthesisPanel.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.SetAsLastSibling();
            }
            ReapplyRightBottomShift(false);
            UpdateDerivedPanelVisibilitySnapshot();
        }

        private System.Collections.IEnumerator SwitchFromRecipeToBuild(string nodeId)
        {
            if (recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible)
            {
                recipeSynthesisPanel.Hide();
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, GetRecipePanelSlideDuration()));
            }

            OpenBuildPanelForNode(nodeId);
            _panelSwitchRoutine = null;
        }

        private System.Collections.IEnumerator SwitchFromBuildToRecipe(string nodeId, string buildingType, string ownerId)
        {
            if (IsBuildPanelCurrentlyVisible())
            {
                CloseBuildPanel(false);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, GetBuildPanelSlideDuration()));
            }

            OpenRecipePanelForBuilding(nodeId, buildingType, ownerId);
            _panelSwitchRoutine = null;
        }

        private void CloseAllDerivedPanels(bool resetUnitInfoOffset, bool closeTechTree)
        {
            StopPanelSwitchRoutine();

            if (cityCoreProductionPanel != null)
            {
                cityCoreProductionPanel.Close();
            }

            if (recipeSynthesisPanel != null)
            {
                recipeSynthesisPanel.Hide();
            }

            CloseBuildPanel(resetUnitInfoOffset: false);

            if (closeTechTree && techTreePanelController != null)
            {
                techTreePanelController.gameObject.SetActive(false);
            }

            if (resetUnitInfoOffset)
            {
                ReapplyRightBottomShift(false);
            }

            UpdateDerivedPanelVisibilitySnapshot();
        }

        private void OnProductionActionClicked(UnitView unit)
        {
            if (!TryResolveCityCoreNodeId(unit, out var nodeId))
            {
                return;
            }

            _activeUnitInfoNodeId = nodeId;
            ResolveReferences();
            StopPanelSwitchRoutine();
            CloseBuildPanel(true);
            if (techTreePanelController != null)
            {
                techTreePanelController.gameObject.SetActive(false);
            }
            if (recipeSynthesisPanel != null)
            {
                recipeSynthesisPanel.Hide();
            }

            if (cityCoreProductionPanel == null)
            {
                Debug.LogWarning($"[CityCoreBuildingActionRegistrar] CityCoreProductionPanel missing. node={nodeId}");
                return;
            }

            cityCoreProductionPanel.OpenForCityCore(nodeId);
            ReapplyRightBottomShift(false);
            UpdateDerivedPanelVisibilitySnapshot();
        }

        private void OnBuildActionClicked(UnitView unit)
        {
            if (!TryResolveCityCoreNodeId(unit, out var nodeId))
            {
                return;
            }

            _activeUnitInfoNodeId = nodeId;
            ResolveReferences();
            StopPanelSwitchRoutine();
            if (cityCoreProductionPanel != null)
            {
                cityCoreProductionPanel.Close();
            }

            var isBuildPanelVisible = IsBuildPanelCurrentlyVisible();
            var isRecipePanelVisible = recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible;

            if (isBuildPanelVisible)
            {
                CloseBuildPanel(true);
                return;
            }

            if (techTreePanelController != null)
            {
                techTreePanelController.gameObject.SetActive(false);
            }

            if (isRecipePanelVisible)
            {
                _panelSwitchRoutine = StartCoroutine(SwitchFromRecipeToBuild(nodeId));
                return;
            }

            OpenBuildPanelForNode(nodeId);
        }

        private void OnTechTreeActionClicked(UnitView unit)
        {
            if (!TryResolveCityCoreNodeId(unit, out var nodeId))
            {
                return;
            }

            _activeUnitInfoNodeId = nodeId;
            ResolveReferences();
            StopPanelSwitchRoutine();
            CloseBuildPanel(true);

            if (cityCoreProductionPanel != null)
            {
                cityCoreProductionPanel.Close();
            }
            if (recipeSynthesisPanel != null)
            {
                recipeSynthesisPanel.Hide();
            }

            if (techTreePanelController == null)
            {
                Debug.LogWarning("[CityCoreBuildingActionRegistrar] TechTreePanelController missing.");
                return;
            }

            var panelGo = techTreePanelController.gameObject;
            if (panelGo.activeSelf)
            {
                panelGo.SetActive(false);
                return;
            }

            panelGo.SetActive(true);
            var panelRect = panelGo.transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.SetAsLastSibling();
            }
        }

        private void OnRecipeActionClicked(UnitView unit)
        {
            if (!TryResolveRecipeBuilding(unit, out var nodeId, out var buildingType, out var ownerId))
            {
                return;
            }

            _activeUnitInfoNodeId = nodeId;
            ResolveReferences();
            StopPanelSwitchRoutine();

            if (cityCoreProductionPanel != null)
            {
                cityCoreProductionPanel.Close();
            }

            if (techTreePanelController != null)
            {
                techTreePanelController.gameObject.SetActive(false);
            }

            var recipeVisible = recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible;
            if (recipeVisible)
            {
                recipeSynthesisPanel.Hide();
                ReapplyRightBottomShift(false);
                UpdateDerivedPanelVisibilitySnapshot();
                return;
            }

            if (IsBuildPanelCurrentlyVisible())
            {
                _panelSwitchRoutine = StartCoroutine(SwitchFromBuildToRecipe(nodeId, buildingType, ownerId));
                return;
            }

            OpenRecipePanelForBuilding(nodeId, buildingType, ownerId);
        }

        private void OnNonBuildingMapClicked()
        {
            _activeUnitInfoNodeId = string.Empty;
            CloseAllDerivedPanels(resetUnitInfoOffset: true, closeTechTree: false);
        }

        private void OnUnitSelectionChanged(UnitView selected)
        {
            if (selected != null &&
                !string.IsNullOrWhiteSpace(_activeUnitInfoNodeId) &&
                string.Equals(selected.UnitId, _activeUnitInfoNodeId, StringComparison.Ordinal))
            {
                return;
            }

            _activeUnitInfoNodeId = selected != null ? selected.UnitId : string.Empty;
            CloseAllDerivedPanels(resetUnitInfoOffset: true, closeTechTree: true);
        }

        private void CloseBuildPanel(bool resetUnitInfoOffset)
        {
            if (buildPanelSlideToggle != null)
            {
                buildPanelSlideToggle.Collapse();
            }
            else if (fallbackHideBuildPanelGameObjectWhenNoSlideToggle && buildCommandPanel != null)
            {
                SetBuildPanelDirectVisible(false);
            }
            _buildPanelOpen = false;

            if (buildCommandPanel != null)
            {
                buildCommandPanel.ClearCityCoreContext();
            }

            if (resetUnitInfoOffset)
            {
                ReapplyRightBottomShift(false);
            }

            UpdateDerivedPanelVisibilitySnapshot();
        }

        private void SetBuildPanelDirectVisible(bool visible)
        {
            if (buildCommandPanel == null)
            {
                return;
            }

            buildCommandPanel.gameObject.SetActive(visible);
            if (!visible)
            {
                buildCommandPanel.ClearCityCoreContext();
            }
        }

        private static BuildPanelSlideToggle ResolveBuildPanelSlideToggleForBuildPanel(BuildCommandPanel panel)
        {
            if (panel == null)
            {
                return null;
            }

            var toggles = panel.GetComponentsInChildren<BuildPanelSlideToggle>(true);
            if (toggles == null || toggles.Length == 0)
            {
                return null;
            }

            for (var i = 0; i < toggles.Length; i++)
            {
                var candidate = toggles[i];
                if (candidate == null)
                {
                    continue;
                }

                var owner = candidate.GetComponentInParent<BuildCommandPanel>(true);
                if (ReferenceEquals(owner, panel))
                {
                    return candidate;
                }
            }

            return toggles[0];
        }

        private bool TryResolveCityCoreNodeId(UnitView unit, out string nodeId)
        {
            nodeId = string.Empty;
            if (!IsOwnedCityCoreBuildingProxy(unit))
            {
                return false;
            }

            nodeId = unit.UnitId;
            return !string.IsNullOrWhiteSpace(nodeId);
        }

        private bool TryResolveRecipeBuilding(UnitView unit, out string nodeId, out string buildingType, out string ownerId)
        {
            nodeId = string.Empty;
            buildingType = string.Empty;
            ownerId = string.Empty;
            if (!IsOwnedRecipeBuildingProxy(unit))
            {
                return false;
            }

            var cache = GameStateCache.Instance;
            if (cache == null)
            {
                return false;
            }

            nodeId = unit.UnitId;
            var node = cache.GetNode(nodeId);
            if (node == null)
            {
                return false;
            }

            buildingType = NormalizeToken(string.IsNullOrWhiteSpace(node.BuildingType) ? unit.UnitType : node.BuildingType);
            ownerId = string.IsNullOrWhiteSpace(node.Owner) ? node.TerritoryOwner : node.Owner;
            return !string.IsNullOrWhiteSpace(nodeId) && !string.IsNullOrWhiteSpace(buildingType);
        }

        private bool IsOwnedCityCoreBuildingProxy(UnitView unit)
        {
            if (unit == null)
            {
                return false;
            }

            var cache = GameStateCache.Instance;
            if (cache == null)
            {
                return false;
            }

            var localOwner = NormalizeToken(cache.MyPlayerID);
            if (string.IsNullOrWhiteSpace(localOwner))
            {
                return false;
            }

            var fallbackType = NormalizeToken(unit.UnitType);
            if (!IsCityCoreBuildingType(fallbackType))
            {
                return false;
            }

            // If node id is missing or backend owner fields are temporarily empty, fall back to proxy faction.
            if (string.IsNullOrWhiteSpace(unit.UnitId))
            {
                return string.Equals(NormalizeToken(unit.Faction), localOwner, StringComparison.Ordinal);
            }

            var node = cache.GetNode(unit.UnitId);
            if (node == null)
            {
                return string.Equals(NormalizeToken(unit.Faction), localOwner, StringComparison.Ordinal);
            }

            var buildingType = NormalizeToken(string.IsNullOrWhiteSpace(node.BuildingType) ? fallbackType : node.BuildingType);
            if (!IsCityCoreBuildingType(buildingType))
            {
                return false;
            }

            var owner = NormalizeToken(node.Owner);
            var territoryOwner = NormalizeToken(node.TerritoryOwner);
            var faction = NormalizeToken(unit.Faction);
            return string.Equals(owner, localOwner, StringComparison.Ordinal)
                   || string.Equals(territoryOwner, localOwner, StringComparison.Ordinal)
                   || string.Equals(faction, localOwner, StringComparison.Ordinal);
        }

        private bool IsOwnedRecipeBuildingProxy(UnitView unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.UnitId))
            {
                return false;
            }

            var cache = GameStateCache.Instance;
            if (cache == null)
            {
                return false;
            }

            var node = cache.GetNode(unit.UnitId);
            if (node == null)
            {
                return false;
            }

            var localOwner = NormalizeToken(cache.MyPlayerID);
            if (string.IsNullOrWhiteSpace(localOwner))
            {
                return false;
            }

            var owner = NormalizeToken(node.Owner);
            var territoryOwner = NormalizeToken(node.TerritoryOwner);
            var owned = string.Equals(owner, localOwner, StringComparison.Ordinal)
                        || string.Equals(territoryOwner, localOwner, StringComparison.Ordinal);
            if (!owned)
            {
                return false;
            }

            var buildingType = NormalizeToken(string.IsNullOrWhiteSpace(node.BuildingType) ? unit.UnitType : node.BuildingType);
            if (string.IsNullOrWhiteSpace(buildingType))
            {
                return false;
            }

            var catalog = StaticCatalogCache.EnsureInstance();
            if (catalog == null)
            {
                return false;
            }

            if (!catalog.TryGetBuilding(buildingType, out var building) || building == null)
            {
                return false;
            }

            var recipeIds = building.recipe_ids;
            var hasRecipeIds = recipeIds != null && recipeIds.Length > 0;
            var hasDefaultRecipe = !string.IsNullOrWhiteSpace(building.default_recipe_id);
            return hasRecipeIds || hasDefaultRecipe;
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static bool IsCityCoreBuildingType(string value)
        {
            var normalized = NormalizeToken(value);
            return string.Equals(normalized, CityCoreBuildingType, StringComparison.Ordinal);
        }

        private float ResolveUnitInfoShiftX()
        {
            var fallback = Mathf.Abs(unitInfoShiftXWhenBuildPanelOpen);
            if (!useBuildPanelWidthForShift)
            {
                return fallback;
            }

            var panelWidth = 0f;
            if (buildPanelSlideToggle != null)
            {
                panelWidth = Mathf.Max(panelWidth, buildPanelSlideToggle.GetPanelWidth());
            }

            if (buildCommandPanel != null)
            {
                var rect = buildCommandPanel.transform as RectTransform;
                if (rect != null)
                {
                    panelWidth = Mathf.Max(panelWidth, Mathf.Abs(rect.rect.width));
                }
            }

            if (panelWidth <= 1f)
            {
                return fallback;
            }

            var shiftFactor = Mathf.Max(1.15f, buildPanelWidthShiftFactor);
            var resolved = panelWidth * shiftFactor + buildPanelWidthShiftExtra;
            return Mathf.Max(1f, resolved);
        }

        private float ResolveProductionShiftX()
        {
            var fallback = Mathf.Abs(productionPanelShiftXWhenOpen);
            if (!useProductionPanelWidthForShift || cityCoreProductionPanel == null)
            {
                return fallback;
            }

            var panelWidth = cityCoreProductionPanel.GetPanelWidth();
            if (panelWidth <= 1f)
            {
                return fallback;
            }

            var shiftFactor = Mathf.Max(1.05f, productionPanelWidthShiftFactor);
            var resolved = panelWidth * shiftFactor + productionPanelWidthShiftExtra;
            return Mathf.Max(1f, resolved);
        }

        private float ResolveRecipeShiftX()
        {
            var fallback = Mathf.Abs(recipePanelShiftXWhenOpen);
            if (!useRecipePanelWidthForShift || recipeSynthesisPanel == null)
            {
                return fallback;
            }

            var panelWidth = recipeSynthesisPanel.GetPanelWidth();
            if (panelWidth <= 1f)
            {
                return fallback;
            }

            var shiftFactor = Mathf.Max(1.05f, recipePanelWidthShiftFactor);
            var resolved = panelWidth * shiftFactor + recipePanelWidthShiftExtra;
            return Mathf.Max(1f, resolved);
        }
    }
}
