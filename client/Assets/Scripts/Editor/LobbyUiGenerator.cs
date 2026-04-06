using System.Collections.Generic;
using System;
using Panoptes.Runtime.UI.Lobby;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Panoptes.Editor
{
    public static class LobbyUiGenerator
    {
        private const string ScenePath = "Assets/Scenes/Lobby.unity";
        private const string PlayerSlotPrefabPath = "Assets/Prefabs/UI/PlayerSlot.prefab";

        [MenuItem("Panoptes/UI/Rebuild Lobby UI")]
        public static void RebuildLobbyUi()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/UI");

            var playerSlotPrefab = BuildPlayerSlotPrefab();
            RebuildLobbyScene(playerSlotPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LobbyUiGenerator] Lobby UI rebuilt.");
        }

        public static void RebuildLobbyUiBatch()
        {
            RebuildLobbyUi();
            EditorApplication.Exit(0);
        }

        private static GameObject BuildPlayerSlotPrefab()
        {
            var resources = GetResources();

            var root = new GameObject("PlayerSlot", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(PlayerSlotView));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(0f, 84f);

            var rootImage = root.GetComponent<Image>();
            rootImage.sprite = resources.standard;
            rootImage.type = Image.Type.Sliced;
            rootImage.color = new Color(0.96f, 0.96f, 0.98f, 1f);

            var rootLayout = root.GetComponent<HorizontalLayoutGroup>();
            rootLayout.padding = new RectOffset(20, 20, 14, 14);
            rootLayout.spacing = 12f;
            rootLayout.childAlignment = TextAnchor.MiddleLeft;
            rootLayout.childControlHeight = false;
            rootLayout.childControlWidth = false;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childForceExpandWidth = false;

            var layoutElement = root.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 84f;

            var usernameText = CreateText("UsernameText", root.transform, "玩家", 30, TextAlignmentOptions.Left);
            var usernameLayout = usernameText.gameObject.AddComponent<LayoutElement>();
            usernameLayout.flexibleWidth = 1f;
            usernameLayout.minWidth = 200f;

            var hostBadge = CreateBadge("HostBadge", root.transform, "房主", new Color(0.89f, 0.72f, 0.22f, 1f));
            var botBadge = CreateBadge("BotBadge", root.transform, "Bot", new Color(0.22f, 0.45f, 0.74f, 1f));
            var readyBadge = CreateBadge("ReadyBadge", root.transform, "已准备", new Color(0.24f, 0.62f, 0.36f, 1f));
            var kickButton = CreateButton("KickButton", root.transform, "踢出", new Vector2(0.5f, 0.5f), new Vector2(110f, 44f), Vector2.zero, new Color(0.71f, 0.24f, 0.25f, 1f));
            var kickLayout = kickButton.gameObject.AddComponent<LayoutElement>();
            kickLayout.preferredWidth = 110f;
            kickLayout.preferredHeight = 44f;
            kickButton.gameObject.SetActive(false);

            var viewSerialized = new SerializedObject(root.GetComponent<PlayerSlotView>());
            viewSerialized.FindProperty("usernameText").objectReferenceValue = usernameText;
            viewSerialized.FindProperty("hostBadge").objectReferenceValue = hostBadge;
            viewSerialized.FindProperty("readyBadge").objectReferenceValue = readyBadge;
            viewSerialized.FindProperty("botBadge").objectReferenceValue = botBadge;
            viewSerialized.FindProperty("kickButton").objectReferenceValue = kickButton;
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerSlotPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void RebuildLobbyScene(GameObject playerSlotPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMainCamera();
            CreateEventSystem();

            var canvas = CreateCanvas();
            CreateFullscreenBackground(canvas.transform, new Color(0.08f, 0.12f, 0.17f, 1f));
            CreateDecor(canvas.transform);

            var lobbyPanel = CreatePanel("LobbyPanel", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(880f, 760f), new Color(0.97f, 0.97f, 0.98f, 0.98f));
            var roomPanel = CreatePanel("RoomPanel", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(920f, 760f), new Color(0.97f, 0.97f, 0.98f, 0.98f));
            roomPanel.SetActive(false);

            BuildLobbyPanel(lobbyPanel.transform);
            BuildRoomPanel(roomPanel.transform, playerSlotPrefab);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void BuildLobbyPanel(Transform parent)
        {
            var controller = parent.gameObject.AddComponent<LobbyPanelController>();

            CreateText("Title", parent, "大厅", 56, TextAlignmentOptions.Center, FontStyles.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(360f, 72f), new Color(0.1f, 0.14f, 0.19f, 1f));

            var playerInfo = CreateBlock("PlayerInfo", parent, new Vector2(0.5f, 1f), new Vector2(780f, 84f), new Vector2(0f, -122f), new Color(0.92f, 0.95f, 0.98f, 1f));
            CreateText("PlayerInfoLabel", playerInfo.transform, "当前玩家", 24, TextAlignmentOptions.Left, FontStyles.Bold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(160f, 32f), new Color(0.29f, 0.36f, 0.44f, 1f));
            var usernameText = CreateText("UsernameText", playerInfo.transform, "", 30, TextAlignmentOptions.Right, FontStyles.Normal, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(360f, 36f), new Color(0.1f, 0.14f, 0.19f, 1f));

            var createSection = CreateBlock("CreateSection", parent, new Vector2(0.5f, 1f), new Vector2(780f, 230f), new Vector2(0f, -272f), new Color(0.93f, 0.95f, 0.98f, 1f));
            CreateText("CreateTitle", createSection.transform, "创建房间", 34, TextAlignmentOptions.Left, FontStyles.Bold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(220f, 40f), new Color(0.1f, 0.14f, 0.19f, 1f));
            var roomNameField = CreateInputField("RoomNameField", createSection.transform, "输入房间名", new Vector2(0f, 1f), new Vector2(340f, 56f), new Vector2(28f, -98f));
            var maxPlayersDropdown = CreateDropdown("MaxPlayersDropdown", createSection.transform, new List<string> { "2", "4", "6", "8" }, new Vector2(0f, 1f), new Vector2(160f, 56f), new Vector2(392f, -98f));
            var createButton = CreateButton("CreateButton", createSection.transform, "创建房间", new Vector2(1f, 1f), new Vector2(180f, 56f), new Vector2(-28f, -98f), new Color(0.17f, 0.43f, 0.78f, 1f));

            var joinSection = CreateBlock("JoinSection", parent, new Vector2(0.5f, 1f), new Vector2(780f, 190f), new Vector2(0f, -528f), new Color(0.93f, 0.95f, 0.98f, 1f));
            CreateText("JoinTitle", joinSection.transform, "加入房间", 34, TextAlignmentOptions.Left, FontStyles.Bold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(220f, 40f), new Color(0.1f, 0.14f, 0.19f, 1f));
            var roomCodeField = CreateInputField("RoomCodeField", joinSection.transform, "输入6位邀请码", new Vector2(0f, 1f), new Vector2(500f, 56f), new Vector2(28f, -98f));
            var joinButton = CreateButton("JoinButton", joinSection.transform, "加入房间", new Vector2(1f, 1f), new Vector2(180f, 56f), new Vector2(-28f, -98f), new Color(0.11f, 0.55f, 0.56f, 1f));

            var errorText = CreateText("ErrorText", parent, "", 24, TextAlignmentOptions.Center, FontStyles.Bold, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(560f, 32f), new Color(0.73f, 0.2f, 0.22f, 1f));
            errorText.gameObject.SetActive(false);

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("roomNameField").objectReferenceValue = roomNameField;
            serialized.FindProperty("maxPlayersDropdown").objectReferenceValue = maxPlayersDropdown;
            serialized.FindProperty("createButton").objectReferenceValue = createButton;
            serialized.FindProperty("roomCodeField").objectReferenceValue = roomCodeField;
            serialized.FindProperty("joinButton").objectReferenceValue = joinButton;
            serialized.FindProperty("errorText").objectReferenceValue = errorText;
            serialized.FindProperty("usernameText").objectReferenceValue = usernameText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildRoomPanel(Transform parent, GameObject playerSlotPrefab)
        {
            var controller = parent.gameObject.AddComponent<RoomPanelController>();

            var roomInfo = CreateBlock("RoomInfo", parent, new Vector2(0.5f, 1f), new Vector2(820f, 148f), new Vector2(0f, -44f), new Color(0.92f, 0.95f, 0.98f, 1f));
            var roomNameText = CreateText("RoomNameText", roomInfo.transform, "房间名", 44, TextAlignmentOptions.Left, FontStyles.Bold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -34f), new Vector2(420f, 48f), new Color(0.1f, 0.14f, 0.19f, 1f));
            var roomCodeText = CreateText("RoomCodeText", roomInfo.transform, "邀请码：------", 26, TextAlignmentOptions.Right, FontStyles.Bold, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -38f), new Vector2(300f, 32f), new Color(0.17f, 0.43f, 0.78f, 1f));
            var playerCountText = CreateText("PlayerCountText", roomInfo.transform, "0 / 0", 26, TextAlignmentOptions.Left, FontStyles.Normal, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(180f, 30f), new Color(0.29f, 0.36f, 0.44f, 1f));

            var playerList = CreateBlock("PlayerList", parent, new Vector2(0.5f, 1f), new Vector2(820f, 370f), new Vector2(0f, -220f), new Color(0.93f, 0.95f, 0.98f, 1f));
            CreateText("PlayerListTitle", playerList.transform, "玩家列表", 34, TextAlignmentOptions.Left, FontStyles.Bold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(220f, 40f), new Color(0.1f, 0.14f, 0.19f, 1f));
            var playerSlotContainer = new GameObject("PlayerSlotContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            playerSlotContainer.transform.SetParent(playerList.transform, false);
            var containerRect = playerSlotContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0f, 0f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.offsetMin = new Vector2(24f, 24f);
            containerRect.offsetMax = new Vector2(-24f, -76f);
            var verticalLayout = playerSlotContainer.GetComponent<VerticalLayoutGroup>();
            verticalLayout.spacing = 10f;
            verticalLayout.padding = new RectOffset(0, 0, 0, 0);
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlHeight = true;
            verticalLayout.childControlWidth = true;
            verticalLayout.childForceExpandHeight = false;
            verticalLayout.childForceExpandWidth = true;
            var fitter = playerSlotContainer.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var readyButton = CreateButton("ReadyButton", parent, "准备", new Vector2(0f, 0f), new Vector2(220f, 60f), new Vector2(50f, 66f), new Color(0.24f, 0.62f, 0.36f, 1f));
            var addBotButton = CreateButton("AddBotButton", parent, "添加 Bot", new Vector2(0.5f, 0f), new Vector2(220f, 60f), new Vector2(0f, 66f), new Color(0.17f, 0.43f, 0.78f, 1f));
            addBotButton.gameObject.SetActive(false);
            var leaveButton = CreateButton("LeaveButton", parent, "离开房间", new Vector2(1f, 0f), new Vector2(220f, 60f), new Vector2(-50f, 66f), new Color(0.71f, 0.24f, 0.25f, 1f));
            var statusText = CreateText("StatusText", parent, "等待玩家准备...", 26, TextAlignmentOptions.Center, FontStyles.Bold, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(520f, 34f), new Color(0.29f, 0.36f, 0.44f, 1f));

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("roomNameText").objectReferenceValue = roomNameText;
            serialized.FindProperty("roomCodeText").objectReferenceValue = roomCodeText;
            serialized.FindProperty("playerCountText").objectReferenceValue = playerCountText;
            serialized.FindProperty("playerSlotContainer").objectReferenceValue = playerSlotContainer.transform;
            serialized.FindProperty("playerSlotPrefab").objectReferenceValue = playerSlotPrefab;
            serialized.FindProperty("addBotButton").objectReferenceValue = addBotButton;
            serialized.FindProperty("readyButton").objectReferenceValue = readyButton;
            serialized.FindProperty("leaveButton").objectReferenceValue = leaveButton;
            serialized.FindProperty("statusText").objectReferenceValue = statusText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateCanvas()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(LobbySceneController));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvasObject;
        }

        private static void CreateMainCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.1f, 0.15f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            var inputModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType == null)
            {
                throw new InvalidOperationException("找不到 InputSystemUIInputModule，请确认已安装并启用 Input System package。");
            }

            eventSystem.AddComponent(inputModuleType);
        }

        private static void CreateFullscreenBackground(Transform parent, Color color)
        {
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(parent, false);
            var rect = background.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = color;
        }

        private static void CreateDecor(Transform parent)
        {
            var glowA = new GameObject("GlowA", typeof(RectTransform), typeof(Image));
            glowA.transform.SetParent(parent, false);
            var glowARect = glowA.GetComponent<RectTransform>();
            glowARect.anchorMin = new Vector2(0f, 1f);
            glowARect.anchorMax = new Vector2(0f, 1f);
            glowARect.anchoredPosition = new Vector2(220f, -160f);
            glowARect.sizeDelta = new Vector2(420f, 420f);
            glowA.GetComponent<Image>().color = new Color(0.16f, 0.43f, 0.78f, 0.12f);

            var glowB = new GameObject("GlowB", typeof(RectTransform), typeof(Image));
            glowB.transform.SetParent(parent, false);
            var glowBRect = glowB.GetComponent<RectTransform>();
            glowBRect.anchorMin = new Vector2(1f, 0f);
            glowBRect.anchorMax = new Vector2(1f, 0f);
            glowBRect.anchoredPosition = new Vector2(-220f, 180f);
            glowBRect.sizeDelta = new Vector2(520f, 520f);
            glowB.GetComponent<Image>().color = new Color(0.11f, 0.55f, 0.56f, 0.1f);
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchor, Vector2 size, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var image = panel.GetComponent<Image>();
            image.sprite = GetResources().standard;
            image.type = Image.Type.Sliced;
            image.color = color;
            return panel;
        }

        private static GameObject CreateBlock(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
        {
            var block = new GameObject(name, typeof(RectTransform), typeof(Image));
            block.transform.SetParent(parent, false);
            var rect = block.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = block.GetComponent<Image>();
            image.sprite = GetResources().standard;
            image.type = Image.Type.Sliced;
            image.color = color;
            return block;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string content,
            float fontSize,
            TextAlignmentOptions alignment,
            FontStyles fontStyle = FontStyles.Normal,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? anchoredPosition = null,
            Vector2? size = null,
            Color? color = null)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin ?? new Vector2(0.5f, 0.5f);
            rect.anchorMax = anchorMax ?? new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition ?? Vector2.zero;
            rect.sizeDelta = size ?? new Vector2(240f, 40f);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = fontStyle;
            text.color = color ?? new Color(0.1f, 0.14f, 0.19f, 1f);
            text.enableWordWrapping = false;
            return text;
        }

        private static TMP_InputField CreateInputField(string name, Transform parent, string placeholder, Vector2 anchor, Vector2 size, Vector2 anchoredPosition)
        {
            var inputObject = TMP_DefaultControls.CreateInputField(GetResources());
            inputObject.name = name;
            inputObject.transform.SetParent(parent, false);

            var rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = inputObject.GetComponent<Image>();
            image.color = Color.white;

            var field = inputObject.GetComponent<TMP_InputField>();
            field.text = string.Empty;
            field.textComponent.font = TMP_Settings.defaultFontAsset;
            field.textComponent.fontSize = 28;
            field.textComponent.color = new Color(0.1f, 0.14f, 0.19f, 1f);

            if (field.placeholder is TMP_Text placeholderText)
            {
                placeholderText.font = TMP_Settings.defaultFontAsset;
                placeholderText.fontSize = 26;
                placeholderText.text = placeholder;
            }

            return field;
        }

        private static TMP_Dropdown CreateDropdown(string name, Transform parent, List<string> options, Vector2 anchor, Vector2 size, Vector2 anchoredPosition)
        {
            var dropdownObject = TMP_DefaultControls.CreateDropdown(GetResources());
            dropdownObject.name = name;
            dropdownObject.transform.SetParent(parent, false);

            var rect = dropdownObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            dropdown.captionText.font = TMP_Settings.defaultFontAsset;
            dropdown.captionText.fontSize = 28;
            dropdown.itemText.font = TMP_Settings.defaultFontAsset;
            dropdown.itemText.fontSize = 26;
            dropdown.options.Clear();
            foreach (var option in options)
            {
                dropdown.options.Add(new TMP_Dropdown.OptionData(option));
            }
            dropdown.value = 0;
            dropdown.RefreshShownValue();
            if (dropdown.template != null)
            {
                dropdown.template.gameObject.SetActive(false);
            }

            return dropdown;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
        {
            var buttonObject = TMP_DefaultControls.CreateButton(GetResources());
            buttonObject.name = name;
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, anchor.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = buttonObject.GetComponent<Image>();
            image.color = color;

            var text = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 28;
            text.fontStyle = FontStyles.Bold;
            text.text = label;
            text.color = Color.white;

            return buttonObject.GetComponent<Button>();
        }

        private static GameObject CreateBadge(string name, Transform parent, string content, Color color)
        {
            var badge = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            badge.transform.SetParent(parent, false);

            var rect = badge.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(110f, 40f);

            var image = badge.GetComponent<Image>();
            image.sprite = GetResources().standard;
            image.type = Image.Type.Sliced;
            image.color = color;

            var layout = badge.GetComponent<LayoutElement>();
            layout.preferredWidth = 110f;
            layout.preferredHeight = 40f;

            var text = CreateText("Text", badge.transform, content, 22, TextAlignmentOptions.Center, FontStyles.Bold);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.color = Color.white;

            return badge;
        }

        private static TMP_DefaultControls.Resources GetResources()
        {
            return new TMP_DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
            };
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parts = path.Split('/');
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
    }
}
