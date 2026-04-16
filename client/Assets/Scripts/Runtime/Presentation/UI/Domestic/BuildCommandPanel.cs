/*************************************************
 * Project: Panoptes
 * File: BuildCommandPanel.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: UI bridge for build placement buttons.
 *************************************************/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Panoptes.Core.Application.Cache;
using Panoptes.Presentation.Map;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class BuildCommandPanel : MonoBehaviour
    {
        public enum PanelMode
        {
            Governance = 0,
            Build = 1
        }

        public enum BuildingType
        {
            Mine = 0,
            Farm = 1,
            Smelter = 2,
            Workshop = 3,
            Barracks = 4,
            EngineerCamp = 5,
            Wall = 6,
            Tower = 7,
            Watchtower = 8,
            Archery = 9,
            Blacksmith = 10,
            Lumberyard = 11,
            Engineer = 12,
            Custom = 100
        }

        public enum BuildRule
        {
            AnyTerrain = 0,
            ResourceOnly = 1,
            CityOnly = 2
        }

        [Serializable]
        private struct BuildButtonBinding
        {
            public Button button;
            public Image iconImage;
            public TMP_Text labelText;
            [Tooltip("Used when config icon is missing or not configured.")]
            public Sprite fallbackIcon;
            public BuildingType buildingType;
            [Tooltip("Only used when Building Type = Custom")]
            public string customBuildingType;
            [TextArea] public string tooltipText;
            public BuildRule rule;
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
            public readonly string key;
            public readonly int amount;
            public readonly bool isPoint;

            public BuildCostEntry(string key, int amount, bool isPoint)
            {
                this.key = key;
                this.amount = amount;
                this.isPoint = isPoint;
            }
        }

        private readonly struct DynamicBuildRenderEntry
        {
            public readonly string buildingId;
            public readonly BuildConfigEntry config;
            public readonly BuildRule rule;

            public DynamicBuildRenderEntry(string buildingId, BuildConfigEntry config, BuildRule rule)
            {
                this.buildingId = buildingId;
                this.config = config;
                this.rule = rule;
            }
        }

        [Header("Top Area")]
        [SerializeField] private Image emblemImage;
        [SerializeField] private Sprite fallbackEmblem;

        [Header("Mode Toggles")]
        [SerializeField] private ToggleGroup modeToggleGroup;
        [SerializeField] private Toggle governanceToggle;
        [SerializeField] private Toggle buildToggle;
        [SerializeField] private PanelMode defaultMode = PanelMode.Build;

        [Header("Mode Content Roots")]
        [SerializeField] private GameObject governanceContentRoot;
        [SerializeField] private GameObject buildContentRoot;

        [Header("Bindings")]
        [SerializeField] private MapInputHandler mapInputHandler;
        [SerializeField] private bool autoFindMapInputHandler = true;
        [SerializeField] private BuildButtonBinding[] buildButtons;
        [SerializeField] private bool autoGenerateBuildButtons = true;
        [SerializeField] private Transform buildButtonsRoot;
        [SerializeField] private bool cloneTemplateButtons = true;
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
        [SerializeField] private Button cancelButton;
        [SerializeField] private BuildTooltipView tooltipView;

        [Header("Dynamic BuildItem")]
        [SerializeField] private bool useDynamicBuildItemList = true;
        [SerializeField] private RectTransform buildItemListRoot;
        [SerializeField] private BuildItemView buildItemTemplate;
        [SerializeField] private bool hideUnusedBuildItems = true;
        [SerializeField] private bool includeUnknownRuntimeBuildOrderEntries = false;
        [SerializeField] private bool includeCityFoundationBuilding = false;
        [SerializeField] private string[] hiddenBuildingTypes = { "city_core" };
        [SerializeField] private string localCatalogBundleResourcePath = "Data/catalog.bundle";
        [SerializeField] private string[] materialIconResourcesRoots = { "Icons/Resources", "Icons/Points", "Icons" };
        [SerializeField] private Sprite fallbackMaterialIcon;
        [SerializeField] private Sprite buildItemLockedIcon;
        [SerializeField] private string buildItemLockedTooltipSuffix = "Locked: requires technology unlock";
        [SerializeField] private bool enableWheelScrollOnBuildList = true;
        [SerializeField] private float wheelScrollStepPixels = 110f;
        [SerializeField] private float inputSystemWheelScale = 0.01f;

        [Header("Build Catalog Source")]
        [SerializeField] private bool applyConfigToButtons = true;
        [SerializeField] private bool useConfigPlacementRule = true;
        [SerializeField] private bool listenCatalogUpdates = true;
        [SerializeField] private TextAsset buildConfigJson;
        [Tooltip("Used when Build Config Json is empty. Relative to Resources/, without extension.")]
        [SerializeField] private string buildConfigResourcesPath = "Config/buildconfig";
        [Tooltip("Icon load path under Resources/. Final path: <Icon Resources Root>/<icon_key>")]
        [SerializeField] private string iconResourcesRoot = "Icons/Buildings";
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
        private string _activeCityCoreNodeId = string.Empty;
        private Vector2 _buildListBaseAnchoredPos;
        private float _buildListScrollOffset;
        private bool _buildListScrollInitialized;
        private bool _loggedMissingBuildConfigThisEnable;

        private static readonly Regex NumberPairRegex = new("\"([^\"]+)\"\\s*:\\s*(-?\\d+)", RegexOptions.Compiled);

        private void OnEnable()
        {
            _loggedMissingBuildConfigThisEnable = false;
            SubscribeCatalogUpdates();
            ResolveMapInputHandler();
            LoadBuildConfig();
            BindButtons();
            BindModeToggles();
            SetMode(defaultMode, true);
            ResetBuildListScroll(true);
        }

        private void OnDisable()
        {
            UnbindButtons();
            UnbindModeToggles();
            UnsubscribeCatalogUpdates();
            if (tooltipView != null)
            {
                tooltipView.Hide();
            }
            _buildListScrollInitialized = false;
            _loggedMissingBuildConfigThisEnable = false;
        }

        private void Update()
        {
            HandleBuildListWheelScroll();
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
        }

        public void RefreshBuildItems()
        {
            LoadBuildConfig();
            BindButtons();
            ResetBuildListScroll(true);
        }

        public void SetCancelButtonVisible(bool visible)
        {
            if (cancelButton != null)
            {
                cancelButton.gameObject.SetActive(visible);
            }
        }

        public void SelectGovernanceMode()
        {
            SetMode(PanelMode.Governance, true);
        }

        public void SelectBuildMode()
        {
            SetMode(PanelMode.Build, true);
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
            var sprite = Resources.Load<Sprite>(resourcePath);
            SetEmblemSprite(sprite);
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

        private void BindButtons()
        {
            UnbindButtons();

            if (TryBindDynamicBuildItems())
            {
                BindCancelButton();
                return;
            }

            var effectiveButtons = GetEffectiveBuildButtons();
            if (effectiveButtons != null)
            {
                for (int i = 0; i < effectiveButtons.Length; i++)
                {
                    var binding = effectiveButtons[i];
                    if (binding.button == null)
                    {
                        continue;
                    }

                    var requestedType = ResolveBuildingTypeToken(binding);
                    var buildingType = ResolveConfiguredBuildingType(requestedType);
                    var configEntry = GetBuildConfigEntry(buildingType);
                    var rule = ResolveBuildRule(binding.rule, configEntry);
                    UnityAction action = () => TriggerBuild(buildingType, rule);
                    binding.button.onClick.AddListener(action);
                    _boundButtons.Add(binding.button);
                    _boundActions.Add(action);

                    ApplyButtonPresentation(binding, buildingType, configEntry);
                    var tooltip = ResolveTooltipText(binding, configEntry);
                    InstallTooltip(binding.button, tooltip);
                }
            }

            BindCancelButton();
        }

        private void BindCancelButton()
        {
            if (cancelButton != null)
            {
                UnityAction cancelAction = CancelPlacement;
                cancelButton.onClick.AddListener(cancelAction);
                _boundButtons.Add(cancelButton);
                _boundActions.Add(cancelAction);
            }
        }

        private bool TryBindDynamicBuildItems()
        {
            if (!useDynamicBuildItemList)
            {
                return false;
            }

            var listRoot = ResolveBuildItemListRoot();
            if (listRoot == null)
            {
                if (logConfigWarnings)
                {
                    Debug.LogWarning("[BuildCommandPanel] Build list root is missing.");
                }
                return true;
            }

            var renderEntries = BuildDynamicRenderEntries();
            if (renderEntries.Count == 0)
            {
                HideAllBuildItemViews(listRoot);
                WarnMissingBuildConfigOnce("[BuildCommandPanel] Build list config is empty. Waiting for server/static catalog.");
                return true;
            }

            var views = CollectBuildItemViews(listRoot);
            if (views.Count == 0 && buildItemTemplate != null)
            {
                var first = CreateBuildItemClone(listRoot);
                if (first != null)
                {
                    views.Add(first);
                }
            }

            if (views.Count == 0)
            {
                WarnMissingBuildConfigOnce("[BuildCommandPanel] BuildItem template is missing. Cannot render dynamic build list.");
                return true;
            }

            if (buildItemTemplate == null)
            {
                buildItemTemplate = views[0];
            }

            RebuildRequiredTechMap();
            RebuildActiveTechnologySet();

            for (var i = 0; i < renderEntries.Count; i++)
            {
                var view = i < views.Count ? views[i] : CreateBuildItemClone(listRoot);
                if (view == null)
                {
                    continue;
                }

                var renderEntry = renderEntries[i];
                var config = renderEntry.config;
                var icon = config != null ? LoadIconByKey(config.icon_key) : null;
                var displayName = config != null && !string.IsNullOrWhiteSpace(config.name)
                    ? config.name.Trim()
                    : renderEntry.buildingId;
                var description = config != null ? (config.description ?? string.Empty) : string.Empty;
                var requirements = BuildRequirements(renderEntry.buildingId);
                var isUnlocked = IsBuildingUnlockedForLocalPlayer(renderEntry.buildingId);

                view.gameObject.SetActive(true);
                view.ConfigureVisual(displayName, description, icon, requirements);
                view.SetLocked(!isUnlocked, buildItemLockedIcon);

                if (isUnlocked)
                {
                    UnityAction action = () => TriggerBuild(renderEntry.buildingId, renderEntry.rule);
                    view.SetClickAction(action);
                    if (view.ClickButton != null)
                    {
                        _boundButtons.Add(view.ClickButton);
                        _boundActions.Add(action);
                    }
                }
                else
                {
                    view.ClearClickAction();
                }

                if (view.ClickButton != null)
                {
                    var tooltip = isUnlocked
                        ? description
                        : BuildLockedTooltip(description);
                    InstallTooltip(view.ClickButton, tooltip);
                }
            }

            if (hideUnusedBuildItems)
            {
                for (var i = renderEntries.Count; i < views.Count; i++)
                {
                    if (views[i] == null)
                    {
                        continue;
                    }

                    views[i].ClearClickAction();
                    views[i].gameObject.SetActive(false);
                }
            }

            return true;
        }

        private void HideAllBuildItemViews(RectTransform listRoot)
        {
            if (listRoot == null)
            {
                return;
            }

            for (var i = 0; i < listRoot.childCount; i++)
            {
                var child = listRoot.GetChild(i);
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                }
            }
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

        private string BuildLockedTooltip(string description)
        {
            if (string.IsNullOrWhiteSpace(buildItemLockedTooltipSuffix))
            {
                return description;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return buildItemLockedTooltipSuffix;
            }

            return $"{description}\n{buildItemLockedTooltipSuffix}";
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
                    if (effect == null)
                    {
                        continue;
                    }

                    if (!string.Equals(NormalizeToken(effect.type), "unlock_building", StringComparison.Ordinal))
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

            var cache = GameStateCache.Instance;
            if (cache == null)
            {
                return;
            }

            var activeTechs = cache.GetActiveTechnologyIds();
            if (activeTechs == null || activeTechs.Count == 0)
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

        private bool IsBuildingUnlockedForLocalPlayer(string buildingType)
        {
            var normalizedBuilding = ResolveConfiguredBuildingType(buildingType);
            if (string.IsNullOrWhiteSpace(normalizedBuilding))
            {
                return false;
            }

            var runtimeConfig = ClientRuntimeConfigCache.Instance;
            if (runtimeConfig != null && runtimeConfig.DevMode)
            {
                return true;
            }

            if (!_requiredTechsByBuilding.TryGetValue(normalizedBuilding, out var requiredTechs) ||
                requiredTechs == null || requiredTechs.Count == 0)
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

        private RectTransform ResolveBuildItemListRoot()
        {
            if (buildItemListRoot != null)
            {
                return buildItemListRoot;
            }

            var root = ResolveBuildButtonsRoot();
            buildItemListRoot = root as RectTransform;
            return buildItemListRoot;
        }

        private List<DynamicBuildRenderEntry> BuildDynamicRenderEntries()
        {
            var result = new List<DynamicBuildRenderEntry>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (runtimeBuildOrder != null)
            {
                for (var i = 0; i < runtimeBuildOrder.Length; i++)
                {
                    var resolvedId = ResolveConfiguredBuildingType(runtimeBuildOrder[i]);
                    if (string.IsNullOrWhiteSpace(resolvedId) || visited.Contains(resolvedId))
                    {
                        continue;
                    }

                    if (IsHiddenBuildingType(resolvedId))
                    {
                        continue;
                    }

                    var config = GetBuildConfigEntry(resolvedId);
                    if (config == null && !includeUnknownRuntimeBuildOrderEntries)
                    {
                        continue;
                    }

                    var rule = ResolveBuildRule(ResolveDefaultRule(resolvedId), config);
                    result.Add(new DynamicBuildRenderEntry(resolvedId, config, rule));
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
                if (sortCompare != 0)
                {
                    return sortCompare;
                }

                return string.Compare(left.id, right.id, StringComparison.OrdinalIgnoreCase);
            });

            for (var i = 0; i < remaining.Count; i++)
            {
                var config = remaining[i];
                if (config == null)
                {
                    continue;
                }

                var resolvedId = ResolveConfiguredBuildingType(config.id);
                if (string.IsNullOrWhiteSpace(resolvedId) || visited.Contains(resolvedId))
                {
                    continue;
                }

                if (IsHiddenBuildingType(resolvedId))
                {
                    continue;
                }

                var rule = ResolveBuildRule(ResolveDefaultRule(resolvedId), config);
                result.Add(new DynamicBuildRenderEntry(resolvedId, config, rule));
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

        private List<BuildItemView> CollectBuildItemViews(RectTransform listRoot)
        {
            var result = new List<BuildItemView>();
            if (listRoot == null)
            {
                return result;
            }

            for (var i = 0; i < listRoot.childCount; i++)
            {
                var child = listRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                var view = child.GetComponent<BuildItemView>();
                if (view == null)
                {
                    view = child.gameObject.AddComponent<BuildItemView>();
                }
                result.Add(view);
            }

            return result;
        }

        private BuildItemView CreateBuildItemClone(RectTransform listRoot)
        {
            if (buildItemTemplate == null || listRoot == null)
            {
                return null;
            }

            var clone = Instantiate(buildItemTemplate.gameObject, listRoot, false);
            clone.name = $"BuildItem_{listRoot.childCount}";
            var view = clone.GetComponent<BuildItemView>();
            if (view == null)
            {
                view = clone.AddComponent<BuildItemView>();
            }
            return view;
        }

        private BuildButtonBinding[] GetEffectiveBuildButtons()
        {
            if (!autoGenerateBuildButtons)
            {
                return buildButtons;
            }

            var root = ResolveBuildButtonsRoot();
            if (root == null)
            {
                return buildButtons;
            }

            var buttons = CollectButtons(root);
            if (buttons.Count == 0)
            {
                return buildButtons;
            }

            var buildOrder = runtimeBuildOrder;
            if (buildOrder == null || buildOrder.Length == 0)
            {
                return buildButtons;
            }

            if (cloneTemplateButtons && buttons.Count < buildOrder.Length)
            {
                ExpandButtons(root, buttons, buildOrder.Length);
                buttons = CollectButtons(root);
            }

            var count = Mathf.Min(buttons.Count, buildOrder.Length);
            if (count <= 0)
            {
                return buildButtons;
            }

            var generated = new BuildButtonBinding[count];
            for (int i = 0; i < count; i++)
            {
                var button = buttons[i];
                generated[i] = new BuildButtonBinding
                {
                    button = button,
                    iconImage = ResolveIconImage(button),
                    labelText = ResolveLabelText(button),
                    fallbackIcon = null,
                    buildingType = BuildingType.Custom,
                    customBuildingType = NormalizeToken(buildOrder[i]),
                    tooltipText = string.Empty,
                    rule = ResolveDefaultRule(buildOrder[i])
                };
            }

            return generated;
        }

        private Transform ResolveBuildButtonsRoot()
        {
            if (buildButtonsRoot != null)
            {
                return buildButtonsRoot;
            }

            if (buildContentRoot != null)
            {
                var grid = buildContentRoot.transform.Find("BuildGrid");
                if (grid != null)
                {
                    buildButtonsRoot = grid;
                    return buildButtonsRoot;
                }

                buildButtonsRoot = buildContentRoot.transform;
                return buildButtonsRoot;
            }

            return null;
        }

        private static List<Button> CollectButtons(Transform root)
        {
            var result = new List<Button>(16);
            if (root == null)
            {
                return result;
            }

            CollectButtonsRecursive(root, result);

            return result;
        }

        private static void CollectButtonsRecursive(Transform node, List<Button> result)
        {
            if (node == null || result == null)
            {
                return;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                var child = node.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                var button = child.GetComponent<Button>();
                if (button != null)
                {
                    result.Add(button);
                }

                if (child.childCount > 0)
                {
                    CollectButtonsRecursive(child, result);
                }
            }
        }

        private static void ExpandButtons(Transform root, List<Button> buttons, int targetCount)
        {
            if (root == null || buttons == null || buttons.Count == 0 || targetCount <= buttons.Count)
            {
                return;
            }

            var template = buttons[0];
            if (template == null)
            {
                return;
            }

            while (buttons.Count < targetCount)
            {
                var clone = Instantiate(template.gameObject, root, false);
                clone.name = $"BuildBtn_{buttons.Count + 1}";
                var cloneButton = clone.GetComponent<Button>();
                if (cloneButton == null)
                {
                    cloneButton = clone.GetComponentInChildren<Button>(true);
                }

                if (cloneButton != null)
                {
                    buttons.Add(cloneButton);
                }
                else
                {
                    break;
                }
            }
        }

        private static Image ResolveIconImage(Button button)
        {
            if (button == null)
            {
                return null;
            }

            var icon = button.transform.Find("Icon");
            if (icon != null)
            {
                var image = icon.GetComponent<Image>();
                if (image != null)
                {
                    return image;
                }
            }

            var images = button.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject != button.gameObject)
                {
                    return images[i];
                }
            }

            return null;
        }

        private static TMP_Text ResolveLabelText(Button button)
        {
            if (button == null)
            {
                return null;
            }

            var label = button.transform.Find("Label");
            if (label != null)
            {
                var text = label.GetComponent<TMP_Text>();
                if (text != null)
                {
                    return text;
                }
            }

            return button.GetComponentInChildren<TMP_Text>(true);
        }

        private static BuildRule ResolveDefaultRule(string buildingType)
        {
            switch (NormalizeToken(buildingType))
            {
                case "farm":
                case "lumberyard":
                case "smelter":
                case "mine":
                case "lumber":
                    return BuildRule.ResourceOnly;
                default:
                    return BuildRule.CityOnly;
            }
        }

        private void UnbindButtons()
        {
            var count = Mathf.Min(_boundButtons.Count, _boundActions.Count);
            for (int i = 0; i < count; i++)
            {
                var btn = _boundButtons[i];
                var action = _boundActions[i];
                if (btn != null && action != null)
                {
                    btn.onClick.RemoveListener(action);
                }
            }

            _boundButtons.Clear();
            _boundActions.Clear();
        }

        private void BindModeToggles()
        {
            if (modeToggleGroup != null)
            {
                if (governanceToggle != null)
                {
                    governanceToggle.group = modeToggleGroup;
                }
                if (buildToggle != null)
                {
                    buildToggle.group = modeToggleGroup;
                }
            }

            if (governanceToggle != null)
            {
                governanceToggle.onValueChanged.AddListener(OnGovernanceToggleChanged);
            }

            if (buildToggle != null)
            {
                buildToggle.onValueChanged.AddListener(OnBuildToggleChanged);
            }
        }

        private void UnbindModeToggles()
        {
            if (governanceToggle != null)
            {
                governanceToggle.onValueChanged.RemoveListener(OnGovernanceToggleChanged);
            }

            if (buildToggle != null)
            {
                buildToggle.onValueChanged.RemoveListener(OnBuildToggleChanged);
            }
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

        private void OnGovernanceToggleChanged(bool isOn)
        {
            if (isOn)
            {
                SetMode(PanelMode.Governance, false);
            }
        }

        private void OnBuildToggleChanged(bool isOn)
        {
            if (isOn)
            {
                SetMode(PanelMode.Build, false);
            }
        }

        private void SetMode(PanelMode mode, bool syncToggles)
        {
            if (governanceContentRoot != null)
            {
                governanceContentRoot.SetActive(mode == PanelMode.Governance);
            }

            if (buildContentRoot != null)
            {
                buildContentRoot.SetActive(mode == PanelMode.Build);
            }

            if (syncToggles)
            {
                if (governanceToggle != null)
                {
                    governanceToggle.SetIsOnWithoutNotify(mode == PanelMode.Governance);
                }

                if (buildToggle != null)
                {
                    buildToggle.SetIsOnWithoutNotify(mode == PanelMode.Build);
                }
            }

            if (mode == PanelMode.Governance)
            {
                CancelPlacement();
            }
            else if (mode == PanelMode.Build)
            {
                BindButtons();
                ResetBuildListScroll(true);
            }
        }

        private void HandleBuildListWheelScroll()
        {
            if (!enableWheelScrollOnBuildList)
            {
                return;
            }

            if (buildContentRoot == null || !buildContentRoot.activeInHierarchy)
            {
                return;
            }

            var listRoot = ResolveBuildItemListRoot();
            if (listRoot == null)
            {
                return;
            }

            if (!IsPointerOverRect(buildContentRoot.transform as RectTransform))
            {
                return;
            }

            var rawScroll = GetMouseWheelDeltaY();
            if (Mathf.Abs(rawScroll) <= 0.0001f)
            {
                return;
            }

            EnsureBuildListScrollInitialized(listRoot);

            var maxOffset = Mathf.Max(0f, CalculateBuildListOverflow(listRoot));
            if (maxOffset <= 0.01f)
            {
                _buildListScrollOffset = 0f;
                listRoot.anchoredPosition = _buildListBaseAnchoredPos;
                return;
            }

            var step = Mathf.Max(1f, wheelScrollStepPixels);
            var direction = Mathf.Sign(rawScroll);
            _buildListScrollOffset = Mathf.Clamp(_buildListScrollOffset - direction * step, 0f, maxOffset);
            listRoot.anchoredPosition = _buildListBaseAnchoredPos + new Vector2(0f, _buildListScrollOffset);
        }

        private void EnsureBuildListScrollInitialized(RectTransform listRoot)
        {
            if (_buildListScrollInitialized)
            {
                return;
            }

            _buildListBaseAnchoredPos = listRoot.anchoredPosition;
            _buildListScrollOffset = 0f;
            _buildListScrollInitialized = true;
        }

        private void ResetBuildListScroll(bool force)
        {
            var listRoot = ResolveBuildItemListRoot();
            if (listRoot == null)
            {
                return;
            }

            if (!_buildListScrollInitialized || force)
            {
                _buildListBaseAnchoredPos = listRoot.anchoredPosition;
            }

            _buildListScrollOffset = 0f;
            listRoot.anchoredPosition = _buildListBaseAnchoredPos;
            _buildListScrollInitialized = true;
        }

        private float CalculateBuildListOverflow(RectTransform listRoot)
        {
            if (listRoot == null || buildContentRoot == null)
            {
                return 0f;
            }

            var viewportHeight = Mathf.Max(0f, (buildContentRoot.transform as RectTransform)?.rect.height ?? 0f);
            if (viewportHeight <= 0.01f)
            {
                return 0f;
            }

            var layout = listRoot.GetComponent<GridLayoutGroup>();
            if (layout == null)
            {
                return 0f;
            }

            var activeCount = 0;
            for (var i = 0; i < listRoot.childCount; i++)
            {
                var child = listRoot.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                {
                    activeCount++;
                }
            }

            if (activeCount <= 0)
            {
                return 0f;
            }

            var columns = 1;
            if (layout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                columns = Mathf.Max(1, layout.constraintCount);
            }
            else if (layout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                var rowsFixed = Mathf.Max(1, layout.constraintCount);
                columns = Mathf.Max(1, Mathf.CeilToInt(activeCount / (float)rowsFixed));
            }

            var rows = Mathf.Max(1, Mathf.CeilToInt(activeCount / (float)columns));
            var contentHeight =
                layout.padding.top +
                layout.padding.bottom +
                rows * layout.cellSize.y +
                Mathf.Max(0, rows - 1) * layout.spacing.y;

            return Mathf.Max(0f, contentHeight - viewportHeight);
        }

        private bool IsPointerOverRect(RectTransform rect)
        {
            if (rect == null)
            {
                return false;
            }

            var pointer = GetMousePosition();
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, pointer, camera);
        }

        private float GetMouseWheelDeltaY()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return 0f;
            }

            return mouse.scroll.ReadValue().y * inputSystemWheelScale;
#else
            return Input.mouseScrollDelta.y;
#endif
        }

        private Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        private void InstallTooltip(Button button, string text)
        {
            if (button == null || tooltipView == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var trigger = button.GetComponent<BuildTooltipTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<BuildTooltipTrigger>();
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
            LoadBuildConfig();
            BindButtons();
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

            foreach (var pair in cache.Resources)
            {
                if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Value.key))
                {
                    continue;
                }

                _resourceMetaByKey[NormalizeToken(pair.Value.key)] = pair.Value;
            }

            foreach (var pair in cache.Points)
            {
                if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Value.key))
                {
                    continue;
                }

                _pointMetaByKey[NormalizeToken(pair.Value.key)] = pair.Value;
            }

            return _buildConfigById.Count > 0;
        }

        private bool ParseBuildConfigText(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                return false;
            }

            BuildConfigRoot config = null;
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
                if (logConfigWarnings)
                {
                    Debug.LogWarning("[BuildCommandPanel] Build config JSON has no buildings array.");
                }
                return false;
            }

            for (int i = 0; i < config.buildings.Length; i++)
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

            ParseBuildCostsFromCatalogJson(asset.text);
        }

        private void ParseBuildCostsFromCatalogJson(string catalogJson)
        {
            EnumerateArrayObjects(catalogJson, "buildings", ParseSingleBuildingCostObject);
        }

        private void ParseSingleBuildingCostObject(string objectText)
        {
            if (string.IsNullOrWhiteSpace(objectText))
            {
                return;
            }

            if (!TryExtractStringField(objectText, "id", out var id))
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
            for (int i = 0; i < aliases.Length; i++)
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
            for (int i = 0; i < aliases.Length; i++)
            {
                if (_buildConfigById.ContainsKey(aliases[i]))
                {
                    return aliases[i];
                }
            }

            return key;
        }

        private List<BuildItemView.MaterialRequirement> BuildRequirements(string buildingType)
        {
            var result = new List<BuildItemView.MaterialRequirement>();
            var normalizedBuilding = NormalizeToken(buildingType);
            if (string.IsNullOrWhiteSpace(normalizedBuilding))
            {
                return result;
            }

            if (!_buildCostsById.TryGetValue(normalizedBuilding, out var costs) || costs == null || costs.Count == 0)
            {
                return result;
            }

            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                if (string.IsNullOrWhiteSpace(cost.key) || cost.amount <= 0)
                {
                    continue;
                }

                var displayName = cost.key;
                var iconKey = string.Empty;
                if (cost.isPoint)
                {
                    if (_pointMetaByKey.TryGetValue(cost.key, out var pointMeta) && pointMeta != null)
                    {
                        displayName = string.IsNullOrWhiteSpace(pointMeta.display_name)
                            ? displayName
                            : pointMeta.display_name.Trim();
                        iconKey = pointMeta.icon_key;
                    }
                }
                else
                {
                    if (_resourceMetaByKey.TryGetValue(cost.key, out var resourceMeta) && resourceMeta != null)
                    {
                        displayName = string.IsNullOrWhiteSpace(resourceMeta.display_name)
                            ? displayName
                            : resourceMeta.display_name.Trim();
                        iconKey = resourceMeta.icon_key;
                    }
                }

                result.Add(new BuildItemView.MaterialRequirement
                {
                    key = cost.key,
                    displayName = displayName,
                    amount = cost.amount,
                    icon = LoadMaterialIconByKey(iconKey)
                });
            }

            return result;
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

            switch (placementRule)
            {
                case "resource_only":
                case "resource_node":
                    return BuildRule.ResourceOnly;
                case "city_only":
                case "city_territory":
                    return BuildRule.CityOnly;
                case "any_terrain":
                case "any":
                    return BuildRule.AnyTerrain;
                default:
                    return fallback;
            }
        }

        private string ResolveTooltipText(BuildButtonBinding binding, BuildConfigEntry entry)
        {
            if (applyConfigToButtons && entry != null && !string.IsNullOrWhiteSpace(entry.description))
            {
                return entry.description.Trim();
            }

            return binding.tooltipText;
        }

        private void ApplyButtonPresentation(BuildButtonBinding binding, string buildingType, BuildConfigEntry entry)
        {
            if (!applyConfigToButtons)
            {
                return;
            }

            var displayName = (entry != null && !string.IsNullOrWhiteSpace(entry.name))
                ? entry.name.Trim()
                : buildingType;

            if (binding.labelText != null && !string.IsNullOrWhiteSpace(displayName))
            {
                binding.labelText.text = displayName;
            }

            if (binding.iconImage == null)
            {
                return;
            }

            Sprite sprite = null;
            if (entry != null)
            {
                sprite = LoadIconByKey(entry.icon_key);
            }

            if (sprite == null)
            {
                sprite = binding.fallbackIcon;
            }

            if (sprite != null)
            {
                binding.iconImage.sprite = sprite;
                binding.iconImage.preserveAspect = true;
            }
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

            using (var request = UnityWebRequestTexture.GetTexture(imageUrl))
            {
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
        }

        private static string NormalizeToken(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string[] GetAliasKeys(string key)
        {
            switch (NormalizeToken(key))
            {
                case "lumberyard":
                    return new[] { "lumber" };
                case "lumber":
                    return new[] { "lumberyard" };
                case "engineer":
                    return new[] { "engineer_camp" };
                case "engineer_camp":
                    return new[] { "engineer" };
                case "archery":
                    return new[] { "barracks" };
                case "barracks":
                    return new[] { "archery" };
                case "blacksmith":
                case "backsmith":
                    return new[] { "workshop" };
                case "atktower":
                    return new[] { "tower" };
                case "viewtower":
                    return new[] { "watchtower" };
                default:
                    return System.Array.Empty<string>();
            }
        }

        private static string ResolveBuildingTypeToken(BuildButtonBinding binding)
        {
            switch (binding.buildingType)
            {
                case BuildingType.Mine:
                    return "mine";
                case BuildingType.Farm:
                    return "farm";
                case BuildingType.Lumberyard:
                    return "lumberyard";
                case BuildingType.Smelter:
                    return "smelter";
                case BuildingType.Workshop:
                    return "workshop";
                case BuildingType.Barracks:
                    return "barracks";
                case BuildingType.EngineerCamp:
                    return "engineer_camp";
                case BuildingType.Engineer:
                    return "engineer";
                case BuildingType.Wall:
                    return "wall";
                case BuildingType.Tower:
                    return "tower";
                case BuildingType.Watchtower:
                    return "watchtower";
                case BuildingType.Archery:
                    return "archery";
                case BuildingType.Blacksmith:
                    return "blacksmith";
                case BuildingType.Custom:
                    return NormalizeToken(binding.customBuildingType);
                default:
                    return string.Empty;
            }
        }
    }
}
