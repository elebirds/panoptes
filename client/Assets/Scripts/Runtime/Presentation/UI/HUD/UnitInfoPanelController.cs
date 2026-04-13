using System;
using System.Collections;
using Panoptes.Core.Application.Cache;
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
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TMP_Text hpValueText;
        [SerializeField] private RectTransform actionButtonsRoot;
        [SerializeField] private ActionButtonSlot[] actionButtons;
        [SerializeField] private UnitInfoActionRegistry actionRegistry;
        [SerializeField] private MapInputHandler mapInputHandler;

        [Header("Auto Find")]
        [SerializeField] private bool autoFindActionRegistry = true;
        [SerializeField] private bool autoFindMapInputHandler = true;
        [SerializeField] private bool autoBuildDefaultLayout = true;

        [Header("Icon")]
        [SerializeField] private string unitIconResourcesRoot = "Icons/Units";
        [SerializeField] private string buildingIconResourcesRoot = "Icons/Buildings";

        [Header("Slide")]
        [SerializeField] private float hiddenOffsetX = 420f;
        [SerializeField] private float shownRightMargin = 16f;
        [SerializeField] private float shownBottomMargin = 16f;
        [SerializeField] private float slideDuration = 0.2f;
        [SerializeField] private AnimationCurve slideCurve = null;
        [SerializeField] private float externalOffsetSlideDuration = 0.2f;
        [SerializeField] private AnimationCurve externalOffsetCurve = null;

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
        public UnitView CurrentUnit => _currentUnit;
        public bool IsOpen => _isOpen;

        private void Awake()
        {
            ResolveReferences();
            if (slideCurve == null || slideCurve.length == 0)
            {
                slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
            if (externalOffsetCurve == null || externalOffsetCurve.length == 0)
            {
                externalOffsetCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
            if (autoBuildDefaultLayout)
            {
                EnsureDefaultLayout();
            }
            if (autoRepairActionButtons)
            {
                RepairActionButtonLayoutAndVisuals();
            }
            ResolveAnchoredPositions();
            SetPanelVisibleImmediate(false);
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
                cache.OnPhaseChanged += OnPhaseChanged;
            }
        }

        private void OnDisable()
        {
            UnsubscribeUnitSelection();
            UnsubscribeActionRegistry();

            var cache = GameStateCache.Instance;
            if (cache != null)
            {
                cache.OnUnitsChanged -= OnUnitsChanged;
                cache.OnPhaseChanged -= OnPhaseChanged;
            }
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
        }

        public void OpenForUnit(UnitView unit)
        {
            if (unit == null)
            {
                Close();
                return;
            }

            _currentUnit = unit;
            RefreshUnitView();
            RefreshActionButtons();
            AnimateVisibility(true);
        }

        public void Close()
        {
            _currentUnit = null;
            AnimateVisibility(false);
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
        }

        private void OnPhaseChanged(PhaseChangedEvent _)
        {
            if (_currentUnit == null || !_isOpen)
            {
                return;
            }

            RefreshActionButtons();
        }

        private void OnActionRegistryChanged()
        {
            if (_currentUnit == null || !_isOpen)
            {
                return;
            }

            RefreshActionButtons();
        }

        private void RefreshUnitView()
        {
            if (_currentUnit == null)
            {
                return;
            }

            if (unitNameText != null)
            {
                unitNameText.text = BuildUnitDisplayName(_currentUnit);
            }

            RefreshUnitHpFromCache();
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

        private static void EnsureActionProvidersRegistered()
        {
            var providers = UnityEngine.Object.FindObjectsByType<UnitInfoActionProviderBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
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
            _unitSelectionSubscribed = true;
        }

        private void UnsubscribeUnitSelection()
        {
            if (mapInputHandler != null)
            {
                mapInputHandler.UnitSelectionChanged -= OnUnitSelectionChanged;
            }

            _unitSelectionSubscribed = false;
        }

        private void EnsureDefaultLayout()
        {
            if (panelBackground != null && unitIcon != null && unitNameText != null && hpSlider != null && actionButtonsRoot != null && actionButtons != null && actionButtons.Length > 0)
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
            panelRoot.sizeDelta = new Vector2(420f, 170f);

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
                    BuildDefaultButtonSlot("expand_territory", "Expand"),
                    BuildDefaultButtonSlot("action_2", "Action2"),
                    BuildDefaultButtonSlot("action_3", "Action3")
                };
            }
        }

        private void ResolveAnchoredPositions()
        {
            var y = shownBottomMargin;
            var x = -shownRightMargin;
            _shownAnchoredPos = new Vector2(x, y);
            _hiddenAnchoredPos = new Vector2(x + Mathf.Abs(hiddenOffsetX), y);
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
            _slideRoutine = null;
        }

        private void SetPanelVisibleImmediate(bool open)
        {
            _isOpen = open;
            if (panelRoot != null)
            {
                panelRoot.anchoredPosition = GetTargetAnchoredPosition(open);
            }
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
            return basePos + _externalOffset;
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
            text.enableWordWrapping = false;
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
