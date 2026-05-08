using System;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Services;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Binders.Ugui;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.Common;
using Panoptes.Presentation.ViewModels;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    /// <summary>
    /// Unit detail panel shown at the bottom-right when a unit is selected.
    /// </summary>
    public sealed class UnitInfoPanelController : MonoBehaviour
    {
        [Serializable]
        private sealed class ActionButtonSlot : UnitInfoActionButtonSlot
        {
        }

        [Header("References")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image panelBackground;
        [SerializeField] private Image unitIcon;
        [SerializeField] private TMP_Text unitNameText;
        [SerializeField] private TMP_Text unitDescriptionText;
        [SerializeField] private TMP_Text planningSummaryText;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TMP_Text hpValueText;
        [SerializeField] private RectTransform actionButtonsRoot;
        [SerializeField] private RectTransform directOrderButtonsRoot;
        [SerializeField] private ActionButtonSlot[] actionButtons;
        [SerializeField] private Button moveButton;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button holdButton;
        [SerializeField] private Button chargeButton;
        [SerializeField] private UnitInfoActionRegistry actionRegistry;
        [SerializeField] private MapPlanningInputController mapPlanningInputController;

        [Header("Auto Layout")]
        [SerializeField] private bool autoBuildDefaultLayout = true;

        [Header("Icon")]
        [SerializeField] private string unitIconResourcesRoot = "Icons/Units";
        [SerializeField] private string buildingIconResourcesRoot = "Icons/Buildings";
        [SerializeField] private string resourceIconResourcesRoot = "Icons/Resources";

        [Header("Portrait Camera")]
        [SerializeField] private RawImage unitPortraitRawImage;
        [SerializeField] private bool enablePortraitCamera = false;
        [SerializeField] private bool portraitRealtime = false;
        [SerializeField] private bool portraitKeepSceneBackground = true;
        [SerializeField] private int portraitTextureSize = 256;
        [SerializeField] private float portraitFov = 30f;
        [SerializeField] private float portraitMinDistance = 0.9f;
        [SerializeField] private float portraitDistanceScale = 1.15f;
        [SerializeField] private float portraitDistanceOffset = 0.5f;
        [SerializeField] private float portraitHeightOffset = 0.2f;
        [SerializeField] private float portraitCameraVerticalOffsetScale = -0.1f;
        [SerializeField] private bool enablePortraitFillLight = true;
        [SerializeField] private Color portraitFillLightColor = new Color(1f, 0.97f, 0.9f, 1f);
        [SerializeField] private float portraitFillLightIntensity = 1.8f;
        [SerializeField] private float portraitFillLightRange = 18f;
        [SerializeField] private float portraitFillLightSpotAngle = 80f;
        [SerializeField] private float portraitFillLightVerticalOffset = 0.06f;
        [SerializeField] private float portraitFillLightForwardOffset = -0.05f;

        [Header("Slide")]
        [SerializeField] private float hiddenOffsetX = 420f;
        [SerializeField] private float hiddenBottomMargin = 16f;
        [SerializeField] private float shownRightMargin = 16f;
        [SerializeField] private float shownBottomMargin = 16f;
        [SerializeField] private float slideDuration = 0.2f;
        [SerializeField] private AnimationCurve slideCurve = null;
        [SerializeField] private float externalOffsetSlideDuration = 0.2f;
        [SerializeField] private AnimationCurve externalOffsetCurve = null;
        [SerializeField] private RectTransform dockRightOfRect;
        [SerializeField] private float dockSpacing = 12f;

        [Header("Button Repair")]
        [SerializeField] private bool autoRepairActionButtons = true;
        [SerializeField] private Vector2 defaultActionButtonSize = new Vector2(90f, 28f);
        [SerializeField] private Color defaultActionButtonColor = new Color(0.2f, 0.45f, 0.8f, 0.92f);

        private UnitView _currentUnit;
        private MapPlanningInputController _injectedMapPlanningInputController;
        private readonly EventSubscriptionBag _subscriptions = new();
        private readonly UnitInfoDefaultLayoutBuilder _defaultLayoutBuilder = new();
        private readonly UnitInfoActionListBinder _actionListBinder = new();
        private readonly UnitInfoDirectOrderPanelBinder _directOrderPanelBinder = new();
        private bool _unitSelectionSubscribed;
        private MapPlanningInputController _subscribedMapPlanningInputController;
        private UnitInfoPanelSlideAnimator _slideAnimator;
        private UnitInfoPortraitCameraLifecycle _portraitCameraLifecycle;
        private UnitInfoViewModel _unitInfoViewModel;
        private UnitInfoUguiBinder _unitInfoBinder;
        private IDisposable _unitInfoStateSubscription;
        private UnitInfoUguiBinder.References _unitInfoBinderReferences;
        private bool _unitInfoBinderReferencesSet;
        private bool _reactiveBinderReady;
        private bool _builtInActionsRegistered;
        private bool _registeringBuiltInActions;
        private bool _refreshingActionButtons;
        private GameStateStore _gameStateStore;
        private PlanningIntentService _planningIntentService;
        public UnitView CurrentUnit => _currentUnit;
        public bool IsOpen => _slideAnimator != null && _slideAnimator.IsOpen;

        [Inject]
        private void Construct(
            UnitInfoViewModel unitInfoViewModel,
            MapPlanningInputController injectedMapPlanningInputController,
            GameStateStore gameStateStore,
            PlanningIntentService planningIntentService)
        {
            _unitInfoViewModel = unitInfoViewModel;
            _injectedMapPlanningInputController = injectedMapPlanningInputController;
            _gameStateStore = gameStateStore;
            _planningIntentService = planningIntentService;
            ResolveInjectedMapPlanningInputController();
            if (_reactiveBinderReady)
            {
                EnsureReactiveBinder();
            }
        }

        private void Awake()
        {
            ResolveReferences();
            RegisterBuiltInActions();
            if (slideCurve == null || slideCurve.length == 0)
            {
                slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
            if (externalOffsetCurve == null || externalOffsetCurve.length == 0)
            {
                externalOffsetCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
            EnsureRuntimeHelpers();
            ConfigureSlideAnimator();
            EnsurePortraitUi();
            if (autoBuildDefaultLayout)
            {
                EnsureDefaultLayout();
            }
            EnsureUnitDescriptionUi();
            HideLegacyPlanningTexts();
            EnsurePlanningSummaryUi();
            EnsureRequiredActionButtonSlots();
            if (autoRepairActionButtons)
            {
                RepairActionButtonLayoutAndVisuals();
            }
            _directOrderPanelBinder.BindListeners(
                GetDirectOrderButtons(),
                UnitInfoDirectOrderButtonActions.ForController(() => mapPlanningInputController));
            _reactiveBinderReady = true;
            EnsureReactiveBinder();
            ResolveAnchoredPositions();
            SetPanelVisibleImmediate(false);
            SetPortraitVisible(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            _subscriptions.Clear();
            TrySubscribeUnitSelection();
            TrySubscribeActionRegistry();
            EnsureReactiveBinder();

            _subscriptions.Add(
                () => ActionLock.OnChanged += OnActionLockChanged,
                () => ActionLock.OnChanged -= OnActionLockChanged);
        }

        private void Start()
        {
            // Prefabs are kept active for authoring, so enforce the runtime hidden state after all
            // scene references such as NextStageBtn have had a chance to dock this panel.
            ForceHideImmediate();
        }

        private void OnDisable()
        {
            UnsubscribeUnitSelection();
            UnsubscribeActionRegistry();
            _subscriptions.Clear();
            _unitInfoBinder?.Unbind();
            UnsubscribeReactiveState();
            _slideAnimator?.StopAnimations();
            DisablePortraitCamera();
        }

        private void OnDestroy()
        {
            _unitInfoBinder?.Dispose();
            _unitInfoBinder = null;
            UnsubscribeReactiveState();
            _unitInfoBinderReferencesSet = false;
            ReleasePortraitResources();
        }

        private void LateUpdate()
        {
            if (!_unitSelectionSubscribed)
            {
                TrySubscribeUnitSelection();
            }

            if (actionRegistry == null)
            {
                ResolveReferences();
                RegisterBuiltInActions();
                TrySubscribeActionRegistry();
            }

            if (IsOpen && _currentUnit != null && enablePortraitCamera && portraitRealtime)
            {
                if (!TryRefreshUnitPortrait(forceRender: false))
                {
                    SetPortraitVisible(false);
                    RefreshUnitIcon();
                }
            }
        }

        public void OpenForUnit(UnitView unit)
        {
            if (unit == null)
            {
                Close();
                return;
            }

            _currentUnit = unit;
            SelectReactiveUnit(unit);
            RefreshSelectionUi();
            AnimateVisibility(true);
        }

        public void Close()
        {
            _currentUnit = null;
            ClearReactiveSelection();
            DisablePortraitCamera();
            SetPortraitVisible(false);
            AnimateVisibility(false);
        }

        public void ForceHideImmediate()
        {
            _currentUnit = null;
            ClearReactiveSelection();

            ResolveReferences();
            ResolveAnchoredPositions();
            _slideAnimator?.StopAnimations();
            SetPanelVisibleImmediate(false);
            DisablePortraitCamera();
            SetPortraitVisible(false);
            RefreshActionButtons();
            RefreshPlanningUi();
        }

        public void SetExternalOffset(Vector2 offset, bool immediate = false)
        {
            ResolveAnchoredPositions();
            _slideAnimator?.SetExternalOffset(offset, immediate);
        }

        private void OnUnitSelectionChanged(UnitView selected)
        {
            if (selected == null)
            {
                Close();
                return;
            }

            OpenForUnit(selected);
        }

        private void OnActionLockChanged(bool _)
        {
            if (_currentUnit == null || !IsOpen)
            {
                return;
            }

            SelectReactiveUnit(_currentUnit);
            RenderReactiveState();
        }

        private void OnActionRegistryChanged()
        {
            if (_registeringBuiltInActions || _refreshingActionButtons)
            {
                return;
            }

            if (_currentUnit == null || !IsOpen)
            {
                return;
            }

            RefreshActionButtons();
        }

        private void RefreshSelectionUi()
        {
            RefreshUnitView();
            RefreshActionButtons();
            RefreshPlanningUi();
        }

        private void RefreshUnitView()
        {
            if (_currentUnit == null)
            {
                return;
            }

            EnsureUnitDescriptionUi();
            RenderReactiveState();

            DisablePortraitCamera();
            SetPortraitVisible(false);
            RefreshUnitIcon();
        }

        public void SetDockRightOf(RectTransform target, float spacing = -1f, bool immediate = true)
        {
            if (ReferenceEquals(dockRightOfRect, target) && spacing < 0f)
            {
                return;
            }

            dockRightOfRect = target;
            if (spacing >= 0f)
            {
                dockSpacing = spacing;
            }

            ResolveAnchoredPositions();
            if (immediate && panelRoot != null)
            {
                panelRoot.anchoredPosition = _slideAnimator.GetTargetAnchoredPosition(IsOpen);
            }
        }

        private void RefreshUnitIcon()
        {
            if (unitIcon == null || _currentUnit == null)
            {
                return;
            }

            var unitType = NormalizeToken(_currentUnit.UnitType);
            var state = _unitInfoViewModel?.Current;
            var iconKey = NormalizeToken(state != null &&
                state.HasSelection &&
                string.Equals(state.UnitId, _currentUnit.UnitId, StringComparison.Ordinal)
                    ? state.IconKey
                    : string.Empty);
            if (string.IsNullOrEmpty(unitType) && string.IsNullOrEmpty(iconKey))
            {
                unitIcon.sprite = null;
                return;
            }

            var sprite = UiIconLoader.LoadSprite(
                iconKey,
                unitType,
                unitIconResourcesRoot,
                buildingIconResourcesRoot,
                resourceIconResourcesRoot);
            unitIcon.enabled = true;
            unitIcon.gameObject.SetActive(true);
            unitIcon.preserveAspect = true;
            unitIcon.sprite = sprite;
            unitIcon.color = sprite == null ? new Color(0.3f, 0.3f, 0.3f, 1f) : Color.white;
        }

        private bool TryRefreshUnitPortrait(bool forceRender)
        {
            if (!enablePortraitCamera || _currentUnit == null)
            {
                DisablePortraitCamera();
                return false;
            }

            EnsurePortraitUi();
            if (unitPortraitRawImage == null)
            {
                DisablePortraitCamera();
                return false;
            }

            EnsureRuntimeHelpers();
            return _portraitCameraLifecycle.TryRefresh(
                _currentUnit,
                unitPortraitRawImage,
                forceRender,
                BuildPortraitSettings(),
                isActiveAndEnabled,
                IsOpen);
        }

        private void EnsurePortraitUi()
        {
            unitPortraitRawImage = UnitInfoDefaultLayoutBuilder.EnsurePortraitRawImage(
                panelRoot,
                unitIcon,
                unitPortraitRawImage);
            EnsureRuntimeHelpers();
            _portraitCameraLifecycle.BindRawImageTexture(unitPortraitRawImage);
        }

        private void EnsureUnitDescriptionUi()
        {
            unitDescriptionText = UnitInfoDefaultLayoutBuilder.EnsureUnitDescriptionText(
                panelRoot,
                unitDescriptionText);
        }

        private void EnsurePlanningSummaryUi()
        {
            planningSummaryText = UnitInfoDefaultLayoutBuilder.EnsurePlanningSummaryText(
                panelRoot,
                planningSummaryText);
        }

        private void SetPortraitVisible(bool visible)
        {
            EnsureRuntimeHelpers();
            _portraitCameraLifecycle.SetVisible(
                unitPortraitRawImage,
                unitIcon,
                visible,
                BuildPortraitSettings(),
                isActiveAndEnabled,
                IsOpen,
                _currentUnit != null);
        }

        private void UpdatePortraitCameraEnabledState()
        {
            EnsureRuntimeHelpers();
            _portraitCameraLifecycle.SetVisible(
                unitPortraitRawImage,
                unitIcon,
                unitPortraitRawImage != null && unitPortraitRawImage.enabled,
                BuildPortraitSettings(),
                isActiveAndEnabled,
                IsOpen,
                _currentUnit != null);
        }

        private void DisablePortraitCamera()
        {
            _portraitCameraLifecycle?.Disable();
        }

        private void ReleasePortraitResources()
        {
            _portraitCameraLifecycle?.Release(unitPortraitRawImage);
        }

        private void RefreshActionButtons()
        {
            _refreshingActionButtons = true;
            try
            {
                PositionActionButtonsForCurrentUnit();
                _actionListBinder.Refresh(actionButtons, actionRegistry, _currentUnit);
                RegisterBuiltInActions(force: true);
                _actionListBinder.Refresh(actionButtons, actionRegistry, _currentUnit);
            }
            finally
            {
                _refreshingActionButtons = false;
            }
        }

        private void HideLegacyPlanningTexts()
        {
            HideChildByName("PlanningPrompt");
            HideChildByName("QueuedOrderText");
        }

        private void HideChildByName(string childName)
        {
            if (panelRoot == null || string.IsNullOrWhiteSpace(childName))
            {
                return;
            }

            var child = panelRoot.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private void RefreshPlanningUi()
        {
            RenderReactiveState();
        }

        private void ResolveReferences()
        {
            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }
            if (panelRoot == null)
            {
                panelRoot = gameObject.AddComponent<RectTransform>();
            }

            if (actionRegistry == null)
            {
                actionRegistry = GetComponent<UnitInfoActionRegistry>();
                if (actionRegistry == null)
                {
                    actionRegistry = gameObject.AddComponent<UnitInfoActionRegistry>();
                }
            }

            if (mapPlanningInputController == null)
            {
                ResolveInjectedMapPlanningInputController();
            }
        }

        private void RegisterBuiltInActions(bool force = false)
        {
            if ((!force && _builtInActionsRegistered) || actionRegistry == null)
            {
                return;
            }

            _registeringBuiltInActions = true;
            try
            {
                actionRegistry.RegisterAction(
                    "expand_territory",
                    OnBuiltInExpandTerritoryClicked,
                    "建立城堡",
                    IsTerritoryExpansionUnit);
                actionRegistry.RegisterAction(
                    "settle_city",
                    OnBuiltInExpandTerritoryClicked,
                    "建立城堡",
                    IsTerritoryExpansionUnit);
                _builtInActionsRegistered = true;
            }
            finally
            {
                _registeringBuiltInActions = false;
            }
        }

        private void OnBuiltInExpandTerritoryClicked(UnitView unit)
        {
            if (unit == null || !IsTerritoryExpansionUnit(unit))
            {
                return;
            }

            ResolveInjectedMapPlanningInputController();
            if (mapPlanningInputController != null &&
                mapPlanningInputController.RequestExpandTerritory(unit.UnitId))
            {
                return;
            }

            if (_planningIntentService == null)
            {
                PanoptesLog.Warning("[UnitInfoPanelController] PlanningIntentService missing, cannot send expand request.");
                return;
            }

            if (!TryResolveCenterNodeId(unit, out var centerNodeId))
            {
                centerNodeId = string.Empty;
            }

            _planningIntentService.ExpandTerritory(unit.UnitId, centerNodeId);
            PanoptesLog.Log($"[UnitInfoPanelController] territory action sent. unit={unit.UnitId} center={centerNodeId}");
        }

        private static bool IsTerritoryExpansionUnit(UnitView unit)
        {
            var unitType = NormalizeToken(unit != null ? unit.UnitType : string.Empty);
            return string.Equals(unitType, "settler", StringComparison.Ordinal) ||
                   string.Equals(unitType, "pioneer", StringComparison.Ordinal) ||
                   string.Equals(unitType, "expander", StringComparison.Ordinal) ||
                   string.Equals(unitType, "engineer", StringComparison.Ordinal);
        }

        private bool TryResolveCenterNodeId(UnitView unit, out string centerNodeId)
        {
            centerNodeId = string.Empty;
            var state = _gameStateStore?.Snapshot;
            if (unit == null ||
                state?.Units == null ||
                state.Nodes == null ||
                !TryGetUnitState(state, unit.UnitId, out var unitState))
            {
                return false;
            }

            foreach (var node in state.Nodes.Values)
            {
                if (node == null ||
                    node.Q != unitState.Q ||
                    node.R != unitState.R ||
                    string.IsNullOrWhiteSpace(node.Id))
                {
                    continue;
                }

                centerNodeId = node.Id.Trim();
                return true;
            }

            return false;
        }

        private static bool TryGetUnitState(GameStateStoreState state, string unitId, out UnitDto unit)
        {
            unit = null;
            if (state?.Units == null || string.IsNullOrWhiteSpace(unitId))
            {
                return false;
            }

            var normalizedUnitId = unitId.Trim();
            if (state.Units.TryGetValue(normalizedUnitId, out unit) && unit != null)
            {
                return true;
            }

            foreach (var candidate in state.Units.Values)
            {
                if (candidate != null &&
                    string.Equals(candidate.Id?.Trim(), normalizedUnitId, StringComparison.OrdinalIgnoreCase))
                {
                    unit = candidate;
                    return true;
                }
            }

            return false;
        }

        private void ResolveInjectedMapPlanningInputController()
        {
            if (mapPlanningInputController == null)
            {
                mapPlanningInputController = _injectedMapPlanningInputController;
            }
        }

        private void TrySubscribeActionRegistry()
        {
            if (actionRegistry == null)
            {
                return;
            }

            actionRegistry.ActionRegistryChanged -= OnActionRegistryChanged;
            actionRegistry.ActionRegistryChanged += OnActionRegistryChanged;
        }

        private void UnsubscribeActionRegistry()
        {
            if (actionRegistry == null)
            {
                return;
            }

            actionRegistry.ActionRegistryChanged -= OnActionRegistryChanged;
        }

        private void TrySubscribeUnitSelection()
        {
            ResolveInjectedMapPlanningInputController();

            if (mapPlanningInputController == null)
            {
                return;
            }

            if (_unitSelectionSubscribed &&
                ReferenceEquals(_subscribedMapPlanningInputController, mapPlanningInputController))
            {
                return;
            }

            UnsubscribeUnitSelection();
            mapPlanningInputController.UnitSelectionChanged -= OnUnitSelectionChanged;
            mapPlanningInputController.UnitSelectionChanged += OnUnitSelectionChanged;
            mapPlanningInputController.CombatSelectionChanged -= RefreshPlanningUi;
            mapPlanningInputController.CombatSelectionChanged += RefreshPlanningUi;
            _subscribedMapPlanningInputController = mapPlanningInputController;
            _unitSelectionSubscribed = true;
        }

        private void UnsubscribeUnitSelection()
        {
            if (_subscribedMapPlanningInputController != null)
            {
                _subscribedMapPlanningInputController.UnitSelectionChanged -= OnUnitSelectionChanged;
                _subscribedMapPlanningInputController.CombatSelectionChanged -= RefreshPlanningUi;
            }

            _subscribedMapPlanningInputController = null;
            _unitSelectionSubscribed = false;
        }

        private void EnsureDefaultLayout()
        {
            var references = _defaultLayoutBuilder.EnsureLayout(
                new UnitInfoDefaultLayoutBuilder.References<ActionButtonSlot>
                {
                    PanelRoot = panelRoot,
                    PanelBackground = panelBackground,
                    UnitIcon = unitIcon,
                    UnitPortraitRawImage = unitPortraitRawImage,
                    UnitNameText = unitNameText,
                    UnitDescriptionText = unitDescriptionText,
                    PlanningSummaryText = planningSummaryText,
                    HpSlider = hpSlider,
                    HpValueText = hpValueText,
                    ActionButtonsRoot = actionButtonsRoot,
                    DirectOrderButtonsRoot = directOrderButtonsRoot,
                    ActionButtons = actionButtons,
                    MoveButton = moveButton,
                    AttackButton = attackButton,
                    HoldButton = holdButton,
                    ChargeButton = chargeButton
                },
                defaultActionButtonSize,
                defaultActionButtonColor,
                _actionListBinder,
                _directOrderPanelBinder,
                CreateSerializedActionButtonSlot);

            panelBackground = references.PanelBackground;
            unitIcon = references.UnitIcon;
            unitPortraitRawImage = references.UnitPortraitRawImage;
            unitNameText = references.UnitNameText;
            unitDescriptionText = references.UnitDescriptionText;
            planningSummaryText = references.PlanningSummaryText;
            hpSlider = references.HpSlider;
            hpValueText = references.HpValueText;
            actionButtonsRoot = references.ActionButtonsRoot;
            directOrderButtonsRoot = references.DirectOrderButtonsRoot;
            actionButtons = references.ActionButtons;
            moveButton = references.MoveButton;
            attackButton = references.AttackButton;
            holdButton = references.HoldButton;
            chargeButton = references.ChargeButton;

            EnsureRuntimeHelpers();
            _portraitCameraLifecycle.BindRawImageTexture(unitPortraitRawImage);

            _directOrderPanelBinder.BindListeners(
                GetDirectOrderButtons(),
                UnitInfoDirectOrderButtonActions.ForController(() => mapPlanningInputController));
            RecreateReactiveBinder();
        }

        private void EnsureRequiredActionButtonSlots()
        {
            actionButtons = _actionListBinder.EnsureRequiredSlots(
                actionButtons,
                actionButtonsRoot,
                defaultActionButtonSize,
                defaultActionButtonColor,
                CreateSerializedActionButtonSlot);
        }

        private static ActionButtonSlot CreateSerializedActionButtonSlot()
        {
            return new ActionButtonSlot();
        }

        private void ResolveAnchoredPositions()
        {
            EnsureRuntimeHelpers();
            ConfigureSlideAnimator();
        }

        private void AnimateVisibility(bool open)
        {
            ResolveAnchoredPositions();
            _slideAnimator?.AnimateVisibility(open);
        }

        private void SetPanelVisibleImmediate(bool open)
        {
            ResolveAnchoredPositions();
            _slideAnimator?.SetImmediate(open);
        }

        private void EnsureRuntimeHelpers()
        {
            _slideAnimator ??= new UnitInfoPanelSlideAnimator(this, UpdatePortraitCameraEnabledState);
            _portraitCameraLifecycle ??= new UnitInfoPortraitCameraLifecycle();
        }

        private void ConfigureSlideAnimator()
        {
            EnsureRuntimeHelpers();
            _slideAnimator.Configure(
                panelRoot,
                hiddenOffsetX,
                hiddenBottomMargin,
                shownRightMargin,
                shownBottomMargin,
                slideDuration,
                slideCurve,
                externalOffsetSlideDuration,
                externalOffsetCurve,
                dockRightOfRect,
                dockSpacing);
        }

        private UnitInfoDirectOrderButtons GetDirectOrderButtons()
        {
            return new UnitInfoDirectOrderButtons(moveButton, attackButton, holdButton, chargeButton);
        }

        private void EnsureReactiveBinder()
        {
            if (_unitInfoViewModel == null)
            {
                return;
            }

            var references = BuildReactiveBinderReferences();
            if (_unitInfoBinder != null &&
                (!_unitInfoBinderReferencesSet || !ReactiveBinderReferencesMatch(_unitInfoBinderReferences, references)))
            {
                _unitInfoBinder.Dispose();
                _unitInfoBinder = null;
                _unitInfoBinderReferencesSet = false;
            }

            if (_unitInfoBinder == null)
            {
                _unitInfoBinderReferences = references;
                _unitInfoBinderReferencesSet = true;
                _unitInfoBinder = new UnitInfoUguiBinder(references, _directOrderPanelBinder);
            }

            _unitInfoBinder.Bind(_unitInfoViewModel);
            SubscribeReactiveState();
        }

        private void RenderReactiveState()
        {
            EnsureReactiveBinder();
            if (_unitInfoViewModel == null)
            {
                return;
            }

            if (_currentUnit != null)
            {
                SelectReactiveUnit(_currentUnit);
            }

            _unitInfoBinder?.Render(_unitInfoViewModel.Current);
        }

        private void SubscribeReactiveState()
        {
            if (_unitInfoStateSubscription != null || _unitInfoViewModel == null)
            {
                return;
            }

            _unitInfoStateSubscription = _unitInfoViewModel.State.Subscribe(
                this,
                static (state, self) => self.OnReactiveStateChanged(state));
        }

        private void UnsubscribeReactiveState()
        {
            _unitInfoStateSubscription?.Dispose();
            _unitInfoStateSubscription = null;
        }

        private void OnReactiveStateChanged(UnitInfoState state)
        {
            if (_currentUnit == null)
            {
                return;
            }

            if (state != null &&
                state.HasSelection &&
                string.Equals(state.UnitId, _currentUnit.UnitId, StringComparison.Ordinal))
            {
                RefreshUnitIcon();
                RefreshActionButtons();
                return;
            }

            _currentUnit = null;
            DisablePortraitCamera();
            SetPortraitVisible(false);
            AnimateVisibility(false);
        }

        private void SelectReactiveUnit(UnitView unit)
        {
            if (_unitInfoViewModel == null || unit == null)
            {
                return;
            }

            _unitInfoViewModel.SelectUnit(unit.UnitId);
            _unitInfoViewModel.SetActionLocked(ActionLock.IsLocked);
        }

        private void ClearReactiveSelection()
        {
            _unitInfoViewModel?.ClearSelection();
            _unitInfoBinder?.Render(_unitInfoViewModel?.Current);
        }

        private void RecreateReactiveBinder()
        {
            _unitInfoBinder?.Dispose();
            _unitInfoBinder = null;
            _unitInfoBinderReferencesSet = false;
            EnsureReactiveBinder();
        }

        private UnitInfoUguiBinder.References BuildReactiveBinderReferences()
        {
            return new UnitInfoUguiBinder.References(
                unitNameText,
                unitDescriptionText,
                planningSummaryText,
                hpSlider,
                hpValueText,
                directOrderButtonsRoot,
                GetDirectOrderButtons(),
                unitIcon,
                unitIconResourcesRoot,
                buildingIconResourcesRoot,
                resourceIconResourcesRoot);
        }

        private static bool ReactiveBinderReferencesMatch(
            UnitInfoUguiBinder.References current,
            UnitInfoUguiBinder.References next)
        {
            return ReferenceEquals(current.UnitNameText, next.UnitNameText) &&
                   ReferenceEquals(current.UnitDescriptionText, next.UnitDescriptionText) &&
                   ReferenceEquals(current.PlanningSummaryText, next.PlanningSummaryText) &&
                   ReferenceEquals(current.HpSlider, next.HpSlider) &&
                   ReferenceEquals(current.HpValueText, next.HpValueText) &&
                   ReferenceEquals(current.DirectOrderButtonsRoot, next.DirectOrderButtonsRoot) &&
                   ReferenceEquals(current.UnitIcon, next.UnitIcon) &&
                   string.Equals(current.UnitIconResourcesRoot, next.UnitIconResourcesRoot, StringComparison.Ordinal) &&
                   string.Equals(current.BuildingIconResourcesRoot, next.BuildingIconResourcesRoot, StringComparison.Ordinal) &&
                   string.Equals(current.ResourceIconResourcesRoot, next.ResourceIconResourcesRoot, StringComparison.Ordinal) &&
                   ReferenceEquals(current.DirectOrderButtons.Move, next.DirectOrderButtons.Move) &&
                   ReferenceEquals(current.DirectOrderButtons.Attack, next.DirectOrderButtons.Attack) &&
                   ReferenceEquals(current.DirectOrderButtons.Hold, next.DirectOrderButtons.Hold) &&
                   ReferenceEquals(current.DirectOrderButtons.Charge, next.DirectOrderButtons.Charge);
        }

        private UnitInfoPortraitCameraLifecycle.Settings BuildPortraitSettings()
        {
            return new UnitInfoPortraitCameraLifecycle.Settings(
                enablePortraitCamera,
                portraitRealtime,
                portraitKeepSceneBackground,
                portraitTextureSize,
                portraitFov,
                portraitMinDistance,
                portraitDistanceScale,
                portraitDistanceOffset,
                portraitHeightOffset,
                portraitCameraVerticalOffsetScale,
                enablePortraitFillLight,
                portraitFillLightColor,
                portraitFillLightIntensity,
                portraitFillLightRange,
                portraitFillLightSpotAngle,
                portraitFillLightVerticalOffset,
                portraitFillLightForwardOffset);
        }

        private void RepairActionButtonLayoutAndVisuals()
        {
            PositionActionButtonsForCurrentUnit();
            _actionListBinder.RepairLayoutAndVisuals(
                actionButtons,
                actionButtonsRoot,
                defaultActionButtonSize,
                defaultActionButtonColor);
        }

        private void PositionActionButtonsForCurrentUnit()
        {
            if (actionButtonsRoot == null)
            {
                return;
            }

            actionButtonsRoot.anchorMin = new Vector2(0f, 1f);
            actionButtonsRoot.anchorMax = new Vector2(0f, 1f);
            actionButtonsRoot.pivot = new Vector2(0f, 1f);
            actionButtonsRoot.anchoredPosition = IsTerritoryExpansionUnit(_currentUnit)
                ? new Vector2(112f, -10f)
                : new Vector2(14f, -10f);
            actionButtonsRoot.SetAsLastSibling();
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

#if UNITY_EDITOR
        [ContextMenu("Build Default Layout")]
        public void BuildDefaultLayoutForEditor()
        {
            ResolveReferences();
            EnsureDefaultLayout();
            RepairActionButtonLayoutAndVisuals();
            ResolveAnchoredPositions();
            SetPanelVisibleImmediate(true);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
#endif
    }
}
