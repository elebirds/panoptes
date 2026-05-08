/*************************************************
 * Project: Panoptes
 * File: CityCoreHpBarOverlayController.cs
 * Author: Panoptes Team
 * Date: 2026-04-12
 * Description: Screen-space city core HP bar overlay manager.
 *************************************************/

using System.Collections.Generic;
using Panoptes.Core.Application.Stores;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Panoptes.Presentation.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class CityCoreHpBarOverlayController : MonoBehaviour
    {
        private static int s_uiSuppressionCount;

        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateOverlayCanvas = true;
        [SerializeField] private string canvasName = "CityCoreHpOverlayCanvas";

        [Header("Tracking")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float screenYOffset = 32f;
        [SerializeField] private bool hideWhenBehindCamera = true;
        [SerializeField] private bool hideWhenOffScreen = true;
        [SerializeField] private bool disableWorldSpaceCityCoreHpBar = true;

        [Header("Entry Layout")]
        [SerializeField] private Vector2 entrySize = new Vector2(228f, 34f);
        [SerializeField] private Vector2 plateSize = new Vector2(88f, 24f);
        [SerializeField] private Vector2 barSize = new Vector2(126f, 14f);
        [SerializeField] private float spacing = 8f;
        [SerializeField] private int fontSize = 18;

        [Header("Unit Stack Layout")]
        [SerializeField] private Vector2 unitStackEntrySize = new Vector2(136f, 20f);
        [SerializeField] private Vector2 unitStackIconSize = new Vector2(18f, 18f);
        [SerializeField] private Vector2 unitStackBarSize = new Vector2(78f, 8f);
        [SerializeField] private float unitStackCountWidth = 30f;
        [SerializeField] private float unitStackScreenYOffset = -6f;
        [SerializeField] private int unitStackCountFontSize = 14;
        [SerializeField] private bool showUnitStackNameLabel = false;
        [SerializeField] private float unitStackNameLabelHeight = 14f;
        [SerializeField] private int unitStackNameFontSize = 11;
        [SerializeField] private string unitIconResourcesRoot = "Icons/Units";

        [Header("Colors")]
        [SerializeField] private Color neutralOwnerColor = Color.white;
        [SerializeField] private Color friendlyOwnerColor = new Color(0.26f, 0.78f, 1f, 1f);
        [SerializeField] private Color enemyOwnerColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color hpBarBgColor = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        [SerializeField] private Color hpBarFillColor = new Color(0.2f, 0.95f, 0.35f, 1f);
        [SerializeField] private Color hpHealthyColor = new Color(0.2f, 0.95f, 0.35f, 1f);
        [SerializeField] private Color hpWoundedColor = new Color(1f, 0.82f, 0.18f, 1f);
        [SerializeField] private Color hpCriticalColor = new Color(1f, 0.24f, 0.18f, 1f);
        [SerializeField] private Color unitStackIconPlateColor = new Color(0.05f, 0.05f, 0.05f, 0.82f);
        [SerializeField] private Color unitStackMixedIconPlateColor = new Color(0.32f, 0.24f, 0.48f, 0.92f);

        private sealed class Entry
        {
            public string NodeId;
            public NodeView Node;
            public BuildingView Building;
            public UnitView Unit;
            public RectTransform Root;
            public Image Plate;
            public TextMeshProUGUI Name;
            public Image UnitIconPlate;
            public Image UnitIcon;
            public TextMeshProUGUI UnitIconFallbackText;
            public TextMeshProUGUI UnitNameText;
            public Image Fill;
            public TextMeshProUGUI ValueText;
            public TextMeshProUGUI CountText;
            public float WorldHeightOffset;
            public float ScreenYOffset;
            public int SeenVersion;
            public int LastHp = int.MinValue;
            public int LastMaxHp = int.MinValue;
            public int LastCount = int.MinValue;
            public float LastRatio = -1f;
            public string LastOwnerId = string.Empty;
            public string LastIdentityKey = string.Empty;
        }

        private readonly Dictionary<string, Entry> _cityCoreEntries = new Dictionary<string, Entry>();
        private readonly Dictionary<string, Entry> _unitStackEntries = new Dictionary<string, Entry>();
        private readonly List<UnitView> _unitStackScratch = new List<UnitView>(8);
        private readonly List<UnitView> _combatUnitStackScratch = new List<UnitView>(8);
        private readonly List<string> _staleScratch = new List<string>(16);
        private readonly List<Renderer> _rendererScratch = new List<Renderer>(32);
        private readonly UnitStackOverlayStateBuilder _unitStackStateBuilder = new UnitStackOverlayStateBuilder();

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private GameStateStore _gameStateStore;
        private StaticCatalogStore _staticCatalogStore;
        private MapRenderer _mapRenderer;
        private Sprite _defaultUiSprite;
        private bool _entriesDirty = true;
        private int _syncVersion;

        [Inject]
        private void Construct(GameStateStore gameStateStore, StaticCatalogStore staticCatalogStore, MapRenderer mapRenderer)
        {
            _gameStateStore = gameStateStore;
            _staticCatalogStore = staticCatalogStore;
            _mapRenderer = mapRenderer;
        }

        public static void PushUiSuppression()
        {
            s_uiSuppressionCount = Mathf.Max(0, s_uiSuppressionCount) + 1;
        }

        public static void PopUiSuppression()
        {
            s_uiSuppressionCount = Mathf.Max(0, s_uiSuppressionCount - 1);
        }

        public static bool IsUiSuppressed => s_uiSuppressionCount > 0;

        private void Awake()
        {
            ResolveCamera();
            EnsureCanvas();
        }

        private void OnEnable()
        {
            SubscribeMapRenderer();
            _entriesDirty = true;
        }

        private void OnDisable()
        {
            UnsubscribeMapRenderer();
        }

        private void LateUpdate()
        {
            ResolveCamera();
            if (_canvasRect == null || targetCamera == null)
            {
                return;
            }

            if (IsUiSuppressed)
            {
                if (_canvas != null && _canvas.enabled)
                {
                    _canvas.enabled = false;
                }

                HideAll();
                return;
            }

            if (_canvas != null && !_canvas.enabled)
            {
                _canvas.enabled = true;
            }

            var map = _mapRenderer;
            if (map == null || map.TileViews == null || map.TileViews.Count == 0)
            {
                HideAll();
                return;
            }

            if (_entriesDirty)
            {
                _entriesDirty = false;
                SyncEntriesFromMap(map);
                SyncUnitStackEntriesFromMap(map);
            }

            UpdateEntryTransforms();
        }

        private void SubscribeMapRenderer()
        {
            if (_mapRenderer == null)
            {
                return;
            }

            _mapRenderer.StatePresentationRefreshed -= MarkEntriesDirty;
            _mapRenderer.StatePresentationRefreshed += MarkEntriesDirty;
        }

        private void UnsubscribeMapRenderer()
        {
            if (_mapRenderer == null)
            {
                return;
            }

            _mapRenderer.StatePresentationRefreshed -= MarkEntriesDirty;
        }

        private void MarkEntriesDirty()
        {
            _entriesDirty = true;
        }

        private void ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void EnsureCanvas()
        {
            if (_canvas != null && _canvasRect != null)
            {
                return;
            }

            _canvas = GetComponentInParent<Canvas>();
            _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;
            if (_canvas != null && _canvasRect != null)
            {
                return;
            }

            if (!autoCreateOverlayCanvas)
            {
                return;
            }

            var root = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasRect = root.transform as RectTransform;
            _canvas = root.GetComponent<Canvas>();
            if (_canvas != null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 400;
                _canvas.pixelPerfect = true;
            }

            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        private void SyncEntriesFromMap(MapRenderer map)
        {
            _syncVersion++;
            foreach (var pair in map.TileViews)
            {
                var node = pair.Value;
                if (node == null)
                {
                    continue;
                }

                var building = node.BuildingInstance;
                if (building == null || !building.IsCityCore || building.IsGhost)
                {
                    continue;
                }

                var nodeId = node.NodeId;
                if (string.IsNullOrEmpty(nodeId))
                {
                    continue;
                }

                if (!_cityCoreEntries.TryGetValue(nodeId, out var entry) || entry == null)
                {
                    entry = CreateCityCoreEntry(nodeId, node, building);
                    _cityCoreEntries[nodeId] = entry;
                }
                else
                {
                    entry.Node = node;
                    if (!ReferenceEquals(entry.Building, building))
                    {
                        entry.Building = building;
                        entry.WorldHeightOffset = ComputeHeightOffset(building);
                    }
                }

                entry.SeenVersion = _syncVersion;
                if (disableWorldSpaceCityCoreHpBar && building != null && building.IsCityCoreHpBarEnabled)
                {
                    building.SetCityCoreHpBarEnabled(false);
                }

                RefreshEntryVisual(entry);
            }

            _staleScratch.Clear();
            foreach (var pair in _cityCoreEntries)
            {
                if (pair.Value == null || pair.Value.SeenVersion != _syncVersion)
                {
                    _staleScratch.Add(pair.Key);
                }
            }

            for (var i = 0; i < _staleScratch.Count; i++)
            {
                RemoveEntry(_staleScratch[i]);
            }

            _staleScratch.Clear();
        }

        private void SyncUnitStackEntriesFromMap(MapRenderer map)
        {
            _syncVersion++;
            foreach (var pair in map.TileViews)
            {
                var node = pair.Value;
                if (node == null || string.IsNullOrEmpty(node.NodeId))
                {
                    continue;
                }

                if (!map.TryGetUnitsOnNode(node.NodeId, _unitStackScratch))
                {
                    continue;
                }

                _combatUnitStackScratch.Clear();
                UnitView trackedUnit = null;
                for (var i = 0; i < _unitStackScratch.Count; i++)
                {
                    var unit = _unitStackScratch[i];
                    if (unit == null || !IsCombatUnit(unit))
                    {
                        continue;
                    }

                    if (trackedUnit == null || (!trackedUnit.IsMovingVisual && unit.IsMovingVisual))
                    {
                        trackedUnit = unit;
                    }
                    _combatUnitStackScratch.Add(unit);
                }

                var unitState = _unitStackStateBuilder.Build(_combatUnitStackScratch, _staticCatalogStore?.Snapshot);
                if (!unitState.HasUnits)
                {
                    _combatUnitStackScratch.Clear();
                    continue;
                }

                if (!_unitStackEntries.TryGetValue(node.NodeId, out var entry) || entry == null)
                {
                    entry = CreateUnitStackEntry(node);
                    _unitStackEntries[node.NodeId] = entry;
                }
                else
                {
                    entry.Node = node;
                    if (entry.LastCount != unitState.Count)
                    {
                        entry.WorldHeightOffset = ComputeUnitStackHeightOffset(_combatUnitStackScratch, node);
                    }
                }

                entry.Unit = trackedUnit;
                entry.SeenVersion = _syncVersion;
                RefreshUnitStackVisual(entry, unitState);
                _combatUnitStackScratch.Clear();
            }

            _staleScratch.Clear();
            foreach (var pair in _unitStackEntries)
            {
                if (pair.Value == null || pair.Value.SeenVersion != _syncVersion)
                {
                    _staleScratch.Add(pair.Key);
                }
            }

            for (var i = 0; i < _staleScratch.Count; i++)
            {
                RemoveEntry(_unitStackEntries, _staleScratch[i]);
            }

            _staleScratch.Clear();
        }

        private Entry CreateCityCoreEntry(string nodeId, NodeView node, BuildingView building)
        {
            var go = new GameObject($"CityCoreHp_{nodeId}", typeof(RectTransform));
            var root = go.transform as RectTransform;
            root.SetParent(_canvasRect, false);
            root.sizeDelta = entrySize;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);

            var plateGo = new GameObject("OwnerPlate", typeof(RectTransform), typeof(Image));
            var plateRect = plateGo.transform as RectTransform;
            plateRect.SetParent(root, false);
            plateRect.anchorMin = new Vector2(0f, 0.5f);
            plateRect.anchorMax = new Vector2(0f, 0.5f);
            plateRect.pivot = new Vector2(0f, 0.5f);
            plateRect.sizeDelta = plateSize;
            plateRect.anchoredPosition = new Vector2(0f, 0f);
            var plateImage = plateGo.GetComponent<Image>();
            plateImage.sprite = GetDefaultSprite();

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            var nameRect = nameGo.transform as RectTransform;
            nameRect.SetParent(plateRect, false);
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(4f, 0f);
            nameRect.offsetMax = new Vector2(-4f, 0f);
            var name = nameGo.GetComponent<TextMeshProUGUI>();
            name.fontSize = fontSize;
            name.fontStyle = FontStyles.Bold;
            name.alignment = TextAlignmentOptions.Center;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = false;
            name.extraPadding = true;
            name.raycastTarget = false;

            var barBgGo = new GameObject("HpBarBg", typeof(RectTransform), typeof(Image));
            var barBgRect = barBgGo.transform as RectTransform;
            barBgRect.SetParent(root, false);
            barBgRect.anchorMin = new Vector2(0f, 0.5f);
            barBgRect.anchorMax = new Vector2(0f, 0.5f);
            barBgRect.pivot = new Vector2(0f, 0.5f);
            barBgRect.sizeDelta = barSize;
            barBgRect.anchoredPosition = new Vector2(plateSize.x + spacing, 0f);
            var barBg = barBgGo.GetComponent<Image>();
            barBg.sprite = GetDefaultSprite();
            barBg.color = hpBarBgColor;

            var fillGo = new GameObject("HpBarFill", typeof(RectTransform), typeof(Image));
            var fillRect = fillGo.transform as RectTransform;
            fillRect.SetParent(barBgRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fill = fillGo.GetComponent<Image>();
            fill.sprite = GetDefaultSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.color = hpBarFillColor;

            var valueGo = new GameObject("HpValue", typeof(RectTransform), typeof(TextMeshProUGUI));
            var valueRect = valueGo.transform as RectTransform;
            valueRect.SetParent(barBgRect, false);
            valueRect.anchorMin = Vector2.zero;
            valueRect.anchorMax = Vector2.one;
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;
            var valueText = valueGo.GetComponent<TextMeshProUGUI>();
            valueText.fontSize = Mathf.Max(10, fontSize - 4);
            valueText.fontStyle = FontStyles.Bold;
            valueText.alignment = TextAlignmentOptions.Center;
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            valueText.enableAutoSizing = false;
            valueText.extraPadding = true;
            valueText.color = Color.white;
            valueText.raycastTarget = false;

            root.sizeDelta = new Vector2(plateSize.x + spacing + barSize.x, Mathf.Max(plateSize.y, barSize.y));

            return new Entry
            {
                NodeId = nodeId,
                Node = node,
                Building = building,
                Unit = null,
                Root = root,
                Plate = plateImage,
                Name = name,
                Fill = fill,
                ValueText = valueText,
                WorldHeightOffset = ComputeHeightOffset(building),
                ScreenYOffset = screenYOffset
            };
        }

        private Entry CreateUnitStackEntry(NodeView node)
        {
            var nodeId = node != null ? node.NodeId : string.Empty;
            var go = new GameObject($"UnitStackHp_{nodeId}", typeof(RectTransform));
            var root = go.transform as RectTransform;
            root.SetParent(_canvasRect, false);
            root.sizeDelta = unitStackEntrySize;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);

            var iconPlateGo = new GameObject("UnitIconPlate", typeof(RectTransform), typeof(Image));
            var iconPlateRect = iconPlateGo.transform as RectTransform;
            iconPlateRect.SetParent(root, false);
            iconPlateRect.anchorMin = new Vector2(0f, 0.5f);
            iconPlateRect.anchorMax = new Vector2(0f, 0.5f);
            iconPlateRect.pivot = new Vector2(0f, 0.5f);
            iconPlateRect.sizeDelta = unitStackIconSize;
            iconPlateRect.anchoredPosition = Vector2.zero;
            var iconPlate = iconPlateGo.GetComponent<Image>();
            iconPlate.sprite = GetDefaultSprite();
            iconPlate.color = unitStackIconPlateColor;

            var iconGo = new GameObject("UnitIcon", typeof(RectTransform), typeof(Image));
            var iconRect = iconGo.transform as RectTransform;
            iconRect.SetParent(iconPlateRect, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(2f, 2f);
            iconRect.offsetMax = new Vector2(-2f, -2f);
            var icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            var fallbackGo = new GameObject("UnitIconFallback", typeof(RectTransform), typeof(TextMeshProUGUI));
            var fallbackRect = fallbackGo.transform as RectTransform;
            fallbackRect.SetParent(iconPlateRect, false);
            fallbackRect.anchorMin = Vector2.zero;
            fallbackRect.anchorMax = Vector2.one;
            fallbackRect.offsetMin = Vector2.zero;
            fallbackRect.offsetMax = Vector2.zero;
            var fallbackText = fallbackGo.GetComponent<TextMeshProUGUI>();
            fallbackText.fontSize = Mathf.Max(9, unitStackNameFontSize);
            fallbackText.fontStyle = FontStyles.Bold;
            fallbackText.alignment = TextAlignmentOptions.Center;
            fallbackText.textWrappingMode = TextWrappingModes.NoWrap;
            fallbackText.enableAutoSizing = false;
            fallbackText.extraPadding = true;
            fallbackText.color = Color.white;
            fallbackText.raycastTarget = false;

            var barBgGo = new GameObject("HpBarBg", typeof(RectTransform), typeof(Image));
            var barBgRect = barBgGo.transform as RectTransform;
            barBgRect.SetParent(root, false);
            barBgRect.anchorMin = new Vector2(0f, 0.5f);
            barBgRect.anchorMax = new Vector2(0f, 0.5f);
            barBgRect.pivot = new Vector2(0f, 0.5f);
            barBgRect.sizeDelta = unitStackBarSize;
            barBgRect.anchoredPosition = new Vector2(unitStackIconSize.x + spacing, 0f);
            var barBg = barBgGo.GetComponent<Image>();
            barBg.sprite = GetDefaultSprite();
            barBg.color = hpBarBgColor;

            var fillGo = new GameObject("HpBarFill", typeof(RectTransform), typeof(Image));
            var fillRect = fillGo.transform as RectTransform;
            fillRect.SetParent(barBgRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fill = fillGo.GetComponent<Image>();
            fill.sprite = GetDefaultSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillClockwise = true;
            fill.fillAmount = 1f;
            fill.color = hpHealthyColor;

            var countGo = new GameObject("UnitCount", typeof(RectTransform), typeof(TextMeshProUGUI));
            var countRect = countGo.transform as RectTransform;
            countRect.SetParent(root, false);
            countRect.anchorMin = new Vector2(0f, 0.5f);
            countRect.anchorMax = new Vector2(0f, 0.5f);
            countRect.pivot = new Vector2(0f, 0.5f);
            countRect.sizeDelta = new Vector2(Mathf.Max(18f, unitStackCountWidth), Mathf.Max(12f, unitStackEntrySize.y));
            countRect.anchoredPosition = new Vector2(unitStackIconSize.x + spacing + unitStackBarSize.x + spacing, 0f);
            var countText = countGo.GetComponent<TextMeshProUGUI>();
            countText.fontSize = Mathf.Max(10, unitStackCountFontSize);
            countText.fontStyle = FontStyles.Bold;
            countText.alignment = TextAlignmentOptions.Left;
            countText.textWrappingMode = TextWrappingModes.NoWrap;
            countText.enableAutoSizing = false;
            countText.extraPadding = true;
            countText.color = Color.white;
            countText.raycastTarget = false;

            var nameGo = new GameObject("UnitName", typeof(RectTransform), typeof(TextMeshProUGUI));
            var nameRect = nameGo.transform as RectTransform;
            nameRect.SetParent(root, false);
            nameRect.anchorMin = new Vector2(0f, 0.5f);
            nameRect.anchorMax = new Vector2(0f, 0.5f);
            nameRect.pivot = new Vector2(0f, 0.5f);
            var entryWidth = unitStackIconSize.x + spacing + unitStackBarSize.x + spacing + unitStackCountWidth;
            nameRect.sizeDelta = new Vector2(entryWidth, Mathf.Max(10f, unitStackNameLabelHeight));
            nameRect.anchoredPosition = new Vector2(0f, -(unitStackIconSize.y + unitStackNameLabelHeight) * 0.5f);
            var nameText = nameGo.GetComponent<TextMeshProUGUI>();
            nameText.fontSize = Mathf.Max(9, unitStackNameFontSize);
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.enableAutoSizing = false;
            nameText.extraPadding = true;
            nameText.color = Color.white;
            nameText.raycastTarget = false;
            nameGo.SetActive(showUnitStackNameLabel);

            var entryHeight = Mathf.Max(unitStackEntrySize.y, unitStackIconSize.y, unitStackBarSize.y);
            if (showUnitStackNameLabel)
            {
                entryHeight += Mathf.Max(10f, unitStackNameLabelHeight);
            }
            root.sizeDelta = new Vector2(entryWidth, entryHeight);

            return new Entry
            {
                NodeId = nodeId,
                Node = node,
                Unit = null,
                Root = root,
                UnitIconPlate = iconPlate,
                UnitIcon = icon,
                UnitIconFallbackText = fallbackText,
                UnitNameText = nameText,
                Fill = fill,
                CountText = countText,
                WorldHeightOffset = ComputeUnitStackHeightOffset(_combatUnitStackScratch, node),
                ScreenYOffset = unitStackScreenYOffset
            };
        }

        private void RefreshEntryVisual(Entry entry)
        {
            if (entry == null || entry.Building == null)
            {
                return;
            }

            var ownerColor = ResolveOwnerColor(entry.Building.OwnerId);
            var ownerId = entry.Building.OwnerId ?? string.Empty;
            if (entry.Plate != null && !string.Equals(entry.LastOwnerId, ownerId, System.StringComparison.Ordinal))
            {
                entry.Plate.color = ownerColor;
                entry.LastOwnerId = ownerId;
            }

            if (entry.Name != null && entry.Name.text != "城堡")
            {
                entry.Name.text = "城堡";
                entry.Name.color = Color.white;
            }

            var maxHp = Mathf.Max(1, entry.Building.MaxHitPoints);
            var hp = Mathf.Clamp(entry.Building.HitPoints, 0, maxHp);
            var ratio = Mathf.Clamp01(hp / (float)maxHp);
            if (entry.Fill != null && !Mathf.Approximately(entry.LastRatio, ratio))
            {
                entry.Fill.fillAmount = ratio;
                entry.Fill.color = ResolveHpColor(ratio);
                entry.LastRatio = ratio;
            }

            if (entry.ValueText != null && (entry.LastHp != hp || entry.LastMaxHp != maxHp))
            {
                entry.ValueText.text = $"{hp}/{maxHp}";
            }

            entry.LastHp = hp;
            entry.LastMaxHp = maxHp;
        }
        private void RefreshUnitStackVisual(Entry entry, UnitStackOverlayState state)
        {
            if (entry == null)
            {
                return;
            }

            var maxHp = Mathf.Max(1, state.MaxHp);
            var hp = Mathf.Clamp(state.Hp, 0, maxHp);
            var ratio = Mathf.Clamp01(hp / (float)maxHp);
            if (entry.Fill != null && !Mathf.Approximately(entry.LastRatio, ratio))
            {
                entry.Fill.fillAmount = ratio;
                entry.Fill.color = ResolveHpColor(ratio);
                entry.LastRatio = ratio;
            }

            if (entry.CountText != null && entry.LastCount != state.Count)
            {
                entry.CountText.text = $"x{Mathf.Max(1, state.Count)}";
            }

            RefreshUnitStackIdentityVisual(entry, state);
            entry.LastHp = hp;
            entry.LastMaxHp = maxHp;
            entry.LastCount = state.Count;
        }

        private void RefreshUnitStackIdentityVisual(Entry entry, UnitStackOverlayState state)
        {
            var identityKey = $"{state.UnitType}|{state.IconKey}|{state.DisplayName}|{state.FallbackText}|{state.IsMixed}|{showUnitStackNameLabel}";
            if (string.Equals(entry.LastIdentityKey, identityKey, System.StringComparison.Ordinal))
            {
                return;
            }

            var icon = UiIconLoader.LoadSprite(state.IconKey, state.UnitType, unitIconResourcesRoot);
            if (entry.UnitIcon != null)
            {
                entry.UnitIcon.sprite = icon;
                entry.UnitIcon.enabled = icon != null;
                entry.UnitIcon.color = icon == null ? Color.clear : Color.white;
            }

            if (entry.UnitIconFallbackText != null)
            {
                entry.UnitIconFallbackText.text = icon == null ? state.FallbackText : string.Empty;
                entry.UnitIconFallbackText.gameObject.SetActive(icon == null);
            }

            if (entry.UnitIconPlate != null)
            {
                entry.UnitIconPlate.color = state.IsMixed ? unitStackMixedIconPlateColor : unitStackIconPlateColor;
            }

            if (entry.UnitNameText != null)
            {
                entry.UnitNameText.text = state.DisplayName;
                entry.UnitNameText.gameObject.SetActive(showUnitStackNameLabel && !string.IsNullOrWhiteSpace(state.DisplayName));
            }

            entry.LastIdentityKey = identityKey;
        }
        private void UpdateEntryTransforms()
        {
            UpdateEntryTransforms(_cityCoreEntries);
            UpdateEntryTransforms(_unitStackEntries);
        }

        private void UpdateEntryTransforms(Dictionary<string, Entry> entries)
        {
            foreach (var pair in entries)
            {
                var entry = pair.Value;
                if (entry == null || entry.Root == null)
                {
                    continue;
                }

                if (!TryResolveWorldAnchor(entry, out var world))
                {
                    SetActiveIfChanged(entry.Root.gameObject, false);
                    continue;
                }

                var screen = targetCamera.WorldToScreenPoint(world);

                var visible = true;
                if (hideWhenBehindCamera && screen.z <= 0f)
                {
                    visible = false;
                }

                if (hideWhenOffScreen &&
                    (screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height))
                {
                    visible = false;
                }

                if (!visible)
                {
                    SetActiveIfChanged(entry.Root.gameObject, false);
                    continue;
                }

                SetActiveIfChanged(entry.Root.gameObject, true);
                var sp = new Vector2(screen.x, screen.y + entry.ScreenYOffset);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, sp, null, out var localPos);
                entry.Root.anchoredPosition = localPos;
            }
        }

        private bool TryResolveWorldAnchor(Entry entry, out Vector3 world)
        {
            if (entry.Building != null)
            {
                world = entry.Building.transform.position + Vector3.up * Mathf.Max(0.1f, entry.WorldHeightOffset);
                return true;
            }

            if (entry.Unit != null)
            {
                world = entry.Unit.transform.position + Vector3.up * Mathf.Max(0.1f, entry.WorldHeightOffset);
                return true;
            }

            if (entry.Node != null)
            {
                world = entry.Node.ResolveUnitAnchorWorldPosition() + Vector3.up * Mathf.Max(0.1f, entry.WorldHeightOffset);
                return true;
            }

            world = default;
            return false;
        }

        private float ComputeHeightOffset(BuildingView building)
        {
            if (building == null)
            {
                return 1.2f;
            }

            var hasBounds = false;
            var bounds = default(Bounds);
            EncapsulateRendererBounds(building, ref hasBounds, ref bounds);

            if (!hasBounds)
            {
                return 1.2f;
            }

            return Mathf.Max(0.3f, bounds.max.y - building.transform.position.y + 0.25f);
        }

        private float ComputeUnitStackHeightOffset(List<UnitView> units, NodeView node)
        {
            var hasBounds = false;
            var bounds = default(Bounds);
            if (units != null)
            {
                for (var i = 0; i < units.Count; i++)
                {
                    var unit = units[i];
                    if (unit == null || !IsCombatUnit(unit))
                    {
                        continue;
                    }

                    EncapsulateRendererBounds(unit, ref hasBounds, ref bounds);
                }
            }

            var originY = node != null ? node.ResolveUnitAnchorWorldPosition().y : 0f;
            if (!hasBounds)
            {
                return 0.65f;
            }

            return Mathf.Max(0.42f, bounds.max.y - originY + 0.02f);
        }

        private void EncapsulateRendererBounds(Component source, ref bool hasBounds, ref Bounds bounds)
        {
            if (source == null)
            {
                return;
            }

            _rendererScratch.Clear();
            source.GetComponentsInChildren(true, _rendererScratch);
            for (var i = 0; i < _rendererScratch.Count; i++)
            {
                var renderer = _rendererScratch[i];
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

            _rendererScratch.Clear();
        }

        private bool IsCombatUnit(UnitView unit)
        {
            if (unit == null)
            {
                return false;
            }

            var unitType = NormalizeToken(unit.UnitType);
            if (string.IsNullOrEmpty(unitType))
            {
                return false;
            }

            if (IsSettlerLikeUnit(unitType) && HasHitPoints(unit))
            {
                return true;
            }

            var units = _staticCatalogStore?.Snapshot?.Units;
            if (units != null && units.TryGetValue(unitType, out var catalogUnit) && catalogUnit != null)
            {
                if (catalogUnit.Attack > 0 || catalogUnit.AttackRange > 0)
                {
                    return true;
                }

                var unitClass = NormalizeToken(catalogUnit.Class);
                if (!string.IsNullOrEmpty(unitClass) && unitClass != "civilian")
                {
                    return true;
                }

                return false;
            }

            return unitType != "settler" &&
                   unitType != "pioneer" &&
                   unitType != "expander" &&
                   unitType != "engineer" &&
                   unitType != "scout";
        }

        private static bool IsSettlerLikeUnit(string unitType)
        {
            return string.Equals(unitType, "settler", System.StringComparison.Ordinal) ||
                   string.Equals(unitType, "pioneer", System.StringComparison.Ordinal) ||
                   string.Equals(unitType, "expander", System.StringComparison.Ordinal) ||
                   string.Equals(unitType, "engineer", System.StringComparison.Ordinal);
        }

        private static bool HasHitPoints(UnitView unit)
        {
            return unit != null && (unit.HitPoints > 0 || unit.MaxHitPoints > 0);
        }

        private Color ResolveHpColor(float ratio01)
        {
            var ratio = Mathf.Clamp01(ratio01);
            if (ratio < 0.2f)
            {
                return hpCriticalColor;
            }

            if (ratio < 0.5f)
            {
                return hpWoundedColor;
            }

            return hpHealthyColor;
        }

        private Color ResolveOwnerColor(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return neutralOwnerColor;
            }

            var myPlayerId = _gameStateStore != null ? _gameStateStore.Snapshot.MyPlayerId : string.Empty;
            if (!string.IsNullOrWhiteSpace(myPlayerId))
            {
                return string.Equals(ownerId, myPlayerId, System.StringComparison.OrdinalIgnoreCase)
                    ? friendlyOwnerColor
                    : enemyOwnerColor;
            }

            return enemyOwnerColor;
        }

        private Sprite GetDefaultSprite()
        {
            if (_defaultUiSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _defaultUiSprite = tex != null
                    ? Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f))
                    : null;
            }

            return _defaultUiSprite;
        }

        private void HideAll()
        {
            HideEntries(_cityCoreEntries);
            HideEntries(_unitStackEntries);
        }

        private static void HideEntries(Dictionary<string, Entry> entries)
        {
            foreach (var pair in entries)
            {
                var entry = pair.Value;
                if (entry?.Root != null)
                {
                    SetActiveIfChanged(entry.Root.gameObject, false);
                }
            }
        }

        private static void SetActiveIfChanged(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        private void RemoveEntry(string nodeId)
        {
            RemoveEntry(_cityCoreEntries, nodeId);
        }

        private void RemoveEntry(Dictionary<string, Entry> entries, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            if (entries.TryGetValue(nodeId, out var entry) && entry != null && entry.Root != null)
            {
                Destroy(entry.Root.gameObject);
            }

            entries.Remove(nodeId);
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
