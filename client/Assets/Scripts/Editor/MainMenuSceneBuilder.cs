using System;
using System.Collections.Generic;
using System.IO;
using Panoptes.Presentation.UI.Lobby;
using Panoptes.Presentation.UI.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Panoptes.Editor
{
    public static class MainMenuSceneBuilder
    {
        private const string SourceLobbyScenePath = "Assets/Scenes/Lobby.unity";
        private const string SourceLoginScenePath = "Assets/Scenes/Login.unity";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string BackgroundAssetPath = "Assets/Art/UI/MainMenu/MainMenuBackground.png";
        private const string GameSettingScenePath = "Assets/Scenes/Game Setting.unity";

        [MenuItem("Panoptes/UI/Rebuild Main Menu Scene")]
        public static void RebuildFromMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            ImportBackground();

            var backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundAssetPath);
            var backgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundAssetPath);
            if (backgroundSprite == null || backgroundTexture == null)
            {
                throw new InvalidOperationException("Main menu background was not imported as a Sprite/Texture2D.");
            }

            var lobbyScene = EditorSceneManager.OpenScene(SourceLobbyScenePath, OpenSceneMode.Single);
            var sourceCanvas = FindRootInScene(lobbyScene, "Canvas");
            if (sourceCanvas == null)
            {
                throw new InvalidOperationException("Could not find Canvas in Lobby scene.");
            }

            var sourceLobbyPanel = FindDirectChild(sourceCanvas, "LobbyPanel");
            var sourceRoomPanel = FindDirectChild(sourceCanvas, "RoomPanel");
            var sourceEventSystem = GameObject.Find("EventSystem");
            if (sourceLobbyPanel == null || sourceRoomPanel == null || sourceEventSystem == null)
            {
                throw new InvalidOperationException("Could not find LobbyPanel/RoomPanel/EventSystem in Lobby scene.");
            }

            var loginScene = EditorSceneManager.OpenScene(SourceLoginScenePath, OpenSceneMode.Additive);
            var sourceLoginCanvas = FindRootInScene(loginScene, "Canvas");
            var sourceLoginPanel = sourceLoginCanvas != null ? FindDirectChild(sourceLoginCanvas, "Panel") : null;
            if (sourceLoginPanel == null)
            {
                throw new InvalidOperationException("Could not find login Panel in Login scene.");
            }

            var settingsScene = EditorSceneManager.OpenScene(GameSettingScenePath, OpenSceneMode.Additive);

            var mainMenuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(mainMenuScene);

            CreateCamera(mainMenuScene);
            var eventSystemClone = UnityEngine.Object.Instantiate(sourceEventSystem);
            eventSystemClone.name = "EventSystem";
            SceneManager.MoveGameObjectToScene(eventSystemClone, mainMenuScene);

            var canvasGo = CreateCanvas(mainMenuScene);
            AddBackground(canvasGo, backgroundSprite);
            AddFireOverlay(canvasGo, mainMenuScene, backgroundTexture);

            var menuPanelRect = CreateMainMenuPanel(canvasGo.transform);
            var startButton = CreateButton("StartGameButton", menuPanelRect, "\u5f00\u59cb\u6e38\u620f", new Vector2(0f, -122f), new Vector2(310f, 64f), new Color(0.34f, 0.12f, 0.07f, 0.9f));
            var settingsButton = CreateButton("SettingsButton", menuPanelRect, "\u6e38\u620f\u8bbe\u7f6e", new Vector2(0f, -204f), new Vector2(310f, 64f), new Color(0.18f, 0.18f, 0.2f, 0.86f));
            var quitButton = CreateButton("QuitButton", menuPanelRect, "\u9000\u51fa\u6e38\u620f", new Vector2(0f, -286f), new Vector2(310f, 64f), new Color(0.12f, 0.1f, 0.1f, 0.86f));

            var lobbyPanelClone = UnityEngine.Object.Instantiate(sourceLobbyPanel, canvasGo.transform, false);
            lobbyPanelClone.name = "LobbyPanel";
            lobbyPanelClone.SetActive(false);
            lobbyPanelClone.GetComponent<RectTransform>().SetAsLastSibling();

            var roomPanelClone = UnityEngine.Object.Instantiate(sourceRoomPanel, canvasGo.transform, false);
            roomPanelClone.name = "RoomPanel";
            roomPanelClone.SetActive(false);
            roomPanelClone.GetComponent<RectTransform>().SetAsLastSibling();

            var loginPanelClone = UnityEngine.Object.Instantiate(sourceLoginPanel, canvasGo.transform, false);
            loginPanelClone.name = "LoginPanel";
            loginPanelClone.SetActive(true);
            loginPanelClone.GetComponent<RectTransform>().SetAsLastSibling();

            var settingsPanelClone = CreateSettingsPanel(settingsScene, canvasGo.transform, mainMenuScene, out var settingsBackButton);
            settingsPanelClone.SetActive(false);
            settingsPanelClone.GetComponent<RectTransform>().SetAsLastSibling();

            var backButtonRootRect = CreateRect("LobbyBackButtonRoot", canvasGo.transform, mainMenuScene, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, 58f), new Vector2(28f, -28f));
            var backButton = CreateButton("BackToMainMenuButton", backButtonRootRect, "\u8fd4\u56de\u4e3b\u754c\u9762", new Vector2(125f, 0f), new Vector2(250f, 58f), new Color(0.08f, 0.08f, 0.1f, 0.86f));
            backButtonRootRect.gameObject.SetActive(false);
            backButtonRootRect.SetAsLastSibling();
            loginPanelClone.GetComponent<RectTransform>().SetAsLastSibling();
            settingsPanelClone.GetComponent<RectTransform>().SetAsLastSibling();

            WireControllers(
                canvasGo,
                menuPanelRect.gameObject,
                loginPanelClone,
                lobbyPanelClone,
                roomPanelClone,
                settingsPanelClone,
                backButtonRootRect.gameObject,
                startButton,
                settingsButton,
                quitButton,
                backButton,
                settingsBackButton);

            EditorSceneManager.SaveScene(mainMenuScene, MainMenuScenePath);
            EditorSceneManager.CloseScene(settingsScene, true);
            EditorSceneManager.CloseScene(loginScene, true);
            EditorSceneManager.CloseScene(lobbyScene, true);
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();

            return $"Created {MainMenuScenePath} with login, main menu, lobby panels, and animated fire overlays.";
        }

        private static void ImportBackground()
        {
            AssetDatabase.ImportAsset(BackgroundAssetPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(BackgroundAssetPath) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }

        private static GameObject FindDirectChild(GameObject parent, string childName)
        {
            for (var i = 0; i < parent.transform.childCount; i++)
            {
                var child = parent.transform.GetChild(i);
                if (child.name == childName)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static GameObject FindRootInScene(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == objectName)
                {
                    return root;
                }
            }

            return null;
        }

        private static void CreateCamera(Scene scene)
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.015f, 0.012f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
        }

        private static GameObject CreateCanvas(Scene scene)
        {
            var canvasGo = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(LobbySceneController),
                typeof(MainMenuController));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.sizeDelta = Vector2.zero;
            canvasRect.localScale = Vector3.one;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvasGo;
        }

        private static void AddBackground(GameObject canvasGo, Sprite backgroundSprite)
        {
            var backgroundRect = CreateRect("Background", canvasGo.transform, canvasGo.scene, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(backgroundRect);
            var backgroundImage = backgroundRect.gameObject.AddComponent<Image>();
            backgroundImage.sprite = backgroundSprite;
            backgroundImage.color = Color.white;
            backgroundImage.preserveAspect = false;
            backgroundImage.raycastTarget = false;

            var shadeRect = CreateRect("MenuShade", canvasGo.transform, canvasGo.scene, new Vector2(0f, 0f), new Vector2(0.44f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;
            var shadeImage = shadeRect.gameObject.AddComponent<Image>();
            shadeImage.color = new Color(0.02f, 0.012f, 0.01f, 0.58f);
            shadeImage.raycastTarget = false;
        }

        private static void AddFireOverlay(GameObject canvasGo, Scene scene, Texture2D backgroundTexture)
        {
            var fireRoot = CreateRect("FireMotionOverlay", canvasGo.transform, scene, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(fireRoot);

            var fireAnimator = fireRoot.gameObject.AddComponent<MainMenuFireAnimator>();
            var fireLayers = new List<RawImage>
            {
                CreateFireLayer("FireGlowRight", fireRoot, scene, backgroundTexture, new Vector2(0.52f, -0.02f), new Vector2(1.02f, 1.02f), new Rect(0.52f, 0.02f, 0.48f, 0.96f), new Color(1f, 0.42f, 0.12f, 0.12f)),
                CreateFireLayer("FireGlowSky", fireRoot, scene, backgroundTexture, new Vector2(0.44f, 0.42f), new Vector2(1.02f, 1.03f), new Rect(0.44f, 0.42f, 0.56f, 0.57f), new Color(1f, 0.28f, 0.08f, 0.08f)),
                CreateFireLayer("FireSparksCenter", fireRoot, scene, backgroundTexture, new Vector2(0.35f, 0.05f), new Vector2(0.98f, 0.84f), new Rect(0.35f, 0.05f, 0.63f, 0.79f), new Color(1f, 0.58f, 0.24f, 0.055f)),
            };

            var fireSo = new SerializedObject(fireAnimator);
            var layersProp = fireSo.FindProperty("fireLayers");
            layersProp.arraySize = fireLayers.Count;
            for (var i = 0; i < fireLayers.Count; i++)
            {
                layersProp.GetArrayElementAtIndex(i).objectReferenceValue = fireLayers[i];
            }

            fireSo.FindProperty("alphaMin").floatValue = 0.035f;
            fireSo.FindProperty("alphaMax").floatValue = 0.15f;
            fireSo.FindProperty("positionAmplitude").floatValue = 12f;
            fireSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RawImage CreateFireLayer(
            string name,
            RectTransform parent,
            Scene scene,
            Texture2D texture,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Rect uv,
            Color color)
        {
            var rect = CreateRect(name, parent, scene, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var raw = rect.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.uvRect = uv;
            raw.color = color;
            raw.raycastTarget = false;
            return raw;
        }

        private static RectTransform CreateMainMenuPanel(Transform parent)
        {
            var menuPanelRect = CreateRect("MainMenuPanel", parent, parent.gameObject.scene, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(430f, 390f), new Vector2(130f, -20f));
            var menuPanelImage = menuPanelRect.gameObject.AddComponent<Image>();
            menuPanelImage.color = new Color(0.05f, 0.027f, 0.018f, 0.68f);
            menuPanelImage.raycastTarget = false;

            var titleRect = CreateRect("Title", menuPanelRect, parent.gameObject.scene, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(380f, 82f), new Vector2(0f, -18f));
            var titleText = titleRect.gameObject.AddComponent<TextMeshProUGUI>();
            titleText.text = "PANOPTES";
            titleText.font = TMP_Settings.defaultFontAsset;
            titleText.fontSize = 56f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(1f, 0.76f, 0.42f, 1f);
            titleText.raycastTarget = false;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;

            return menuPanelRect;
        }

        private static GameObject CreateSettingsPanel(Scene settingsScene, Transform parent, Scene targetScene, out Button backButton)
        {
            backButton = null;
            var settingsRoot = CreateRect("SettingsPanel", parent, targetScene, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Stretch(settingsRoot);

            var shade = settingsRoot.gameObject.AddComponent<Image>();
            shade.color = new Color(0.02f, 0.02f, 0.025f, 0.72f);
            shade.raycastTarget = true;
            var adaptiveLayout = settingsRoot.gameObject.AddComponent<SettingsPanelAdaptiveLayout>();

            foreach (var root in settingsScene.GetRootGameObjects())
            {
                if (root == null ||
                    root.GetComponent<Camera>() != null ||
                    root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)
                {
                    continue;
                }

                if (root.GetComponent<RectTransform>() == null && root.GetComponentInChildren<RectTransform>(true) == null)
                {
                    continue;
                }

                var clone = UnityEngine.Object.Instantiate(root, settingsRoot, false);
                clone.name = root.name;
                StripNestedCanvasComponents(clone);
            }

            adaptiveLayout.Apply();

            backButton = FindNamedButton(settingsRoot, "exit") ?? FindNamedButton(settingsRoot, "Back");
            if (backButton == null)
            {
                backButton = CreateButton("SettingsBackButton", settingsRoot, "\u8fd4\u56de", new Vector2(90f, -40f), new Vector2(160f, 56f), new Color(0.08f, 0.08f, 0.1f, 0.86f));
            }

            backButton.onClick = new Button.ButtonClickedEvent();
            return settingsRoot.gameObject;
        }

        private static Button FindNamedButton(RectTransform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var buttons = root.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button != null && string.Equals(button.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return button;
                }
            }

            return null;
        }

        private static void StripNestedCanvasComponents(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var raycaster in root.GetComponentsInChildren<GraphicRaycaster>(true))
            {
                UnityEngine.Object.DestroyImmediate(raycaster);
            }

            foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
            {
                UnityEngine.Object.DestroyImmediate(scaler);
            }

            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                UnityEngine.Object.DestroyImmediate(canvas);
            }
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition, Vector2 size, Color normalColor)
        {
            var rect = CreateRect(name, parent, parent.gameObject.scene, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), size, anchoredPosition);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = normalColor;
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 0.94f, 0.68f, 1f);
            colors.pressedColor = new Color(0.68f, 0.37f, 0.22f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.7f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            CreateText("Label", rect, label, 30f, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1f, 0.92f, 0.78f, 1f));
            return button;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string label, float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            var rect = CreateRect(name, parent, parent.gameObject.scene, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = false;
            return text;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Scene scene,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta,
            Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void WireControllers(
            GameObject canvasGo,
            GameObject mainMenuPanel,
            GameObject loginPanel,
            GameObject lobbyPanel,
            GameObject roomPanel,
            GameObject settingsPanel,
            GameObject lobbyBackButtonRoot,
            Button startButton,
            Button settingsButton,
            Button quitButton,
            Button backButton,
            Button settingsBackButton)
        {
            var lobbySceneController = canvasGo.GetComponent<LobbySceneController>();

            var mainMenuController = canvasGo.GetComponent<MainMenuController>();
            var menuSo = new SerializedObject(mainMenuController);
            menuSo.FindProperty("mainMenuPanel").objectReferenceValue = mainMenuPanel;
            menuSo.FindProperty("loginPanel").objectReferenceValue = loginPanel;
            menuSo.FindProperty("lobbyPanel").objectReferenceValue = lobbyPanel;
            menuSo.FindProperty("roomPanel").objectReferenceValue = roomPanel;
            menuSo.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            menuSo.FindProperty("lobbyBackButtonRoot").objectReferenceValue = lobbyBackButtonRoot;
            menuSo.FindProperty("lobbySceneController").objectReferenceValue = lobbySceneController;
            menuSo.FindProperty("startButton").objectReferenceValue = startButton;
            menuSo.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            menuSo.FindProperty("quitButton").objectReferenceValue = quitButton;
            menuSo.FindProperty("lobbyBackButton").objectReferenceValue = backButton;
            menuSo.FindProperty("settingsBackButton").objectReferenceValue = settingsBackButton;
            menuSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateBuildSettings()
        {
            var desiredPaths = new[]
            {
                "Assets/Scenes/Boot.unity",
                MainMenuScenePath,
                "Assets/Scenes/Login.unity",
                "Assets/Scenes/Lobby.unity",
                "Assets/Scenes/Game.unity",
                GameSettingScenePath,
            };

            var scenes = new List<EditorBuildSettingsScene>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in desiredPaths)
            {
                if (!File.Exists(path) || !seen.Add(path))
                {
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing == null || string.IsNullOrWhiteSpace(existing.path) || !seen.Add(existing.path))
                {
                    continue;
                }

                scenes.Add(existing);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
