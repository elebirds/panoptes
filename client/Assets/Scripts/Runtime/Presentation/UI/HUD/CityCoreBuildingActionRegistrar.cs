using System;
using System.Collections;
using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.Domestic;
using UnityEngine;

namespace Panoptes.Presentation.UI.HUD
{
    /// <summary>
    /// Registers city-core-specific actions on UnitInfoPanel:
    /// 1) open derived build panel
    /// 2) open unified recipe panel
    /// </summary>
    public sealed class CityCoreBuildingActionRegistrar : UnitInfoActionProviderBase
    {
        private const string CityCoreBuildingType = "city_core";

        [Header("Action IDs")]
        [SerializeField] private string buildActionId = "action_3";
        [SerializeField] private string recipeActionId = "open_recipe_synthesis";

        [Header("Labels")]
        [SerializeField] private string buildActionLabel = "Build";
        [SerializeField] private string recipeActionLabel = "Synthesis";

        [Header("References")]
        [SerializeField] private MapInputHandler mapInputHandler;
        [SerializeField] private UnitInfoPanelController unitInfoPanelController;
        [SerializeField] private RectTransform nextStageButtonRect;
        [SerializeField] private RectTransform turnPanelRect;
        [SerializeField] private RecipeSynthesisPanel recipeSynthesisPanel;
        [SerializeField] private BuildPanelSlideToggle buildPanelSlideToggle;
        [SerializeField] private BuildCommandPanel buildCommandPanel;
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

        [Header("Recipe Derived Panel")]
        [SerializeField] private float recipePanelShiftXWhenOpen = 360f;
        [SerializeField] private bool useRecipePanelWidthForShift = true;
        [SerializeField] private float recipePanelWidthShiftFactor = 1.12f;
        [SerializeField] private float recipePanelWidthShiftExtra = 0f;

        [Header("Right-Bottom Group Shift")]
        [SerializeField] private float rightGroupShiftDuration = 0.2f;
        [SerializeField] private AnimationCurve rightGroupShiftCurve = null;

        private bool _buildPanelOpen;
        private RecipeSynthesisPanel _subscribedRecipePanel;
        private bool _nextStageBasePositionReady;
        private Vector2 _nextStageBaseAnchoredPos;
        private bool _turnPanelBasePositionReady;
        private Vector2 _turnPanelBaseAnchoredPos;
        private Coroutine _nextStageShiftRoutine;
        private Coroutine _panelSwitchRoutine;
        private Coroutine _initialPanelStateRoutine;
        private MapInputHandler _subscribedMapInputHandler;
        private string _activeUnitInfoNodeId = string.Empty;
        private bool _lastBuildPanelVisible;
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
                buildActionId,
                OnBuildActionClicked,
                string.IsNullOrWhiteSpace(buildActionLabel) ? "Build" : buildActionLabel,
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

        private void Start()
        {
            if (_initialPanelStateRoutine != null)
            {
                StopCoroutine(_initialPanelStateRoutine);
            }

            _initialPanelStateRoutine = StartCoroutine(ForceInitialPanelStateAfterLayout());
        }

        private void OnDisable()
        {
            UnsubscribeInputEvents();
            if (_initialPanelStateRoutine != null)
            {
                StopCoroutine(_initialPanelStateRoutine);
                _initialPanelStateRoutine = null;
            }

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
                CloseAllDerivedPanels(resetUnitInfoOffset: true);
            }

            var currentSelection = unitInfoPanelController != null ? unitInfoPanelController.CurrentUnit : null;
            if (currentSelection == null && IsAnyDerivedPanelVisible())
            {
                CloseAllDerivedPanels(resetUnitInfoOffset: true);
                return;
            }

            if (currentSelection != null && IsBuildPanelCurrentlyVisible() && !IsOwnedCityCoreBuildingProxy(currentSelection))
            {
                CloseBuildPanel(resetUnitInfoOffset: true);
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

            if (unitInfoPanelController != null && nextStageButtonRect != null)
            {
                unitInfoPanelController.SetDockRightOf(nextStageButtonRect);
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

            SubscribeRecipePanelEvents();
        }

        private IEnumerator ForceInitialPanelStateAfterLayout()
        {
            yield return null;
            ResolveReferences();
            EnsureInitialPanelState(forceUnitInfoHidden: true);
            _initialPanelStateRoutine = null;
        }

        private void EnsureInitialPanelState(bool forceUnitInfoHidden = false)
        {
            EnsureRightGroupAnimationCurve();
            CacheRightGroupBasePositionIfNeeded();
            var collapseBuildPanelOnStartup = forceBuildPanelCollapsedOnStartup || startBuildPanelCollapsed;

            if (buildPanelSlideToggle != null)
            {
                if (collapseBuildPanelOnStartup)
                {
                    buildPanelSlideToggle.ForceRecalculatePositions();
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

            if (hideRecipePanelOnStart && recipeSynthesisPanel != null)
            {
                recipeSynthesisPanel.Hide();
            }

            if (forceUnitInfoHidden && unitInfoPanelController != null)
            {
                unitInfoPanelController.ForceHideImmediate();
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

        private void UnsubscribeRecipePanelEvents()
        {
            if (_subscribedRecipePanel == null)
            {
                return;
            }

            _subscribedRecipePanel.VisibilityChanged -= OnRecipePanelVisibilityChanged;
            _subscribedRecipePanel = null;
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
                   || (recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible);
        }

        private void UpdateDerivedPanelVisibilitySnapshot()
        {
            _lastBuildPanelVisible = IsBuildPanelCurrentlyVisible();
            _lastRecipePanelVisible = recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible;
        }

        private void SyncDerivedPanelStateFromVisibility()
        {
            var buildVisible = IsBuildPanelCurrentlyVisible();
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
                          || recipeVisible != _lastRecipePanelVisible;

            if (changed)
            {
                _lastBuildPanelVisible = buildVisible;
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

            var buildPanelRect = buildCommandPanel != null ? buildCommandPanel.transform as RectTransform : null;
            if (buildPanelSlideToggle == null ||
                (buildPanelRect != null && !buildPanelSlideToggle.ControlsPanel(buildPanelRect)))
            {
                buildPanelSlideToggle = ResolveBuildPanelSlideToggleForBuildPanel(buildCommandPanel);
            }

            if (buildPanelSlideToggle != null)
            {
                if (!buildPanelSlideToggle.gameObject.activeSelf)
                {
                    buildPanelSlideToggle.gameObject.SetActive(true);
                }
                buildPanelSlideToggle.ForceRecalculatePositions();
                buildPanelSlideToggle.SetCollapsed(true, true);
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

        private void CloseAllDerivedPanels(bool resetUnitInfoOffset)
        {
            StopPanelSwitchRoutine();

            if (recipeSynthesisPanel != null)
            {
                recipeSynthesisPanel.Hide();
            }

            CloseBuildPanel(resetUnitInfoOffset: false);

            if (resetUnitInfoOffset)
            {
                ReapplyRightBottomShift(false);
            }

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

            var isBuildPanelVisible = IsBuildPanelCurrentlyVisible();
            var isRecipePanelVisible = recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible;

            if (isBuildPanelVisible)
            {
                CloseBuildPanel(true);
                return;
            }

            if (isRecipePanelVisible)
            {
                _panelSwitchRoutine = StartCoroutine(SwitchFromRecipeToBuild(nodeId));
                return;
            }

            OpenBuildPanelForNode(nodeId);
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
            CloseAllDerivedPanels(resetUnitInfoOffset: true);
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
            CloseAllDerivedPanels(resetUnitInfoOffset: true);
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
                if (ReferenceEquals(owner, panel) && candidate.ControlsPanel(panel.transform as RectTransform))
                {
                    return candidate;
                }
            }

            return null;
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
            if (!cache.TryGetBuilding(nodeId, out var building) || building == null)
            {
                return false;
            }

            buildingType = NormalizeToken(building.BuildingTypeId);
            ownerId = ResolveAuthoritativeBuildingOwner(cache, building);
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

            if (string.IsNullOrWhiteSpace(unit.UnitId))
            {
                return false;
            }

            if (!cache.TryGetBuilding(unit.UnitId, out var building) || building == null)
            {
                return false;
            }

            var buildingType = NormalizeToken(building.BuildingTypeId);
            if (!building.IsCityCore && !IsCityCoreBuildingType(buildingType))
            {
                return false;
            }

            var owner = NormalizeToken(ResolveAuthoritativeBuildingOwner(cache, building));
            return string.Equals(owner, localOwner, StringComparison.Ordinal);
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

            if (!cache.TryGetBuilding(unit.UnitId, out var building) || building == null)
            {
                return false;
            }

            var localOwner = NormalizeToken(cache.MyPlayerID);
            if (string.IsNullOrWhiteSpace(localOwner))
            {
                return false;
            }

            var owner = NormalizeToken(ResolveAuthoritativeBuildingOwner(cache, building));
            if (!string.Equals(owner, localOwner, StringComparison.Ordinal))
            {
                return false;
            }

            var buildingType = NormalizeToken(building.BuildingTypeId);
            if (string.IsNullOrWhiteSpace(buildingType))
            {
                return false;
            }

            var catalog = StaticCatalogCache.EnsureInstance();
            if (catalog == null)
            {
                return false;
            }

            if (!catalog.TryGetBuilding(buildingType, out var catalogBuilding) || catalogBuilding == null)
            {
                return false;
            }

            var recipeIds = catalogBuilding.recipe_ids;
            var hasRecipeIds = recipeIds != null && recipeIds.Length > 0;
            var hasDefaultRecipe = !string.IsNullOrWhiteSpace(catalogBuilding.default_recipe_id);
            return hasRecipeIds || hasDefaultRecipe;
        }

        private static string ResolveAuthoritativeBuildingOwner(GameStateCache cache, Panoptes.Core.Domain.BuildingDto building)
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
