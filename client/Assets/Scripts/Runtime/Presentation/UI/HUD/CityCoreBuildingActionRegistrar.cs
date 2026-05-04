using System;
using System.Collections;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.Map;
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

        [Header("Recipe Derived Panel")]
        [SerializeField] private float recipePanelShiftXWhenOpen = 360f;

        [Header("Right-Bottom Group Shift")]
        [SerializeField] private float rightGroupShiftDuration = 0.2f;
        [SerializeField] private AnimationCurve rightGroupShiftCurve = null;

        private BuildCatalogContextStore _buildCatalogContextStore;
        private ManagementPanelVisibilityStore _managementPanelVisibilityStore;
        private RecipeSynthesisContextStore _recipeSynthesisContextStore;
        private CityCoreBuildingActionResolver _resolver;
        private bool _nextStageBasePositionReady;
        private Vector2 _nextStageBaseAnchoredPos;
        private bool _turnPanelBasePositionReady;
        private Vector2 _turnPanelBaseAnchoredPos;
        private Coroutine _nextStageShiftRoutine;
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
            BuildCatalogContextStore buildCatalogContextStore,
            RecipeSynthesisContextStore recipeSynthesisContextStore,
            MapPlanningInputController injectedMapPlanningInputController,
            UnitInfoPanelController injectedUnitInfoPanelController)
        {
            _resolver = new CityCoreBuildingActionResolver(gameStateStore, staticCatalogStore);
            _managementPanelVisibilityStore = managementPanelVisibilityStore;
            _buildCatalogContextStore = buildCatalogContextStore;
            _recipeSynthesisContextStore = recipeSynthesisContextStore;
            if (mapPlanningInputController == null)
            {
                mapPlanningInputController = injectedMapPlanningInputController;
            }
            if (unitInfoPanelController == null)
            {
                unitInfoPanelController = injectedUnitInfoPanelController;
            }
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
            EnsureRightGroupAnimationCurve();
            EnsureInitialPanelState();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
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
                IsRecipeSynthesisVisible() &&
                (_resolver == null || !_resolver.IsOwnedRecipeBuildingProxy(currentSelection)))
            {
                CloseRecipeSynthesis(resetUnitInfoOffset: false);
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

            StopShiftRoutine();
        }

        private void ResolveReferences()
        {
            if (unitInfoPanelController != null && nextStageButtonRect != null)
            {
                unitInfoPanelController.SetDockRightOf(nextStageButtonRect);
            }
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
            CloseRecipeSynthesis(resetUnitInfoOffset: false);

            if (forceUnitInfoHidden && unitInfoPanelController != null)
            {
                unitInfoPanelController.ForceHideImmediate();
            }

            UpdateDerivedPanelVisibilitySnapshot();
        }

        private bool IsBuildCatalogVisible()
        {
            return _managementPanelVisibilityStore != null &&
                   _managementPanelVisibilityStore.IsVisible(ManagementPanelId.BuildCatalog);
        }

        private bool IsRecipeSynthesisVisible()
        {
            return _managementPanelVisibilityStore != null &&
                   _managementPanelVisibilityStore.IsVisible(ManagementPanelId.RecipeSynthesis);
        }

        private bool IsAnyDerivedPanelVisible()
        {
            return IsBuildCatalogVisible() || IsRecipeSynthesisVisible();
        }

        private void UpdateDerivedPanelVisibilitySnapshot()
        {
            _lastBuildCatalogVisible = IsBuildCatalogVisible();
            _lastRecipePanelVisible = IsRecipeSynthesisVisible();
        }

        private void SyncDerivedPanelStateFromVisibility()
        {
            var buildVisible = IsBuildCatalogVisible();
            var recipeVisible = IsRecipeSynthesisVisible();

            if (!buildVisible)
            {
                _buildCatalogContextStore?.Clear();
            }

            if (!recipeVisible)
            {
                _recipeSynthesisContextStore?.Clear();
            }

            var changed = buildVisible != _lastBuildCatalogVisible || recipeVisible != _lastRecipePanelVisible;
            if (!changed)
            {
                return;
            }

            _lastBuildCatalogVisible = buildVisible;
            _lastRecipePanelVisible = recipeVisible;
            ReapplyRightBottomShift(!buildVisible && !recipeVisible);
        }

        private void OpenBuildCatalogForNode(string nodeId)
        {
            if (_managementPanelVisibilityStore == null || _buildCatalogContextStore == null)
            {
                Debug.LogWarning("[CityCoreBuildingActionRegistrar] Build catalog stores are missing.");
                return;
            }

            _buildCatalogContextStore.SetCityCoreNode(nodeId);
            _recipeSynthesisContextStore?.Clear();
            _managementPanelVisibilityStore.Show(ManagementPanelId.BuildCatalog);
            ReapplyRightBottomShift(false);
            UpdateDerivedPanelVisibilitySnapshot();
        }

        private void OpenRecipePanelForBuilding(string nodeId, string buildingType, string ownerId)
        {
            if (_managementPanelVisibilityStore == null || _recipeSynthesisContextStore == null)
            {
                Debug.LogWarning("[CityCoreBuildingActionRegistrar] Recipe synthesis stores are missing.");
                return;
            }

            _recipeSynthesisContextStore.SetContext(nodeId, buildingType, ownerId);
            _buildCatalogContextStore?.Clear();
            _managementPanelVisibilityStore.Show(ManagementPanelId.RecipeSynthesis);
            ReapplyRightBottomShift(false);
            UpdateDerivedPanelVisibilitySnapshot();
        }

        private void CloseAllDerivedPanels(bool resetUnitInfoOffset)
        {
            CloseRecipeSynthesis(resetUnitInfoOffset: false);
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

            if (IsBuildCatalogVisible())
            {
                CloseBuildCatalog(resetUnitInfoOffset: true);
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

            if (IsRecipeSynthesisVisible())
            {
                CloseRecipeSynthesis(resetUnitInfoOffset: true);
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

        private void CloseRecipeSynthesis(bool resetUnitInfoOffset)
        {
            if (IsRecipeSynthesisVisible())
            {
                _managementPanelVisibilityStore.Hide();
            }

            _recipeSynthesisContextStore?.Clear();

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
            if (!IsRecipeSynthesisVisible())
            {
                return 0f;
            }

            return Mathf.Max(1f, Mathf.Abs(recipePanelShiftXWhenOpen));
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

    }
}
