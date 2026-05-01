using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Presentation.Common;
using Panoptes.Presentation.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Auto Find")]
        [SerializeField] private bool autoFindActionRegistry = true;
        [SerializeField] private bool autoFindMapPlanningInputController = true;
        [SerializeField] private bool autoBuildDefaultLayout = true;

        [Header("Icon")]
        [SerializeField] private string unitIconResourcesRoot = "Icons/Units";
        [SerializeField] private string buildingIconResourcesRoot = "Icons/Buildings";

        [Header("Portrait Camera")]
        [SerializeField] private RawImage unitPortraitRawImage;
        [SerializeField] private bool enablePortraitCamera = true;
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
        private PlanningDraftCache _planningDraftCache;
        private readonly EventSubscriptionBag _subscriptions = new();
        private readonly UnitInfoActionListBinder _actionListBinder = new();
        private readonly UnitInfoDirectOrderPanelBinder _directOrderPanelBinder = new();
        private bool _unitSelectionSubscribed;
        private UnitInfoPanelSlideAnimator _slideAnimator;
        private UnitInfoPortraitCameraLifecycle _portraitCameraLifecycle;
        private UnitInfoPlanningSummaryPresenter _planningSummaryPresenter;
        public UnitView CurrentUnit => _currentUnit;
        public bool IsOpen => _slideAnimator != null && _slideAnimator.IsOpen;

        private void Awake()
        {
            ResolveReferences();
            EnsureDefaultActionProviders();
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
            ResolveAnchoredPositions();
            SetPanelVisibleImmediate(false);
            SetPortraitVisible(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            _subscriptions.Clear();
            _planningDraftCache = PlanningDraftCache.Instance ?? PlanningDraftCache.EnsureInstance();
            if (_planningDraftCache != null)
            {
                var planningDraftCache = _planningDraftCache;
                _subscriptions.Add(
                    () => planningDraftCache.OrdersChanged += RefreshPlanningUi,
                    () => planningDraftCache.OrdersChanged -= RefreshPlanningUi);
            }
            TrySubscribeUnitSelection();
            TrySubscribeActionRegistry();

            var cache = GameStateCache.Instance;
            if (cache != null)
            {
                _subscriptions.Add(
                    () => cache.OnUnitsChanged += OnUnitsChanged,
                    () => cache.OnUnitsChanged -= OnUnitsChanged);
                _subscriptions.Add(
                    () => cache.OnNodeChanged += OnNodeChanged,
                    () => cache.OnNodeChanged -= OnNodeChanged);
                _subscriptions.Add(
                    () => cache.OnPhaseChanged += OnPhaseChanged,
                    () => cache.OnPhaseChanged -= OnPhaseChanged);
                _subscriptions.Add(
                    () => cache.OnGameOver += OnGameOver,
                    () => cache.OnGameOver -= OnGameOver);
            }

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
            _planningDraftCache = null;
            _slideAnimator?.StopAnimations();
            DisablePortraitCamera();
        }

        private void OnDestroy()
        {
            ReleasePortraitResources();
        }

        private void LateUpdate()
        {
            if (!_unitSelectionSubscribed || mapPlanningInputController == null)
            {
                TrySubscribeUnitSelection();
            }

            if (actionRegistry == null)
            {
                ResolveReferences();
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
            RefreshSelectionUi();
            AnimateVisibility(true);
        }

        public void Close()
        {
            _currentUnit = null;
            DisablePortraitCamera();
            SetPortraitVisible(false);
            AnimateVisibility(false);
        }

        public void ForceHideImmediate()
        {
            _currentUnit = null;

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

        private void OnUnitsChanged(Panoptes.Core.Events.UnitsChangedEvent evt)
        {
            if (_currentUnit == null || evt == null)
            {
                return;
            }

            if (evt.RemovedIDs != null)
            {
                for (var i = 0; i < evt.RemovedIDs.Count; i++)
                {
                    if (string.Equals(evt.RemovedIDs[i], _currentUnit.UnitId, StringComparison.Ordinal))
                    {
                        Close();
                        return;
                    }
                }
            }

            RefreshUnitHpFromCache();
            RefreshPlanningUi();
        }

        private void OnNodeChanged(NodeChangedEvent evt)
        {
            if (_currentUnit == null || evt == null || string.IsNullOrWhiteSpace(evt.NodeID))
            {
                return;
            }

            if (!string.Equals(evt.NodeID, _currentUnit.UnitId, StringComparison.Ordinal))
            {
                return;
            }

            RefreshUnitHpFromCache();
        }

        private void OnPhaseChanged(PhaseChangedEvent _)
        {
            if (_currentUnit == null || !IsOpen)
            {
                return;
            }

            RefreshSelectionUi();
        }

        private void OnGameOver(GameOverEvent _)
        {
            if (_currentUnit == null || !IsOpen)
            {
                return;
            }

            RefreshSelectionUi();
        }
        private void OnActionLockChanged(bool _)
        {
            if (_currentUnit == null || !IsOpen)
            {
                return;
            }

            RefreshPlanningUi();
        }

        private void OnActionRegistryChanged()
        {
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
            ResolveUnitDisplayTexts(_currentUnit, out var displayName, out var description);
            if (unitNameText != null)
            {
                unitNameText.text = displayName;
            }

            if (unitDescriptionText != null)
            {
                unitDescriptionText.text = description;
                unitDescriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(description));
            }

            RefreshUnitHpFromCache();
            if (TryRefreshUnitPortrait(forceRender: true))
            {
                SetPortraitVisible(true);
                return;
            }

            SetPortraitVisible(false);
            RefreshUnitIcon();
        }

        private void RefreshUnitHpFromCache()
        {
            var state = UnitInfoHpStateResolver.Resolve(_currentUnit, GameStateCache.Instance);
            UnitInfoHpBinder.Apply(hpSlider, hpValueText, state);
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
            if (string.IsNullOrEmpty(unitType))
            {
                unitIcon.sprite = null;
                return;
            }

            var spritePath = $"{unitIconResourcesRoot}/{unitType}";
            var sprite = Resources.Load<Sprite>(spritePath);
            if (sprite == null)
            {
                var buildingSpritePath = $"{buildingIconResourcesRoot}/{unitType}";
                sprite = Resources.Load<Sprite>(buildingSpritePath);
            }
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
            if (panelRoot == null)
            {
                return;
            }

            RectTransform portraitRect;
            if (unitPortraitRawImage == null)
            {
                portraitRect = panelRoot.Find("UnitPortrait") as RectTransform;
                if (portraitRect == null)
                {
                    portraitRect = EnsureChild("UnitPortrait");
                }

                unitPortraitRawImage = portraitRect.GetComponent<RawImage>();
                if (unitPortraitRawImage == null)
                {
                    unitPortraitRawImage = portraitRect.gameObject.AddComponent<RawImage>();
                }
            }

            if (unitPortraitRawImage == null)
            {
                return;
            }

            portraitRect = unitPortraitRawImage.rectTransform;
            if (unitIcon != null)
            {
                var iconRect = unitIcon.rectTransform;
                portraitRect.anchorMin = iconRect.anchorMin;
                portraitRect.anchorMax = iconRect.anchorMax;
                portraitRect.pivot = iconRect.pivot;
                portraitRect.anchoredPosition = iconRect.anchoredPosition;
                portraitRect.sizeDelta = iconRect.sizeDelta;

                if (panelRoot != null)
                {
                    var maxSibling = Mathf.Max(0, panelRoot.childCount - 1);
                    var targetSibling = Mathf.Clamp(iconRect.GetSiblingIndex() + 1, 0, maxSibling);
                    portraitRect.SetSiblingIndex(targetSibling);
                }
            }
            else
            {
                portraitRect.anchorMin = new Vector2(0f, 0f);
                portraitRect.anchorMax = new Vector2(0f, 0f);
                portraitRect.pivot = new Vector2(0f, 0f);
                portraitRect.anchoredPosition = new Vector2(14f, 14f);
                portraitRect.sizeDelta = new Vector2(78f, 78f);
            }

            unitPortraitRawImage.raycastTarget = false;
            unitPortraitRawImage.color = Color.white;
            EnsureRuntimeHelpers();
            _portraitCameraLifecycle.BindRawImageTexture(unitPortraitRawImage);
        }

        private void EnsureUnitDescriptionUi()
        {
            if (panelRoot == null)
            {
                return;
            }

            RectTransform descriptionRect = null;
            var createdNow = false;
            if (unitDescriptionText == null)
            {
                descriptionRect = panelRoot.Find("UnitDescription") as RectTransform;
                if (descriptionRect == null)
                {
                    descriptionRect = EnsureChild("UnitDescription");
                    createdNow = true;
                }

                unitDescriptionText = descriptionRect != null
                    ? descriptionRect.GetComponent<TextMeshProUGUI>()
                    : null;
                if (unitDescriptionText == null && descriptionRect != null)
                {
                    unitDescriptionText = descriptionRect.gameObject.AddComponent<TextMeshProUGUI>();
                    createdNow = true;
                }
            }

            if (unitDescriptionText == null)
            {
                return;
            }

            if (unitDescriptionText.font == null && TMP_Settings.defaultFontAsset != null)
            {
                unitDescriptionText.font = TMP_Settings.defaultFontAsset;
            }

            descriptionRect = unitDescriptionText.rectTransform;
            if (createdNow && descriptionRect != null)
            {
                descriptionRect.anchorMin = new Vector2(0f, 1f);
                descriptionRect.anchorMax = new Vector2(0f, 1f);
                descriptionRect.pivot = new Vector2(0f, 1f);
                descriptionRect.anchoredPosition = new Vector2(102f, -78f);
                descriptionRect.sizeDelta = new Vector2(300f, 30f);
                unitDescriptionText.fontSize = 14f;
                unitDescriptionText.color = new Color(0.86f, 0.9f, 0.95f, 0.95f);
            }

            unitDescriptionText.alignment = TextAlignmentOptions.TopLeft;
            unitDescriptionText.textWrappingMode = TextWrappingModes.Normal;
            unitDescriptionText.overflowMode = TextOverflowModes.Ellipsis;
            if (unitDescriptionText.text == null)
            {
                unitDescriptionText.text = string.Empty;
            }
        }

        private void EnsurePlanningSummaryUi()
        {
            if (panelRoot == null)
            {
                return;
            }

            if (planningSummaryText == null)
            {
                var summaryRect = panelRoot.Find("PlanningSummaryText") as RectTransform;
                if (summaryRect == null)
                {
                    summaryRect = EnsureChild("PlanningSummaryText");
                    summaryRect.anchorMin = new Vector2(0f, 1f);
                    summaryRect.anchorMax = new Vector2(1f, 1f);
                    summaryRect.pivot = new Vector2(0f, 1f);
                    summaryRect.anchoredPosition = new Vector2(102f, -112f);
                    summaryRect.sizeDelta = new Vector2(-118f, 24f);
                }

                planningSummaryText = summaryRect.GetComponent<TextMeshProUGUI>();
                if (planningSummaryText == null)
                {
                    planningSummaryText = summaryRect.gameObject.AddComponent<TextMeshProUGUI>();
                }
            }

            if (planningSummaryText == null)
            {
                return;
            }

            if (planningSummaryText.font == null && TMP_Settings.defaultFontAsset != null)
            {
                planningSummaryText.font = TMP_Settings.defaultFontAsset;
            }

            planningSummaryText.fontSize = 13f;
            planningSummaryText.color = new Color(0.78f, 0.9f, 1f, 0.95f);
            planningSummaryText.alignment = TextAlignmentOptions.TopLeft;
            planningSummaryText.textWrappingMode = TextWrappingModes.NoWrap;
            planningSummaryText.overflowMode = TextOverflowModes.Ellipsis;
            planningSummaryText.gameObject.SetActive(false);
            _planningSummaryPresenter = new UnitInfoPlanningSummaryPresenter(planningSummaryText);
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
            _actionListBinder.Refresh(actionButtons, actionRegistry, _currentUnit);
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
            var interactive = IsInteractivePlanning();
            var controllable = IsCurrentUnitControllable();
            var unitType = _currentUnit != null ? _currentUnit.UnitType : string.Empty;
            var directOrderState = UnitInfoDirectOrderStateResolver.Resolve(unitType);
            var showDirectOrderButtons = interactive &&
                                         controllable &&
                                         (directOrderState.CanMove || directOrderState.IsMilitaryUnit);

            RefreshPlanningSummaryText();
            _directOrderPanelBinder.ApplyState(
                directOrderButtonsRoot,
                GetDirectOrderButtons(),
                showDirectOrderButtons,
                directOrderState,
                ActionLock.IsLocked);
        }

        private void RefreshPlanningSummaryText()
        {
            if (planningSummaryText == null)
            {
                return;
            }

            _planningSummaryPresenter ??= new UnitInfoPlanningSummaryPresenter(planningSummaryText);
            _planningSummaryPresenter.Refresh(_currentUnit, _planningDraftCache ?? PlanningDraftCache.Instance);
        }

        private bool IsInteractivePlanning()
        {
            var cache = GameStateCache.Instance;
            return cache != null && GamePhases.IsPlanning(cache.Phase) && !cache.IsGameOver;
        }

        private bool IsCurrentUnitControllable()
        {
            var cache = GameStateCache.Instance;
            return _currentUnit != null &&
                   cache != null &&
                   !string.IsNullOrWhiteSpace(cache.MyPlayerID) &&
                   string.Equals(cache.MyPlayerID, _currentUnit.Faction, StringComparison.Ordinal);
        }

        private void EnsureDefaultActionProviders()
        {
            // Some prefab variants only contain Settler registrar.
            // Ensure city-core actions (Build/Production/Tech) can still be registered at runtime.
            if (GetComponent<CityCoreBuildingActionRegistrar>() == null &&
                UnityEngine.Object.FindAnyObjectByType<CityCoreBuildingActionRegistrar>() == null)
            {
                gameObject.AddComponent<CityCoreBuildingActionRegistrar>();
            }

            if (GetComponent<SettlerUnitActionRegistrar>() == null &&
                UnityEngine.Object.FindAnyObjectByType<SettlerUnitActionRegistrar>() == null)
            {
                gameObject.AddComponent<SettlerUnitActionRegistrar>();
            }
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

            if (autoFindActionRegistry && actionRegistry == null)
            {
                actionRegistry = GetComponent<UnitInfoActionRegistry>();
                if (actionRegistry == null)
                {
                    actionRegistry = UnityEngine.Object.FindAnyObjectByType<UnitInfoActionRegistry>();
                }
                if (actionRegistry == null)
                {
                    actionRegistry = gameObject.AddComponent<UnitInfoActionRegistry>();
                }
            }

            if (autoFindMapPlanningInputController && mapPlanningInputController == null)
            {
                mapPlanningInputController = MapPlanningInputController.Instance;
                if (mapPlanningInputController == null)
                {
                    mapPlanningInputController = SceneObjectFinder.FindFirstSceneObject<MapPlanningInputController>();
                }
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
            if (_unitSelectionSubscribed && mapPlanningInputController != null)
            {
                return;
            }

            if (mapPlanningInputController == null)
            {
                mapPlanningInputController = MapPlanningInputController.Instance;
                if (mapPlanningInputController == null)
                {
                    mapPlanningInputController = SceneObjectFinder.FindFirstSceneObject<MapPlanningInputController>();
                }
            }

            if (mapPlanningInputController == null)
            {
                return;
            }

            mapPlanningInputController.UnitSelectionChanged -= OnUnitSelectionChanged;
            mapPlanningInputController.UnitSelectionChanged += OnUnitSelectionChanged;
            mapPlanningInputController.CombatSelectionChanged -= RefreshPlanningUi;
            mapPlanningInputController.CombatSelectionChanged += RefreshPlanningUi;
            _unitSelectionSubscribed = true;
        }

        private void UnsubscribeUnitSelection()
        {
            if (mapPlanningInputController != null)
            {
                mapPlanningInputController.UnitSelectionChanged -= OnUnitSelectionChanged;
                mapPlanningInputController.CombatSelectionChanged -= RefreshPlanningUi;
            }

            _unitSelectionSubscribed = false;
        }

        private void EnsureDefaultLayout()
        {
            if (panelBackground != null &&
                unitIcon != null &&
                unitPortraitRawImage != null &&
                unitNameText != null &&
                hpSlider != null &&
                hpValueText != null &&
                actionButtonsRoot != null &&
                directOrderButtonsRoot != null &&
                moveButton != null &&
                attackButton != null &&
                holdButton != null &&
                chargeButton != null &&
                actionButtons != null &&
                actionButtons.Length > 0)
            {
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                panelRoot.SetParent(canvas.transform, false);
            }

            panelRoot.anchorMin = new Vector2(1f, 0f);
            panelRoot.anchorMax = new Vector2(1f, 0f);
            panelRoot.pivot = new Vector2(1f, 0f);
            panelRoot.sizeDelta = new Vector2(420f, 280f);

            if (panelBackground == null)
            {
                var bg = EnsureChild("Background");
                panelBackground = bg.GetComponent<Image>();
                if (panelBackground == null)
                {
                    panelBackground = bg.gameObject.AddComponent<Image>();
                }
                panelBackground.color = new Color(0.06f, 0.09f, 0.16f, 0.9f);
                var bgRt = bg as RectTransform;
                UnitInfoPanelLayoutBuilder.StretchToParent(bgRt, Vector2.zero, Vector2.zero);
            }

            if (actionButtonsRoot == null)
            {
                var actionRoot = EnsureChild("ActionButtons");
                actionButtonsRoot = actionRoot;
                actionButtonsRoot.anchorMin = new Vector2(0f, 1f);
                actionButtonsRoot.anchorMax = new Vector2(0f, 1f);
                actionButtonsRoot.pivot = new Vector2(0f, 1f);
                actionButtonsRoot.anchoredPosition = new Vector2(14f, -10f);
                actionButtonsRoot.sizeDelta = new Vector2(190f, 30f);
                var layout = actionButtonsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 6f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                var fitter = actionButtonsRoot.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            if (directOrderButtonsRoot == null)
            {
                var directRoot = EnsureChild("DirectOrderButtons");
                directOrderButtonsRoot = directRoot;
                directOrderButtonsRoot.anchorMin = new Vector2(0f, 1f);
                directOrderButtonsRoot.anchorMax = new Vector2(0f, 1f);
                directOrderButtonsRoot.pivot = new Vector2(0f, 1f);
                directOrderButtonsRoot.anchoredPosition = new Vector2(14f, -52f);
                directOrderButtonsRoot.sizeDelta = new Vector2(190f, 72f);
            }

            if (unitIcon == null)
            {
                var iconRT = EnsureChild("UnitIcon");
                unitIcon = iconRT.gameObject.GetComponent<Image>();
                if (unitIcon == null)
                {
                    unitIcon = iconRT.gameObject.AddComponent<Image>();
                }
                iconRT.anchorMin = new Vector2(0f, 0f);
                iconRT.anchorMax = new Vector2(0f, 0f);
                iconRT.pivot = new Vector2(0f, 0f);
                iconRT.anchoredPosition = new Vector2(14f, 14f);
                iconRT.sizeDelta = new Vector2(78f, 78f);
                unitIcon.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            }

            EnsurePortraitUi();
            if (unitPortraitRawImage != null)
            {
                unitPortraitRawImage.enabled = false;
            }

            if (unitNameText == null)
            {
                var nameRT = EnsureChild("UnitName");
                unitNameText = UnitInfoPanelLayoutBuilder.CreateTmpText(nameRT, "Unit");
                nameRT.anchorMin = new Vector2(0f, 1f);
                nameRT.anchorMax = new Vector2(0f, 1f);
                nameRT.pivot = new Vector2(0f, 1f);
                nameRT.anchoredPosition = new Vector2(102f, -46f);
                nameRT.sizeDelta = new Vector2(280f, 32f);
                unitNameText.fontSize = 24f;
                unitNameText.alignment = TextAlignmentOptions.Left;
            }

            EnsureUnitDescriptionUi();

            if (hpSlider == null)
            {
                var sliderRT = EnsureChild("HpBar");
                hpSlider = sliderRT.gameObject.GetComponent<Slider>();
                if (hpSlider == null)
                {
                    hpSlider = sliderRT.gameObject.AddComponent<Slider>();
                }
                sliderRT.anchorMin = new Vector2(0f, 0f);
                sliderRT.anchorMax = new Vector2(0f, 0f);
                sliderRT.pivot = new Vector2(0f, 0f);
                sliderRT.anchoredPosition = new Vector2(102f, 40f);
                sliderRT.sizeDelta = new Vector2(240f, 24f);
                UnitInfoPanelLayoutBuilder.BuildDefaultSliderVisual(hpSlider, sliderRT);
            }

            if (hpValueText == null)
            {
                var hpTextRT = EnsureChild("HpText");
                hpValueText = UnitInfoPanelLayoutBuilder.CreateTmpText(hpTextRT, "0/0");
                hpTextRT.anchorMin = new Vector2(0f, 0f);
                hpTextRT.anchorMax = new Vector2(0f, 0f);
                hpTextRT.pivot = new Vector2(0f, 0f);
                hpTextRT.anchoredPosition = new Vector2(102f, 14f);
                hpTextRT.sizeDelta = new Vector2(120f, 20f);
                hpValueText.fontSize = 16f;
                hpValueText.alignment = TextAlignmentOptions.Left;
            }

            if (actionButtons == null || actionButtons.Length == 0)
            {
                actionButtons = _actionListBinder.EnsureDefaultSlots(
                    actionButtons,
                    actionButtonsRoot,
                    defaultActionButtonSize,
                    defaultActionButtonColor,
                    CreateSerializedActionButtonSlot);
            }

            var directOrderButtons = _directOrderPanelBinder.EnsureButtons(
                directOrderButtonsRoot,
                moveButton,
                attackButton,
                holdButton,
                chargeButton,
                defaultActionButtonColor);
            moveButton = directOrderButtons.Move;
            attackButton = directOrderButtons.Attack;
            holdButton = directOrderButtons.Hold;
            chargeButton = directOrderButtons.Charge;
            _directOrderPanelBinder.BindListeners(
                directOrderButtons,
                UnitInfoDirectOrderButtonActions.ForController(() => mapPlanningInputController));
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
            _actionListBinder.RepairLayoutAndVisuals(
                actionButtons,
                actionButtonsRoot,
                defaultActionButtonSize,
                defaultActionButtonColor);
        }

        private RectTransform EnsureChild(string childName)
        {
            var child = panelRoot.Find(childName) as RectTransform;
            if (child != null)
            {
                return child;
            }

            var go = new GameObject(childName, typeof(RectTransform));
            child = go.GetComponent<RectTransform>();
            child.SetParent(panelRoot, false);
            return child;
        }

        private void ResolveUnitDisplayTexts(UnitView unit, out string displayName, out string description)
        {
            displayName = BuildUnitDisplayName(unit);
            description = string.Empty;
            if (unit == null)
            {
                return;
            }

            var unitType = NormalizeToken(unit.UnitType);
            if (string.IsNullOrWhiteSpace(unitType))
            {
                return;
            }

            var catalog = StaticCatalogCache.EnsureInstance();
            if (catalog == null)
            {
                return;
            }

            if (catalog.TryGetUnit(unitType, out var unitEntry) && unitEntry != null)
            {
                if (!string.IsNullOrWhiteSpace(unitEntry.name))
                {
                    displayName = unitEntry.name.Trim();
                }

                if (!string.IsNullOrWhiteSpace(unitEntry.description))
                {
                    description = unitEntry.description.Trim();
                }

                return;
            }

            if (catalog.TryGetBuilding(unitType, out var buildingEntry) && buildingEntry != null)
            {
                if (!string.IsNullOrWhiteSpace(buildingEntry.name))
                {
                    displayName = buildingEntry.name.Trim();
                }

                if (!string.IsNullOrWhiteSpace(buildingEntry.description))
                {
                    description = buildingEntry.description.Trim();
                }
            }
        }

        private static string BuildUnitDisplayName(UnitView unit)
        {
            if (unit == null)
            {
                return "Unit";
            }

            var type = NormalizeToken(unit.UnitType);
            if (string.IsNullOrEmpty(type))
            {
                return $"Unit {unit.UnitId}";
            }

            return $"{type} [{unit.UnitId}]";
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
