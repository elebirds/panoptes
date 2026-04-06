using System.IO;
using System.Collections.Generic;
using Panoptes.Runtime.Map;
using Panoptes.Runtime.UI.Domestic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Panoptes.EditorTools
{
    /// <summary>
    /// One-click setup tool for BuildCommandPanel UI in MapEditor scene.
    /// Creates:
    /// - Emblem + Governance/Build tab toggles
    /// - Governance/Build content roots
    /// - Build buttons (icon + label)
    /// - Tooltip view
    /// and wires everything into BuildCommandPanel serialized fields.
    /// </summary>
    public static class MapEditorBuildPanelSetupTool
    {
        private const string MenuPath = "Panoptes/MapEditor/Rebuild Build Panel UI";
        private const string SavePrefabMenuPath = "Panoptes/MapEditor/Save Build Panel As Prefab";
        private const string BuildPanelPrefabPath = "Assets/Prefabs/UI/BuildPanelRoot.prefab";

        private sealed class BuildButtonDef
        {
            public string key;
            public BuildCommandPanel.BuildingType type;
            public BuildCommandPanel.BuildRule rule;
        }

        private sealed class BuildButtonRefs
        {
            public Button button;
            public Image icon;
            public TMP_Text label;
        }

        [MenuItem(MenuPath)]
        private static void RebuildBuildPanelUi()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[MapEditorBuildPanelSetupTool] Active scene is invalid or not loaded.");
                return;
            }

            EnsureEventSystem();

            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                canvas = CreateCanvas();
            }

            NormalizeCanvasRect(canvas);

            var panelGo = FindOrCreate(canvas.transform, "BuildPanelRoot");
            var panelRt = panelGo.GetComponent<RectTransform>();
            if (panelRt == null) panelRt = panelGo.AddComponent<RectTransform>();
            SetRect(panelRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(430f, 760f));

            var panelImage = EnsureComponent<Image>(panelGo);
            panelImage.color = new Color(0.08f, 0.1f, 0.14f, 0.92f);
            panelImage.raycastTarget = true;

            var panel = EnsureComponent<BuildCommandPanel>(panelGo);

            // Rebuild child layout from scratch for predictable hierarchy.
            ClearChildren(panelRt);

            var topBar = FindOrCreate(panelRt, "TopBar").GetComponent<RectTransform>();
            SetRect(topBar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 104f));

            var emblemGo = FindOrCreate(topBar, "EmblemImage");
            var emblemRt = emblemGo.GetComponent<RectTransform>();
            SetRect(emblemRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(84f, 84f));
            var emblemImage = EnsureComponent<Image>(emblemGo);
            emblemImage.color = new Color(1f, 1f, 1f, 0.95f);

            var modeTabs = FindOrCreate(topBar, "ModeTabs").GetComponent<RectTransform>();
            SetRect(modeTabs, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52f, 0f), new Vector2(-68f, 56f));

            var toggleGroup = EnsureComponent<ToggleGroup>(modeTabs.gameObject);
            var tabsLayout = EnsureComponent<HorizontalLayoutGroup>(modeTabs.gameObject);
            tabsLayout.spacing = 8f;
            tabsLayout.childForceExpandHeight = true;
            tabsLayout.childForceExpandWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childControlWidth = true;
            tabsLayout.padding = new RectOffset(0, 0, 0, 0);

            var governanceToggle = CreateTabToggle(modeTabs, "Toggle_Governance", "Governance", toggleGroup, true);
            var buildToggle = CreateTabToggle(modeTabs, "Toggle_Build", "Build", toggleGroup, false);

            var body = FindOrCreate(panelRt, "Body").GetComponent<RectTransform>();
            SetRect(body, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -58f), new Vector2(0f, -120f));

            var governanceRoot = FindOrCreate(body, "GovernanceContentRoot").GetComponent<RectTransform>();
            SetRect(governanceRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(-24f, -24f));
            BuildGovernancePlaceholder(governanceRoot);

            var buildRoot = FindOrCreate(body, "BuildContentRoot").GetComponent<RectTransform>();
            SetRect(buildRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(-24f, -24f));

            var buildGrid = FindOrCreate(buildRoot, "BuildGrid").GetComponent<RectTransform>();
            SetRect(buildGrid, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 0f));

            var buildGridLayout = EnsureComponent<GridLayoutGroup>(buildGrid.gameObject);
            buildGridLayout.cellSize = new Vector2(188f, 56f);
            buildGridLayout.spacing = new Vector2(10f, 10f);
            buildGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            buildGridLayout.constraintCount = 2;
            buildGridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            buildGridLayout.childAlignment = TextAnchor.UpperLeft;
            buildGridLayout.padding = new RectOffset(8, 8, 8, 8);

            var defs = GetBuildButtonDefs();
            var refs = new List<BuildButtonRefs>(defs.Count);
            for (var i = 0; i < defs.Count; i++)
            {
                refs.Add(CreateBuildButton(buildGrid, defs[i]));
            }

            var tooltipGo = FindOrCreate(panelRt, "BuildTooltip");
            var tooltipRt = tooltipGo.GetComponent<RectTransform>();
            SetRect(tooltipRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(220f, -140f), new Vector2(260f, 86f));
            tooltipRt.SetAsLastSibling();
            var tooltipBg = EnsureComponent<Image>(tooltipGo);
            tooltipBg.color = new Color(0f, 0f, 0f, 0.85f);

            var tooltipTextGo = FindOrCreate(tooltipRt, "TooltipText");
            var tooltipTextRt = tooltipTextGo.GetComponent<RectTransform>();
            SetRect(tooltipTextRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(-16f, -16f));
            var tooltipText = EnsureTmpText(tooltipTextGo, "Building Description", 20, TextAlignmentOptions.TopLeft);
            tooltipText.enableWordWrapping = true;

            var tooltipView = EnsureComponent<BuildTooltipView>(tooltipGo);
            ConfigureTooltipView(tooltipView, tooltipRt, tooltipText, canvas);
            tooltipGo.SetActive(false);

            var input = Object.FindObjectOfType<MapInputHandler>();

            ConfigureBuildPanelSerialized(
                panel,
                emblemImage,
                toggleGroup,
                governanceToggle,
                buildToggle,
                governanceRoot.gameObject,
                buildRoot.gameObject,
                tooltipView,
                input,
                defs,
                refs);

            governanceRoot.gameObject.SetActive(false);
            buildRoot.gameObject.SetActive(true);
            buildToggle.SetIsOnWithoutNotify(true);
            governanceToggle.SetIsOnWithoutNotify(false);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = panelGo;
            Debug.Log("[MapEditorBuildPanelSetupTool] Build panel rebuilt and wired. Save scene to keep changes.");
        }

        [MenuItem(SavePrefabMenuPath)]
        private static void SaveBuildPanelAsPrefab()
        {
            var panel = Object.FindObjectOfType<BuildCommandPanel>();
            if (panel == null)
            {
                RebuildBuildPanelUi();
                panel = Object.FindObjectOfType<BuildCommandPanel>();
            }

            if (panel == null)
            {
                Debug.LogError("[MapEditorBuildPanelSetupTool] BuildCommandPanel not found. Cannot save prefab.");
                return;
            }

            // Clear scene-only references before prefab save.
            var so = new SerializedObject(panel);
            so.FindProperty("mapInputHandler").objectReferenceValue = null;
            so.FindProperty("autoFindMapInputHandler").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefabDir = Path.GetDirectoryName(BuildPanelPrefabPath);
            if (!string.IsNullOrEmpty(prefabDir) && !AssetDatabase.IsValidFolder(prefabDir))
            {
                EnsureFolderRecursive(prefabDir);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(panel.gameObject, BuildPanelPrefabPath, out var success);
            if (!success || prefab == null)
            {
                Debug.LogError("[MapEditorBuildPanelSetupTool] Failed to save BuildPanelRoot prefab.");
                return;
            }

            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            Debug.Log($"[MapEditorBuildPanelSetupTool] Prefab saved: {BuildPanelPrefabPath}");
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var esGo = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas");
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            var rt = go.AddComponent<RectTransform>();
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            rt.localScale = Vector3.one;
            return canvas;
        }

        private static void NormalizeCanvasRect(Canvas canvas)
        {
            var rt = canvas.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.localScale = Vector3.one;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private static GameObject FindOrCreate(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void ClearChildren(Transform parent)
        {
            var toDelete = new List<GameObject>();
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null)
                {
                    toDelete.Add(child.gameObject);
                }
            }

            for (var i = 0; i < toDelete.Count; i++)
            {
                Undo.DestroyObjectImmediate(toDelete[i]);
            }
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = Undo.AddComponent<T>(go);
            return c;
        }

        private static void EnsureFolderRecursive(string folderPath)
        {
            var normalized = folderPath.Replace("\\", "/");
            var parts = normalized.Split('/');
            if (parts.Length == 0) return;

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            rt.localScale = Vector3.one;
        }

        private static Toggle CreateTabToggle(RectTransform parent, string name, string label, ToggleGroup group, bool isOn)
        {
            var go = FindOrCreate(parent, name);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 56f);

            var bg = EnsureComponent<Image>(go);
            bg.color = new Color(0.18f, 0.22f, 0.28f, 1f);

            var toggle = EnsureComponent<Toggle>(go);
            toggle.group = group;
            toggle.isOn = isOn;
            toggle.transition = Selectable.Transition.ColorTint;
            toggle.targetGraphic = bg;
            toggle.graphic = null;

            var colors = toggle.colors;
            colors.normalColor = new Color(0.18f, 0.22f, 0.28f, 1f);
            colors.highlightedColor = new Color(0.24f, 0.29f, 0.36f, 1f);
            colors.pressedColor = new Color(0.12f, 0.16f, 0.2f, 1f);
            colors.selectedColor = new Color(0.31f, 0.45f, 0.58f, 1f);
            colors.disabledColor = new Color(0.14f, 0.14f, 0.14f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            toggle.colors = colors;

            var labelGo = FindOrCreate(rt, "Label");
            var labelRt = labelGo.GetComponent<RectTransform>();
            SetRect(labelRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16f, -8f));
            var text = EnsureTmpText(labelGo, label, 24, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;

            return toggle;
        }

        private static void BuildGovernancePlaceholder(RectTransform root)
        {
            ClearChildren(root);
            var titleGo = FindOrCreate(root, "Title");
            var titleRt = titleGo.GetComponent<RectTransform>();
            SetRect(titleRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(-16f, 40f));
            var titleText = EnsureTmpText(titleGo, "Governance Mode", 26, TextAlignmentOptions.MidlineLeft);
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = new Color(0.95f, 0.95f, 0.95f, 1f);

            var gridGo = FindOrCreate(root, "GovernanceIconGrid");
            var gridRt = gridGo.GetComponent<RectTransform>();
            SetRect(gridRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(-16f, -72f));
            var grid = EnsureComponent<GridLayoutGroup>(gridGo);
            grid.cellSize = new Vector2(84f, 84f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.padding = new RectOffset(4, 4, 4, 4);

            for (var i = 0; i < 4; i++)
            {
                var iconGo = FindOrCreate(gridRt, $"GovIcon_{i + 1}");
                var iconBg = EnsureComponent<Image>(iconGo);
                iconBg.color = new Color(1f, 1f, 1f, 0.12f);
            }
        }

        private static BuildButtonRefs CreateBuildButton(RectTransform buildGrid, BuildButtonDef def)
        {
            var go = FindOrCreate(buildGrid, $"BuildBtn_{def.key}");
            var bg = EnsureComponent<Image>(go);
            bg.color = new Color(0.2f, 0.23f, 0.29f, 1f);

            var btn = EnsureComponent<Button>(go);
            var colors = btn.colors;
            colors.normalColor = new Color(0.2f, 0.23f, 0.29f, 1f);
            colors.highlightedColor = new Color(0.28f, 0.33f, 0.42f, 1f);
            colors.pressedColor = new Color(0.14f, 0.18f, 0.24f, 1f);
            colors.selectedColor = new Color(0.24f, 0.31f, 0.41f, 1f);
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            btn.colors = colors;

            var layout = EnsureComponent<HorizontalLayoutGroup>(go);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(10, 10, 8, 8);

            var iconGo = FindOrCreate(go.transform, "Icon");
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(30f, 30f);
            var icon = EnsureComponent<Image>(iconGo);
            icon.color = new Color(1f, 1f, 1f, 0.95f);

            var textGo = FindOrCreate(go.transform, "Label");
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.sizeDelta = new Vector2(126f, 32f);
            var text = EnsureTmpText(textGo, def.key, 20, TextAlignmentOptions.MidlineLeft);
            text.color = Color.white;
            text.enableWordWrapping = false;

            return new BuildButtonRefs
            {
                button = btn,
                icon = icon,
                label = text
            };
        }

        private static TMP_Text EnsureTmpText(GameObject go, string text, float fontSize, TextAlignmentOptions alignment)
        {
            var tmp = EnsureComponent<TextMeshProUGUI>(go);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            if (TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }
            return tmp;
        }

        private static List<BuildButtonDef> GetBuildButtonDefs()
        {
            return new List<BuildButtonDef>
            {
                new BuildButtonDef { key = "farm", type = BuildCommandPanel.BuildingType.Farm, rule = BuildCommandPanel.BuildRule.ResourceOnly },
                new BuildButtonDef { key = "mine", type = BuildCommandPanel.BuildingType.Mine, rule = BuildCommandPanel.BuildRule.ResourceOnly },
                new BuildButtonDef { key = "smelter", type = BuildCommandPanel.BuildingType.Smelter, rule = BuildCommandPanel.BuildRule.CityOnly },
                new BuildButtonDef { key = "workshop", type = BuildCommandPanel.BuildingType.Workshop, rule = BuildCommandPanel.BuildRule.CityOnly },
                new BuildButtonDef { key = "barracks", type = BuildCommandPanel.BuildingType.Barracks, rule = BuildCommandPanel.BuildRule.CityOnly },
                new BuildButtonDef { key = "engineer_camp", type = BuildCommandPanel.BuildingType.EngineerCamp, rule = BuildCommandPanel.BuildRule.CityOnly },
                new BuildButtonDef { key = "wall", type = BuildCommandPanel.BuildingType.Wall, rule = BuildCommandPanel.BuildRule.AnyTerrain },
                new BuildButtonDef { key = "tower", type = BuildCommandPanel.BuildingType.Tower, rule = BuildCommandPanel.BuildRule.AnyTerrain },
                new BuildButtonDef { key = "watchtower", type = BuildCommandPanel.BuildingType.Watchtower, rule = BuildCommandPanel.BuildRule.AnyTerrain }
            };
        }

        private static void ConfigureTooltipView(BuildTooltipView tooltipView, RectTransform tooltipRoot, TMP_Text text, Canvas canvas)
        {
            var so = new SerializedObject(tooltipView);
            so.FindProperty("tooltipRoot").objectReferenceValue = tooltipRoot;
            so.FindProperty("tooltipText").objectReferenceValue = text;
            so.FindProperty("canvas").objectReferenceValue = canvas;
            so.FindProperty("screenOffset").vector2Value = new Vector2(16f, -16f);
            so.FindProperty("clampToScreen").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tooltipView);
        }

        private static void ConfigureBuildPanelSerialized(
            BuildCommandPanel panel,
            Image emblemImage,
            ToggleGroup modeToggleGroup,
            Toggle governanceToggle,
            Toggle buildToggle,
            GameObject governanceRoot,
            GameObject buildRoot,
            BuildTooltipView tooltipView,
            MapInputHandler mapInputHandler,
            List<BuildButtonDef> defs,
            List<BuildButtonRefs> refs)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("emblemImage").objectReferenceValue = emblemImage;
            so.FindProperty("modeToggleGroup").objectReferenceValue = modeToggleGroup;
            so.FindProperty("governanceToggle").objectReferenceValue = governanceToggle;
            so.FindProperty("buildToggle").objectReferenceValue = buildToggle;
            so.FindProperty("governanceContentRoot").objectReferenceValue = governanceRoot;
            so.FindProperty("buildContentRoot").objectReferenceValue = buildRoot;
            so.FindProperty("tooltipView").objectReferenceValue = tooltipView;
            so.FindProperty("mapInputHandler").objectReferenceValue = mapInputHandler;
            so.FindProperty("autoFindMapInputHandler").boolValue = true;
            so.FindProperty("applyConfigToButtons").boolValue = true;
            so.FindProperty("useConfigPlacementRule").boolValue = true;
            so.FindProperty("buildConfigResourcesPath").stringValue = "Config/buildconfig";
            so.FindProperty("iconResourcesRoot").stringValue = "Icons/Buildings";
            so.FindProperty("logConfigWarnings").boolValue = true;

            var buildButtons = so.FindProperty("buildButtons");
            buildButtons.arraySize = defs.Count;
            for (var i = 0; i < defs.Count; i++)
            {
                var item = buildButtons.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("button").objectReferenceValue = refs[i].button;
                item.FindPropertyRelative("iconImage").objectReferenceValue = refs[i].icon;
                item.FindPropertyRelative("labelText").objectReferenceValue = refs[i].label;
                item.FindPropertyRelative("fallbackIcon").objectReferenceValue = null;
                item.FindPropertyRelative("buildingType").enumValueIndex = (int)defs[i].type;
                item.FindPropertyRelative("customBuildingType").stringValue = string.Empty;
                item.FindPropertyRelative("tooltipText").stringValue = string.Empty;
                item.FindPropertyRelative("rule").enumValueIndex = (int)defs[i].rule;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);
        }
    }
}
