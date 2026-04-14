/*************************************************
 * Project: Panoptes
 * File: CastleProductionPanel.cs
 * Author: Panoptes Team
 * Date: 2026-04-12
 * Description: Castle production/refine panel controller.
 *************************************************/

using System;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Presentation.Map;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Panoptes.Presentation.UI.Domestic
{
    public sealed class CastleProductionPanel : MonoBehaviour
    {
        [Serializable]
        private sealed class SavedSelection
        {
            public string player_id;
            public string castle_node_id;
            public int turn;
            public int wood;
            public int stone;
            public int troops;
            public string active_tab;
            public string updated_utc;
        }

        [Serializable]
        private sealed class SubmissionPayload
        {
            public string player_id;
            public string castle_node_id;
            public int turn;
            public int wood;
            public int stone;
            public int troops;
            public int workshop_capacity;
            public int archery_capacity;
            public int wood_essence_per_unit;
            public int stone_essence_per_unit;
            public string local_timestamp_utc;
        }

        [Serializable]
        private sealed class ProductionConfigRoot
        {
            public ProductionConfigEntry[] buildings;
            public ProductionConfigEntry[] entries;
            public ProductionConfigEntry[] units;
        }

        [Serializable]
        private sealed class ProductionConfigEntry
        {
            public string id;
            public string name;
            public string description;
            public int refine_capacity;
            public int recruit_capacity;
            public int wood_essence_per_unit;
            public int stone_essence_per_unit;
            public int capacity;
        }

        private enum Tab
        {
            Materials = 0,
            Army = 1
        }

        [Header("Root")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private bool startHidden = true;
        [SerializeField] private bool autoBuildFallbackUi = true;
        [SerializeField] private bool autoBindByName = true;

        [Header("Tabs")]
        [SerializeField] private Button materialsTabButton;
        [SerializeField] private Button armyTabButton;
        [SerializeField] private GameObject materialsTabRoot;
        [SerializeField] private GameObject armyTabRoot;

        [Header("Sliders")]
        [SerializeField] private Slider woodSlider;
        [SerializeField] private Slider stoneSlider;
        [SerializeField] private Slider troopSlider;

        [Header("Labels")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text workshopCapacityText;
        [SerializeField] private TMP_Text archeryCapacityText;
        [SerializeField] private TMP_Text woodValueText;
        [SerializeField] private TMP_Text stoneValueText;
        [SerializeField] private TMP_Text troopValueText;
        [SerializeField] private TMP_Text workshopHintText;
        [SerializeField] private TMP_Text archeryHintText;

        [Header("Actions")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button closeButton;

        [Header("Castle Gating")]
        [SerializeField] private string castleBuildingType = "castle";
        [SerializeField] private bool requireLocalOwnership = true;
        [SerializeField] private bool requireCastleBuildingType = true;

        [Header("Capacity Fallbacks")]
        [SerializeField] private int fallbackWorkshopCapacity = 20;
        [SerializeField] private int fallbackArcheryCapacity = 10;
        [SerializeField] private int woodEssencePerUnit = 1;
        [SerializeField] private int stoneEssencePerUnit = 1;
        [SerializeField] private bool tryReadCapacityFromConfig = true;
        [SerializeField] private string buildConfigKey = "buildconfig";
        [SerializeField] private string armyConfigKey = "armyconfig";

        [Header("Persistence")]
        [SerializeField] private string saveKeyPrefix = "castle_production_plan";

        private string _castleNodeId = string.Empty;
        private Tab _activeTab = Tab.Materials;
        private int _workshopCapacity;
        private int _archeryCapacity;
        private SavedSelection _pendingSelection = new SavedSelection();

        private void Awake()
        {
            EnsureUiReady();
            ConfigureSliders();
            WireButtons();

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (startHidden)
            {
                if (panelRoot != gameObject)
                {
                    SetVisible(false);
                }
                else
                {
                    Debug.LogWarning("[CastleProductionPanel] panelRoot points to the controller object. Assign a child visual root to support hide/show.");
                }
            }
        }

        private void EnsureUiReady()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (autoBindByName)
            {
                TryBindByName();
            }

            if (autoBuildFallbackUi && !HasEssentialReferences())
            {
                BuildFallbackUi();
                TryBindByName();
            }
        }

        private bool HasEssentialReferences()
        {
            return panelRoot != null
                   && materialsTabButton != null
                   && armyTabButton != null
                   && materialsTabRoot != null
                   && armyTabRoot != null
                   && woodSlider != null
                   && stoneSlider != null
                   && troopSlider != null
                   && saveButton != null
                   && closeButton != null;
        }

        private void TryBindByName()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (titleText == null) titleText = FindText("TitleText");
            if (statusText == null) statusText = FindText("StatusText");
            if (workshopCapacityText == null) workshopCapacityText = FindText("WorkshopCapacityText");
            if (archeryCapacityText == null) archeryCapacityText = FindText("ArcheryCapacityText");
            if (woodValueText == null) woodValueText = FindText("WoodValueText");
            if (stoneValueText == null) stoneValueText = FindText("StoneValueText");
            if (troopValueText == null) troopValueText = FindText("TroopValueText");
            if (workshopHintText == null) workshopHintText = FindText("WorkshopHintText");
            if (archeryHintText == null) archeryHintText = FindText("ArcheryHintText");

            if (materialsTabButton == null) materialsTabButton = FindButton("BtnMaterials");
            if (armyTabButton == null) armyTabButton = FindButton("BtnArmy");
            if (saveButton == null) saveButton = FindButton("BtnSave");
            if (closeButton == null) closeButton = FindButton("BtnClose");

            if (materialsTabRoot == null) materialsTabRoot = FindObject("MaterialsTabRoot");
            if (armyTabRoot == null) armyTabRoot = FindObject("ArmyTabRoot");

            if (woodSlider == null) woodSlider = FindSlider("WoodSlider");
            if (stoneSlider == null) stoneSlider = FindSlider("StoneSlider");
            if (troopSlider == null) troopSlider = FindSlider("TroopSlider");
        }

        private Button FindButton(string name)
        {
            var t = FindChild(name);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private Slider FindSlider(string name)
        {
            var t = FindChild(name);
            return t != null ? t.GetComponent<Slider>() : null;
        }

        private TMP_Text FindText(string name)
        {
            var t = FindChild(name);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        private GameObject FindObject(string name)
        {
            var t = FindChild(name);
            return t != null ? t.gameObject : null;
        }

        private Transform FindChild(string name)
        {
            var root = panelRoot != null ? panelRoot.transform : transform;
            return FindChildRecursive(root, name);
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            if (string.Equals(root.name, targetName, StringComparison.Ordinal))
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var found = FindChildRecursive(child, targetName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void BuildFallbackUi()
        {
            var rootRect = GetOrAddRectTransform(gameObject);
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var visualRoot = panelRoot != gameObject && panelRoot != null
                ? panelRoot
                : CreateUiObject("PanelRoot", transform, new Vector2(0.82f, 0.5f), new Vector2(1f, 1f), new Vector2(-420f, 0f), new Vector2(-12f, -12f));
            panelRoot = visualRoot;
            var panelImage = visualRoot.GetComponent<Image>() ?? visualRoot.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.1f, 0.16f, 0.92f);

            var top = CreateUiObject("Top", visualRoot.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -90f), new Vector2(-10f, -10f));
            titleText = CreateText("TitleText", top.transform, "Castle Production", 30, FontStyles.Bold, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            statusText = CreateText("StatusText", top.transform, "Waiting", 22, FontStyles.Normal, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 0f));

            var tabs = CreateUiObject("Tabs", visualRoot.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -150f), new Vector2(-10f, -96f));
            materialsTabButton = CreateButton("BtnMaterials", tabs.transform, "Refine", new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(-4f, 0f));
            armyTabButton = CreateButton("BtnArmy", tabs.transform, "Train", new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f), new Vector2(0f, 0f));

            materialsTabRoot = CreateUiObject("MaterialsTabRoot", visualRoot.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 64f), new Vector2(-10f, -156f));
            workshopCapacityText = CreateText("WorkshopCapacityText", materialsTabRoot.transform, "Refine Capacity: 0", 22, FontStyles.Bold, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -32f), new Vector2(0f, 0f));
            workshopHintText = CreateText("WorkshopHintText", materialsTabRoot.transform, "", 18, FontStyles.Normal, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -72f), new Vector2(0f, -38f));
            woodSlider = CreateSliderRow("Wood", "WoodSlider", "WoodValueText", materialsTabRoot.transform, -128f, out woodValueText);
            stoneSlider = CreateSliderRow("Stone", "StoneSlider", "StoneValueText", materialsTabRoot.transform, -196f, out stoneValueText);

            armyTabRoot = CreateUiObject("ArmyTabRoot", visualRoot.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 64f), new Vector2(-10f, -156f));
            archeryCapacityText = CreateText("ArcheryCapacityText", armyTabRoot.transform, "Recruit Capacity: 0", 22, FontStyles.Bold, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -32f), new Vector2(0f, 0f));
            archeryHintText = CreateText("ArcheryHintText", armyTabRoot.transform, "", 18, FontStyles.Normal, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -72f), new Vector2(0f, -38f));
            troopSlider = CreateSliderRow("Troops", "TroopSlider", "TroopValueText", armyTabRoot.transform, -128f, out troopValueText);

            var actions = CreateUiObject("Actions", visualRoot.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 10f), new Vector2(-10f, 56f));
            saveButton = CreateButton("BtnSave", actions.transform, "Save", new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(-4f, 0f));
            closeButton = CreateButton("BtnClose", actions.transform, "Close", new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f), new Vector2(0f, 0f));
        }

        private static RectTransform GetOrAddRectTransform(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            return rect != null ? rect : go.AddComponent<RectTransform>();
        }

        private static GameObject CreateUiObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return go;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float fontSize, FontStyles style, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = CreateUiObject(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.enableWordWrapping = true;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string caption, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = CreateUiObject(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.21f, 0.31f, 0.45f, 0.95f);
            var button = go.AddComponent<Button>();

            var label = CreateText("Label", go.transform, caption, 20f, FontStyles.Bold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private Slider CreateSliderRow(string label, string sliderName, string valueName, Transform parent, float yTop, out TMP_Text valueText)
        {
            var row = CreateUiObject($"{sliderName}_Row", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, yTop - 40f), new Vector2(0f, yTop));
            CreateText($"{sliderName}_Label", row.transform, label, 20f, FontStyles.Normal, new Vector2(0f, 0f), new Vector2(0.25f, 1f), Vector2.zero, Vector2.zero);
            valueText = CreateText(valueName, row.transform, "0 / 0", 20f, FontStyles.Normal, new Vector2(0.75f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            valueText.alignment = TextAlignmentOptions.Center;

            var sliderGo = CreateUiObject(sliderName, row.transform, new Vector2(0.25f, 0f), new Vector2(0.75f, 1f), new Vector2(6f, 8f), new Vector2(-6f, -8f));
            var slider = sliderGo.AddComponent<Slider>();

            var background = CreateUiObject("Background", sliderGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.2f, 0.27f, 1f);

            var fillArea = CreateUiObject("FillArea", sliderGo.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -8f));
            var fill = CreateUiObject("Fill", fillArea.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.31f, 0.84f, 0.45f, 1f);

            var handleArea = CreateUiObject("HandleSlideArea", sliderGo.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -8f));
            var handle = CreateUiObject("Handle", handleArea.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-10f, 0f), new Vector2(10f, 0f));
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.95f, 0.95f, 0.95f, 1f);

            slider.targetGraphic = handleImage;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(0f);
            return slider;
        }

        private void OnEnable()
        {
            GameIntents.TurnSubmitRequested += HandleTurnSubmitRequested;
            RefreshFromCache();
        }

        private void OnDisable()
        {
            GameIntents.TurnSubmitRequested -= HandleTurnSubmitRequested;
            SaveSelection();
        }

        public bool OpenForCastle(string nodeId)
        {
            if (!TryResolveCastleNode(nodeId, out var node))
            {
                SetStatus("Cannot open: this node is not your castle.");
                return false;
            }

            _castleNodeId = node.Id ?? string.Empty;
            RefreshFromCache();
            LoadSelection();
            SetVisible(true);

            if (titleText != null)
            {
                titleText.text = $"Castle Production - {_castleNodeId}";
            }

            SetStatus($"Opened castle panel: {_castleNodeId}");
            return true;
        }

        public void Close()
        {
            SaveSelection();
            SetVisible(false);
        }

        public void SelectMaterialsTab()
        {
            SetTab(Tab.Materials);
        }

        public void SelectArmyTab()
        {
            SetTab(Tab.Army);
        }

        public void SaveSelection()
        {
            if (string.IsNullOrEmpty(_castleNodeId))
            {
                return;
            }

            CaptureSelection();
            _pendingSelection.updated_utc = DateTime.UtcNow.ToString("o");
            PlayerPrefs.SetString(GetSaveKey(_castleNodeId), JsonUtility.ToJson(_pendingSelection));
            PlayerPrefs.Save();

            SetStatus($"Saved: wood {_pendingSelection.wood}, stone {_pendingSelection.stone}, troops {_pendingSelection.troops}");
        }

        public void RefreshFromCache()
        {
            ResolveCapacities();
            UpdateCapacityLabels();
            UpdateHintLabels();
            SyncSliderRanges();
            RefreshSliderTexts();

            if (!string.IsNullOrEmpty(_castleNodeId) && titleText != null)
            {
                titleText.text = $"Castle Production - {_castleNodeId}";
            }

            SetTab(_activeTab);
        }

        private void ConfigureSliders()
        {
            ConfigureSlider(woodSlider, 0f, 1f, OnWoodChanged);
            ConfigureSlider(stoneSlider, 0f, 1f, OnStoneChanged);
            ConfigureSlider(troopSlider, 0f, 1f, OnTroopChanged);
        }

        private static void ConfigureSlider(Slider slider, float minValue, float maxValue, UnityAction<float> listener)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.wholeNumbers = true;
            slider.onValueChanged.RemoveListener(listener);
            slider.onValueChanged.AddListener(listener);
        }

        private void WireButtons()
        {
            WireButton(materialsTabButton, SelectMaterialsTab);
            WireButton(armyTabButton, SelectArmyTab);
            WireButton(saveButton, SaveSelection);
            WireButton(closeButton, Close);
        }

        private static void WireButton(Button button, UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void SetVisible(bool visible)
        {
            if (panelRoot == null)
            {
                return;
            }

            if (panelRoot == gameObject && !visible)
            {
                Debug.LogWarning("[CastleProductionPanel] Skip hiding because panelRoot points to controller object.");
                return;
            }

            panelRoot.SetActive(visible);
        }

        private void SetTab(Tab tab)
        {
            _activeTab = tab;

            if (materialsTabRoot != null)
            {
                materialsTabRoot.SetActive(tab == Tab.Materials);
            }

            if (armyTabRoot != null)
            {
                armyTabRoot.SetActive(tab == Tab.Army);
            }

            SetTabButtonState(materialsTabButton, tab == Tab.Materials);
            SetTabButtonState(armyTabButton, tab == Tab.Army);
        }

        private static void SetTabButtonState(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = !isActive;
        }

        private void OnWoodChanged(float _)
        {
            ClampMaterialValues();
            CaptureSelection();
        }

        private void OnStoneChanged(float _)
        {
            ClampMaterialValues();
            CaptureSelection();
        }

        private void OnTroopChanged(float _)
        {
            ClampTroopValue();
            CaptureSelection();
        }

        private void ClampMaterialValues()
        {
            if (woodSlider == null || stoneSlider == null)
            {
                return;
            }

            var total = Mathf.Max(0, _workshopCapacity);
            var wood = Mathf.Clamp(Mathf.RoundToInt(woodSlider.value), 0, total);
            var stone = Mathf.Clamp(Mathf.RoundToInt(stoneSlider.value), 0, total - wood);

            woodSlider.SetValueWithoutNotify(wood);
            stoneSlider.SetValueWithoutNotify(stone);
            RefreshSliderTexts();
        }

        private void ClampTroopValue()
        {
            if (troopSlider == null)
            {
                return;
            }

            var troop = Mathf.Clamp(Mathf.RoundToInt(troopSlider.value), 0, Mathf.Max(0, _archeryCapacity));
            troopSlider.SetValueWithoutNotify(troop);
            RefreshSliderTexts();
        }

        private void SyncSliderRanges()
        {
            if (woodSlider != null)
            {
                woodSlider.minValue = 0f;
                woodSlider.maxValue = Mathf.Max(0, _workshopCapacity);
                woodSlider.wholeNumbers = true;
            }

            if (stoneSlider != null)
            {
                stoneSlider.minValue = 0f;
                stoneSlider.maxValue = Mathf.Max(0, _workshopCapacity);
                stoneSlider.wholeNumbers = true;
            }

            if (troopSlider != null)
            {
                troopSlider.minValue = 0f;
                troopSlider.maxValue = Mathf.Max(0, _archeryCapacity);
                troopSlider.wholeNumbers = true;
            }

            ClampMaterialValues();
            ClampTroopValue();
        }

        private void RefreshSliderTexts()
        {
            if (woodValueText != null && woodSlider != null)
            {
                woodValueText.text = $"{Mathf.RoundToInt(woodSlider.value)} / {_workshopCapacity}";
            }

            if (stoneValueText != null && stoneSlider != null)
            {
                stoneValueText.text = $"{Mathf.RoundToInt(stoneSlider.value)} / {_workshopCapacity}";
            }

            if (troopValueText != null && troopSlider != null)
            {
                troopValueText.text = $"{Mathf.RoundToInt(troopSlider.value)} / {_archeryCapacity}";
            }
        }

        private void UpdateCapacityLabels()
        {
            if (workshopCapacityText != null)
            {
                workshopCapacityText.text = $"Refine Capacity: {_workshopCapacity}";
            }

            if (archeryCapacityText != null)
            {
                archeryCapacityText.text = $"Recruit Capacity: {_archeryCapacity}";
            }
        }

        private void UpdateHintLabels()
        {
            if (workshopHintText != null)
            {
                workshopHintText.text = $"Wood and stone share {_workshopCapacity}. 1 wood consumes {woodEssencePerUnit}, 1 stone consumes {stoneEssencePerUnit}.";
            }

            if (archeryHintText != null)
            {
                archeryHintText.text = $"Up to {_archeryCapacity} troop units can be queued per turn.";
            }
        }

        private void ResolveCapacities()
        {
            _workshopCapacity = ResolveBuildingCapacity("workshop", fallbackWorkshopCapacity, true);
            _archeryCapacity = ResolveBuildingCapacity("archery", fallbackArcheryCapacity, false);
        }

        private int ResolveBuildingCapacity(string buildingId, int fallbackValue, bool isWorkshop)
        {
            var resolved = fallbackValue;
            if (!tryReadCapacityFromConfig)
            {
                return Mathf.Max(0, resolved);
            }

            var buildEntries = TryGetConfigEntries(buildConfigKey);
            if (buildEntries != null)
            {
                resolved = ResolveFromEntries(buildEntries, buildingId, resolved, isWorkshop);
            }

            var armyEntries = TryGetConfigEntries(armyConfigKey);
            if (armyEntries != null)
            {
                resolved = ResolveFromEntries(armyEntries, buildingId, resolved, isWorkshop);
            }

            return Mathf.Max(0, resolved);
        }

        private int ResolveFromEntries(ProductionConfigEntry[] entries, string buildingId, int fallback, bool isWorkshop)
        {
            if (entries == null)
            {
                return fallback;
            }

            var targetId = Normalize(buildingId);
            var result = fallback;

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || !string.Equals(Normalize(entry.id), targetId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (isWorkshop)
                {
                    if (entry.refine_capacity > 0)
                    {
                        result = entry.refine_capacity;
                    }
                    else if (entry.capacity > 0)
                    {
                        result = entry.capacity;
                    }

                    if (entry.wood_essence_per_unit > 0)
                    {
                        woodEssencePerUnit = entry.wood_essence_per_unit;
                    }

                    if (entry.stone_essence_per_unit > 0)
                    {
                        stoneEssencePerUnit = entry.stone_essence_per_unit;
                    }
                }
                else
                {
                    if (entry.recruit_capacity > 0)
                    {
                        result = entry.recruit_capacity;
                    }
                    else if (entry.capacity > 0)
                    {
                        result = entry.capacity;
                    }
                }
            }

            return result;
        }

        private static ProductionConfigEntry[] TryGetConfigEntries(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || ConfigCache.Instance == null)
            {
                return null;
            }

            if (!ConfigCache.Instance.TryGetJson(key, out var json) || string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var root = JsonUtility.FromJson<ProductionConfigRoot>(json);
                if (root == null)
                {
                    return null;
                }

                if (root.buildings != null && root.buildings.Length > 0)
                {
                    return root.buildings;
                }

                if (root.entries != null && root.entries.Length > 0)
                {
                    return root.entries;
                }

                if (root.units != null && root.units.Length > 0)
                {
                    return root.units;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CastleProductionPanel] Failed to parse config '{key}': {ex.Message}");
            }

            return null;
        }

        private void HandleTurnSubmitRequested()
        {
            if (string.IsNullOrEmpty(_castleNodeId))
            {
                return;
            }

            CaptureSelection();
            SaveSelection();

            var payload = BuildPayload();
            Debug.Log($"[CastleProductionPanel] No protocol defined for castle production yet. payload={JsonUtility.ToJson(payload)}");
        }

        private SubmissionPayload BuildPayload()
        {
            return new SubmissionPayload
            {
                player_id = ResolveLocalPlayerId(),
                castle_node_id = _castleNodeId,
                turn = GameStateCache.Instance != null ? GameStateCache.Instance.Turn : 0,
                wood = _pendingSelection.wood,
                stone = _pendingSelection.stone,
                troops = _pendingSelection.troops,
                workshop_capacity = _workshopCapacity,
                archery_capacity = _archeryCapacity,
                wood_essence_per_unit = woodEssencePerUnit,
                stone_essence_per_unit = stoneEssencePerUnit,
                local_timestamp_utc = DateTime.UtcNow.ToString("o")
            };
        }

        private void CaptureSelection()
        {
            _pendingSelection.player_id = ResolveLocalPlayerId();
            _pendingSelection.castle_node_id = _castleNodeId;
            _pendingSelection.turn = GameStateCache.Instance != null ? GameStateCache.Instance.Turn : 0;
            _pendingSelection.wood = woodSlider != null ? Mathf.RoundToInt(woodSlider.value) : 0;
            _pendingSelection.stone = stoneSlider != null ? Mathf.RoundToInt(stoneSlider.value) : 0;
            _pendingSelection.troops = troopSlider != null ? Mathf.RoundToInt(troopSlider.value) : 0;
            _pendingSelection.active_tab = _activeTab == Tab.Materials ? "materials" : "army";
            _pendingSelection.updated_utc = DateTime.UtcNow.ToString("o");
        }

        private void LoadSelection()
        {
            if (string.IsNullOrEmpty(_castleNodeId))
            {
                return;
            }

            var saveKey = GetSaveKey(_castleNodeId);
            if (!PlayerPrefs.HasKey(saveKey))
            {
                ResetSelection();
                return;
            }

            var json = PlayerPrefs.GetString(saveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                ResetSelection();
                return;
            }

            try
            {
                var loaded = JsonUtility.FromJson<SavedSelection>(json);
                if (loaded == null)
                {
                    ResetSelection();
                    return;
                }

                _pendingSelection = loaded;
                if (woodSlider != null)
                {
                    woodSlider.SetValueWithoutNotify(Mathf.Clamp(loaded.wood, 0, Mathf.Max(0, _workshopCapacity)));
                }

                if (stoneSlider != null)
                {
                    stoneSlider.SetValueWithoutNotify(Mathf.Clamp(loaded.stone, 0, Mathf.Max(0, _workshopCapacity)));
                }

                if (troopSlider != null)
                {
                    troopSlider.SetValueWithoutNotify(Mathf.Clamp(loaded.troops, 0, Mathf.Max(0, _archeryCapacity)));
                }

                SetTab(loaded.active_tab == "army" ? Tab.Army : Tab.Materials);
                RefreshSliderTexts();
                SetStatus($"Loaded saved plan: wood {loaded.wood}, stone {loaded.stone}, troops {loaded.troops}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CastleProductionPanel] Failed to load saved selection: {ex.Message}");
                ResetSelection();
            }
        }

        private void ResetSelection()
        {
            _pendingSelection = new SavedSelection
            {
                player_id = ResolveLocalPlayerId(),
                castle_node_id = _castleNodeId,
                turn = GameStateCache.Instance != null ? GameStateCache.Instance.Turn : 0,
                wood = 0,
                stone = 0,
                troops = 0,
                active_tab = "materials",
                updated_utc = DateTime.UtcNow.ToString("o")
            };

            if (woodSlider != null)
            {
                woodSlider.SetValueWithoutNotify(0f);
            }

            if (stoneSlider != null)
            {
                stoneSlider.SetValueWithoutNotify(0f);
            }

            if (troopSlider != null)
            {
                troopSlider.SetValueWithoutNotify(0f);
            }

            SetTab(Tab.Materials);
            RefreshSliderTexts();
        }

        private string GetSaveKey(string castleNodeId)
        {
            return $"{saveKeyPrefix}_{ResolveLocalPlayerId()}_{castleNodeId}";
        }

        private bool TryResolveCastleNode(string nodeId, out NodeDto node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            var map = MapRenderer.Instance;
            if (map != null && map.TryGetNodeState(nodeId, out var mapNode))
            {
                node = mapNode;
            }
            else if (GameStateCache.Instance != null)
            {
                node = GameStateCache.Instance.GetNode(nodeId);
            }

            if (node == null)
            {
                return false;
            }

            if (requireCastleBuildingType && !string.Equals(Normalize(node.BuildingType), Normalize(castleBuildingType), StringComparison.Ordinal))
            {
                return false;
            }

            if (requireLocalOwnership)
            {
                var localPlayerId = Normalize(ResolveLocalPlayerId());
                var owner = Normalize(node.Owner);
                var territoryOwner = Normalize(node.TerritoryOwner);
                var ownerMatch = !string.IsNullOrEmpty(owner) && string.Equals(owner, localPlayerId, StringComparison.Ordinal);
                var territoryOwnerMatch = !string.IsNullOrEmpty(territoryOwner) && string.Equals(territoryOwner, localPlayerId, StringComparison.Ordinal);
                if (!ownerMatch && !territoryOwnerMatch)
                {
                    return false;
                }
            }

            return true;
        }

        private static string ResolveLocalPlayerId()
        {
            if (GameStateCache.Instance != null && !string.IsNullOrWhiteSpace(GameStateCache.Instance.MyPlayerID))
            {
                return GameStateCache.Instance.MyPlayerID.Trim();
            }

            return "blue";
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

#if UNITY_EDITOR
        public void EditorRebuildUiForPrefab()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != null)
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            panelRoot = null;
            materialsTabButton = null;
            armyTabButton = null;
            materialsTabRoot = null;
            armyTabRoot = null;
            woodSlider = null;
            stoneSlider = null;
            troopSlider = null;
            titleText = null;
            statusText = null;
            workshopCapacityText = null;
            archeryCapacityText = null;
            woodValueText = null;
            stoneValueText = null;
            troopValueText = null;
            workshopHintText = null;
            archeryHintText = null;
            saveButton = null;
            closeButton = null;

            autoBuildFallbackUi = true;
            autoBindByName = true;
            EnsureUiReady();
            ConfigureSliders();
            WireButtons();
            SetTab(Tab.Materials);
            EditorUtility.SetDirty(gameObject);
        }
#endif
    }
}
