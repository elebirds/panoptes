/*************************************************
 * Project: Panoptes
 * File: BuildCommandPanel.cs
 * Author: Panoptes Team
 * Date: 2026-04-17
 * Description: Prefab-driven grouped build panel controller.
 *************************************************/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Feedback;
using Panoptes.Core.Events;
using Panoptes.Presentation.Map;
using Panoptes.Presentation.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class BuildCommandPanel : MonoBehaviour
    {
        public enum BuildRule
        {
            AnyTerrain = 0,
            ResourceOnly = 1,
            CityOnly = 2
        }

        [Serializable]
        private sealed class BuildConfigRoot
        {
            public string config_version;
            public string default_locale;
            public BuildConfigEntry[] buildings;
        }

        [Serializable]
        private sealed class BuildConfigEntry
        {
            public string id;
            public string name;
            public string description;
            public string icon_key;
            public string placement_rule;
            public string placement_kind;
            public string required_resource_type;
            public int sort_order;
        }

        private readonly struct BuildCostEntry
        {
            public readonly string Key;
            public readonly int Amount;
            public readonly bool IsPoint;

            public BuildCostEntry(string key, int amount, bool isPoint)
            {
                Key = key;
                Amount = amount;
                IsPoint = isPoint;
            }
        }

        private readonly struct DynamicBuildRenderEntry
        {
            public readonly string BuildingId;
            public readonly BuildConfigEntry Config;
            public readonly BuildRule Rule;

            public DynamicBuildRenderEntry(string buildingId, BuildConfigEntry config, BuildRule rule)
            {
                BuildingId = buildingId;
                Config = config;
                Rule = rule;
            }
        }

        [Header("Top Area")]
        [SerializeField] private Image emblemImage;
        [SerializeField] private Sprite fallbackEmblem;

        [Header("Bindings")]
        [SerializeField] private MapInputHandler mapInputHandler;
        [SerializeField] private bool autoFindMapInputHandler = true;
        [SerializeField] private Button cancelButton;
        [SerializeField] private BuildTooltipView tooltipView;
        [SerializeField] private ScrollRect listScrollRect;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private RectTransform buildItemListRoot;
        [SerializeField] private BuildGroupView buildGroupPrefab;
        [SerializeField] private BuildItemView buildItemPrefab;

        [Header("Build Layout")]
        [SerializeField] private string[] runtimeBuildOrder =
        {
            "farm",
            "lumberyard",
            "smelter",
            "engineer",
            "workshop",
            "archery",
            "blacksmith",
            "tower",
            "watchtower"
        };
        [SerializeField] private bool includeUnknownRuntimeBuildOrderEntries = false;
        [SerializeField] private bool includeCityFoundationBuilding = false;
        [SerializeField] private string[] hiddenBuildingTypes = { "city_core" };

        [Header("Catalog Assets")]
        [SerializeField] private string localCatalogBundleResourcePath = "Data/catalog.bundle";
        [SerializeField] private string iconResourcesRoot = "Icons/Buildings";
        [SerializeField] private string[] materialIconResourcesRoots = { "Icons/Resources", "Icons/Points", "Icons" };
        [SerializeField] private Sprite fallbackMaterialIcon;
        [SerializeField] private Sprite buildItemLockedIcon;
        [SerializeField] private string buildItemLockedTooltipSuffix = "Locked: requires technology unlock";

        [Header("Build Catalog Source")]
        [SerializeField] private bool applyConfigToButtons = true;
        [SerializeField] private bool useConfigPlacementRule = true;
        [SerializeField] private bool listenCatalogUpdates = true;
        [SerializeField] private TextAsset buildConfigJson;
        [SerializeField] private string buildConfigResourcesPath = "Config/buildconfig";
        [SerializeField] private bool logConfigWarnings = true;

        private readonly List<Button> _boundButtons = new();
        private readonly List<UnityAction> _boundActions = new();
        private readonly Dictionary<string, BuildConfigEntry> _buildConfigById = new();
        private readonly Dictionary<string, List<BuildCostEntry>> _buildCostsById = new();
        private readonly Dictionary<string, StaticCatalogCache.ResourceEntryJson> _resourceMetaByKey = new();
        private readonly Dictionary<string, StaticCatalogCache.PointEntryJson> _pointMetaByKey = new();
        private readonly Dictionary<string, List<string>> _requiredTechsByBuilding = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _activeTechnologyIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Sprite> _spriteCache = new();

        private Coroutine _emblemLoadRoutine;
        private StaticCatalogCache _catalogCache;
        private GameStateCache _gameStateCache;
        private PlanningDraftCache _planningDraftCache;
        private string _activeCityCoreNodeId = string.Empty;
        private bool _loggedMissingBuildConfigThisEnable;
        private Vector2 _buildListBaseAnchoredPos;
        private float _buildListScrollOffset;
        private bool _buildListScrollInitialized;

        private static readonly Regex NumberPairRegex = new("\"([^\"]+)\"\\s*:\\s*(-?\\d+)", RegexOptions.Compiled);

        private void OnEnable()
        {
            _loggedMissingBuildConfigThisEnable = false;
            SubscribeCatalogUpdates();
            SubscribeFeedbackEvents();
            ResolveMapInputHandler();
            ResolveViewReferences();
            RefreshBuildItems();
        }

        private void OnDisable()
        {
            UnbindButtons();
            UnsubscribeCatalogUpdates();
            UnsubscribeFeedbackEvents();
            if (tooltipView != null)
            {
                tooltipView.Hide();
            }
        }

        public void TriggerAny(string buildingType)
        {
            TriggerBuild(buildingType, BuildRule.AnyTerrain);
        }

        public void TriggerResource(string buildingType)
        {
            TriggerBuild(buildingType, BuildRule.ResourceOnly);
        }

        public void TriggerCity(string buildingType)
        {
            TriggerBuild(buildingType, BuildRule.CityOnly);
        }

        public void CancelPlacement()
        {
            ResolveMapInputHandler();
            mapInputHandler?.CancelCurrentMode();
        }

        public void SetCityCoreContext(string cityCoreNodeId)
        {
            _activeCityCoreNodeId = (cityCoreNodeId ?? string.Empty).Trim();
            RefreshBuildItems();
        }

        public void ClearCityCoreContext()
        {
            _activeCityCoreNodeId = string.Empty;
            RefreshBuildItems();
        }

        public void RefreshBuildItems()
        {
            ResolveViewReferences();
            LoadBuildConfig();
            RenderBuildItems();
            ResetScrollPosition();
        }

        public void SetCancelButtonVisible(bool visible)
        {
            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(visible);
            }
        }

        public void SetEmblemSprite(Sprite sprite)
        {
            if (emblemImage == null)
            {
                return;
            }

            emblemImage.sprite = sprite != null ? sprite : fallbackEmblem;
            emblemImage.preserveAspect = true;
        }

        public void SetEmblemFromResources(string resourcePath)
        {
            SetEmblemSprite(Resources.Load<Sprite>(resourcePath));
        }

        public void SetEmblemFromUrl(string imageUrl)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_emblemLoadRoutine != null)
            {
                StopCoroutine(_emblemLoadRoutine);
            }

            _emblemLoadRoutine = StartCoroutine(LoadEmblemCoroutine(imageUrl));
        }

        private void RenderBuildItems()
        {
            UnbindButtons();
            BindCancelButton();

            if (!ResolveViewReferences())
            {
                WarnMissingBuildConfigOnce("[BuildCommandPanel] Build panel list references are missing.");
                return;
            }

            var renderEntries = BuildDynamicRenderEntries();
            if (renderEntries.Count == 0)
            {
                ClearRenderedItems();
                WarnMissingBuildConfigOnce("[BuildCommandPanel] Build list config is empty. Waiting for server/static catalog.");
                return;
            }

            RebuildRequiredTechMap();
            RebuildActiveTechnologySet();

            var sources = BuildItemSources(renderEntries);
            var groups = BuildPanelRenderBuilder.Build(sources);
            RebuildListContent(groups);
        }

        private void BindCancelButton()
        {
            if (cancelButton == null)
            {
                return;
            }

            UnityAction cancelAction = CancelPlacement;
            cancelButton.onClick.AddListener(cancelAction);
            _boundButtons.Add(cancelButton);
            _boundActions.Add(cancelAction);
        }

        private List<BuildItemRenderSource> BuildItemSources(IReadOnlyList<DynamicBuildRenderEntry> renderEntries)
        {
            var result = new List<BuildItemRenderSource>(renderEntries.Count);
            for (var i = 0; i < renderEntries.Count; i++)
            {
                var renderEntry = renderEntries[i];
                var config = renderEntry.Config;
                var buildingId = renderEntry.BuildingId;
                var availability = ResolveAvailabilityState(buildingId);
                var source = new BuildItemRenderSource
                {
                    BuildingId = buildingId,
                    Title = config != null && !string.IsNullOrWhiteSpace(config.name) ? config.name.Trim() : buildingId,
                    Description = config != null ? (config.description ?? string.Empty) : string.Empty,
                    PlacementKind = config != null ? (!string.IsNullOrWhiteSpace(config.placement_kind) ? config.placement_kind : config.placement_rule) : string.Empty,
                    RequiredResourceType = config != null ? config.required_resource_type : string.Empty,
                    Icon = config != null ? LoadIconByKey(config.icon_key) : null,
                    AvailabilityState = availability,
                    LockedSuffix = buildItemLockedTooltipSuffix
                };

                var costSources = BuildMetricSources(buildingId);
                for (var costIndex = 0; costIndex < costSources.Count; costIndex++)
                {
                    source.Costs.Add(costSources[costIndex]);
                }

                var techNames = ResolveRequiredTechnologyNames(buildingId);
                for (var techIndex = 0; techIndex < techNames.Count; techIndex++)
                {
                    source.RequiredTechNames.Add(techNames[techIndex]);
                }

                result.Add(source);
            }

            return result;
        }

        private void RebuildListContent(IReadOnlyList<BuildGroupRenderModel> groups)
        {
            ClearRenderedItems();
            if (groups == null || groups.Count == 0)
            {
                return;
            }

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var groupModel = groups[groupIndex];
                if (groupModel == null || groupModel.Items.Count == 0)
                {
                    continue;
                }

                var groupView = InstantiateGroupView();
                if (groupView == null)
                {
                    continue;
                }

                groupView.Bind(groupModel.Title);
                groupView.gameObject.SetActive(true);

                for (var itemIndex = 0; itemIndex < groupModel.Items.Count; itemIndex++)
                {
                    var itemModel = groupModel.Items[itemIndex];
                    var itemView = InstantiateItemView();
                    if (itemView == null)
                    {
                        continue;
                    }

                    itemView.Bind(itemModel);
                    itemView.SetLocked(itemModel.AvailabilityState == BuildItemAvailabilityState.Locked, buildItemLockedIcon);

                    var targetBuilding = itemModel.BuildingId;
                    var rule = ResolveBuildRuleFor(targetBuilding);
                    UnityAction action = () => TriggerBuild(targetBuilding, rule);
                    itemView.SetClickAction(action);
                    if (itemView.ClickButton != null)
                    {
                        _boundButtons.Add(itemView.ClickButton);
                        _boundActions.Add(action);
                        InstallTooltip(itemView.ClickButton, itemModel.TooltipText);
                    }

                    itemView.gameObject.SetActive(true);
                }
            }
        }

        private BuildGroupView InstantiateGroupView()
        {
            var template = ResolveGroupPrefab();
            if (template == null || listContent == null)
            {
                return null;
            }

            var instance = Instantiate(template.gameObject, listContent, false);
            instance.name = "BuildGroup";
            return instance.GetComponent<BuildGroupView>();
        }

        private BuildItemView InstantiateItemView()
        {
            var template = ResolveItemPrefab();
            if (template == null || listContent == null)
            {
                return null;
            }

            var instance = Instantiate(template.gameObject, listContent, false);
            instance.name = "BuildItem";
            return instance.GetComponent<BuildItemView>();
        }

        private void ClearRenderedItems()
        {
            if (listContent == null)
            {
                return;
            }

            for (var i = listContent.childCount - 1; i >= 0; i--)
            {
                var child = listContent.GetChild(i);
                if (child == null || IsTemplateTransform(child))
                {
                    continue;
                }

                DestroyUiObject(child.gameObject);
            }
        }

        private bool IsTemplateTransform(Transform child)
        {
            if (child == null)
            {
                return false;
            }

            return (buildGroupPrefab != null && ReferenceEquals(child, buildGroupPrefab.transform))
                   || (buildItemPrefab != null && ReferenceEquals(child, buildItemPrefab.transform));
        }

        private static void DestroyUiObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private bool ResolveViewReferences()
        {
            ResolveTooltipView();
            ResolveScrollRect();
            ResolveListContent();
            ResolveGroupPrefab();
            ResolveItemPrefab();

            return listContent != null && ResolveItemPrefab() != null && ResolveGroupPrefab() != null;
        }

        private void ResolveTooltipView()
        {
            if (tooltipView != null)
            {
                return;
            }

            tooltipView = GetComponentInChildren<BuildTooltipView>(true);
            if (tooltipView == null)
            {
                tooltipView = UnityEngine.Object.FindAnyObjectByType<BuildTooltipView>();
            }
        }

        private void ResolveScrollRect()
        {
            if (listScrollRect == null)
            {
                listScrollRect = GetComponent<ScrollRect>();
            }

            if (listScrollRect == null)
            {
                listScrollRect = gameObject.AddComponent<ScrollRect>();
            }

            listScrollRect.horizontal = false;
            listScrollRect.vertical = true;
            listScrollRect.movementType = ScrollRect.MovementType.Clamped;
            listScrollRect.scrollSensitivity = 20f;
        }

        private void ResolveListContent()
        {
            if (listContent == null && listScrollRect != null && listScrollRect.content != null)
            {
                listContent = listScrollRect.content;
            }

            if (listContent == null && buildItemListRoot != null)
            {
                listContent = buildItemListRoot;
            }

            var viewport = transform.Find("Viewport") as RectTransform;
            if (viewport == null)
            {
                var legacyViewport = transform.Find("OneGroup") as RectTransform;
                if (legacyViewport != null)
                {
                    viewport = legacyViewport;
                    legacyViewport.name = "Viewport";
                }
            }

            if (viewport == null)
            {
                viewport = CreateViewport();
            }

            EnsureViewportComponents(viewport);

            if (listContent == null)
            {
                var existingContent = viewport.Find("Content") as RectTransform;
                listContent = existingContent != null ? existingContent : CreateContentRoot(viewport);
            }

            EnsureContentLayout(listContent);
            buildItemListRoot = listContent;
            listScrollRect.viewport = viewport;
            listScrollRect.content = listContent;
        }

        private RectTransform CreateViewport()
        {
            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(transform, false);
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = new Vector2(0f, -28f);
            viewport.sizeDelta = new Vector2(-20f, -120f);
            return viewport;
        }

        private static void EnsureViewportComponents(RectTransform viewport)
        {
            if (viewport == null)
            {
                return;
            }

            var image = viewport.GetComponent<Image>();
            if (image == null)
            {
                image = viewport.gameObject.AddComponent<Image>();
            }

            image.color = new Color(0.02f, 0.03f, 0.06f, 0.15f);
            var mask = viewport.GetComponent<Mask>();
            if (mask == null)
            {
                mask = viewport.gameObject.AddComponent<Mask>();
            }

            mask.showMaskGraphic = false;
        }

        private static RectTransform CreateContentRoot(RectTransform viewport)
        {
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(-6f, 0f);
            return content;
        }

        private static void EnsureContentLayout(RectTransform content)
        {
            if (content == null)
            {
                return;
            }

            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(0, 0, 0, 12);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private BuildGroupView ResolveGroupPrefab()
        {
            if (buildGroupPrefab != null)
            {
                buildGroupPrefab.gameObject.SetActive(false);
                return buildGroupPrefab;
            }

            buildGroupPrefab = GetComponentInChildren<BuildGroupView>(true);
            if (buildGroupPrefab != null)
            {
                buildGroupPrefab.gameObject.SetActive(false);
            }

            return buildGroupPrefab;
        }

        private BuildItemView ResolveItemPrefab()
        {
            if (buildItemPrefab != null)
            {
                buildItemPrefab.gameObject.SetActive(false);
                return buildItemPrefab;
            }

            var candidates = GetComponentsInChildren<BuildItemView>(true);
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.GetComponent<BuildGroupView>() != null)
                {
                    continue;
                }

                buildItemPrefab = candidate;
                buildItemPrefab.gameObject.SetActive(false);
                break;
            }

            return buildItemPrefab;
        }

        private void ResetScrollPosition()
        {
            ResetBuildListScroll(true);
            if (listScrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            listScrollRect.verticalNormalizedPosition = 1f;
        }

        private void ResetBuildListScroll(bool forceReset)
        {
            var root = buildItemListRoot != null ? buildItemListRoot : listContent;
            if (root == null)
            {
                return;
            }

            if (forceReset || !_buildListScrollInitialized)
            {
                _buildListBaseAnchoredPos = new Vector2(root.anchoredPosition.x, 0f);
                _buildListScrollOffset = 0f;
                _buildListScrollInitialized = true;
                root.anchoredPosition = _buildListBaseAnchoredPos;
                return;
            }

            root.anchoredPosition = _buildListBaseAnchoredPos + new Vector2(0f, _buildListScrollOffset);
        }

        private void ResolveMapInputHandler()
        {
            if (mapInputHandler != null)
            {
                return;
            }

            if (!autoFindMapInputHandler)
            {
                return;
            }

            mapInputHandler = MapInputHandler.Instance;
            if (mapInputHandler == null)
            {
                mapInputHandler = UnityEngine.Object.FindAnyObjectByType<MapInputHandler>();
            }
        }

        private List<DynamicBuildRenderEntry> BuildDynamicRenderEntries()
        {
            var result = new List<DynamicBuildRenderEntry>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var buildOrder = ResolveRuntimeBuildOrder();

            if (buildOrder != null)
            {
                for (var i = 0; i < buildOrder.Length; i++)
                {
                    var resolvedId = ResolveConfiguredBuildingType(buildOrder[i]);
                    if (string.IsNullOrWhiteSpace(resolvedId) || visited.Contains(resolvedId) || IsHiddenBuildingType(resolvedId))
                    {
                        continue;
                    }

                    var config = GetBuildConfigEntry(resolvedId);
                    if (config == null && !includeUnknownRuntimeBuildOrderEntries)
                    {
                        continue;
                    }

                    result.Add(new DynamicBuildRenderEntry(
                        resolvedId,
                        config,
                        ResolveBuildRule(ResolveDefaultRule(resolvedId), config)));
                    visited.Add(resolvedId);
                }
            }

            if (_buildConfigById.Count == 0)
            {
                return result;
            }

            var remaining = new List<BuildConfigEntry>(_buildConfigById.Values);
            remaining.Sort((left, right) =>
            {
                var sortCompare = left.sort_order.CompareTo(right.sort_order);
                return sortCompare != 0
                    ? sortCompare
                    : string.Compare(left.id, right.id, StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < remaining.Count; i++)
            {
                var config = remaining[i];
                if (config == null)
                {
                    continue;
                }

                var resolvedId = ResolveConfiguredBuildingType(config.id);
                if (string.IsNullOrWhiteSpace(resolvedId) || visited.Contains(resolvedId) || IsHiddenBuildingType(resolvedId))
                {
                    continue;
                }

                result.Add(new DynamicBuildRenderEntry(
                    resolvedId,
                    config,
                    ResolveBuildRule(ResolveDefaultRule(resolvedId), config)));
                visited.Add(resolvedId);
            }

            return result;
        }

        private bool IsHiddenBuildingType(string buildingType)
        {
            var normalized = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(normalized))
            {
                return true;
            }

            if (hiddenBuildingTypes != null)
            {
                for (var i = 0; i < hiddenBuildingTypes.Length; i++)
                {
                    if (string.Equals(normalized, NormalizeToken(hiddenBuildingTypes[i]), StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            var layout = (_catalogCache != null ? _catalogCache : StaticCatalogCache.Instance)?.BuildMenuLayout;
            if (layout != null && layout.hidden_building_ids != null)
            {
                for (var i = 0; i < layout.hidden_building_ids.Length; i++)
                {
                    if (string.Equals(normalized, NormalizeToken(layout.hidden_building_ids[i]), StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            if (includeCityFoundationBuilding)
            {
                return false;
            }

            var config = GetBuildConfigEntry(normalized);
            if (config == null)
            {
                return false;
            }

            var placementKind = NormalizeToken(!string.IsNullOrWhiteSpace(config.placement_kind)
                ? config.placement_kind
                : config.placement_rule);
            return placementKind == "city_foundation_center";
        }

        private string[] ResolveRuntimeBuildOrder()
        {
            var layout = (_catalogCache != null ? _catalogCache : StaticCatalogCache.Instance)?.BuildMenuLayout;
            if (layout != null && layout.building_order != null && layout.building_order.Length > 0)
            {
                return layout.building_order;
            }

            return runtimeBuildOrder;
        }

        private static BuildRule ResolveDefaultRule(string buildingType)
        {
            return NormalizeToken(buildingType) switch
            {
                "farm" => BuildRule.ResourceOnly,
                "mine" => BuildRule.ResourceOnly,
                "lumberyard" => BuildRule.ResourceOnly,
                "lumber" => BuildRule.ResourceOnly,
                "smelter" => BuildRule.ResourceOnly,
                _ => BuildRule.CityOnly
            };
        }

        private BuildRule ResolveBuildRuleFor(string buildingType)
        {
            var config = GetBuildConfigEntry(buildingType);
            return ResolveBuildRule(ResolveDefaultRule(buildingType), config);
        }

        private void UnbindButtons()
        {
            var count = Mathf.Min(_boundButtons.Count, _boundActions.Count);
            for (var i = 0; i < count; i++)
            {
                var button = _boundButtons[i];
                var action = _boundActions[i];
                if (button != null && action != null)
                {
                    button.onClick.RemoveListener(action);
                }
            }

            _boundButtons.Clear();
            _boundActions.Clear();
        }

        private void TriggerBuild(string buildingType, BuildRule rule)
        {
            ResolveMapInputHandler();
            if (mapInputHandler == null)
            {
                Debug.LogWarning("[BuildCommandPanel] MapInputHandler is missing.");
                return;
            }

            var normalized = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(normalized))
            {
                Debug.LogWarning("[BuildCommandPanel] Building type is empty.");
                return;
            }

            switch (rule)
            {
                case BuildRule.ResourceOnly:
                    mapInputHandler.EnterBuildPlacementResource(normalized, _activeCityCoreNodeId);
                    break;
                case BuildRule.CityOnly:
                    mapInputHandler.EnterBuildPlacementCity(normalized, _activeCityCoreNodeId);
                    break;
                default:
                    mapInputHandler.EnterBuildPlacementAny(normalized, _activeCityCoreNodeId);
                    break;
            }
        }

        private void SubscribeFeedbackEvents()
        {
            _gameStateCache = GameStateCache.Instance;
            if (_gameStateCache != null)
            {
                _gameStateCache.OnPlanningCommandResult -= OnPlanningCommandResult;
                _gameStateCache.OnPlanningCommandResult += OnPlanningCommandResult;
                _gameStateCache.OnStateChanged -= OnGameStateChanged;
                _gameStateCache.OnStateChanged += OnGameStateChanged;
            }

            _planningDraftCache = PlanningDraftCache.Instance ?? PlanningDraftCache.EnsureInstance();
            if (_planningDraftCache != null)
            {
                _planningDraftCache.BuildPreviewChanged -= OnBuildPreviewChanged;
                _planningDraftCache.BuildPreviewChanged += OnBuildPreviewChanged;
                _planningDraftCache.OrdersChanged -= OnOrdersChanged;
                _planningDraftCache.OrdersChanged += OnOrdersChanged;
            }
        }

        private void UnsubscribeFeedbackEvents()
        {
            if (_gameStateCache != null)
            {
                _gameStateCache.OnPlanningCommandResult -= OnPlanningCommandResult;
                _gameStateCache.OnStateChanged -= OnGameStateChanged;
                _gameStateCache = null;
            }

            if (_planningDraftCache != null)
            {
                _planningDraftCache.BuildPreviewChanged -= OnBuildPreviewChanged;
                _planningDraftCache.OrdersChanged -= OnOrdersChanged;
                _planningDraftCache = null;
            }
        }

        private void OnOrdersChanged()
        {
            RefreshBuildItems();
        }

        private void OnBuildPreviewChanged()
        {
            RefreshBuildItems();
        }

        private void OnGameStateChanged()
        {
            RefreshBuildItems();
        }

        private void OnPlanningCommandResult(PlanningCommandResultEvent evt)
        {
            if (evt == null || !string.Equals(NormalizeToken(evt.CommandType), "build", StringComparison.Ordinal))
            {
                return;
            }

            if (!evt.Success)
            {
                var message = GameplayFeedbackText.ResolveMessage(evt.Message, evt.ErrorCode);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    if (ErrorToast.Instance != null)
                    {
                        ErrorToast.Instance.Show(message, false);
                    }
                    else
                    {
                        Debug.LogWarning($"[BuildCommandPanel] {message}");
                    }
                }
            }

            RefreshBuildItems();
        }

        private void InstallTooltip(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            var trigger = button.GetComponent<BuildTooltipTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<BuildTooltipTrigger>();
            }

            if (tooltipView == null || string.IsNullOrWhiteSpace(text))
            {
                trigger.Configure(null, string.Empty);
                return;
            }

            trigger.Configure(tooltipView, text.Trim());
        }

        private void SubscribeCatalogUpdates()
        {
            if (!listenCatalogUpdates)
            {
                return;
            }

            _catalogCache = StaticCatalogCache.EnsureInstance();
            if (_catalogCache != null)
            {
                _catalogCache.CatalogChanged -= OnCatalogUpdated;
                _catalogCache.CatalogChanged += OnCatalogUpdated;
            }
        }

        private void UnsubscribeCatalogUpdates()
        {
            if (_catalogCache != null)
            {
                _catalogCache.CatalogChanged -= OnCatalogUpdated;
                _catalogCache = null;
            }
        }

        private void OnCatalogUpdated()
        {
            RefreshBuildItems();
        }

        private void LoadBuildConfig()
        {
            _buildConfigById.Clear();
            _buildCostsById.Clear();
            _resourceMetaByKey.Clear();
            _pointMetaByKey.Clear();
            if (!applyConfigToButtons)
            {
                return;
            }

            var loadedFromCatalog = TryLoadBuildConfigFromStaticCatalog();
            if (!loadedFromCatalog)
            {
                var source = buildConfigJson;
                if (source == null && !string.IsNullOrWhiteSpace(buildConfigResourcesPath))
                {
                    source = Resources.Load<TextAsset>(buildConfigResourcesPath);
                }

                if (source == null || string.IsNullOrWhiteSpace(source.text))
                {
                    if (logConfigWarnings)
                    {
                        Debug.LogWarning("[BuildCommandPanel] Build config JSON is missing.");
                    }

                    return;
                }

                ParseBuildConfigText(source.text);
            }

            TryLoadBuildCostsFromLocalCatalogBundle();
        }

        private bool TryLoadBuildConfigFromStaticCatalog()
        {
            var cache = _catalogCache != null ? _catalogCache : StaticCatalogCache.Instance;
            if (cache == null || cache.Buildings == null || cache.Buildings.Count == 0)
            {
                return false;
            }

            foreach (var pair in cache.Buildings)
            {
                var building = pair.Value;
                if (building == null || string.IsNullOrWhiteSpace(building.id))
                {
                    continue;
                }

                _buildConfigById[NormalizeToken(building.id)] = new BuildConfigEntry
                {
                    id = building.id,
                    name = building.name,
                    description = building.description,
                    icon_key = building.icon_key,
                    placement_rule = building.placement_kind,
                    placement_kind = building.placement_kind,
                    required_resource_type = building.required_resource_type,
                    sort_order = building.sort_order
                };
            }

            if (cache.Resources != null)
            {
                foreach (var pair in cache.Resources)
                {
                    if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Value.key))
                    {
                        continue;
                    }

                    _resourceMetaByKey[NormalizeToken(pair.Value.key)] = pair.Value;
                }
            }

            if (cache.Points != null)
            {
                foreach (var pair in cache.Points)
                {
                    if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Value.key))
                    {
                        continue;
                    }

                    _pointMetaByKey[NormalizeToken(pair.Value.key)] = pair.Value;
                }
            }

            return _buildConfigById.Count > 0;
        }

        private bool ParseBuildConfigText(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                return false;
            }

            BuildConfigRoot config;
            try
            {
                config = JsonUtility.FromJson<BuildConfigRoot>(jsonText);
            }
            catch (Exception ex)
            {
                if (logConfigWarnings)
                {
                    Debug.LogWarning($"[BuildCommandPanel] Failed to parse build config JSON: {ex.Message}");
                }

                return false;
            }

            if (config == null || config.buildings == null || config.buildings.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < config.buildings.Length; i++)
            {
                var entry = config.buildings[i];
                var id = NormalizeToken(entry != null ? entry.id : string.Empty);
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                _buildConfigById[id] = entry;
            }

            return true;
        }

        private void TryLoadBuildCostsFromLocalCatalogBundle()
        {
            if (string.IsNullOrWhiteSpace(localCatalogBundleResourcePath))
            {
                return;
            }

            var asset = Resources.Load<TextAsset>(localCatalogBundleResourcePath.Trim());
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                return;
            }

            EnumerateArrayObjects(asset.text, "buildings", ParseSingleBuildingCostObject);
        }

        private void ParseSingleBuildingCostObject(string objectText)
        {
            if (string.IsNullOrWhiteSpace(objectText) || !TryExtractStringField(objectText, "id", out var id))
            {
                return;
            }

            var normalizedId = NormalizeToken(id);
            if (string.IsNullOrEmpty(normalizedId))
            {
                return;
            }

            var costs = new List<BuildCostEntry>(8);
            if (TryExtractObjectField(objectText, "resource_costs", out var resourceCosts))
            {
                AppendCostEntries(costs, resourceCosts, false);
            }

            if (TryExtractObjectField(objectText, "point_costs", out var pointCosts))
            {
                AppendCostEntries(costs, pointCosts, true);
            }

            if (costs.Count > 0)
            {
                _buildCostsById[normalizedId] = costs;
            }
        }

        private static void EnumerateArrayObjects(string jsonText, string fieldName, Action<string> consume)
        {
            if (string.IsNullOrWhiteSpace(jsonText) || string.IsNullOrWhiteSpace(fieldName) || consume == null)
            {
                return;
            }

            var token = $"\"{fieldName}\"";
            var fieldIndex = jsonText.IndexOf(token, StringComparison.Ordinal);
            if (fieldIndex < 0)
            {
                return;
            }

            var arrayStart = jsonText.IndexOf('[', fieldIndex);
            if (arrayStart < 0)
            {
                return;
            }

            var inString = false;
            var escaped = false;
            var depth = 0;
            var objectStart = -1;
            for (var i = arrayStart + 1; i < jsonText.Length; i++)
            {
                var c = jsonText[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0)
                    {
                        objectStart = i;
                    }

                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    if (depth <= 0)
                    {
                        continue;
                    }

                    depth--;
                    if (depth == 0 && objectStart >= 0)
                    {
                        consume(jsonText.Substring(objectStart, i - objectStart + 1));
                        objectStart = -1;
                    }

                    continue;
                }

                if (c == ']' && depth == 0)
                {
                    break;
                }
            }
        }

        private static bool TryExtractStringField(string objectText, string fieldName, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(objectText) || string.IsNullOrWhiteSpace(fieldName))
            {
                return false;
            }

            var pattern = $"\"{Regex.Escape(fieldName)}\"\\s*:\\s*\"([^\"]*)\"";
            var match = Regex.Match(objectText, pattern);
            if (!match.Success || match.Groups.Count < 2)
            {
                return false;
            }

            value = match.Groups[1].Value;
            return true;
        }

        private static bool TryExtractObjectField(string objectText, string fieldName, out string nestedObject)
        {
            nestedObject = string.Empty;
            if (string.IsNullOrWhiteSpace(objectText) || string.IsNullOrWhiteSpace(fieldName))
            {
                return false;
            }

            var token = $"\"{fieldName}\"";
            var fieldIndex = objectText.IndexOf(token, StringComparison.Ordinal);
            if (fieldIndex < 0)
            {
                return false;
            }

            var startBrace = objectText.IndexOf('{', fieldIndex);
            if (startBrace < 0)
            {
                return false;
            }

            var inString = false;
            var escaped = false;
            var depth = 0;
            for (var i = startBrace; i < objectText.Length; i++)
            {
                var c = objectText[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        nestedObject = objectText.Substring(startBrace, i - startBrace + 1);
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AppendCostEntries(List<BuildCostEntry> target, string amountObject, bool isPoint)
        {
            if (target == null || string.IsNullOrWhiteSpace(amountObject))
            {
                return;
            }

            var matches = NumberPairRegex.Matches(amountObject);
            for (var i = 0; i < matches.Count; i++)
            {
                var key = NormalizeToken(matches[i].Groups[1].Value);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!int.TryParse(matches[i].Groups[2].Value, out var amount))
                {
                    continue;
                }

                target.Add(new BuildCostEntry(key, amount, isPoint));
            }
        }

        private BuildConfigEntry GetBuildConfigEntry(string buildingType)
        {
            var key = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (_buildConfigById.TryGetValue(key, out var entry))
            {
                return entry;
            }

            var aliases = GetAliasKeys(key);
            for (var i = 0; i < aliases.Length; i++)
            {
                if (_buildConfigById.TryGetValue(aliases[i], out entry))
                {
                    return entry;
                }
            }

            return null;
        }

        private string ResolveConfiguredBuildingType(string buildingType)
        {
            var key = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            if (_buildConfigById.ContainsKey(key))
            {
                return key;
            }

            var aliases = GetAliasKeys(key);
            for (var i = 0; i < aliases.Length; i++)
            {
                if (_buildConfigById.ContainsKey(aliases[i]))
                {
                    return aliases[i];
                }
            }

            return key;
        }

        private List<BuildMetricSource> BuildMetricSources(string buildingType)
        {
            var result = new List<BuildMetricSource>();
            var normalizedBuilding = NormalizeToken(buildingType);
            if (string.IsNullOrWhiteSpace(normalizedBuilding) ||
                !_buildCostsById.TryGetValue(normalizedBuilding, out var costs) ||
                costs == null ||
                costs.Count == 0)
            {
                return result;
            }

            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                if (string.IsNullOrWhiteSpace(cost.Key) || cost.Amount <= 0)
                {
                    continue;
                }

                var displayName = cost.Key;
                var iconKey = string.Empty;
                if (cost.IsPoint)
                {
                    if (_pointMetaByKey.TryGetValue(cost.Key, out var pointMeta) && pointMeta != null)
                    {
                        displayName = string.IsNullOrWhiteSpace(pointMeta.display_name)
                            ? displayName
                            : pointMeta.display_name.Trim();
                        iconKey = pointMeta.icon_key;
                    }
                }
                else if (_resourceMetaByKey.TryGetValue(cost.Key, out var resourceMeta) && resourceMeta != null)
                {
                    displayName = string.IsNullOrWhiteSpace(resourceMeta.display_name)
                        ? displayName
                        : resourceMeta.display_name.Trim();
                    iconKey = resourceMeta.icon_key;
                }

                result.Add(new BuildMetricSource(
                    cost.Key,
                    displayName,
                    cost.Amount,
                    LoadMaterialIconByKey(iconKey),
                    cost.IsPoint));
            }

            return result;
        }

        private void RebuildRequiredTechMap()
        {
            _requiredTechsByBuilding.Clear();

            var cache = _catalogCache != null ? _catalogCache : StaticCatalogCache.Instance;
            if (cache == null || cache.Technologies == null || cache.Technologies.Count == 0)
            {
                return;
            }

            foreach (var pair in cache.Technologies)
            {
                var technology = pair.Value;
                if (technology == null || technology.explicit_effects == null || technology.explicit_effects.Length == 0)
                {
                    continue;
                }

                var technologyId = NormalizeToken(technology.id);
                if (string.IsNullOrWhiteSpace(technologyId))
                {
                    continue;
                }

                for (var i = 0; i < technology.explicit_effects.Length; i++)
                {
                    var effect = technology.explicit_effects[i];
                    if (effect == null || !string.Equals(NormalizeToken(effect.type), "unlock_building", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var targetBuilding = ResolveConfiguredBuildingType(effect.target_id);
                    if (string.IsNullOrWhiteSpace(targetBuilding))
                    {
                        continue;
                    }

                    if (!_requiredTechsByBuilding.TryGetValue(targetBuilding, out var requiredTechs))
                    {
                        requiredTechs = new List<string>();
                        _requiredTechsByBuilding[targetBuilding] = requiredTechs;
                    }

                    if (!requiredTechs.Contains(technologyId))
                    {
                        requiredTechs.Add(technologyId);
                    }
                }
            }
        }

        private void RebuildActiveTechnologySet()
        {
            _activeTechnologyIds.Clear();

            var activeTechs = GameStateCache.Instance?.GetCurrentResearchState()?.ActiveTechnologyIds;
            if (activeTechs == null)
            {
                return;
            }

            for (var i = 0; i < activeTechs.Count; i++)
            {
                var techId = NormalizeToken(activeTechs[i]);
                if (!string.IsNullOrWhiteSpace(techId))
                {
                    _activeTechnologyIds.Add(techId);
                }
            }
        }

        private BuildItemAvailabilityState ResolveAvailabilityState(string buildingType)
        {
            if (IsBuildingPending(buildingType))
            {
                return BuildItemAvailabilityState.Pending;
            }

            return IsBuildingUnlockedForLocalPlayer(buildingType)
                ? BuildItemAvailabilityState.Available
                : BuildItemAvailabilityState.Locked;
        }

        private bool IsBuildingUnlockedForLocalPlayer(string buildingType)
        {
            var normalizedBuilding = ResolveConfiguredBuildingType(buildingType);
            if (string.IsNullOrWhiteSpace(normalizedBuilding))
            {
                return false;
            }

            if (!_requiredTechsByBuilding.TryGetValue(normalizedBuilding, out var requiredTechs) ||
                requiredTechs == null ||
                requiredTechs.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < requiredTechs.Count; i++)
            {
                if (_activeTechnologyIds.Contains(NormalizeToken(requiredTechs[i])))
                {
                    return true;
                }
            }

            return false;
        }

        private List<string> ResolveRequiredTechnologyNames(string buildingType)
        {
            var result = new List<string>();
            var normalizedBuilding = ResolveConfiguredBuildingType(buildingType);
            if (string.IsNullOrWhiteSpace(normalizedBuilding) ||
                !_requiredTechsByBuilding.TryGetValue(normalizedBuilding, out var requiredTechs) ||
                requiredTechs == null ||
                requiredTechs.Count == 0)
            {
                return result;
            }

            var technologies = (_catalogCache != null ? _catalogCache : StaticCatalogCache.Instance)?.Technologies;
            for (var i = 0; i < requiredTechs.Count; i++)
            {
                var techId = NormalizeToken(requiredTechs[i]);
                if (string.IsNullOrWhiteSpace(techId))
                {
                    continue;
                }

                if (technologies != null && technologies.TryGetValue(techId, out var technology) && technology != null)
                {
                    result.Add(string.IsNullOrWhiteSpace(technology.name) ? techId : technology.name.Trim());
                }
                else
                {
                    result.Add(techId);
                }
            }

            return result;
        }

        private bool IsBuildingPending(string buildingType)
        {
            var normalizedBuilding = NormalizeToken(buildingType);
            if (string.IsNullOrWhiteSpace(normalizedBuilding))
            {
                return false;
            }

            var draftCache = _planningDraftCache ?? PlanningDraftCache.Instance;
            if (draftCache == null)
            {
                return false;
            }

            var preview = draftCache.CurrentBuildPreview;
            if (preview != null &&
                string.Equals(NormalizeToken(preview.BuildingTypeId), normalizedBuilding, StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(_activeCityCoreNodeId) ||
                 string.Equals(preview.CityId, _activeCityCoreNodeId, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var orders = draftCache.BuildOrders;
            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order == null)
                {
                    continue;
                }

                if (!string.Equals(NormalizeToken(order.BuildingTypeId), normalizedBuilding, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(_activeCityCoreNodeId) ||
                    string.Equals(order.CityId, _activeCityCoreNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private BuildRule ResolveBuildRule(BuildRule fallback, BuildConfigEntry entry)
        {
            if (!useConfigPlacementRule || entry == null)
            {
                return fallback;
            }

            var placementRule = NormalizeToken(!string.IsNullOrWhiteSpace(entry.placement_kind)
                ? entry.placement_kind
                : entry.placement_rule);

            return placementRule switch
            {
                "resource_only" => BuildRule.ResourceOnly,
                "resource_node" => BuildRule.ResourceOnly,
                "city_only" => BuildRule.CityOnly,
                "city_territory" => BuildRule.CityOnly,
                "any_terrain" => BuildRule.AnyTerrain,
                "any" => BuildRule.AnyTerrain,
                _ => fallback
            };
        }

        private Sprite LoadIconByKey(string iconKey)
        {
            return LoadIconByKey(iconKey, iconResourcesRoot);
        }

        private Sprite LoadMaterialIconByKey(string iconKey)
        {
            if (string.IsNullOrWhiteSpace(iconKey))
            {
                return fallbackMaterialIcon;
            }

            var icon = LoadIconByKey(iconKey, string.Empty);
            if (icon != null)
            {
                return icon;
            }

            if (materialIconResourcesRoots != null)
            {
                for (var i = 0; i < materialIconResourcesRoots.Length; i++)
                {
                    icon = LoadIconByKey(iconKey, materialIconResourcesRoots[i]);
                    if (icon != null)
                    {
                        return icon;
                    }
                }
            }

            return fallbackMaterialIcon;
        }

        private Sprite LoadIconByKey(string iconKey, string rootPath)
        {
            var key = NormalizeToken(iconKey);
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var normalizedRoot = (rootPath ?? string.Empty).Trim().Trim('/');
            var cacheKey = $"{normalizedRoot}|{key}";
            if (_spriteCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var path = string.IsNullOrWhiteSpace(normalizedRoot) ? key : $"{normalizedRoot}/{key}";
            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null && !string.IsNullOrWhiteSpace(normalizedRoot))
            {
                sprite = Resources.Load<Sprite>(key);
            }

            _spriteCache[cacheKey] = sprite;
            return sprite;
        }

        private System.Collections.IEnumerator LoadEmblemCoroutine(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                SetEmblemSprite(null);
                yield break;
            }

            using var request = UnityWebRequestTexture.GetTexture(imageUrl);
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            var failed = request.result != UnityWebRequest.Result.Success;
#else
            var failed = request.isNetworkError || request.isHttpError;
#endif
            if (failed)
            {
                Debug.LogWarning($"[BuildCommandPanel] Failed to load emblem: {request.error}");
                SetEmblemSprite(null);
                yield break;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null)
            {
                SetEmblemSprite(null);
                yield break;
            }

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            SetEmblemSprite(sprite);
        }

        private void WarnMissingBuildConfigOnce(string message)
        {
            if (!logConfigWarnings || _loggedMissingBuildConfigThisEnable)
            {
                return;
            }

            Debug.LogWarning(message);
            _loggedMissingBuildConfigThisEnable = true;
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string[] GetAliasKeys(string key)
        {
            return NormalizeToken(key) switch
            {
                "lumberyard" => new[] { "lumber" },
                "lumber" => new[] { "lumberyard" },
                "engineer" => new[] { "engineer_camp" },
                "engineer_camp" => new[] { "engineer" },
                "archery" => new[] { "barracks" },
                "barracks" => new[] { "archery" },
                "blacksmith" => new[] { "workshop" },
                "backsmith" => new[] { "workshop" },
                "atktower" => new[] { "tower" },
                "viewtower" => new[] { "watchtower" },
                _ => Array.Empty<string>()
            };
        }
    }
}
