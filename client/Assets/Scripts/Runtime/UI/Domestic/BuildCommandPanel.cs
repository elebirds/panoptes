/*************************************************
 * Project: Panoptes
 * File: BuildCommandPanel.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: UI bridge for build placement buttons.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Protocol.V1;
using Panoptes.Runtime.Cache;
using Panoptes.Runtime.Map;
using Panoptes.Runtime.Network;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;
using Resources = UnityEngine.Resources;

namespace Panoptes.Runtime.UI.Domestic
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
        [SerializeField] private Button cancelButton;
        [SerializeField] private BuildTooltipView tooltipView;

        [Header("Build Config Source")]
        [SerializeField] private bool applyConfigToButtons = true;
        [SerializeField] private bool useConfigPlacementRule = true;
        [SerializeField] private bool preferServerPushedConfig = true;
        [SerializeField] private string serverConfigKey = "buildconfig";
        [SerializeField] private bool listenServerConfigUpdates = true;
        [SerializeField] private TextAsset buildConfigJson;
        [Tooltip("Used when Build Config Json is empty. Relative to Resources/, without extension.")]
        [SerializeField] private string buildConfigResourcesPath = "Config/buildconfig";
        [Tooltip("Icon load path under Resources/. Final path: <Icon Resources Root>/<icon_key>")]
        [SerializeField] private string iconResourcesRoot = "Icons/Buildings";
        [SerializeField] private bool logConfigWarnings = true;

        [Header("Minister Stream")]
        [SerializeField] private bool listenMinisterReportChunk = true;
        [SerializeField] private string ministerReportChunkMessageType = "MsgMinisterReportChunk";
        [SerializeField] private TMP_Text govIcon1SpeechText;
        [SerializeField] private string govIcon1Path = "GovernanceIconGrid/GovIcon_1";
        [SerializeField] private bool autoCreateSpeechTextIfMissing = true;
        [SerializeField] private Vector2 speechTextOffset = new Vector2(16f, 0f);
        [SerializeField] private Vector2 speechTextSize = new Vector2(340f, 90f);
        [SerializeField] private bool sanitizeCjkPunctuation = true;

        private readonly List<Button> _boundButtons = new();
        private readonly List<UnityAction> _boundActions = new();
        private readonly Dictionary<string, BuildConfigEntry> _buildConfigById = new();
        private static readonly char[] SentenceTerminators =
            { '.', '!', '?', '\u3002', '\uFF01', '\uFF1F', '\n' };
        private Coroutine _emblemLoadRoutine;
        private ConfigCache _configCache;
        private bool _ministerHandlerRegistered;
        private string _ministerStreamBuffer = string.Empty;
        private bool _ministerFirstSentenceShown;

        private void OnEnable()
        {
            SubscribeServerConfig();
            TryRegisterMinisterHandler();
            ResolveMapInputHandler();
            LoadBuildConfig();
            BindButtons();
            BindModeToggles();
            SetMode(defaultMode, true);
        }

        private void OnDisable()
        {
            UnbindButtons();
            UnbindModeToggles();
            UnsubscribeServerConfig();
            UnregisterMinisterHandler();
            if (tooltipView != null)
            {
                tooltipView.Hide();
            }
        }

        private void Update()
        {
            if (listenMinisterReportChunk && !_ministerHandlerRegistered && MessageDispatcher.Instance != null)
            {
                TryRegisterMinisterHandler();
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

            if (buildButtons != null)
            {
                for (int i = 0; i < buildButtons.Length; i++)
                {
                    var binding = buildButtons[i];
                    if (binding.button == null)
                    {
                        continue;
                    }

                    var buildingType = ResolveBuildingTypeToken(binding);
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

            if (cancelButton != null)
            {
                UnityAction cancelAction = CancelPlacement;
                cancelButton.onClick.AddListener(cancelAction);
                _boundButtons.Add(cancelButton);
                _boundActions.Add(cancelAction);
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
                    mapInputHandler.EnterBuildPlacementResource(normalized);
                    break;
                case BuildRule.CityOnly:
                    mapInputHandler.EnterBuildPlacementCity(normalized);
                    break;
                default:
                    mapInputHandler.EnterBuildPlacementAny(normalized);
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
                mapInputHandler = UnityEngine.Object.FindObjectOfType<MapInputHandler>();
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

        private void SubscribeServerConfig()
        {
            if (!listenServerConfigUpdates)
            {
                return;
            }

            _configCache = ConfigCache.EnsureInstance();
            if (_configCache != null)
            {
                _configCache.ConfigUpdated += OnServerConfigUpdated;
            }
        }

        private void UnsubscribeServerConfig()
        {
            if (_configCache != null)
            {
                _configCache.ConfigUpdated -= OnServerConfigUpdated;
                _configCache = null;
            }
        }

        private void OnServerConfigUpdated(string key)
        {
            if (!preferServerPushedConfig)
            {
                return;
            }

            if (!string.Equals(NormalizeToken(key), NormalizeToken(serverConfigKey), StringComparison.Ordinal))
            {
                return;
            }

            LoadBuildConfig();
            BindButtons();
        }

        private void LoadBuildConfig()
        {
            _buildConfigById.Clear();
            if (!applyConfigToButtons)
            {
                return;
            }

            if (preferServerPushedConfig && TryLoadBuildConfigFromCache())
            {
                return;
            }

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

        private bool TryLoadBuildConfigFromCache()
        {
            var cache = _configCache != null ? _configCache : ConfigCache.Instance;
            var key = NormalizeToken(serverConfigKey);
            if (cache == null || string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (!cache.TryGetJson(key, out var json) || string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            return ParseBuildConfigText(json);
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

        private BuildConfigEntry GetBuildConfigEntry(string buildingType)
        {
            var key = NormalizeToken(buildingType);
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            _buildConfigById.TryGetValue(key, out var entry);
            return entry;
        }

        private BuildRule ResolveBuildRule(BuildRule fallback, BuildConfigEntry entry)
        {
            if (!useConfigPlacementRule || entry == null)
            {
                return fallback;
            }

            switch (NormalizeToken(entry.placement_rule))
            {
                case "resource_only":
                    return BuildRule.ResourceOnly;
                case "city_only":
                    return BuildRule.CityOnly;
                case "any_terrain":
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
            var key = (iconKey ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            var root = (iconResourcesRoot ?? string.Empty).Trim();
            var path = string.IsNullOrEmpty(root) ? key : $"{root.TrimEnd('/')}/{key}";
            return Resources.Load<Sprite>(path);
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

        private void TryRegisterMinisterHandler()
        {
            if (!listenMinisterReportChunk || _ministerHandlerRegistered || MessageDispatcher.Instance == null)
            {
                return;
            }

            MessageDispatcher.Instance.Register<MsgMinisterReportChunk>(ministerReportChunkMessageType, OnMinisterReportChunk);
            _ministerHandlerRegistered = true;
        }

        private void UnregisterMinisterHandler()
        {
            if (!_ministerHandlerRegistered)
            {
                return;
            }

            if (MessageDispatcher.Instance != null)
            {
                MessageDispatcher.Instance.Unregister(ministerReportChunkMessageType);
            }

            _ministerHandlerRegistered = false;
            _ministerStreamBuffer = string.Empty;
            _ministerFirstSentenceShown = false;
        }

        private void OnMinisterReportChunk(MsgMinisterReportChunk msg)
        {
            if (msg == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(msg.Chunk))
            {
                _ministerStreamBuffer += msg.Chunk;
            }

            if (!_ministerFirstSentenceShown)
            {
                if (TryExtractFirstSentence(_ministerStreamBuffer, out var firstSentence))
                {
                    ShowMinisterSentence(firstSentence);
                    _ministerFirstSentenceShown = true;
                }
                else if (msg.IsFinal)
                {
                    var fallback = (_ministerStreamBuffer ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(fallback))
                    {
                        ShowMinisterSentence(fallback);
                        _ministerFirstSentenceShown = true;
                    }
                }
            }

            if (msg.IsFinal)
            {
                _ministerStreamBuffer = string.Empty;
                _ministerFirstSentenceShown = false;
            }
        }

        private static bool TryExtractFirstSentence(string text, out string sentence)
        {
            sentence = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var idx = text.IndexOfAny(SentenceTerminators);
            if (idx < 0)
            {
                return false;
            }

            sentence = text.Substring(0, idx + 1).Trim();
            return !string.IsNullOrEmpty(sentence);
        }

        private void ShowMinisterSentence(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
            {
                return;
            }

            EnsurePanelVisible();
            SelectGovernanceMode();

            var targetText = ResolveGovIcon1SpeechText();
            if (targetText == null)
            {
                if (logConfigWarnings)
                {
                    Debug.LogWarning("[BuildCommandPanel] Missing GovIcon_1 speech text target.");
                }
                return;
            }

            targetText.text = sanitizeCjkPunctuation ? SanitizePunctuation(sentence) : sentence;
            targetText.gameObject.SetActive(true);
        }

        private void EnsurePanelVisible()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            BuildPanelSlideToggle slideToggle = null;

            if (slideToggle == null)
            {
                slideToggle = GetComponentInChildren<BuildPanelSlideToggle>(true);
            }

            if (slideToggle == null && transform.parent != null)
            {
                slideToggle = transform.parent.GetComponentInChildren<BuildPanelSlideToggle>(true);
            }

            if (slideToggle == null && transform.root != null)
            {
                slideToggle = transform.root.GetComponentInChildren<BuildPanelSlideToggle>(true);
            }

            slideToggle?.Expand();
        }

        private TMP_Text ResolveGovIcon1SpeechText()
        {
            if (govIcon1SpeechText != null)
            {
                return govIcon1SpeechText;
            }

            var govIcon = ResolveGovIcon1Transform();
            if (govIcon == null)
            {
                return null;
            }

            var existing = govIcon.Find("MinisterSpeechText");
            if (existing != null)
            {
                govIcon1SpeechText = existing.GetComponent<TMP_Text>();
                if (govIcon1SpeechText != null)
                {
                    return govIcon1SpeechText;
                }
            }

            if (!autoCreateSpeechTextIfMissing)
            {
                return null;
            }

            var textGo = new GameObject("MinisterSpeechText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(govIcon, false);

            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = speechTextOffset;
            rt.sizeDelta = speechTextSize;

            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 24f;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.text = string.Empty;

            govIcon1SpeechText = tmp;
            return govIcon1SpeechText;
        }

        private Transform ResolveGovIcon1Transform()
        {
            if (governanceContentRoot == null)
            {
                return null;
            }

            var root = governanceContentRoot.transform;
            if (!string.IsNullOrWhiteSpace(govIcon1Path))
            {
                var byPath = root.Find(govIcon1Path);
                if (byPath != null)
                {
                    return byPath;
                }
            }

            var direct = root.Find("GovIcon_1");
            if (direct != null)
            {
                return direct;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && string.Equals(all[i].name, "GovIcon_1", StringComparison.Ordinal))
                {
                    return all[i];
                }
            }

            return null;
        }

        private static string SanitizePunctuation(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
                .Replace('\u3002', '.')
                .Replace('\uFF01', '!')
                .Replace('\uFF1F', '?');
        }

        private static string ResolveBuildingTypeToken(BuildButtonBinding binding)
        {
            switch (binding.buildingType)
            {
                case BuildingType.Mine:
                    return "mine";
                case BuildingType.Farm:
                    return "farm";
                case BuildingType.Smelter:
                    return "smelter";
                case BuildingType.Workshop:
                    return "workshop";
                case BuildingType.Barracks:
                    return "barracks";
                case BuildingType.EngineerCamp:
                    return "engineer_camp";
                case BuildingType.Wall:
                    return "wall";
                case BuildingType.Tower:
                    return "tower";
                case BuildingType.Watchtower:
                    return "watchtower";
                case BuildingType.Custom:
                    return NormalizeToken(binding.customBuildingType);
                default:
                    return string.Empty;
            }
        }
    }
}
