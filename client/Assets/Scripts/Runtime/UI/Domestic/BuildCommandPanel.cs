/*************************************************
 * Project: Panoptes
 * File: BuildCommandPanel.cs
 * Author: Panoptes Team
 * Date: 2026-04-06
 * Description: UI bridge for build placement buttons.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Runtime.Map;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

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
            public BuildingType buildingType;
            [Tooltip("Only used when Building Type = Custom")]
            public string customBuildingType;
            [TextArea] public string tooltipText;
            public BuildRule rule;
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

        private readonly List<Button> _boundButtons = new();
        private readonly List<UnityAction> _boundActions = new();
        private Coroutine _emblemLoadRoutine;

        private void OnEnable()
        {
            ResolveMapInputHandler();
            BindButtons();
            BindModeToggles();
            SetMode(defaultMode, true);
        }

        private void OnDisable()
        {
            UnbindButtons();
            UnbindModeToggles();
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
                    var rule = binding.rule;
                    UnityAction action = () => TriggerBuild(buildingType, rule);
                    binding.button.onClick.AddListener(action);
                    _boundButtons.Add(binding.button);
                    _boundActions.Add(action);

                    InstallTooltip(binding);
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

        private void InstallTooltip(BuildButtonBinding binding)
        {
            if (binding.button == null || tooltipView == null || string.IsNullOrWhiteSpace(binding.tooltipText))
            {
                return;
            }

            var trigger = binding.button.GetComponent<BuildTooltipTrigger>();
            if (trigger == null)
            {
                trigger = binding.button.gameObject.AddComponent<BuildTooltipTrigger>();
            }

            trigger.Configure(tooltipView, binding.tooltipText.Trim());
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
