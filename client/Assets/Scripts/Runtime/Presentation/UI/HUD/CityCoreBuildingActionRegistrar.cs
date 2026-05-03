using System;
using System.Collections;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.Domestic;
using Panoptes.Presentation.ViewModels;
using UnityEngine;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    public sealed class CityCoreBuildingActionRegistrar : UnitInfoActionProviderBase
    {
        [Header("Action IDs")]
        [SerializeField] private string buildActionId = "action_3";
        [SerializeField] private string recipeActionId = "open_recipe_synthesis";

        [Header("Labels")]
        [SerializeField] private string buildActionLabel = "Build";
        [SerializeField] private string recipeActionLabel = "Synthesis";

        [Header("References")]
        [SerializeField] private MapPlanningInputController mapPlanningInputController;
        [SerializeField] private UnitInfoPanelController unitInfoPanelController;
        [SerializeField] private RectTransform nextStageButtonRect;
        [SerializeField] private RectTransform turnPanelRect;
        [SerializeField] private RecipeSynthesisPanel recipeSynthesisPanel;
        [SerializeField] private bool hideRecipePanelOnStart = true;
        [SerializeField] private bool autoFindNextStageButton = true;
        [SerializeField] private bool autoFindTurnPanel = true;

        [Header("Recipe Derived Panel")]
        [SerializeField] private float recipePanelShiftXWhenOpen = 360f;
        [SerializeField] private bool useRecipePanelWidthForShift = true;
        [SerializeField] private float recipePanelWidthShiftFactor = 1.12f;
        [SerializeField] private float recipePanelWidthShiftExtra = 0f;

        [Header("Right-Bottom Group Shift")]
        [SerializeField] private float rightGroupShiftDuration = 0.2f;
        [SerializeField] private AnimationCurve rightGroupShiftCurve = null;

        private BuildCatalogContextStore _buildCatalogContextStore;
        private ManagementPanelVisibilityStore _managementPanelVisibilityStore;
        private CityCoreBuildingActionResolver _resolver;
        private RecipeSynthesisPanel _subscribedRecipePanel;
        private bool _nextStageBasePositionReady;
        private Vector2 _nextStageBaseAnchoredPos;
        private bool _turnPanelBasePositionReady;
        private Vector2 _turnPanelBaseAnchoredPos;
        private Coroutine _nextStageShiftRoutine;
        private Coroutine _panelSwitchRoutine;
        private Coroutine _initialPanelStateRoutine;
        private MapPlanningInputController _subscribedMapPlanningInputController;
        private string _activeUnitInfoNodeId = string.Empty;
        private bool _lastBuildCatalogVisible;
        private bool _lastRecipePanelVisible;

        [Inject]
        private void Construct(
            GameStateStore gameStateStore,
            StaticCatalogStore staticCatalogStore,
            ManagementPanelVisibilityStore managementPanelVisibilityStore,
            BuildCatalogContextStore buildCatalogContextStore)
        {
            _resolver = new CityCoreBuildingActionResolver(gameStateStore, staticCatalogStore);
            _managementPanelVisibilityStore = managementPanelVisibilityStore;
            _buildCatalogContextStore = buildCatalogContextStore;
        }

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
                unit => _resolver != null && _resolver.IsOwnedCityCoreBuildingProxy(unit));

            registry.RegisterAction(
                recipeActionId,
                OnRecipeActionClicked,
                string.IsNullOrWhiteSpace(recipeActionLabel) ? "Synthesis" : recipeActionLabel,
                unit => _resolver != null && _resolver.IsOwnedRecipeBuildingProxy(unit));
        }

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            EnsureRightGroupAnimationCurve();
            EnsureInitialPanelState();
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

        private void Start()
        {
            if (_initialPanelStateRoutine != null)
            {
                StopCoroutine(_initialPanelStateRoutine);
            }

            _initialPanelStateRoutine = StartCoroutine(ForceInitialPanelStateAfterLayout());
        }

        private void LateUpdate()
        {
            if (_subscribedMapPlanningInputController == null || !ReferenceEquals(_subscribedMapPlanningInputController, mapPlanningInputController))
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

            if (currentSelection != null &&
                IsBuildCatalogVisible() &&
                (_resolver == null || !_resolver.IsOwnedCityCoreBuildingProxy(currentSelection)))
            {
                CloseBuildCatalog(resetUnitInfoOffset: true);
            }

            if (currentSelection != null &&
                recipeSynthesisPanel != null &&
                recipeSynthesisPanel.IsVisible &&
                (_resolver == null || !_resolver.IsOwnedRecipeBuildingProxy(currentSelection)))
            {
                recipeSynthesisPanel.Hide();
                ReapplyRightBottomShift(false);
            }
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
            StopShiftRoutine();
            StopPanelSwitchRoutine();
        }

        private void ResolveReferences()
        {
            if (mapPlanningInputController == null)
            {
                mapPlanningInputController = SceneObjectFinder.FindFirstSceneObject<MapPlanningInputController>();
            }

            if (unitInfoPanelController == null)
            {
                unitInfoPanelController = UnityEngine.Object.FindAnyObjectByType<UnitInfoPanelController>();
            }

            if (recipeSynthesisPanel == null)
            {
                recipeSynthesisPanel = SceneObjectFinder.FindFirstSceneObject<RecipeSynthesisPanel>();
            }

            if (nextStageButtonRect == null && autoFindNextStageButton)
            {
                nextStageButtonRect = SceneObjectFinder.FindSceneRectByName("NextStageBtn");
            }

            if (unitInfoPanelController != null && nextStageButtonRect != null)
            {
                unitInfoPanelController.SetDockRightOf(nextStageButtonRect);
            }

            if (turnPanelRect == null && autoFindTurnPanel)
            {
                turnPanelRect = SceneObjectFinder.FindSceneRectByName("TrunPanel", "TurnPanel");
            }

            SubscribeRecipePanelEvents();
        }

        private void SubscribeInputEvents()
        {
            if (mapPlanningInputController == null || ReferenceEquals(_subscribedMapPlanningInputController, mapPlanningInputController))
            {
                return;
            }

            if (_subscribedMapPlanningInputController != null)
            {
                _subscribedMapPlanningInputController.NonBuildingMapClicked -= OnNonBuildingMapClicked;
                _subscribedMapPlanningInputController.UnitSelectionChanged -= OnUnitSelectionChanged;
            }

            mapPlanningInputController.NonBuildingMapClicked -= OnNonBuildingMapClicked;
            mapPlanningInputController.NonBuildingMapClicked += OnNonBuildingMapClicked;
            mapPlanningInputController.UnitSelectionChanged -= OnUnitSelectionChanged;
            mapPlanningInputController.UnitSelectionChanged += OnUnitSelectionChanged;
            _subscribedMapPlanningInputController = mapPlanningInputController;
        }

        private void UnsubscribeInputEvents()
        {
            if (_subscribedMapPlanningInputController == null)
            {
                return;
            }

            _subscribedMapPlanningInputController.NonBuildingMapClicked -= OnNonBuildingMapClicked;
            _subscribedMapPlanningInputController.UnitSelectionChanged -= OnUnitSelectionChanged;
            _subscribedMapPlanningInputController = null;
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
            CloseBuildCatalog(resetUnitInfoOffset: false);

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

        private bool IsBuildCatalogVisible()
        {
            return _managementPanelVisibilityStore != null &&
                   _managementPanelVisibilityStore.IsVisible(ManagementPanelId.BuildCatalog);
        }

        private bool IsAnyDerivedPanelVisible()
        {
            return IsBuildCatalogVisible() || (recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible);
        }

        private void UpdateDerivedPanelVisibilitySnapshot()
        {
            _lastBuildCatalogVisible = IsBuildCatalogVisible();
            _lastRecipePanelVisible = recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible;
        }

        private void SyncDerivedPanelStateFromVisibility()
        {
            var buildVisible = IsBuildCatalogVisible();
            var recipeVisible = recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible;

            if (!buildVisible)
            {
                _buildCatalogContextStore?.Clear();
            }

            var changed = buildVisible != _lastBuildCatalogVisible || recipeVisible != _lastRecipePanelVisible;
            if (!changed)
            {
                return;
            }

            _lastBuildCatalogVisible = buildVisible;
            _lastRecipePanelVisible = recipeVisible;
            ReapplyRightBottomShift(false);
        }

        private void OpenBuildCatalogForNode(string nodeId)
        {
            if (_managementPanelVisibilityStore == null || _buildCatalogContextStore == null)
            {
                Debug.LogWarning("[CityCoreBuildingActionRegistrar] Build catalog stores are missing.");
                return;
            }

            _buildCatalogContextStore.SetCityCoreNode(nodeId);
            _managementPanelVisibilityStore.Show(ManagementPanelId.BuildCatalog);
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

        private IEnumerator SwitchFromRecipeToBuild(string nodeId)
        {
            if (recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible)
            {
                recipeSynthesisPanel.Hide();
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, GetRecipePanelSlideDuration()));
            }

            OpenBuildCatalogForNode(nodeId);
            _panelSwitchRoutine = null;
        }

        private IEnumerator SwitchFromBuildToRecipe(string nodeId, string buildingType, string ownerId)
        {
            if (IsBuildCatalogVisible())
            {
                CloseBuildCatalog(resetUnitInfoOffset: false);
            }

            OpenRecipePanelForBuilding(nodeId, buildingType, ownerId);
            _panelSwitchRoutine = null;
            yield break;
        }

        private void CloseAllDerivedPanels(bool resetUnitInfoOffset)
        {
            StopPanelSwitchRoutine();
            recipeSynthesisPanel?.Hide();
            CloseBuildCatalog(resetUnitInfoOffset: false);

            if (resetUnitInfoOffset)
            {
                ReapplyRightBottomShift(false);
            }

            UpdateDerivedPanelVisibilitySnapshot();
        }

        private void OnBuildActionClicked(UnitView unit)
        {
            if (_resolver == null || !_resolver.TryResolveCityCoreNodeId(unit, out var nodeId))
            {
                return;
            }

            _activeUnitInfoNodeId = nodeId;
            ResolveReferences();
            StopPanelSwitchRoutine();

            if (IsBuildCatalogVisible())
            {
                CloseBuildCatalog(resetUnitInfoOffset: true);
                return;
            }

            if (recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible)
            {
                _panelSwitchRoutine = StartCoroutine(SwitchFromRecipeToBuild(nodeId));
                return;
            }

            OpenBuildCatalogForNode(nodeId);
        }

        private void OnRecipeActionClicked(UnitView unit)
        {
            if (_resolver == null || !_resolver.TryResolveRecipeBuilding(unit, out var context))
            {
                return;
            }

            _activeUnitInfoNodeId = context.NodeId;
            ResolveReferences();
            StopPanelSwitchRoutine();

            if (recipeSynthesisPanel != null && recipeSynthesisPanel.IsVisible)
            {
                recipeSynthesisPanel.Hide();
                ReapplyRightBottomShift(false);
                UpdateDerivedPanelVisibilitySnapshot();
                return;
            }

            if (IsBuildCatalogVisible())
            {
                _panelSwitchRoutine = StartCoroutine(SwitchFromBuildToRecipe(
                    context.NodeId,
                    context.BuildingTypeId,
                    context.OwnerId));
                return;
            }

            OpenRecipePanelForBuilding(context.NodeId, context.BuildingTypeId, context.OwnerId);
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

        private void CloseBuildCatalog(bool resetUnitInfoOffset)
        {
            if (IsBuildCatalogVisible())
            {
                _managementPanelVisibilityStore.Hide();
            }

            _buildCatalogContextStore?.Clear();

            if (resetUnitInfoOffset)
            {
                ReapplyRightBottomShift(false);
            }

            UpdateDerivedPanelVisibilitySnapshot();
        }

        private void ReapplyRightBottomShift(bool immediate)
        {
            ApplyRightBottomShift(ResolveRecipeShiftXWhenVisible(), immediate);
        }

        private float ResolveRecipeShiftXWhenVisible()
        {
            if (recipeSynthesisPanel == null || !recipeSynthesisPanel.IsVisible)
            {
                return 0f;
            }

            var fallback = Mathf.Abs(recipePanelShiftXWhenOpen);
            if (!useRecipePanelWidthForShift)
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

            StopShiftRoutine();
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

        private IEnumerator AnimateNextStageShift(Vector2 nextStageTarget, Vector2 turnPanelTarget)
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

        private void EnsureRightGroupAnimationCurve()
        {
            if (rightGroupShiftCurve == null || rightGroupShiftCurve.length == 0)
            {
                rightGroupShiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
        }

        private void StopShiftRoutine()
        {
            if (_nextStageShiftRoutine == null)
            {
                return;
            }

            StopCoroutine(_nextStageShiftRoutine);
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

        private float GetRecipePanelSlideDuration()
        {
            return recipeSynthesisPanel != null ? recipeSynthesisPanel.GetSlideDuration() : 0.22f;
        }
    }
}
