using System;
using System.Collections;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
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
        private sealed class ActionButtonSlot
        {
            public string actionId;
            public Button button;
            public TMP_Text label;
        }

        [Header("References")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image panelBackground;
        [SerializeField] private Image unitIcon;
        [SerializeField] private TMP_Text unitNameText;
        [SerializeField] private TMP_Text unitDescriptionText;
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
        [SerializeField] private MapInputHandler mapInputHandler;

        [Header("Auto Find")]
        [SerializeField] private bool autoFindActionRegistry = true;
        [SerializeField] private bool autoFindMapInputHandler = true;
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
        private Coroutine _slideRoutine;
        private Coroutine _externalOffsetRoutine;
        private Vector2 _shownAnchoredPos;
        private Vector2 _hiddenAnchoredPos;
        private Vector2 _externalOffset;
        private bool _isOpen;
        private bool _unitSelectionSubscribed;
        private static Sprite _fallbackButtonSprite;
        private static Texture2D _fallbackButtonTexture;
        private Camera _portraitCamera;
        private RenderTexture _portraitRenderTexture;
        private Light _portraitFillLight;
        public UnitView CurrentUnit => _currentUnit;
        public bool IsOpen => _isOpen;

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
            EnsurePortraitUi();
            if (autoBuildDefaultLayout)
            {
                EnsureDefaultLayout();
            }
            EnsureUnitDescriptionUi();
            HideLegacyPlanningTexts();
            EnsureRequiredActionButtonSlots();
            if (autoRepairActionButtons)
            {
                RepairActionButtonLayoutAndVisuals();
            }
            BindDirectOrderButtons();
            ResolveAnchoredPositions();
            SetPanelVisibleImmediate(false);
            SetPortraitVisible(false);
        }

        private void OnEnable()
        {
            ResolveReferences();
            TrySubscribeUnitSelection();
            TrySubscribeActionRegistry();

            var cache = GameStateCache.Instance;
            if (cache != null)
            {
                cache.OnUnitsChanged += OnUnitsChanged;
                cache.OnNodeChanged += OnNodeChanged;
                cache.OnPhaseChanged += OnPhaseChanged;
                cache.OnGameOver += OnGameOver;
            }

            ActionLock.OnChanged += OnActionLockChanged;
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

            var cache = GameStateCache.Instance;
            if (cache != null)
            {
                cache.OnUnitsChanged -= OnUnitsChanged;
                cache.OnNodeChanged -= OnNodeChanged;
                cache.OnPhaseChanged -= OnPhaseChanged;
                cache.OnGameOver -= OnGameOver;
            }

            ActionLock.OnChanged -= OnActionLockChanged;
            DisablePortraitCamera();
        }

        private void OnDestroy()
        {
            ReleasePortraitResources();
        }

        private void LateUpdate()
        {
            if (!_unitSelectionSubscribed || mapInputHandler == null)
            {
                TrySubscribeUnitSelection();
            }

            if (actionRegistry == null)
            {
                ResolveReferences();
                TrySubscribeActionRegistry();
            }

            if (_isOpen && _currentUnit != null && enablePortraitCamera && portraitRealtime)
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

            if (_slideRoutine != null)
            {
                StopCoroutine(_slideRoutine);
                _slideRoutine = null;
            }

            if (_externalOffsetRoutine != null)
            {
                StopCoroutine(_externalOffsetRoutine);
                _externalOffsetRoutine = null;
            }

            ResolveReferences();
            ResolveAnchoredPositions();
            SetPanelVisibleImmediate(false);
            DisablePortraitCamera();
            SetPortraitVisible(false);
            RefreshActionButtons();
            RefreshPlanningUi();
        }

        public void SetExternalOffset(Vector2 offset, bool immediate = false)
        {
            _externalOffset = offset;
            if (panelRoot == null)
            {
                return;
            }

            if (_externalOffsetRoutine != null)
            {
                StopCoroutine(_externalOffsetRoutine);
                _externalOffsetRoutine = null;
            }

            if (immediate)
            {
                panelRoot.anchoredPosition = GetTargetAnchoredPosition(_isOpen);
                return;
            }

            _externalOffsetRoutine = StartCoroutine(AnimateExternalOffset());
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
            if (_currentUnit == null || !_isOpen)
            {
                return;
            }

            RefreshSelectionUi();
        }

        private void OnGameOver(GameOverEvent _)
        {
            if (_currentUnit == null || !_isOpen)
            {
                return;
            }

            RefreshSelectionUi();
        }
        private void OnActionLockChanged(bool _)
        {
            if (_currentUnit == null || !_isOpen)
            {
                return;
            }

            RefreshPlanningUi();
        }

        private void OnActionRegistryChanged()
        {
            if (_currentUnit == null || !_isOpen)
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
            if (_currentUnit == null)
            {
                return;
            }

            var hp = _currentUnit.HitPoints;
            var maxHp = Mathf.Max(1, _currentUnit.MaxHitPoints);
            var cache = GameStateCache.Instance;
            if (cache != null)
            {
                var cachedUnit = cache.GetUnit(_currentUnit.UnitId);
                if (cachedUnit != null)
                {
                    hp = cachedUnit.Hp;
                    maxHp = Mathf.Max(1, cachedUnit.MaxHp);
                }
                else
                {
                    var cachedNode = cache.GetNode(_currentUnit.UnitId);
                    if (cachedNode != null && (!string.IsNullOrWhiteSpace(cachedNode.BuildingType) || cachedNode.IsResourcePoint))
                    {
                        hp = cachedNode.BuildingHp > 0 ? cachedNode.BuildingHp : Mathf.Max(1, hp);
                        maxHp = ResolveInfoPanelBuildingMaxHp(cachedNode, _currentUnit.UnitType, hp);
                    }
                }
            }

            if (hpSlider != null)
            {
                hpSlider.minValue = 0f;
                hpSlider.maxValue = maxHp;
                hpSlider.value = Mathf.Clamp(hp, 0, maxHp);
            }

            if (hpValueText != null)
            {
                hpValueText.text = $"{Mathf.Clamp(hp, 0, maxHp)}/{maxHp}";
            }
        }

        private static int ResolveInfoPanelBuildingMaxHp(NodeDto node, string fallbackType, int hp)
        {
            if (node == null || node.IsResourcePoint)
            {
                return Mathf.Max(1, hp);
            }

            var maxHp = node.BuildingMaxHp;
            if (maxHp <= 0)
            {
                var catalog = StaticCatalogCache.EnsureInstance();
                var buildingType = NormalizeToken(!string.IsNullOrWhiteSpace(node.BuildingType) ? node.BuildingType : fallbackType);
                if (catalog != null)
                {
                    if (string.Equals(buildingType, "city_core", StringComparison.OrdinalIgnoreCase) &&
                        catalog.Rules != null &&
                        catalog.Rules.city_core_max_hp > 0)
                    {
                        maxHp = catalog.Rules.city_core_max_hp;
                    }
                    else if (catalog.TryGetBuilding(buildingType, out var buildingEntry) && buildingEntry != null)
                    {
                        maxHp = buildingEntry.max_hp;
                    }
                }
            }

            return Mathf.Max(1, Mathf.Max(maxHp, hp));
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
                panelRoot.anchoredPosition = GetTargetAnchoredPosition(_isOpen);
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

            if (!EnsurePortraitCameraAndTexture())
            {
                DisablePortraitCamera();
                return false;
            }

            if (!UpdatePortraitCameraPose(_currentUnit))
            {
                DisablePortraitCamera();
                return false;
            }

            UpdatePortraitCameraEnabledState();

            if (_portraitCamera != null &&
                _portraitCamera.targetTexture != null &&
                (!portraitRealtime || forceRender))
            {
                var usePortraitFillLight = _portraitFillLight != null && enablePortraitFillLight;
                if (usePortraitFillLight)
                {
                    _portraitFillLight.enabled = true;
                }

                try
                {
                    _portraitCamera.Render();
                }
                finally
                {
                    if (usePortraitFillLight)
                    {
                        _portraitFillLight.enabled = false;
                    }
                }
            }

            return true;
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
            unitPortraitRawImage.texture = _portraitRenderTexture;
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

        private bool EnsurePortraitCameraAndTexture()
        {
            if (!enablePortraitCamera)
            {
                return false;
            }

            var textureSize = Mathf.Clamp(portraitTextureSize, 64, 1024);
            if (_portraitRenderTexture == null ||
                _portraitRenderTexture.width != textureSize ||
                _portraitRenderTexture.height != textureSize)
            {
                if (_portraitCamera != null && _portraitCamera.targetTexture == _portraitRenderTexture)
                {
                    _portraitCamera.targetTexture = null;
                }

                if (_portraitRenderTexture != null)
                {
                    _portraitRenderTexture.Release();
                    Destroy(_portraitRenderTexture);
                }

                _portraitRenderTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
                {
                    name = "UnitPortraitRT_Runtime",
                    hideFlags = HideFlags.DontSave,
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _portraitRenderTexture.Create();
            }

            if (_portraitCamera == null)
            {
                var cameraGo = new GameObject("UnitPortraitCamera_Runtime", typeof(Camera));
                cameraGo.hideFlags = HideFlags.DontSave;
                _portraitCamera = cameraGo.GetComponent<Camera>();
            }

            if (_portraitCamera == null || _portraitRenderTexture == null)
            {
                return false;
            }

            _portraitCamera.enabled = false;
            _portraitCamera.orthographic = false;
            _portraitCamera.fieldOfView = Mathf.Clamp(portraitFov, 10f, 80f);
            _portraitCamera.nearClipPlane = 0.03f;
            _portraitCamera.farClipPlane = 500f;
            _portraitCamera.cullingMask = ~0;
            _portraitCamera.targetTexture = _portraitRenderTexture;
            ConfigurePortraitCameraClearFlags();
            EnsurePortraitFillLight();
            ConfigurePortraitFillLight();

            if (unitPortraitRawImage != null)
            {
                unitPortraitRawImage.texture = _portraitRenderTexture;
            }

            return true;
        }

        private void ConfigurePortraitCameraClearFlags()
        {
            if (_portraitCamera == null)
            {
                return;
            }

            if (!portraitKeepSceneBackground)
            {
                _portraitCamera.clearFlags = CameraClearFlags.SolidColor;
                _portraitCamera.backgroundColor = Color.clear;
                return;
            }

            if (RenderSettings.skybox != null)
            {
                _portraitCamera.clearFlags = CameraClearFlags.Skybox;
                return;
            }

            _portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            _portraitCamera.backgroundColor = Color.black;
        }

        private void EnsurePortraitFillLight()
        {
            if (_portraitCamera == null)
            {
                _portraitFillLight = null;
                return;
            }

            if (_portraitFillLight != null)
            {
                return;
            }

            var fillLightGo = new GameObject("UnitPortraitFillLight_Runtime", typeof(Light));
            fillLightGo.hideFlags = HideFlags.DontSave;
            fillLightGo.transform.SetParent(_portraitCamera.transform, false);
            _portraitFillLight = fillLightGo.GetComponent<Light>();
        }

        private void ConfigurePortraitFillLight()
        {
            if (_portraitFillLight == null)
            {
                return;
            }

            _portraitFillLight.enabled = false;
            _portraitFillLight.type = LightType.Spot;
            _portraitFillLight.shadows = LightShadows.None;
            _portraitFillLight.renderMode = LightRenderMode.ForcePixel;
            _portraitFillLight.cullingMask = _portraitCamera != null ? _portraitCamera.cullingMask : ~0;
            _portraitFillLight.color = portraitFillLightColor;
            _portraitFillLight.intensity = Mathf.Max(0f, portraitFillLightIntensity);
            _portraitFillLight.range = Mathf.Max(1f, portraitFillLightRange);
            _portraitFillLight.spotAngle = Mathf.Clamp(portraitFillLightSpotAngle, 15f, 150f);
            _portraitFillLight.innerSpotAngle = Mathf.Clamp(
                _portraitFillLight.spotAngle * 0.65f,
                1f,
                _portraitFillLight.spotAngle - 0.1f);
        }

        private bool UpdatePortraitCameraPose(UnitView unit)
        {
            if (_portraitCamera == null || unit == null)
            {
                return false;
            }

            if (!TryComputeUnitBounds(unit, out var bounds))
            {
                return false;
            }

            var visualRoot = unit.VisualRoot != null ? unit.VisualRoot : unit.transform;
            var forward = visualRoot != null ? visualRoot.forward : unit.transform.forward;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = unit.transform.forward;
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            var lookAt = bounds.center + Vector3.up * (bounds.size.y * 0.15f + portraitHeightOffset);
            var distance = Mathf.Max(
                Mathf.Max(0.01f, portraitMinDistance),
                bounds.extents.magnitude * Mathf.Max(0.01f, portraitDistanceScale))
                + Mathf.Max(0f, portraitDistanceOffset);
            var camPos = lookAt - forward * distance + Vector3.up * (bounds.size.y * portraitCameraVerticalOffsetScale);
            var lookDir = lookAt - camPos;
            if (lookDir.sqrMagnitude <= 0.0001f)
            {
                lookDir = forward;
            }

            _portraitCamera.transform.SetPositionAndRotation(
                camPos,
                Quaternion.LookRotation(lookDir.normalized, Vector3.up));
            UpdatePortraitFillLightPose(lookAt);
            return true;
        }

        private void UpdatePortraitFillLightPose(Vector3 lookAt)
        {
            if (_portraitFillLight == null || _portraitCamera == null)
            {
                return;
            }

            var cameraTransform = _portraitCamera.transform;
            var fillPosition = cameraTransform.position +
                               cameraTransform.up * portraitFillLightVerticalOffset +
                               cameraTransform.forward * portraitFillLightForwardOffset;
            var lightDirection = lookAt - fillPosition;
            if (lightDirection.sqrMagnitude <= 0.0001f)
            {
                lightDirection = cameraTransform.forward;
            }

            _portraitFillLight.transform.SetPositionAndRotation(
                fillPosition,
                Quaternion.LookRotation(lightDirection.normalized, Vector3.up));
        }

        private static bool TryComputeUnitBounds(UnitView unit, out Bounds bounds)
        {
            bounds = default;
            if (unit == null)
            {
                return false;
            }

            var renderers = unit.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            var hasBounds = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private void SetPortraitVisible(bool visible)
        {
            if (unitPortraitRawImage != null)
            {
                unitPortraitRawImage.enabled = visible;
            }

            if (unitIcon != null)
            {
                unitIcon.enabled = !visible;
            }

            UpdatePortraitCameraEnabledState();
        }

        private void UpdatePortraitCameraEnabledState()
        {
            if (_portraitCamera == null)
            {
                return;
            }

            var shouldEnable = enablePortraitCamera &&
                               portraitRealtime &&
                               isActiveAndEnabled &&
                               _isOpen &&
                               _currentUnit != null &&
                               unitPortraitRawImage != null &&
                               unitPortraitRawImage.enabled &&
                               _portraitRenderTexture != null;
            _portraitCamera.enabled = shouldEnable;

            if (_portraitFillLight != null)
            {
                _portraitFillLight.enabled = false;
            }
        }

        private void DisablePortraitCamera()
        {
            if (_portraitCamera != null)
            {
                _portraitCamera.enabled = false;
            }

            if (_portraitFillLight != null)
            {
                _portraitFillLight.enabled = false;
            }
        }

        private void ReleasePortraitResources()
        {
            DisablePortraitCamera();

            if (_portraitCamera != null && _portraitCamera.targetTexture == _portraitRenderTexture)
            {
                _portraitCamera.targetTexture = null;
            }

            if (_portraitFillLight != null)
            {
                _portraitFillLight.enabled = false;
                _portraitFillLight = null;
            }

            if (unitPortraitRawImage != null && unitPortraitRawImage.texture == _portraitRenderTexture)
            {
                unitPortraitRawImage.texture = null;
            }

            if (_portraitCamera != null)
            {
                Destroy(_portraitCamera.gameObject);
                _portraitCamera = null;
            }

            if (_portraitRenderTexture != null)
            {
                _portraitRenderTexture.Release();
                Destroy(_portraitRenderTexture);
                _portraitRenderTexture = null;
            }
        }

        private void RefreshActionButtons()
        {
            EnsureActionProvidersRegistered();

            if (actionButtons == null || actionButtons.Length == 0)
            {
                return;
            }

            for (var i = 0; i < actionButtons.Length; i++)
            {
                var slot = actionButtons[i];
                if (slot == null || slot.button == null)
                {
                    continue;
                }

                slot.button.onClick.RemoveAllListeners();

                if (actionRegistry == null || _currentUnit == null || string.IsNullOrWhiteSpace(slot.actionId))
                {
                    slot.button.gameObject.SetActive(false);
                    continue;
                }

                if (!actionRegistry.TryResolve(slot.actionId, _currentUnit, out var handler, out var label, out var visible)
                    || handler == null
                    || !visible)
                {
                    slot.button.gameObject.SetActive(false);
                    continue;
                }

                var buttonHandler = handler;
                var boundUnit = _currentUnit;
                slot.button.onClick.AddListener(() => buttonHandler(boundUnit));

                if (slot.label != null)
                {
                    slot.label.text = string.IsNullOrWhiteSpace(label) ? slot.actionId : label;
                }

                slot.button.gameObject.SetActive(true);
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

        private void BindDirectOrderButtons()
        {
            if (moveButton != null)
            {
                moveButton.onClick.RemoveAllListeners();
                moveButton.onClick.AddListener(() => mapInputHandler?.BeginMoveSelection());
            }

            if (attackButton != null)
            {
                attackButton.onClick.RemoveAllListeners();
                attackButton.onClick.AddListener(() => mapInputHandler?.BeginAttackSelection());
            }

            if (holdButton != null)
            {
                holdButton.onClick.RemoveAllListeners();
                holdButton.onClick.AddListener(() => mapInputHandler?.IssueHoldOrder());
            }

            if (chargeButton != null)
            {
                chargeButton.onClick.RemoveAllListeners();
                chargeButton.onClick.AddListener(() => mapInputHandler?.BeginChargeSelection());
            }
        }
        private void RefreshPlanningUi()
        {
            var interactive = IsInteractivePlanning();
            var controllable = IsCurrentUnitControllable();
            var unitType = _currentUnit != null ? _currentUnit.UnitType : string.Empty;
            var canMove = CanSelectedUnitMove(unitType);
            var militaryUnit = IsCurrentSelectionMilitaryUnit();
            var showDirectOrderButtons = interactive && controllable && (canMove || militaryUnit);

            if (directOrderButtonsRoot != null)
            {
                directOrderButtonsRoot.gameObject.SetActive(showDirectOrderButtons);
            }

            if (!showDirectOrderButtons)
            {
                SetDirectOrderButtonState(moveButton, "Move", false, false);
                SetDirectOrderButtonState(attackButton, "Attack", false, false);
                SetDirectOrderButtonState(holdButton, "Hold", false, false);
                SetDirectOrderButtonState(chargeButton, "Charge", false, false);
                return;
            }

            SetDirectOrderButtonState(moveButton, "Move", true, canMove);
            SetDirectOrderButtonState(attackButton, "Attack", militaryUnit, militaryUnit && CanSelectedUnitAttack(unitType));
            SetDirectOrderButtonState(holdButton, "Hold", militaryUnit, militaryUnit);
            SetDirectOrderButtonState(chargeButton, "Charge", militaryUnit, militaryUnit && CanSelectedUnitCharge(unitType));
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

        private bool CanSelectedUnitAttack(string unitType)
        {
            return TryGetUnitCatalog(unitType, out var entry) && !HasTag(entry, "civilian");
        }

        private bool CanSelectedUnitCharge(string unitType)
        {
            return TryGetUnitCatalog(unitType, out var entry) && HasTag(entry, "charge");
        }

        private bool CanSelectedUnitMove(string unitType)
        {
            var normalizedType = NormalizeToken(unitType);
            if (string.IsNullOrWhiteSpace(normalizedType))
            {
                return false;
            }

            if (string.Equals(normalizedType, "resource_point", StringComparison.Ordinal) ||
                normalizedType.StartsWith("resource_", StringComparison.Ordinal))
            {
                return false;
            }

            var catalog = StaticCatalogCache.EnsureInstance();
            if (catalog != null && catalog.TryGetBuilding(normalizedType, out _))
            {
                return false;
            }

            return TryGetUnitCatalog(normalizedType, out _);
        }

        private bool IsCurrentSelectionMilitaryUnit()
        {
            if (_currentUnit == null)
            {
                return false;
            }

            var normalizedType = NormalizeToken(_currentUnit.UnitType);
            if (string.IsNullOrWhiteSpace(normalizedType))
            {
                return false;
            }

            if (string.Equals(normalizedType, "resource_point", StringComparison.Ordinal) ||
                normalizedType.StartsWith("resource_", StringComparison.Ordinal))
            {
                return false;
            }

            var catalog = StaticCatalogCache.EnsureInstance();
            if (catalog != null && catalog.TryGetBuilding(normalizedType, out _))
            {
                return false;
            }

            return TryGetUnitCatalog(normalizedType, out var entry) && !HasTag(entry, "civilian");
        }

        private bool TryGetUnitCatalog(string unitType, out StaticCatalogCache.UnitEntryJson entry)
        {
            entry = null;
            return !string.IsNullOrWhiteSpace(unitType) &&
                   StaticCatalogCache.EnsureInstance() != null &&
                   StaticCatalogCache.Instance.TryGetUnit(unitType, out entry);
        }

        private static bool HasTag(StaticCatalogCache.UnitEntryJson entry, string tag)
        {
            if (entry?.tags == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            for (var i = 0; i < entry.tags.Length; i++)
            {
                if (string.Equals(entry.tags[i], tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetDirectOrderButtonState(Button button, string label, bool visible, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(visible);
            button.interactable = visible && interactable && !ActionLock.IsLocked;
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = label;
            }
        }

        private static void EnsureActionProvidersRegistered()
        {
            var providers = UnityEngine.Object.FindObjectsByType<UnitInfoActionProviderBase>(FindObjectsInactive.Include);
            if (providers == null || providers.Length == 0)
            {
                return;
            }

            for (var i = 0; i < providers.Length; i++)
            {
                var provider = providers[i];
                if (provider == null)
                {
                    continue;
                }

                provider.EnsureRegistered();
            }
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

            if (autoFindMapInputHandler && mapInputHandler == null)
            {
                mapInputHandler = MapInputHandler.Instance;
                if (mapInputHandler == null)
                {
                    mapInputHandler = UnityEngine.Object.FindAnyObjectByType<MapInputHandler>();
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
            if (_unitSelectionSubscribed && mapInputHandler != null)
            {
                return;
            }

            if (mapInputHandler == null)
            {
                mapInputHandler = MapInputHandler.Instance;
                if (mapInputHandler == null)
                {
                    mapInputHandler = UnityEngine.Object.FindAnyObjectByType<MapInputHandler>();
                }
            }

            if (mapInputHandler == null)
            {
                return;
            }

            mapInputHandler.UnitSelectionChanged -= OnUnitSelectionChanged;
            mapInputHandler.UnitSelectionChanged += OnUnitSelectionChanged;
            mapInputHandler.CombatSelectionChanged -= RefreshPlanningUi;
            mapInputHandler.CombatSelectionChanged += RefreshPlanningUi;
            _unitSelectionSubscribed = true;
        }

        private void UnsubscribeUnitSelection()
        {
            if (mapInputHandler != null)
            {
                mapInputHandler.UnitSelectionChanged -= OnUnitSelectionChanged;
                mapInputHandler.CombatSelectionChanged -= RefreshPlanningUi;
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
                StretchToParent(bgRt, Vector2.zero, Vector2.zero);
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
                unitNameText = CreateTmpText(nameRT, "Unit");
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
                BuildDefaultSliderVisual(hpSlider, sliderRT);
            }

            if (hpValueText == null)
            {
                var hpTextRT = EnsureChild("HpText");
                hpValueText = CreateTmpText(hpTextRT, "0/0");
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
                actionButtons = new[]
                {
                    BuildDefaultButtonSlot("settle_city", "坐城"),
                    BuildDefaultButtonSlot("action_2", "Action2"),
                    BuildDefaultButtonSlot("action_3", "Action3"),
                    BuildDefaultButtonSlot("action_4", "Action4")
                };
            }

            moveButton ??= CreateDirectOrderButton("MoveButton", "移动", new Vector2(0f, 0f), new Vector2(88f, 30f));
            attackButton ??= CreateDirectOrderButton("AttackButton", "攻击", new Vector2(98f, 0f), new Vector2(88f, 30f));
            holdButton ??= CreateDirectOrderButton("HoldButton", "待命", new Vector2(0f, -38f), new Vector2(88f, 30f));
            chargeButton ??= CreateDirectOrderButton("ChargeButton", "冲锋", new Vector2(98f, -38f), new Vector2(88f, 30f));
            BindDirectOrderButtons();
        }

        private void EnsureRequiredActionButtonSlots()
        {
            EnsureActionButtonSlot("expand_territory", "Expand");
            EnsureActionButtonSlot("action_2", "Action2");
            EnsureActionButtonSlot("action_3", "Action3");
            EnsureActionButtonSlot("action_4", "Action4");
            EnsureActionButtonSlot("open_recipe_synthesis", "Synthesis");
        }

        private void EnsureActionButtonSlot(string actionId, string defaultLabel)
        {
            if (actionButtonsRoot == null || string.IsNullOrWhiteSpace(actionId))
            {
                return;
            }

            if (actionButtons != null)
            {
                for (var i = 0; i < actionButtons.Length; i++)
                {
                    var slot = actionButtons[i];
                    if (slot == null)
                    {
                        continue;
                    }

                    if (string.Equals(NormalizeToken(slot.actionId), NormalizeToken(actionId), StringComparison.Ordinal))
                    {
                        return;
                    }
                }
            }

            var newSlot = BuildDefaultButtonSlot(actionId, defaultLabel);
            if (actionButtons == null || actionButtons.Length == 0)
            {
                actionButtons = new[] { newSlot };
                return;
            }

            var expanded = new ActionButtonSlot[actionButtons.Length + 1];
            Array.Copy(actionButtons, expanded, actionButtons.Length);
            expanded[actionButtons.Length] = newSlot;
            actionButtons = expanded;
        }

        private void ResolveAnchoredPositions()
        {
            var y = shownBottomMargin;
            var x = -shownRightMargin;
            if (dockRightOfRect != null)
            {
                x = dockRightOfRect.anchoredPosition.x - Mathf.Abs(dockRightOfRect.rect.width) - Mathf.Max(0f, dockSpacing);
                y = dockRightOfRect.anchoredPosition.y;
            }

            var panelHeight = 0f;
            if (panelRoot != null)
            {
                panelHeight = Mathf.Max(Mathf.Abs(panelRoot.rect.height), Mathf.Abs(panelRoot.sizeDelta.y));
            }
            if (panelHeight <= 0.01f)
            {
                panelHeight = 280f;
            }

            _shownAnchoredPos = new Vector2(x, y);
            _hiddenAnchoredPos = new Vector2(
                x + Mathf.Abs(hiddenOffsetX),
                -panelHeight - Mathf.Max(0f, hiddenBottomMargin));
        }

        private void AnimateVisibility(bool open)
        {
            if (panelRoot == null)
            {
                return;
            }

            if (_slideRoutine != null)
            {
                StopCoroutine(_slideRoutine);
                _slideRoutine = null;
            }

            _slideRoutine = StartCoroutine(SlideRoutine(open));
        }

        private IEnumerator SlideRoutine(bool open)
        {
            _isOpen = open;
            UpdatePortraitCameraEnabledState();
            var duration = Mathf.Max(0.01f, slideDuration);
            var from = panelRoot.anchoredPosition;
            var to = GetTargetAnchoredPosition(open);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var curveT = slideCurve != null && slideCurve.keys != null && slideCurve.length > 0 ? slideCurve.Evaluate(t) : t;
                panelRoot.anchoredPosition = Vector2.LerpUnclamped(from, to, curveT);
                yield return null;
            }

            panelRoot.anchoredPosition = to;
            UpdatePortraitCameraEnabledState();
            _slideRoutine = null;
        }

        private void SetPanelVisibleImmediate(bool open)
        {
            _isOpen = open;
            if (panelRoot != null)
            {
                panelRoot.anchoredPosition = GetTargetAnchoredPosition(open);
            }
            UpdatePortraitCameraEnabledState();
        }

        private IEnumerator AnimateExternalOffset()
        {
            if (panelRoot == null)
            {
                yield break;
            }

            var duration = Mathf.Max(0.01f, externalOffsetSlideDuration);
            var from = panelRoot.anchoredPosition;
            var to = GetTargetAnchoredPosition(_isOpen);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var curveT = externalOffsetCurve != null && externalOffsetCurve.length > 0
                    ? externalOffsetCurve.Evaluate(t)
                    : t;
                panelRoot.anchoredPosition = Vector2.LerpUnclamped(from, to, curveT);
                yield return null;
            }

            panelRoot.anchoredPosition = to;
            _externalOffsetRoutine = null;
        }

        private Vector2 GetTargetAnchoredPosition(bool open)
        {
            var basePos = open ? _shownAnchoredPos : _hiddenAnchoredPos;
            return open ? basePos + _externalOffset : basePos;
        }

        private ActionButtonSlot BuildDefaultButtonSlot(string actionId, string defaultLabel)
        {
            var buttonGO = new GameObject($"Btn_{actionId}", typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRT = buttonGO.GetComponent<RectTransform>();
            buttonRT.SetParent(actionButtonsRoot, false);
            buttonRT.sizeDelta = defaultActionButtonSize;
            var image = buttonGO.GetComponent<Image>();
            image.color = defaultActionButtonColor;
            if (image.sprite == null)
            {
                image.sprite = GetFallbackButtonSprite();
            }
            var button = buttonGO.GetComponent<Button>();

            var labelRT = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
            labelRT.SetParent(buttonRT, false);
            StretchToParent(labelRT, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            var labelText = CreateTmpText(labelRT, defaultLabel);
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 15f;

            return new ActionButtonSlot
            {
                actionId = actionId,
                button = button,
                label = labelText
            };
        }

        private Button CreateDirectOrderButton(string objectName, string label, Vector2 anchoredPosition, Vector2 size)
        {
            var buttonRect = EnsureChild(objectName);
            buttonRect.SetParent(directOrderButtonsRoot, false);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = size;

            var image = buttonRect.GetComponent<Image>();
            if (image == null)
            {
                image = buttonRect.gameObject.AddComponent<Image>();
            }

            image.color = defaultActionButtonColor;
            if (image.sprite == null)
            {
                image.sprite = GetFallbackButtonSprite();
            }

            var button = buttonRect.GetComponent<Button>();
            if (button == null)
            {
                button = buttonRect.gameObject.AddComponent<Button>();
            }

            var labelRect = buttonRect.Find("Label") as RectTransform;
            if (labelRect == null)
            {
                labelRect = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
                labelRect.SetParent(buttonRect, false);
            }

            StretchToParent(labelRect, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            var labelText = CreateTmpText(labelRect, label);
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 15f;
            return button;
        }

        private void RepairActionButtonLayoutAndVisuals()
        {
            if (actionButtons == null || actionButtons.Length == 0)
            {
                return;
            }

            if (actionButtonsRoot != null)
            {
                var rootLayout = actionButtonsRoot.GetComponent<HorizontalLayoutGroup>();
                if (rootLayout == null)
                {
                    rootLayout = actionButtonsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
                }
                rootLayout.spacing = 6f;
                rootLayout.childControlWidth = true;
                rootLayout.childControlHeight = true;
                rootLayout.childForceExpandWidth = false;
                rootLayout.childForceExpandHeight = false;

                var rootFitter = actionButtonsRoot.GetComponent<ContentSizeFitter>();
                if (rootFitter == null)
                {
                    rootFitter = actionButtonsRoot.gameObject.AddComponent<ContentSizeFitter>();
                }
                rootFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                if (actionButtonsRoot.sizeDelta.x < 8f || actionButtonsRoot.sizeDelta.y < 8f)
                {
                    actionButtonsRoot.sizeDelta = new Vector2(190f, 30f);
                }
            }

            var fallbackSprite = GetFallbackButtonSprite();
            for (var i = 0; i < actionButtons.Length; i++)
            {
                var slot = actionButtons[i];
                if (slot == null || slot.button == null)
                {
                    continue;
                }

                var rect = slot.button.transform as RectTransform;
                if (rect != null && (rect.sizeDelta.x < 8f || rect.sizeDelta.y < 8f))
                {
                    rect.sizeDelta = defaultActionButtonSize;
                }

                var layoutElement = slot.button.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = slot.button.gameObject.AddComponent<LayoutElement>();
                }
                layoutElement.preferredWidth = defaultActionButtonSize.x;
                layoutElement.preferredHeight = defaultActionButtonSize.y;
                layoutElement.minWidth = defaultActionButtonSize.x;
                layoutElement.minHeight = defaultActionButtonSize.y;
                layoutElement.flexibleWidth = 0f;

                var image = slot.button.GetComponent<Image>();
                if (image != null)
                {
                    if (image.sprite == null)
                    {
                        image.sprite = fallbackSprite;
                    }
                    image.type = Image.Type.Sliced;
                    if (image.color.a <= 0.01f)
                    {
                        image.color = defaultActionButtonColor;
                    }
                }

                if (slot.label != null && slot.label.font == null && TMP_Settings.defaultFontAsset != null)
                {
                    slot.label.font = TMP_Settings.defaultFontAsset;
                }
            }
        }

        private static Sprite GetFallbackButtonSprite()
        {
            if (_fallbackButtonSprite != null)
            {
                return _fallbackButtonSprite;
            }

            if (_fallbackButtonTexture == null)
            {
                _fallbackButtonTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "UnitInfoButtonFallbackTex",
                    hideFlags = HideFlags.DontSave
                };
                var pixels = new[]
                {
                    Color.white, Color.white,
                    Color.white, Color.white
                };
                _fallbackButtonTexture.SetPixels(pixels);
                _fallbackButtonTexture.Apply(false, true);
            }

            _fallbackButtonSprite = Sprite.Create(
                _fallbackButtonTexture,
                new Rect(0f, 0f, _fallbackButtonTexture.width, _fallbackButtonTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _fallbackButtonSprite.name = "UnitInfoButtonFallbackSprite";
            return _fallbackButtonSprite;
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

        private static void BuildDefaultSliderVisual(Slider slider, RectTransform sliderRoot)
        {
            if (slider == null || sliderRoot == null)
            {
                return;
            }

            var background = EnsureSliderGraphic(sliderRoot, "Background", new Color(0.15f, 0.15f, 0.18f, 0.95f));
            var fillArea = EnsureRect(sliderRoot, "Fill Area");
            StretchToParent(fillArea, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            var fill = EnsureSliderGraphic(fillArea, "Fill", new Color(0.28f, 0.86f, 0.3f, 1f));
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = fill;
            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.ColorTint;
            slider.interactable = false;
            slider.handleRect = null;
            slider.value = 0f;
        }

        private static Image EnsureSliderGraphic(Transform parent, string name, Color color)
        {
            var rect = EnsureRect(parent, name);
            var image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }
            image.color = color;
            StretchToParent(rect, Vector2.zero, Vector2.zero);
            return image;
        }

        private static RectTransform EnsureRect(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static TMP_Text CreateTmpText(RectTransform root, string initialText)
        {
            var text = root.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = root.gameObject.AddComponent<TextMeshProUGUI>();
            }
            text.text = initialText ?? string.Empty;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
            return text;
        }

        private static void StretchToParent(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
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
